#region Using declarations
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.BarsTypes;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using System.Windows.Media;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    /// <summary>Mode d'execution du moteur Sniper.</summary>
    public enum SniperExecutionMode
    {
        /// <summary>Gates actifs, quotas actifs, alertes emises.</summary>
        Sniper,
        /// <summary>Aucune alerte : tous les candidats sont uniquement journalises.</summary>
        Research
    }

    /// <summary>Classification du type de journee utilisee par le niveau N1.</summary>
    public enum SniperDayType
    {
        Undetermined,
        Normal,
        NormalVariation,
        Trend,
        Neutral
    }

    public enum SniperMarketPreset
    {
        /// <summary>Reglages de base (seuils relaches).</summary>
        Standard,
        /// <summary>Seuils renforces : peu de signaux, qualite maximale.</summary>
        Sniper,
        Scanner,
        /// <summary>Profil scalping haute fréquence.</summary>
        Scalping,
        /// <summary>Profil Scalping Pro avec confluence SMC, Footprint et scoring pondéré (5-10 setups/session).</summary>
        ScalpingPro,
    }

    public partial class SniperMarketCorePro : Indicator
    {
        // RingBuffer<T> : buffer circulaire a capacite fixe, O(1) en ajout
        // et suppression du plus ancien. Remplace les List<T>.RemoveAt(0)
        // qui coutaient O(n) a chaque barre (5-7 listes de 300-400 elements).
        private sealed class RingBuffer<T>
        {
            private T[] buf;
            private int head;  // index du prochain slot d'ecriture
            private int count;

            public RingBuffer(int capacity)
            {
                buf = new T[capacity > 0 ? capacity : 16];
                head = 0;
                count = 0;
            }

            public int Count { get { return count; } }
            public int Capacity { get { return buf.Length; } }

            public T this[int index]
            {
                get
                {
                    if (index < 0 || index >= count)
                        throw new System.ArgumentOutOfRangeException("index");
                    return buf[(head - count + index + buf.Length) % buf.Length];
                }
            }

            public void Add(T item)
            {
                buf[head] = item;
                head = (head + 1) % buf.Length;
                if (count < buf.Length) count++;
            }

            public void RemoveAt(int index)
            {
                if (index != 0)
                    throw new System.NotSupportedException("RingBuffer ne supporte que RemoveAt(0)");
                if (count == 0) return;
                count--;
            }

            public void Clear()
            {
                count = 0;
                head = 0;
            }

            public void CopyTo(T[] dest, int destIndex)
            {
                for (int i = 0; i < count; i++)
                    dest[destIndex + i] = this[i];
            }

            public void EnsureCapacity(int minCapacity)
            {
                if (buf.Length >= minCapacity) return;
                T[] newBuf = new T[minCapacity];
                for (int i = 0; i < count; i++)
                    newBuf[i] = this[i];
                buf = newBuf;
                head = count;
            }
        }

        private const int TelegramMaxMessageLength = 4096;

        // Le HttpClient reste partage volontairement : il est thread-safe et
        // mutualise le pool de connexions (recommandation .NET).
        private static readonly HttpClient TelegramClient = CreateTelegramClient();

        // statique serialisait les envois de tous les graphiques entre eux (un
        // instrument lent bloquait les autres) et melangeait l'etat de plusieurs
        // indicateurs. Chaque instance garde l'ordre de SES messages, sans
        // interference avec les autres charts.
        private readonly SemaphoreSlim telegramSendGate = new SemaphoreSlim(1, 1);

        private CancellationTokenSource telegramCts;

        private static HttpClient CreateTelegramClient()
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            // fige la resolution DNS indefiniment. Sur un VPS qui tourne des
            // semaines, une bascule d'IP cote Telegram casserait les envois.
            try
            {
                var sp = System.Net.ServicePointManager.FindServicePoint(new Uri("https://api.telegram.org"));
                sp.ConnectionLeaseTimeout = (int)TimeSpan.FromMinutes(5).TotalMilliseconds;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("VP_ServicePoint: " + ex.Message); }
            return client;
        }

        #region Parameters
        [Display(Name = "Telegram Bot Token", Order = 1, GroupName = "Telegram")]
        public string BotToken { get; set; }

        [Display(Name = "Telegram Chat ID 1 (Score 50-70)", Order = 2, GroupName = "Telegram")]
        public string ChatId { get; set; }

        [Display(Name = "Telegram Chat ID 2 (Score > 70)", Order = 3, GroupName = "Telegram")]
        public string ChatId2 { get; set; }

        [Display(Name = "Telegram Chat ID 3 (Market Intelligence)", Order = 4, GroupName = "Telegram")]
        public string ChatId3 { get; set; }

        [Range(50, 100)]
        [Display(Name = "Seuil Bascule Canal 2", Order = 5, GroupName = "Telegram")]
        public int ScoreThresholdChat2 { get; set; }

        [Range(10, 3600)]
        [Display(Name = "Cooldown Alerte (s)", Order = 3, GroupName = "Telegram")]
        public int AlertCooldownSeconds { get; set; }

        [Range(1, 100)]
        [Display(Name = "Seuil de déplacement (ticks)", Order = 4, GroupName = "Volume Profile")]
        public int MovementThresholdTicks { get; set; }

        [Range(1, 2000)]
        [Display(Name = "Fenêtre de calcul (barres)", Order = 5, GroupName = "Volume Profile")]
        public int LookbackBars { get; set; }

        [Range(50, 95)]
        [Display(Name = "Value Area (%)", Order = 6, GroupName = "Volume Profile")]
        public int ValueAreaPercent { get; set; }

        [Range(1, 60)]
        [Display(Name = "Timeframe Volumetric (min)", Order = 7, GroupName = "Volume Profile")]
        public int VolumetricTimeframe { get; set; }

        [Display(Name = "Mode Profil de Session", Order = 8, GroupName = "Volume Profile")]
        public bool UseSessionProfile { get; set; }

        [Display(Name = "Afficher Dashboard", Order = 1, GroupName = "Dashboard")]
        public bool ShowDashboard { get; set; }

        [Display(Name = "Afficher Lignes POC/VAH/VAL", Order = 2, GroupName = "Dashboard")]
        public bool ShowLevelLines { get; set; }

        [Display(Name = "Preset de trading", Order = 4, GroupName = "Debug", Description = "Standard = reglages de base. Sniper = seuils renforces (moins de signaux, meilleure qualite). Scanner = mode souple V7 (gates modulateurs, quotas elargis, beaucoup plus de signaux). Scalping = mode ultra-reactif V7.2 (emission intrabar, gates quasi neutralises, HTF off, R:R 0.5 : 20-50 signaux/jour, a utiliser en Research/journal avant tout trading reel). ScalpingPro = mode d'EXECUTION REELLE V7.3 (score pondere Structure/Footprint/Volume/Momentum/Contexte, confluence SMC, footprint obligatoire, HTF Soft, R:R 1.0, stop 1.0 ATR, 5-10 setups de haute qualite par session, alertes Moyen/Fort/Tres Fort).")]
        public SniperMarketPreset TradingPreset { get; set; }

        [Display(Name = "Mode Debug", Order = 3, GroupName = "Debug")]
        public bool EnableDebugMode { get; set; }

        [Display(Name = "Signaux Breakout VAH/VAL", Order = 1, GroupName = "Stratégies Volume Profile")]
        public bool EnableBreakoutSignals { get; set; }

        [Display(Name = "Signaux Rejet Support/Résistance", Order = 2, GroupName = "Stratégies Volume Profile")]
        public bool EnableRejectionSignals { get; set; }

        [Display(Name = "Confirmation Delta Bougie", Order = 3, GroupName = "Stratégies Volume Profile")]
        public bool RequireDeltaConfirmation { get; set; }

        [Display(Name = "Évaluer sur clôture de barre", Order = 4, GroupName = "Stratégies Volume Profile")]
        public bool EvaluateOnBarClose { get; set; }

        [Display(Name = "Breakout + Acceptance + Retest", Order = 1, GroupName = "Setups Avancés")]
        public bool EnableAcceptanceSetups { get; set; }

        [Range(1, 10)]
        [Display(Name = "Barres d'acceptance requises", Order = 2, GroupName = "Setups Avancés")]
        public int AcceptanceBars { get; set; }

        [Range(1, 20)]
        [Display(Name = "Tolérance de retest (ticks)", Order = 3, GroupName = "Setups Avancés")]
        public int RetestToleranceTicks { get; set; }

        [Range(1, 50)]
        [Display(Name = "Fenêtre max de retest (barres)", Order = 4, GroupName = "Setups Avancés")]
        public int RetestMaxBars { get; set; }

        [Display(Name = "Détection Failed Auction", Order = 5, GroupName = "Setups Avancés")]
        public bool EnableFailedAuction { get; set; }

        [Range(1, 20)]
        [Display(Name = "Fenêtre max Failed Auction (barres)", Order = 6, GroupName = "Setups Avancés")]
        public int FailedAuctionMaxBars { get; set; }

        [Display(Name = "Setups LVN / HVN", Order = 7, GroupName = "Setups Avancés")]
        public bool EnableNodeSetups { get; set; }

        [Range(5, 90)]
        [Display(Name = "Seuil LVN (% de la médiane)", Order = 8, GroupName = "Setups Avancés")]
        public int LvnThresholdPercent { get; set; }

        [Range(110, 500)]
        [Display(Name = "Seuil HVN (% de la médiane)", Order = 9, GroupName = "Setups Avancés")]
        public int HvnThresholdPercent { get; set; }

        [Range(0, 10)]
        [Display(Name = "Tolérance nœud (ticks)", Order = 10, GroupName = "Setups Avancés")]
        public int NodeToleranceTicks { get; set; }

        [Range(1, 20)]
        [Display(Name = "Validité Signal (barres)", Order = 5, GroupName = "Stratégies Volume Profile")]
        public int SignalValidityBars { get; set; }

        [Range(0, 100)]
        [Display(Name = "Confluence Min pour Alerte (%)", Order = 6, GroupName = "Stratégies Volume Profile")]
        public int MinConfluencePercentToAlert { get; set; }

        // ponderes par leur intensite ; le signal principal est le mieux confirme,
        // et non le premier d'une chaine if/else figee.
        [Display(Name = "Signaux multiples pondérés", Order = 7, GroupName = "Stratégies Volume Profile")]
        public bool UseWeightedMultiSignal { get; set; }

        [Range(50, 100)]
        [Display(Name = "Seuil de Conflit Directionnel (%)", Order = 8, GroupName = "Stratégies Volume Profile")]
        public int DirectionalConflictPercent { get; set; }

        // "tres forts" sur du bruit. Le seuil devient relatif a la volatilite realisee.
        [Display(Name = "Seuil Déplacement Adaptatif (ATR)", Order = 9, GroupName = "Stratégies Volume Profile")]
        public bool UseAdaptiveMovementThreshold { get; set; }

        [Range(0.05, 3.0)]
        [Display(Name = "Facteur ATR Déplacement", Order = 10, GroupName = "Stratégies Volume Profile")]
        public double MovementAtrFactor { get; set; }

        // VWAP utilisee uniquement comme niveau de confluence (pas de filtre directionnel).
        [Display(Name = "VWAP en confluence", Order = 1, GroupName = "Filtres Tendance")]
        public bool UseVwapFilter { get; set; }

        // Les proprietes manuelles ci-dessous restent presentes (retro-compatibilite
        // GUI NinjaTrader) mais sont SURCHARGEES des que AutoCalibrationV3 est actif.
        [Display(Name = "V3 - Calibration automatique (seuils adaptatifs)", Order = 0, GroupName = "V3. Calibration Auto",
                 Description = "Derive les seuils Delta/Volume/Absorption de statistiques glissantes (Z-MAD + percentiles) au lieu de constantes.")]
        public bool AutoCalibrationV3 { get; set; }

        [Display(Name = "V3 - Profiler d'instrument automatique", Order = 1, GroupName = "V3. Calibration Auto",
                 Description = "Classe l'instrument par MESURE (amplitude en ticks, volume median, densite par tick) et applique le preset correspondant.")]
        public bool AutoProfileInstrument { get; set; }

        // Affiche la VALEUR CALCULEE plutot que de simplement griser les reglages
        // manuels : l'utilisateur voit en clair ce que le moteur applique.
        [Display(Name = "V3 - Etat de calibration (lecture seule)", Order = 2, GroupName = "V3. Calibration Auto")]
        public string V3CalibrationState { get { return SniperCalibTag(); } }

        [Display(Name = "Activer Détection Absorption", Order = 1, GroupName = "Détection Absorption")]
        public bool EnableAbsorptionDetection { get; set; }

        [Range(10, 50000)]
        [Display(Name = "Seuil Delta Min (Bougie)", Order = 2, GroupName = "Détection Absorption")]
        public int AbsorptionDeltaThreshold { get; set; }

        [Range(5, 10000)]
        [Display(Name = "Seuil Volume Min par Tick", Order = 3, GroupName = "Détection Absorption")]
        public int AbsorptionTickVolumeThreshold { get; set; }

        [Display(Name = "Absorption aux Niveaux Clés Uniquement", Order = 4, GroupName = "Détection Absorption")]
        public bool AbsorptionOnlyAtKeyLevels { get; set; }

        [Range(1, 50)]
        [Display(Name = "Tolérance Niveaux Clés (ticks)", Order = 5, GroupName = "Détection Absorption")]
        public int AbsorptionKeyLevelTicks { get; set; }

        [Range(1, 100000)]
        [Display(Name = "Volume Total Min Bougie", Order = 6, GroupName = "Détection Absorption")]
        public int MinBarVolumeForAbsorption { get; set; }

        // (quantile glissant de |delta|) au lieu d'une constante instrument-dependante.
        [Display(Name = "Seuil Delta Adaptatif (quantile)", Order = 7, GroupName = "Détection Absorption")]
        public bool UseAdaptiveAbsorptionThreshold { get; set; }

        [Range(50, 99)]
        [Display(Name = "Percentile Delta Adaptatif", Order = 8, GroupName = "Détection Absorption")]
        public int AbsorptionDeltaPercentile { get; set; }

        [Range(50, 2000)]
        [Display(Name = "Fenêtre de Calibration (barres)", Order = 9, GroupName = "Détection Absorption")]
        public int AdaptiveCalibrationBars { get; set; }

        // Une distribution unique melange deux populations dont les moments
        // different d'un ordre de grandeur (volume, |delta|, range). Le
        // percentile global est alors domine par les barres RTH : de nuit tout
        // parait "faible" (aucun signal), et a l'ouverture tout parait "fort"
        // (signaux en rafale). On maintient donc deux distributions separees.
        // Ces proprietes n'ont volontairement pas d'attribut [NinjaScriptProperty]
        // afin de ne pas casser la signature d'appel des scripts existants.
        [Display(Name = "Calibration séparée RTH / ETH", Order = 10, GroupName = "Détection Absorption")]
        public bool EnableSessionBucketCalibration { get; set; }

        [Range(0, 2359)]
        [Display(Name = "Début RTH (HHMM, heure du graphique)", Order = 11, GroupName = "Détection Absorption")]
        public int RthStartHHMM { get; set; }

        [Range(0, 2359)]
        [Display(Name = "Fin RTH (HHMM, heure du graphique)", Order = 12, GroupName = "Détection Absorption")]
        public int RthEndHHMM { get; set; }

        // On scanne une fenetre de N ticks sous le high (resp. au-dessus du low).
        [Range(1, 20)]
        [Display(Name = "Fenêtre de scan aux extrêmes (ticks)", Order = 10, GroupName = "Détection Absorption")]
        public int AbsorptionProbeTicks { get; set; }

        // representer une part significative du volume total au niveau teste.
        [Range(0, 100)]
        [Display(Name = "Ratio d'agression min (%)", Order = 11, GroupName = "Détection Absorption")]
        public int AbsorptionMinAggressionPercent { get; set; }

        [Display(Name = "Exiger signal fort (candle + tick)", Order = 12, GroupName = "Détection Absorption")]
        public bool AbsorptionRequireStrongSignal { get; set; }

        [Display(Name = "Exiger résistance du prix (close vs open)", Order = 13, GroupName = "Détection Absorption")]
        public bool AbsorptionRequireCloseVsOpen { get; set; }

        [Display(Name = "Pondérer selon le biais HTF", Order = 14, GroupName = "Détection Absorption")]
        public bool AbsorptionUseTrendContext { get; set; }

        // l'agression debordant souvent de quelques ticks de part et d'autre du high/low.
        [Range(0, 10)]
        [Display(Name = "Scan symétrique autour des extrêmes (ticks)", Order = 15, GroupName = "Détection Absorption")]
        public int AbsorptionSymmetricTicks { get; set; }

        [Display(Name = "Activer Détection Iceberg", Order = 1, GroupName = "Détection Iceberg")]
        public bool EnableIcebergDetection { get; set; }

        [Range(2, 20)]
        [Display(Name = "Fenêtre Lookback Iceberg (barres)", Order = 2, GroupName = "Détection Iceberg")]
        public int IcebergLookbackBars { get; set; }

        [Range(100, 500000)]
        [Display(Name = "Agression Totale Min (Σ|delta|)", Order = 3, GroupName = "Détection Iceberg")]
        public int IcebergMinAggression { get; set; }

        [Range(1, 50)]
        [Display(Name = "Déplacement Max Prix (ticks)", Order = 4, GroupName = "Détection Iceberg")]
        public int IcebergMaxDisplacementTicks { get; set; }

        [Range(2, 100)]
        [Display(Name = "Range Max Fenêtre (ticks)", Order = 5, GroupName = "Détection Iceberg")]
        public int IcebergMaxRangeTicks { get; set; }

        [Display(Name = "Iceberg aux Niveaux Clés Uniquement", Order = 6, GroupName = "Détection Iceberg")]
        public bool IcebergOnlyAtKeyLevels { get; set; }

        [Range(5, 100)]
        [Display(Name = "Dominance Delta Min (%)", Order = 7, GroupName = "Détection Iceberg")]
        public int IcebergMinDominancePercent { get; set; }

        [Display(Name = "Filtrer avec ATR (Compression Adaptative)", Order = 8, GroupName = "Détection Iceberg")]
        public bool UseAtrRangeFilter { get; set; }

        [Range(0.1, 5.0)]
        [Display(Name = "Ratio Max Range / ATR", Order = 9, GroupName = "Détection Iceberg")]
        public double IcebergMaxAtrRatio { get; set; }

        [Range(1, 100)]
        [Display(Name = "Intensité Agression Min (% Vol)", Order = 10, GroupName = "Détection Iceberg")]
        public int IcebergMinAggressionRatioPercent { get; set; }

        [Range(50, 100)]
        [Display(Name = "Score Min Iceberg (0-100)", Order = 11, GroupName = "Détection Iceberg")]
        public int IcebergMinScore { get; set; }

        [Range(1, 50)]
        [Display(Name = "Tolérance Niveaux Clés Iceberg (ticks)", Order = 12, GroupName = "Détection Iceberg")]
        public int IcebergKeyLevelTicks { get; set; }

        // Une preuve de rejet du niveau est exigee avant toute direction.
        [Display(Name = "Exiger Preuve de Rejet (Iceberg)", Order = 13, GroupName = "Détection Iceberg")]
        public bool IcebergRequireRejection { get; set; }

        [Range(5, 90)]
        [Display(Name = "Rejet Min depuis le Pic (% du range)", Order = 14, GroupName = "Détection Iceberg")]
        public int IcebergMinRejectionPercent { get; set; }

        [Display(Name = "Activer Détection Imbalance/FVG", Order = 1, GroupName = "Détection Imbalance")]
        public bool EnableImbalanceDetection { get; set; }

        [Range(100, 1000)]
        [Display(Name = "Ratio Imbalance Min (%)", Order = 2, GroupName = "Détection Imbalance")]
        public int ImbalanceRatioPercent { get; set; }

        [Range(1, 20)]
        [Display(Name = "Niveaux Consécutifs Min", Order = 3, GroupName = "Détection Imbalance")]
        public int ImbalanceConsecutiveLevels { get; set; }

        [Display(Name = "Imbalance aux Niveaux Clés Uniquement", Order = 4, GroupName = "Détection Imbalance")]
        public bool ImbalanceOnlyAtKeyLevels { get; set; }

        [Range(1, 50)]
        [Display(Name = "Tolérance Niveaux Clés (ticks)", Order = 5, GroupName = "Détection Imbalance")]
        public int ImbalanceKeyLevelTicks { get; set; }

        [Range(0, 100000)]
        [Display(Name = "Volume Min par Niveau", Order = 6, GroupName = "Détection Imbalance")]
        public int ImbalanceMinLevelVolume { get; set; }

        // Ask(p) est compare au Bid(p - 1 tick) et Bid(p) a l'Ask(p + 1 tick),
        // au lieu d'une comparaison horizontale au meme prix.
        [Display(Name = "Imbalance Diagonale (standard)", Order = 7, GroupName = "Détection Imbalance")]
        public bool ImbalanceDiagonalMode { get; set; }

        [Range(0, 200)]
        [Display(Name = "Mémoire Zones Stacked (barres)", Order = 8, GroupName = "Détection Imbalance")]
        public int ImbalanceZoneMemoryBars { get; set; }

        [Range(0, 20)]
        [Display(Name = "Tolérance Retest Zone (ticks)", Order = 9, GroupName = "Détection Imbalance")]
        public int ImbalanceZoneRetestTicks { get; set; }

        [Range(2, 10)]
        [Display(Name = "Niveaux Min Zone Mémorisée", Order = 10, GroupName = "Détection Imbalance")]
        public int ImbalanceZoneMinLevels { get; set; }

        [Display(Name = "Activer Finished Auction", Order = 1, GroupName = "Auction & Épuisement")]
        public bool EnableFinishedAuction { get; set; }

        [Range(0, 20)]
        [Display(Name = "Volume Max à l'Extrême (Finished Auction)", Order = 2, GroupName = "Auction & Épuisement")]
        public int FinishedAuctionMaxVolume { get; set; }

        // POINT 3 : le seuil fixe (2 contrats) rend le module inerte sur les
        // instruments liquides (ES/NQ), ou l'extreme d'une barre porte couramment
        // des dizaines de contrats. Le seuil devient une FRACTION du volume moyen
        // par tick de l'instrument, avec le seuil fixe comme plancher.
        [Display(Name = "Seuil Finished Auction Adaptatif", Order = 5, GroupName = "Auction & Épuisement")]
        public bool UseAdaptiveFinishedAuction { get; set; }

        [Range(1, 100)]
        [Display(Name = "Volume Max = % du Volume/Tick Moyen", Order = 6, GroupName = "Auction & Épuisement")]
        public int FinishedAuctionVolumePercent { get; set; }

        [Display(Name = "Finished Auction aux Niveaux Clés Uniquement", Order = 3, GroupName = "Auction & Épuisement")]
        public bool FinishedAuctionOnlyAtKeyLevels { get; set; }

        [Range(1, 30)]
        [Display(Name = "Tolérance Niveaux Clés (ticks)", Order = 4, GroupName = "Auction & Épuisement")]
        public int FinishedAuctionKeyLevelTicks { get; set; }

        [Display(Name = "Activer Exhaustion", Order = 5, GroupName = "Auction & Épuisement")]
        public bool EnableExhaustion { get; set; }

        [Range(50, 99)]
        [Display(Name = "Percentile Delta/Volume (Exhaustion)", Order = 6, GroupName = "Auction & Épuisement")]
        public int ExhaustionPercentile { get; set; }

        [Range(1, 5)]
        [Display(Name = "Barres sans nouvel extrême (Exhaustion)", Order = 7, GroupName = "Auction & Épuisement")]
        public int ExhaustionFailBars { get; set; }

        [Display(Name = "Activer Delta Flip", Order = 1, GroupName = "Order Flow Delta")]
        public bool EnableDeltaFlip { get; set; }

        [Range(2, 10)]
        [Display(Name = "Barres par côté (Delta Flip)", Order = 2, GroupName = "Order Flow Delta")]
        public int DeltaFlipLookback { get; set; }

        [Range(0, 99)]
        [Display(Name = "Percentile Magnitude Flip", Order = 3, GroupName = "Order Flow Delta")]
        public int DeltaFlipMinPercentile { get; set; }

        [Display(Name = "Activer Divergence Cumulative Delta", Order = 4, GroupName = "Order Flow Delta")]
        public bool EnableCumDeltaDivergence { get; set; }

        [Range(1, 5)]
        [Display(Name = "Force du Swing (barres)", Order = 5, GroupName = "Order Flow Delta")]
        public int CumDeltaSwingStrength { get; set; }

        [Range(5, 200)]
        [Display(Name = "Fenêtre Divergence (barres)", Order = 6, GroupName = "Order Flow Delta")]
        public int CumDeltaDivergenceLookback { get; set; }

        [Range(1, 100)]
        [Display(Name = "Écart Min Cumulative Delta (%)", Order = 7, GroupName = "Order Flow Delta")]
        public int CumDeltaMinDivergencePercent { get; set; }

        [Display(Name = "Activer Filtre Régime ATR", Order = 1, GroupName = "Filtres Régime")]
        public bool UseRegimeFilter { get; set; }

        [Range(1, 100)]
        [Display(Name = "Période ATR Régime", Order = 2, GroupName = "Filtres Régime")]
        public int RegimeAtrPeriod { get; set; }

        [Range(0.1, 10.0)]
        [Display(Name = "ATR Min Régime (ticks)", Order = 3, GroupName = "Filtres Régime")]
        public double RegimeMinAtrTicks { get; set; }

        [Range(0.0, 50.0)]
        [Display(Name = "ATR Max Régime (ticks)", Order = 4, GroupName = "Filtres Régime")]
        public double RegimeMaxAtrTicks { get; set; }

        [Range(0, 200)]
        [Display(Name = "Max alertes / session (0 = illimite)", Order = 6, GroupName = "Telegram")]
        public int MaxAlertsPerSession { get; set; }

        [Display(Name = "Activer Gestion du Risque", Order = 1, GroupName = "Gestion du Risque")]
        public bool EnableRiskManagement { get; set; }

        [Range(1, 100)]
        [Display(Name = "Periode ATR (stop)", Order = 2, GroupName = "Gestion du Risque")]
        public int RiskAtrPeriod { get; set; }

        [Range(0.1, 10.0)]
        [Display(Name = "Multiple ATR du Stop", Order = 3, GroupName = "Gestion du Risque")]
        public double StopAtrMultiple { get; set; }

        [Range(0, 100)]
        [Display(Name = "Buffer Stop (ticks)", Order = 4, GroupName = "Gestion du Risque")]
        public int StopBufferTicks { get; set; }

        [Range(0.1, 20.0)]
        [Display(Name = "Cible 1 (R)", Order = 5, GroupName = "Gestion du Risque")]
        public double TargetR1 { get; set; }

        [Range(0.1, 20.0)]
        [Display(Name = "Cible 2 (R)", Order = 6, GroupName = "Gestion du Risque")]
        public double TargetR2 { get; set; }

        [Range(0.0, 10.0)]
        [Display(Name = "R:R minimum pour alerter", Order = 7, GroupName = "Gestion du Risque")]
        public double MinRiskReward { get; set; }

        [Range(0, 1000000)]
        [Display(Name = "Risque par trade (devise)", Order = 8, GroupName = "Gestion du Risque")]
        public double RiskPerTradeCurrency { get; set; }

        [Range(1, 500)]
        [Display(Name = "Contrats max", Order = 9, GroupName = "Gestion du Risque")]
        public int MaxContracts { get; set; }

        // Pas de [NinjaScriptProperty] pour ne pas modifier la signature publique.
        [Range(0, 20)]
        [Display(Name = "Cout execution (ticks)", Order = 10, GroupName = "Gestion du Risque")]
        public int ExecutionCostTicks { get; set; }

        // trop serre produit des sorties sur bruit, un stop trop large ruine le
        // R:R reel et la taille de position. Les bornes sont parametrables car
        // elles dependent de l'instrument (tick size / volatilite).
        [Range(1, 100)]
        [Display(Name = "Stop minimum (ticks)", Order = 11, GroupName = "Gestion du Risque")]
        public int MinStopTicks { get; set; }

        [Range(2, 1000)]
        [Display(Name = "Stop maximum (ticks)", Order = 12, GroupName = "Gestion du Risque")]
        public int MaxStopTicks { get; set; }

        // Cap du SL en pips : si la distance entry-stop depasse MaxStopPips,
        // le stop est ramene a MaxStopPips de l'entree. PipSize definit la
        // valeur d'un pip en prix (ex: 0.1 pour XAUUSD, 0.0001 pour EURUSD).
        [Range(0, 500)]
        [Display(Name = "Stop maximum (pips)", Order = 12, GroupName = "Gestion du Risque",
            Description = "Distance max du SL en pips. 0 = desactive. Le SL sera ramene a cette distance si trop loin.")]
        public int MaxStopPips { get; set; }

        [Range(0.00001, 100.0)]
        [Display(Name = "Taille d'un pip (prix)", Order = 12, GroupName = "Gestion du Risque",
            Description = "Valeur d'un pip en unite de prix. Ex: 0.1 pour Gold, 0.0001 pour EURUSD, 0.01 pour USDJPY.")]
        public double PipSize { get; set; }

        // devient suiveur une fois qu'une fraction du trajet vers T1 est acquise.
        [Display(Name = "Activer le trailing stop (runner T2)", Order = 13, GroupName = "Gestion du Risque")]
        public bool UseTrailingStop { get; set; }

        [Range(1, 100)]
        [Display(Name = "Declenchement trailing (% de T1)", Order = 14, GroupName = "Gestion du Risque")]
        public int TrailingStartPercent { get; set; }

        [Range(0.2, 10.0)]
        [Display(Name = "Largeur du trailing (R)", Order = 15, GroupName = "Gestion du Risque")]
        public double TrailWidthT2R { get; set; }

        [Display(Name = "Activer Filtre HTF", Order = 1, GroupName = "Confirmation HTF")]
        public bool EnableHtfFilter { get; set; }

        [Range(5, 1440)]
        [Display(Name = "Timeframe HTF (min)", Order = 2, GroupName = "Confirmation HTF")]
        public int HtfMinutes { get; set; }

        [Range(2, 400)]
        [Display(Name = "Periode EMA HTF", Order = 3, GroupName = "Confirmation HTF")]
        public int HtfEmaPeriod { get; set; }

        [Display(Name = "Mode strict (bloque contre-tendance)", Order = 4, GroupName = "Confirmation HTF")]
        public bool HtfStrictMode { get; set; }

        [Display(Name = "Activer Journal & Statistiques", Order = 1, GroupName = "Journal")]
        public bool EnableTradeJournal { get; set; }

        [Range(1, 200)]
        [Display(Name = "Duree max suivi signal (barres)", Order = 2, GroupName = "Journal")]
        public int JournalMaxBarsInTrade { get; set; }

        [Display(Name = "Chemin CSV (vide = dossier NinjaTrader)", Order = 3, GroupName = "Journal")]
        public string JournalFilePath { get; set; }

        [Display(Name = "Inclure stats dans l'alerte Telegram", Order = 4, GroupName = "Journal")]
        public bool IncludeStatsInAlert { get; set; }

        [Display(Name = "Journal temps reel uniquement", Order = 5, GroupName = "Journal")]
        public bool JournalLiveOnly { get; set; }

        // POINT 4 : journal exhaustif. Sans lui, le CSV ne contient que les signaux
        // REELLEMENT alertes : population biaisee par selection, inutilisable pour
        // calibrer des ponderations. En mode exhaustif, chaque signal directionnel
        // rejete par un filtre est aussi suivi et ecrit avec Mode=SHADOW et le
        // motif du rejet, SANS polluer les statistiques affichees.
        [Display(Name = "Journal exhaustif (inclut signaux filtres)", Order = 6, GroupName = "Journal")]
        public bool JournalShadowMode { get; set; }
        #endregion

        private int volumetricBarsIndex;

        // pas ajouter ses propres series (AddVolumetric / AddDataSeries sont interdits
        // hors du Configure de l'hote). La strategie declare donc les series AVANT
        // d'instancier l'indicateur, et l'indicateur se contente d'utiliser les index
        // annonces. [ThreadStatic] : chaque iteration d'optimisation tourne sur son
        // propre thread, la declaration ne fuit pas d'une iteration a l'autre.
        [ThreadStatic] private static bool hostedSeriesDeclared;
        [ThreadStatic] private static int hostedVolumetricIndex;
        [ThreadStatic] private static int hostedHtfIndex;

        /// <summary>Appele depuis State.Configure de la strategie hote, APRES ses
        /// AddVolumetric / AddDataSeries. htfIndex &lt;= 0 = pas de serie HTF.</summary>
        public static void DeclareHostedSeries(int volumetricIndex, int htfIndex)
        {
            hostedSeriesDeclared = true;
            hostedVolumetricIndex = volumetricIndex;
            hostedHtfIndex = htfIndex;
        }

        /// <summary>Remise a zero de la declaration (fin de strategie / Terminated).</summary>
        public static void ClearHostedSeries()
        {
            hostedSeriesDeclared = false;
            hostedVolumetricIndex = 0;
            hostedHtfIndex = -1;
        }
        // Delta de la barre evaluee : champ (et non variable locale) car
        // ComputeConfluence() doit y acceder.
        private long currentBarDelta;
        // Offset d'evaluation : 1 = on evalue la barre reellement cloturee.
        private int evalOffset = 0;
        // Index absolu de la barre evaluee (peut differer de CurrentBars).
        private int evalBarIndex = -1;
        // Index absolu de la premiere barre de la session courante (profil de session).
        private int sessionStartBarIndex = 0;
        // Cooldown par type de signal (au lieu d'un cooldown global unique).
        private readonly Dictionary<string, DateTime> lastAlertTimeBySignal = new Dictionary<string, DateTime>();

        // du Task (ou sur le thread UI via le Dispatcher), JAMAIS sur le thread de
        // calcul NinjaTrader. Il mutait directement lastAlertTimeBySignal, openSignals,
        // statsByFamily et les compteurs de session, lus/ecrits en parallele par
        // OnBarUpdate : collection modifiee pendant enumeration, corruption de
        // Dictionary, perte de compteurs. On ne mute plus rien depuis le callback :
        // on y depose une intention, drainee au debut de OnBarUpdate, donc executee
        // exclusivement sur le thread de calcul.
        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> pendingStateActions
            = new System.Collections.Concurrent.ConcurrentQueue<Action>();

        // A appeler UNIQUEMENT depuis OnBarUpdate (thread de calcul NinjaTrader).
        private void DrainPendingStateActions()
        {
            Action act;
            while (pendingStateActions.TryDequeue(out act))
            {
                try { if (act != null) act(); }
                catch (Exception ex)
                {
                    if (EnableDebugMode)
                        Print("VP_PendingState: " + ex.GetType().Name + " - " + ex.Message);
                }
            }
        }

        private string instrumentRoot;
        private string instrumentName;

        // Volume profile state (active/current calculation)
        private double sessionHigh, sessionLow;

        // Contribution figée d'une barre volumétrique au profil agrégé. Chaque barre
        // n'est balayée qu'une seule fois (puis rafraîchie tant qu'elle est en cours),
        // au lieu de rebalayer LookbackBars barres à chaque tick.
        private sealed class BarProfile
        {
            public long BaseTick;      // tick correspondant à Vols[0]
            public long[] Vols;        // volume par niveau de prix
            public long Total;         // somme des volumes du tableau
            public long Delta;         // BarDelta de la barre
            public long RawVolume;     // TotalVolume brut (détection de changement)
            public double High, Low;
        }

        private readonly Dictionary<int, BarProfile> includedBars = new Dictionary<int, BarProfile>(256);
        private readonly Stack<BarProfile> barProfilePool = new Stack<BarProfile>(256);
        private readonly List<int> barsToDrop = new List<int>(64);

        // Profil agrégé stocké en tableau dense (indexé par tick) : accès O(1),
        // aucune allocation, aucun tri.
        private long[] aggVols = new long[0];
        private long aggBaseTick = 0;
        private long aggMinTick = long.MaxValue;
        private long aggMaxTick = long.MinValue;
        // Permet de resserrer les bornes du profil au retrait d'une barre.
        private int aggNonZeroCount = 0;
        private bool profileDirty = true;
        // ne sont refaits que lorsque le profil a reellement change de barre.
        private bool extremesDirty = true;
        private int profileComputeBarIdx = -1;
        private int currentProfileBarIdx = 0;
        private bool forceProfileRecompute = true;



        // Liste triée réutilisée (paires tick/volume non nulles) pour POC/VA.
        private long[] profileTicks = new long[0];
        private long[] profileVols = new long[0];
        private int profileCount = 0;

        private double tickSize = 1.0;
        private double pocPrice, vahPrice, valPrice;
        private double currentVwapPrice = 0;
        private long currentCumulativeDelta;
        private long sessionTotalVolume;
        private string currentInterpretation = "Équilibre";
        private string currentSignal = "Pas de trade";
        private string activeBreakoutSignal = "NONE";
        private string lastTriggeredSignal = "Aucun";
        private DateTime lastSignalTime = DateTime.MinValue;
        private string lastAlertedSignal = "";
        private int confluenceScore = 0;
        
        // Synchronisation AMC Core <-> Sniper Engine: signaux validés par AMC Core
        private string amcCoreValidatedSignal = "";
        private bool amcCoreSignalDirectional = false;
        private int maxConfluenceScore = 4;
        private string confluenceDetails = "";
        private double confluenceWeighted = 0;
        // contributeurs actifs). Sans lui, pctWeighted n'etait pas borne a 100 %.
        private double maxConfluenceWeighted = 1;
        private bool valueAreaIncomplete = false;
        private double valueAreaCompleteness = 0.0;
        private bool valueAreaTooNarrow = false;


        // POINT 1 : familles de PREUVE (independantes entre elles). Plusieurs
        // candidats d'une meme famille decrivent le meme phenomene sous-jacent
        // (le delta, par exemple) : les additionner gonflait artificiellement la
        // confluence. On ne retient donc que le meilleur poids par famille.
        private const int FamilyStructure = 0;   // niveaux du profil, ruptures, retests
        private const int FamilyFlow = 1;        // tout ce qui derive du delta / order flow
        private const int FamilyExhaustion = 2;  // epuisement : signal de SORTIE, pas d'entree
        private const int FamilyOther = 3;       // divers non correles aux precedents
        private const int FamilyCount = 4;

        private struct SignalCandidate
        {
            public string Signal;
            public string Interpretation;
            public bool IsBuy;
            public double Weight;
            public bool Triggered;
            public int Family;
        }

        private readonly List<SignalCandidate> signalCandidates = new List<SignalCandidate>(16);
        private readonly List<string> triggeredSignalsThisBar = new List<string>(16);
        private readonly double[] bestByFamilyBuy = new double[FamilyCount];
        private readonly double[] bestByFamilySell = new double[FamilyCount];
        private string allSignalsText = "";
        private double buySideWeight = 0;
        private double sellSideWeight = 0;

        // POINT 5 : bascule d'arbitrage pour les egalites de volume strictes lors de
        // l'extension de la Value Area (evite un biais directionnel systematique).
        private bool vaTieBreakToggle = false;

        // RingBuffer remplace List<T> pour eviter les RemoveAt(0) en O(n).
        // La capacite passe de 512 a CalibrationBucketMax : en decoupant la
        // population en deux, garder une fenetre tres longue reintroduirait de
        // l'hysteresis (un seuil calibre sur des barres vieilles de plusieurs
        // jours) sans gain de precision. 200 barres par bucket suffisent a un
        // percentile stable tout en restant reactif a un changement de regime.
        private const int CalibrationBucketMax = 200;

        private RingBuffer<long> absDeltaHistory = new RingBuffer<long>(CalibrationBucketMax);
        private RingBuffer<double> barRangeHistory = new RingBuffer<double>(CalibrationBucketMax);
        private RingBuffer<long> absDeltaHistoryEth = new RingBuffer<long>(CalibrationBucketMax);
        private RingBuffer<double> barRangeHistoryEth = new RingBuffer<double>(CalibrationBucketMax);
        // Regime de la derniere barre close evaluee : pilote le choix du bucket.
        private bool currentBucketIsRth = true;
        private long[] percentileScratch = new long[0];
        private int adaptiveDeltaThreshold = 0;
        private double adaptiveAvgBarRange = 0;
        private int lastCalibrationBarIdx = -1;
        private int calibrationRefreshCounter = 0;

        private int bidAskProbeBars = 0;
        private int bidAskNonZeroBars = 0;
        private bool bidAskWarningSent = false;
        private bool bidAskDataMissing = false;

        // Frozen key levels (captured at bar start) for stable absorption/iceberg filtering
        private double frozenPocPrice = 0;
        private double frozenVahPrice = 0;
        private double frozenValPrice = 0;

        // Signal lifecycle: expiration tracking
        private int signalTriggerBarIndex = -1;

        // Absorption state
        private string currentAbsorptionStatus = "Néant";
        private bool isBullishAbsorptionActive = false;
        private bool isBearishAbsorptionActive = false;
        private long currentAbsorptionVolume = 0;
        private int lastAbsorptionBarIndex = -1;
        private bool isAbsorptionStrong = false;
        private double absorptionQualityFactor = 1.0;

        // Iceberg detection state
        private class IcebergBarSnapshot
        {
            public int BarIndex;
            public DateTime Time;
            public double Close;
            public double High;
            public double Low;
            public long BarDelta;
            public long TotalVolume;
        }

        private LinkedList<IcebergBarSnapshot> icebergHistory = new LinkedList<IcebergBarSnapshot>();
        private IcebergBarSnapshot currentIcebergSnapshot = null;
        private bool isIcebergBullish = false;
        private bool isIcebergBearish = false;
        // Iceberg detecte mais sans preuve de rejet : informatif, non directionnel.
        private bool isIcebergNeutral = false;
        private double icebergPrice = 0;
        private long icebergTotalAggression = 0;
        private long icebergNetDelta = 0;
        private string currentIcebergStatus = "Néant";
        private int lastIcebergBarIndex = -1;

        // Imbalance / FVG detection state
        private bool isImbalanceBullish = false;
        private bool isImbalanceBearish = false;
        private double imbalancePrice = 0;
        private int imbalanceConsecutiveCount = 0;
        private string currentImbalanceStatus = "Néant";
        private int lastImbalanceBarIndex = -1;

        private sealed class ImbalanceZone
        {
            public double Bottom;
            public double Top;
            public bool IsBull;
            public int Levels;
            public int BarIndex;
            public bool Retested;
            /// Une zone dont le candidat a ete bloque par un gate n'est pas consommee.</summary>
            public int RetestCount;
            public long ReferenceBarVolume;
        }

        private readonly List<ImbalanceZone> imbalanceZones = new List<ImbalanceZone>(64);
        private int lastZoneRegisteredBarIdx = -1;

        // absDeltaHistory ne contient que des valeurs ABSOLUES : impossible d'y lire
        // un changement de signe. Ces listes conservent le delta signe, le cumulatif
        // et les extremes de chaque barre close.
        // RingBuffer remplace List<T> pour eviter les RemoveAt(0) en O(n).
        private RingBuffer<long> signedDeltaHistory = new RingBuffer<long>(OrderFlowHistoryMax);
        private RingBuffer<long> cumDeltaHistory = new RingBuffer<long>(OrderFlowHistoryMax);
        private RingBuffer<double> barHighHistory = new RingBuffer<double>(OrderFlowHistoryMax);
        private RingBuffer<double> barLowHistory = new RingBuffer<double>(OrderFlowHistoryMax);
        private long runningCumDelta = 0;
        private long deltaFlipMagnitudeThreshold = 0;
        private const int OrderFlowHistoryMax = 400;

        private bool isDeltaFlipBullish = false;
        private bool isDeltaFlipBearish = false;
        private double deltaFlipStrength = 0;
        private string currentDeltaFlipStatus = "Néant";

        private bool isCumDeltaDivBullish = false;
        private bool isCumDeltaDivBearish = false;
        private double cumDeltaDivStrength = 0;
        private string currentCumDeltaDivStatus = "Néant";

        private bool isFinishedAuctionBuy = false;    // epuisement vendeur au low  -> BUY
        private bool isFinishedAuctionSell = false;   // epuisement acheteur au high -> SELL
        private string currentFinishedAuctionStatus = "Néant";
        private int lastFinishedAuctionBarIndex = -1;

        // Unfinished Business (Poor Highs / Poor Lows sans épuisement)
        private bool hasUnfinishedHigh = false;
        private double unfinishedHighPrice = 0;
        private int unfinishedHighBar = -1;
        private bool hasUnfinishedLow = false;
        private double unfinishedLowPrice = 0;
        private int unfinishedLowBar = -1;

        private RingBuffer<long> barVolumeHistory = new RingBuffer<long>(CalibrationBucketMax);
        private RingBuffer<long> barVolumeHistoryEth = new RingBuffer<long>(CalibrationBucketMax);
        private long exhaustionDeltaThreshold = 0;
        private long exhaustionVolumeThreshold = 0;
        private bool isExhaustionBuy = false;    // epuisement d'une jambe baissiere -> biais BUY faible
        private bool isExhaustionSell = false;   // epuisement d'une jambe haussiere -> biais SELL faible
        private double exhaustionStrength = 0;
        private string currentExhaustionStatus = "Néant";

        // Previous values for movement detection
        private double prevBarPocPrice = 0;
        private double prevBarVahPrice = 0;
        private double prevBarValPrice = 0;

        // Breakout anti-flicker
        private int lastBreakoutBarIndex = -1;

        private enum BreakoutPhase { None, Broken, Accepted, Retest }
        private BreakoutPhase breakoutPhase = BreakoutPhase.None;
        private bool breakoutIsUp = false;
        private double breakoutLevel = 0;
        private int breakoutStartBarIdx = -1;
        private int breakoutLifecycleBarIdx = -1;
        private int acceptanceBarCount = 0;
        // Un piège (Failed Auction) génère par construction des poids des deux
        // côtés : il doit échapper au filtre de conflit directionnel.
        private bool trapSignalThisBar = false;

        private long medianNodeVolume = 0;
        private long lvnVolumeThreshold = 0;
        private long hvnVolumeThreshold = 0;
        private long[] nodeScratch = new long[0];

        // Regime filter
        private ATR regimeAtr;

        private DateTime lastAlertTime = DateTime.MinValue;
        private int signalsSentCount = 0;
        private int sessionAlertsCount = 0;

        private ATR riskAtr;
        private double lastEntryPrice, lastStopPrice, lastTarget1, lastTarget2, lastRiskTicks, lastRiskReward;
        private int lastPositionSize;
        // garde-fous (stop trop serre / trop large / budget de risque insuffisant).
        private bool lastRiskGuardRejected;

        private int htfBarsIndex = -1;
        private EMA htfEma;
        private int htfBias = 0;            // +1 haussier, -1 baissier, 0 neutre
        private string htfBiasText = "N/A";

        private class TrackedSignal
        {
            public DateTime Time;
            public string Signal;
            public string Family;
            public bool IsBuy;
            public double Entry, Stop, Target1, Target2;
            public int BarIndex;
            public int Confluence;
            // POINT 4 : journal exhaustif (population non biaisee par la selection).
            public double ConfluencePercent;
            public bool Shadow;
            public string Reason;
            public long SignalId;
            // posterieure au signal n'y entre, sous peine de fuite temporelle).
            public FeatureSnapshot Features;
            public bool Target1Hit;
            public bool TrailActive;
            public double TrailStop;
            public double BestPrice;
        }

        // dedie possede les StreamWriter (AutoFlush = false), ecrit et flush
        // periodiquement. Plus aucune I/O disque ni aucun lock sur le chemin
        // des ticks : la latence de OnBarUpdate devient independante du disque.
        private sealed class JournalWriterService : IDisposable
        {
            private struct Item { public string Path; public string Header; public string Line; }

            private readonly BlockingCollection<Item> queue =
                new BlockingCollection<Item>(new ConcurrentQueue<Item>(), 8192);
            private readonly Dictionary<string, StreamWriter> writers =
                new Dictionary<string, StreamWriter>(4);
            private readonly object writersLock = new object();
            private readonly Thread worker;
            private readonly Action<string> log;
            private int dropped;

            public JournalWriterService(Action<string> logger)
            {
                log = logger;
                worker = new Thread(Loop);
                worker.IsBackground = true;
                worker.Name = "SniperMarketCorePro.Journal";
                worker.Start();
            }

            public int Dropped { get { return dropped; } }

            /// Si la file est saturee la ligne est comptee comme perdue plutot que
            /// de retarder le traitement des ticks.</summary>
            public void Enqueue(string path, string header, string line)
            {
                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(line)) return;
                Item it = new Item();
                it.Path = path; it.Header = header; it.Line = line;
                try { if (!queue.TryAdd(it)) Interlocked.Increment(ref dropped); }
                catch (Exception) { Interlocked.Increment(ref dropped); }
            }

            private void Loop()
            {
                DateTime lastFlush = DateTime.UtcNow;
                try
                {
                    foreach (Item it in queue.GetConsumingEnumerable())
                    {
                        Write(it);
                        if ((DateTime.UtcNow - lastFlush).TotalMilliseconds >= 1000.0)
                        {
                            FlushAll();
                            lastFlush = DateTime.UtcNow;
                        }
                    }
                }
                catch (Exception ex) { Report("boucle interrompue : " + ex.Message); }
                finally { FlushAll(); CloseAll(); }
            }

            private void Write(Item it)
            {
                try
                {
                    // FIX AUDIT #1: Protection complète du dictionnaire writers avec lock
                    StreamWriter sw;
                    lock (writersLock)
                    {
                        if (!writers.TryGetValue(it.Path, out sw))
                        {
                            string dir = Path.GetDirectoryName(it.Path);
                            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                                Directory.CreateDirectory(dir);
                            // Le seul File.Exists du systeme : une fois par fichier et
                            // par cycle de vie, sur le thread ecrivain (jamais sur les ticks).
                            bool isNew = !File.Exists(it.Path) || new FileInfo(it.Path).Length == 0L;
                            FileStream fs = new FileStream(it.Path, FileMode.Append,
                                FileAccess.Write, FileShare.Read, 8192);
                            sw = new StreamWriter(fs, Encoding.UTF8);
                            sw.AutoFlush = false;
                            writers[it.Path] = sw;
                            if (isNew && !string.IsNullOrEmpty(it.Header)) sw.Write(it.Header);
                        }
                    }
                    sw.Write(it.Line);
                }
                catch (Exception ex)
                {
                    Report("ecriture impossible (" + ex.GetType().Name + " - " + ex.Message + ")");
                }
            }

            private void FlushAll()
            {
                foreach (KeyValuePair<string, StreamWriter> kv in writers)
                {
                    try { kv.Value.Flush(); }
                    catch (Exception ex) { Report("flush impossible (" + ex.Message + ")"); }
                }
            }

            private void CloseAll()
            {
                foreach (KeyValuePair<string, StreamWriter> kv in writers)
                {
                    try { kv.Value.Dispose(); }
                    catch (Exception) { }
                }
                writers.Clear();
            }

            private void Report(string message)
            {
                Action<string> l = log;
                if (l == null) return;
                try { l("VP_Journal: " + message); }
                catch (Exception) { }
            }

            /// <summary>Arret propre : la file est drainee puis les fichiers fermes
            /// (flush garanti, aucune ligne perdue en State.Terminated).</summary>
            public void Dispose()
            {
                try { queue.CompleteAdding(); } catch (Exception) { }
                try { if (worker != null && worker.IsAlive) worker.Join(2000); } catch (Exception) { }
                try { queue.Dispose(); } catch (Exception) { }
            }
        }

        private class FamilyStats
        {
            public int Wins, Losses, Timeouts;
            public double SumR;
            public int Total { get { return Wins + Losses + Timeouts; } }
            public double WinRate { get { return Total > 0 ? 100.0 * Wins / Total : 0.0; } }
        }

        private readonly List<TrackedSignal> openSignals = new List<TrackedSignal>(32);
        private readonly Dictionary<string, FamilyStats> statsByFamily = new Dictionary<string, FamilyStats>();
        private readonly FamilyStats globalStats = new FamilyStats();
        private readonly object journalLock = new object();
        private readonly object writersLock = new object(); // FIX AUDIT #1: Lock pour protéger le dictionnaire writers
        private string journalPathResolved = null;
        private bool journalHeaderWritten = false;
        private JournalWriterService journalWriter = null;

        // FIX AUDIT #6: Méthode de validation des paramètres utilisateur
        private void ValidateParameters()
        {
            try
            {
                // Validation preset Sniper
                if (TradingPreset == SniperMarketPreset.Sniper && MinConfluencePercentToAlert < 80)
                {
                    Print("WARNING: Sniper preset requires MinConfluencePercentToAlert >= 80. Auto-adjusting to 80.");
                    MinConfluencePercentToAlert = 80;
                }
                
                // Validation preset ScalpingPro
                if (TradingPreset == SniperMarketPreset.ScalpingPro && MaxAlertsPerSession > 10)
                {
                    Print("WARNING: ScalpingPro preset typically uses MaxAlertsPerSession <= 10. Current value: " + MaxAlertsPerSession);
                }
                
                // Validation Stop ATR Multiple
                if (StopAtrMultiple < 0.1 || StopAtrMultiple > 10.0)
                {
                    Print("WARNING: StopAtrMultiple out of reasonable range [" + StopAtrMultiple + "]. Resetting to 1.0");
                    StopAtrMultiple = 1.0;
                }
                
                // Validation Target R multiples
                if (TargetR1 < 0.1 || TargetR1 > 20.0)
                {
                    Print("WARNING: TargetR1 out of reasonable range [" + TargetR1 + "]. Resetting to 1.0");
                    TargetR1 = 1.0;
                }
                
                if (TargetR2 < TargetR1)
                {
                    Print("WARNING: TargetR2 [" + TargetR2 + "] should be >= TargetR1 [" + TargetR1 + "]. Auto-adjusting.");
                    TargetR2 = Math.Max(TargetR1 * 1.5, TargetR1);
                }
                
                // Validation Risk per trade
                if (RiskPerTradeCurrency < 0)
                {
                    Print("WARNING: RiskPerTradeCurrency cannot be negative. Resetting to 100");
                    RiskPerTradeCurrency = 100;
                }
                
                // Validation Telegram cooldown
                if (AlertCooldownSeconds < 5)
                {
                    Print("WARNING: AlertCooldownSeconds too low [" + AlertCooldownSeconds + "s]. Minimum recommended: 5s");
                }
                
                // Validation LookbackBars
                if (LookbackBars < 10 || LookbackBars > 2000)
                {
                    Print("WARNING: LookbackBars out of reasonable range [" + LookbackBars + "]. Resetting to 500");
                    LookbackBars = 500;
                }
            }
            catch (Exception ex)
            {
                Print("ERROR during parameter validation: " + ex.Message);
            }
        }
        private int runtimeErrorCount = 0;
        private string lastRuntimeError = "";
        private int profileOutOfRangeCount = 0;
        private readonly StringBuilder alertChangesBuilder = new StringBuilder(512);

        // Cached rolling average volume — computed once per bar index instead of
        // rescanning LookbackBars volumes on every evaluation.
        private int cachedAvgVolBarIdx = -1;
        private long cachedAvgVolume = 0;

        // Caches de rendu : évitent de reconstruire/redessiner ce qui n'a pas changé.
        private readonly StringBuilder dashboardBuilder = new StringBuilder(768);
        private string lastDashboardText = null;
        // evaluee avant toute construction de chaine.
        private long lastDashboardFingerprint = long.MinValue;
        private const int BidAskProbeBars = 50;
        private const int MaxTelegramInFlight = 8;
        private int telegramInFlightCount;
        private double lastDrawnPoc = double.NaN, lastDrawnVah = double.NaN, lastDrawnVal = double.NaN;
        private NinjaTrader.Gui.Tools.SimpleFont levelLabelFont;
        private readonly Dictionary<string, string> cleanTextCache = new Dictionary<string, string>(64);
        private readonly Dictionary<long, long> icebergPriceVolMap = new Dictionary<long, long>(256);
        private readonly List<string> confListReusable = new List<string>(8);

        // Cache du pic de volume iceberg : les barres closes sont immuables,
        // inutile de rebalayer la fenêtre à chaque tick.

        // Échelle de score d'agression, découplée du seuil utilisateur.
        private const double IcebergAggressionScoreScale = 3.0;

        private OrderFlowVWAP ofVwap;

        private NinjaTrader.Gui.Tools.SimpleFont dashboardFont;

        // Applique le preset choisi par l'utilisateur. Les valeurs ne sont ecrasees
        // que si l'utilisateur n'a PAS personnalise le parametre (comparaison avec la
        // des reglages manuels en changeant de preset.
        private void ApplyTradingPreset()
        {
            if (TradingPreset == SniperMarketPreset.Standard)
            {
                // Standard : seuils relaches. On n'ecrase que si la valeur est
                // encore celle du preset Sniper (= non personnalisee).
                // MinConfluencePercentToAlert. Le seuil de confluence est le
                // parametre de risque le plus structurant : il reste sous le
                // controle exclusif de l'utilisateur.
                if (MinConfluencePercentToAlert == 70)
                    Print("SniperMarketCorePro: preset Standard applique. MinConfluencePercentToAlert reste a "
                        + MinConfluencePercentToAlert + "% (valeur non modifiee, ajustez-la manuellement si besoin).");
                if (DirectionalConflictPercent == 70) DirectionalConflictPercent = 80;
                if (IcebergMinAggression == 750) IcebergMinAggression = 500;
                if (IcebergMinScore == 85) IcebergMinScore = 80;
                if (MinRiskReward == 2.0) MinRiskReward = 1.5;
                if (HtfStrictMode) HtfStrictMode = false;
                if (UseRegimeFilter) UseRegimeFilter = false;
            }
            else if (TradingPreset == SniperMarketPreset.Scanner)
            {
                // un profil complet et assume : il ecrase les seuils de selectivite de
                // maniere deterministe (sinon la moitie de la configuration resterait
                // aux valeurs Sniper et le mode serait incoherent). Les reglages qui
                // n'appartiennent pas au profil (Telegram, chemins, risque en devise,
                // periodes d'indicateurs) ne sont jamais touches.
                ApplyScannerPreset();
            }
            else if (TradingPreset == SniperMarketPreset.Scalping)
            {
                // et deterministe), pousse a l'extreme : emission intrabar, gates
                // quasi neutralises, HTF desactive, R:R 0.5, stop serre.
                ApplyScalpingPreset();
            }
            else if (TradingPreset == SniperMarketPreset.ScalpingPro)
            {
                ApplyScalpingProPreset();
            }
            else // Sniper
            {
                // Sniper : seuils renforces (70% de confluence minimum).
                if (MinConfluencePercentToAlert < 70)
                    Print("SniperMarketCorePro: preset Sniper applique. MinConfluencePercentToAlert reste a "
                        + MinConfluencePercentToAlert + "% (valeur non modifiee, 70% recommande en mode Sniper).");
                if (DirectionalConflictPercent == 80) DirectionalConflictPercent = 70;
                if (IcebergMinAggression == 500) IcebergMinAggression = 750;
                if (IcebergMinScore == 80) IcebergMinScore = 85;
                if (MinRiskReward == 1.5) MinRiskReward = 2.0;
                if (!HtfStrictMode) HtfStrictMode = true;
                if (!UseRegimeFilter) UseRegimeFilter = true;
            }
        }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                MarketIntelligenceSetDefaults();
                VolumeProfileSetDefaults();
                Description = "SniperMarketCorePro V7.9 : fusion AMC Pro + Sniper V2.0 + Scalping Pro V7.8 + Market Intelligence V7.8 + Volume Profile V2 Déterministe & Persistant SQLite.";
                Name = "SniperMarketCorePro";
                Calculate = Calculate.OnEachTick;
                // calcul et donc les alertes Telegram des que le chart passe en
                // arriere-plan. Inacceptable pour un moteur d'alerte temps reel.
                IsSuspendedWhileInactive = false;
                IsOverlay = true;

                BotToken = "";
                ChatId = "";
                ChatId2 = "";
                ScoreThresholdChat2 = 70;
                AlertCooldownSeconds = 300;
                MovementThresholdTicks = 2;
                LookbackBars = 150;
                ValueAreaPercent = 70;
                VolumetricTimeframe = 5;
                // Un profil glissant de 150 barres produit un POC qui derive en
                // continu et qui ne correspond a aucun niveau observe par les
                // autres participants. Le profil de session est l'objet de
                // reference sur toutes les plateformes pro : ses niveaux sont
                // partages, donc reellement reactifs.
                UseSessionProfile = true;
                ShowDashboard = true;
                ShowLevelLines = true;
                EnableDebugMode = false;
                TradingPreset = SniperMarketPreset.Sniper;
                IbPeriodMinutes = 60;

                EnableBreakoutSignals = true;
                EnableRejectionSignals = true;
                RequireDeltaConfirmation = true;
                EvaluateOnBarClose = true;

                // Setups avancés
                EnableAcceptanceSetups = true;
                AcceptanceBars = 2;
                RetestToleranceTicks = 4;
                RetestMaxBars = 8;
                EnableFailedAuction = true;
                FailedAuctionMaxBars = 3;
                EnableNodeSetups = true;
                LvnThresholdPercent = 30;
                HvnThresholdPercent = 150;
                NodeToleranceTicks = 2;
                SignalValidityBars = 3;
        
                MinConfluencePercentToAlert = 70;
        
                UseWeightedMultiSignal = true;
        
                // [CONFIG PROD] Plus strict en range : 70% au lieu de 80%
                DirectionalConflictPercent = 70;
        
                UseAdaptiveMovementThreshold = true;
                MovementAtrFactor = 0.25;

                AutoCalibrationV3 = true;
                AutoProfileInstrument = true;

                EnableAbsorptionDetection = true;
                AbsorptionDeltaThreshold = 300;
                AbsorptionTickVolumeThreshold = 100;
                AbsorptionOnlyAtKeyLevels = true;
                AbsorptionKeyLevelTicks = 5;
                MinBarVolumeForAbsorption = 200;
                UseAdaptiveAbsorptionThreshold = true;
                AbsorptionDeltaPercentile = 90;
                AdaptiveCalibrationBars = 300;
                // exprimee dans le fuseau du graphique (ES/NQ : 09:30-16:00 ET).
                EnableSessionBucketCalibration = true;
                RthStartHHMM = 930;
                RthEndHHMM = 1600;
                AbsorptionProbeTicks = 3;
                AbsorptionMinAggressionPercent = 40;
                AbsorptionRequireStrongSignal = true;
                AbsorptionRequireCloseVsOpen = true;
                AbsorptionUseTrendContext = true;
                AbsorptionSymmetricTicks = 2;

                EnableIcebergDetection = true;
                IcebergLookbackBars = 5;
        
                // [CONFIG PROD] Plus sélectif : 750 au lieu de 500
                IcebergMinAggression = 750;
        
                IcebergMaxDisplacementTicks = 3;
                IcebergMaxRangeTicks = 8;
                IcebergOnlyAtKeyLevels = true;
                IcebergMinDominancePercent = 35;
                UseAtrRangeFilter = true;
                IcebergMaxAtrRatio = 1.2;
                IcebergMinAggressionRatioPercent = 15;
        
                // [CONFIG PROD] Plus strict : 85 au lieu de 80
                IcebergMinScore = 85;
        
                IcebergKeyLevelTicks = 5;
                IcebergRequireRejection = true;
                IcebergMinRejectionPercent = 25;

                UseVwapFilter = true;

                EnableImbalanceDetection = true;
                ImbalanceRatioPercent = 300;
                ImbalanceConsecutiveLevels = 3;
                ImbalanceOnlyAtKeyLevels = false;
                ImbalanceKeyLevelTicks = 5;
                ImbalanceMinLevelVolume = 50;
                ImbalanceDiagonalMode = true;
                ImbalanceZoneMemoryBars = 20;
                ImbalanceZoneRetestTicks = 2;
                ImbalanceZoneMinLevels = 3;

                EnableDeltaFlip = true;
                DeltaFlipLookback = 3;
                DeltaFlipMinPercentile = 60;
                EnableCumDeltaDivergence = true;
                // quasi toute fluctuation passe). MinDivergencePercent relevé de 15% à
                // 100% (15% d'un sigma de random walk est franchi ~85% du temps : la
                // condition était quasi permanente et le signal ne discriminait plus).
                CumDeltaSwingStrength = 4;
                CumDeltaDivergenceLookback = 40;
                CumDeltaMinDivergencePercent = 100;
                // backtest charge anterieurement. Le [Range(1,100)] impose 100 max,
                // mais le clamp ici garantit que le chargement XML ne declenche pas
                // l'erreur "not in valid range between 1 and 100".
                if (CumDeltaMinDivergencePercent < 1) CumDeltaMinDivergencePercent = 1;
                if (CumDeltaMinDivergencePercent > 100) CumDeltaMinDivergencePercent = 100;

                EnableFinishedAuction = true;
                FinishedAuctionMaxVolume = 2;
                FinishedAuctionOnlyAtKeyLevels = true;
                FinishedAuctionKeyLevelTicks = 5;
                // POINT 3 : seuil adaptatif actif par defaut (le seuil fixe reste le plancher).
                UseAdaptiveFinishedAuction = true;
                FinishedAuctionVolumePercent = 15;

                EnableExhaustion = true;
                ExhaustionPercentile = 85;
                ExhaustionFailBars = 2;

                // [CONFIG PROD] Filtre de régime activé par défaut
                UseRegimeFilter = true;
                RegimeAtrPeriod = 14;
                RegimeMinAtrTicks = 0.5;
                RegimeMaxAtrTicks = 0.0; // 0 = pas de plafond

                MaxAlertsPerSession = 0;

                EnableRiskManagement = true;
                RiskAtrPeriod = 14;
                StopAtrMultiple = 1.5;
                StopBufferTicks = 2;
                TargetR1 = 1.5;
                TargetR2 = 3.0;
        
                // [CONFIG PROD] R:R plus sélectif : 2.0 au lieu de 1.5
                MinRiskReward = 2.0;
        
                RiskPerTradeCurrency = 200;
                MaxContracts = 5;
                ExecutionCostTicks = 1;
                MinStopTicks = 4;
                MaxStopTicks = 50;
                MaxStopPips = 30;
                PipSize = 0.1;
                UseTrailingStop = true;
                TrailingStartPercent = 50;
                TrailWidthT2R = 2.0;

                EnableHtfFilter = true;
                HtfMinutes = 60;
                HtfEmaPeriod = 50;
                HtfStrictMode = true;

                EnableTradeJournal = true;
                JournalMaxBarsInTrade = 24;
                JournalFilePath = "";
                IncludeStatsInAlert = true;
        
                // [CONFIG PROD] Journal temps réel uniquement pour des stats fiables
                JournalLiveOnly = true;
                // POINT 4 : journal exhaustif actif -> les signaux filtres sont traces
                // (Mode=SHADOW) sans polluer les statistiques diffusees.
                JournalShadowMode = true;

                ApplySniperDefaults();
            }
            else if (State == State.Configure)
            {
                // proprietes. Le SetDefaults se deroule avant le parsing XML, donc
                // une valeur 110 sauvegardee dans le XML du backtest passerait le
                // SetDefaults puis declencherait l'erreur ici. On clamp immediatement.
                if (CumDeltaMinDivergencePercent < 1) CumDeltaMinDivergencePercent = 1;
                if (CumDeltaMinDivergencePercent > 100) CumDeltaMinDivergencePercent = 100;

                MaximumBarsLookBack = MaximumBarsLookBack.Infinite;

                ApplyTradingPreset();

                // lastTarget1 vaut au minimum entree +/- risque x TargetR1. Si
                // MinRiskReward > TargetR1, le filtre R:R ne peut etre satisfait que
                // par un niveau STRUCTUREL plus eloigne : dans tous les autres cas
                // l'alerte est rejetee. Avec le preset Sniper (TargetR1=1.5,
                // MinRiskReward=2.0) le systeme etait donc muet par construction, sans
                // aucun message d'erreur. On aligne l'objectif de volatilite sur
                // l'exigence minimale, et on le dit explicitement.
                // de risque AVANT toute evaluation. Un jeu de parametres incoherent
                // rendait le systeme muet (ou dangereux) sans aucun diagnostic.
                if (StopAtrMultiple < 0.5)
                {
                    Print(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "VP CORRECTIF : StopAtrMultiple ({0:F2}) < 0.5 -> stop trop serre, sorties prematurees. Valeur portee a 1.0.", StopAtrMultiple));
                    StopAtrMultiple = 1.0;
                }
                if (RiskPerTradeCurrency <= 0)
                {
                    Print("VP CORRECTIF : RiskPerTradeCurrency <= 0 -> aucun budget de risque defini. Valeur portee a 100.");
                    RiskPerTradeCurrency = 100;
                }
                if (MinStopTicks < 1) MinStopTicks = 1;
                if (MaxStopTicks <= MinStopTicks) MaxStopTicks = MinStopTicks * 10;
                if (MaxStopPips > 0 && PipSize <= 0)
                {
                    PipSize = 0.1; // defaut Gold XAUUSD
                    Print("VP CORRECTIF : PipSize non defini avec MaxStopPips actif -> defaut 0.1 (Gold XAUUSD).");
                }

                if (MinRiskReward > TargetR1)
                {
                    Print(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "VP AVERTISSEMENT : MinRiskReward ({0:F2}) > TargetR1 ({1:F2}) -> la cible 1 ne pouvait satisfaire le filtre R:R que via un niveau structurel. TargetR1 releve a {0:F2}.",
                        MinRiskReward, TargetR1));
                    TargetR1 = MinRiskReward;
                }

                // cas (et plus seulement dans la branche MinRiskReward > TargetR1).
                if (TargetR2 <= TargetR1)
                {
                    Print(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "VP CORRECTIF : TargetR2 ({0:F2}) <= TargetR1 ({1:F2}) -> cible 2 recalee a {2:F2}R.",
                        TargetR2, TargetR1, TargetR1 * 2.0));
                    TargetR2 = TargetR1 * 2.0;
                }

                // peut valider puis invalider un setup avant sa cloture). On previent
                // explicitement l'utilisateur au lieu de laisser le biais silencieux.
                if (!EvaluateOnBarClose)
                    Print("VP AVERTISSEMENT : EvaluateOnBarClose=false -> signaux intrabar sujets au repaint. Statistiques du journal non fiables en backtest.");

                // strategie, on ne fait qu'adopter les index declares.
                if (hostedSeriesDeclared)
                {
                    volumetricBarsIndex = hostedVolumetricIndex;
                    htfBarsIndex = hostedHtfIndex;
                    if (htfBarsIndex <= 0) EnableHtfFilter = false;
                }
                else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Volumetric &&
                    BarsPeriod.BaseBarsPeriodType == BarsPeriodType.Minute &&
                    BarsPeriod.Value == VolumetricTimeframe)
                {
                    volumetricBarsIndex = 0;
                    if (EnableHtfFilter)
                    {
                        AddDataSeries(Instrument.FullName, BarsPeriodType.Minute, HtfMinutes, MarketDataType.Last);
                        htfBarsIndex = 1;
                    }
                }
                else
                {
                    AddVolumetric(Instrument.FullName, BarsPeriodType.Minute, VolumetricTimeframe, VolumetricDeltaType.BidAsk, 1);
                    volumetricBarsIndex = 1;

                    if (EnableHtfFilter)
                    {
                        AddDataSeries(Instrument.FullName, BarsPeriodType.Minute, HtfMinutes, MarketDataType.Last);
                        htfBarsIndex = 2;
                    }
                }
                MarketIntelligenceConfigure();
            }
            else if (State == State.DataLoaded)
            {
                instrumentRoot = ResolveInstrumentRoot(Instrument.MasterInstrument.Name);
                instrumentName = Instrument.FullName;
                ResetSessionTrackers();
                prevBarPocPrice = 0;
                prevBarVahPrice = 0;
                prevBarValPrice = 0;
                frozenPocPrice = 0;
                frozenVahPrice = 0;
                frozenValPrice = 0;
                signalTriggerBarIndex = -1;
                lastBreakoutBarIndex = -1;
                dashboardFont = new NinjaTrader.Gui.Tools.SimpleFont("Consolas", 12) { Bold = true };
                levelLabelFont = new NinjaTrader.Gui.Tools.SimpleFont("Consolas", 10) { Bold = true };
                tickSize = TickSize;

                ofVwap = OrderFlowVWAP(BarsArray[volumetricBarsIndex], VWAPResolution.Standard, TradingHours.UseDataSeriesSettingsInstance, VWAPStandardDeviations.Three, 1.0, 2.0, 3.0);
                regimeAtr = ATR(BarsArray[volumetricBarsIndex], RegimeAtrPeriod);
                riskAtr = ATR(BarsArray[volumetricBarsIndex], RiskAtrPeriod);
                if (EnableHtfFilter && htfBarsIndex > 0 && htfBarsIndex < BarsArray.Length)
                    htfEma = EMA(BarsArray[htfBarsIndex], HtfEmaPeriod);
                journalPathResolved = ResolveJournalPath();
                openSignals.Clear();
                // (Naked POC) et les zones d'imbalance d'un instrument ou d'une
                // periode precedente survivent a un F5 / changement de timeframe
                // et generent des confluences fantomes a des prix sans rapport.
                sessionHistory.Clear();
                imbalanceZones.Clear();
                lastZoneRegisteredBarIdx = -1;
                sniperJournalPathCached = null;
                sniperOutcomePathCached = null;
                journalHeaderWritten = false;
                sniperJournalHeaderWritten = false;
                runtimeErrorCount = 0;
                lastRuntimeError = "";
                profileOutOfRangeCount = 0;
                if (journalWriter != null) { journalWriter.Dispose(); journalWriter = null; }
                journalWriter = new JournalWriterService(SafePrint);
                // echantillon vide a chaque F5 / changement de timeframe, et
                // globalStats n'etait meme pas remis a zero (compteurs incoherents).
                InitializeFeatureInfrastructure();
                // instrument ou d'une periode precedente (F5, changement de timeframe)
                lastAlertTimeBySignal.Clear();
                if (telegramCts != null)
                {
                    // sinon un envoi en cours utilise un token deja libere
                    // (ObjectDisposedException sur le thread du Task).
                    try { telegramCts.Cancel(); }
                    catch (Exception ex) { RegisterRuntimeError("TelegramCts.Cancel", ex); }
                    telegramCts.Dispose();
                }
                // Les intentions heritees d'un cycle precedent ne doivent pas etre
                // appliquees apres un rechargement de donnees.
                Action drop;
                while (pendingStateActions.TryDequeue(out drop)) { }
                telegramCts = new CancellationTokenSource();

                InitSniperEngine();
                MarketIntelligenceDataLoaded();
                VolumeProfileDataLoaded();
                InitTcpBridge();
                
                // FIX AUDIT #6: Validation des paramètres utilisateur pour éviter les combinaisons invalides
                ValidateParameters();
            }
            else if (State == State.Terminated)
            {
                StopTcpBridge();
                if (telegramCts != null)
                {
                    telegramCts.Cancel();
                    telegramCts.Dispose();
                    telegramCts = null;
                }
                MarketIntelligenceDispose();
                VolumeProfileTerminated();
                lastAlertTimeBySignal.Clear();
                dashboardFont = null;
                levelLabelFont = null;
                includedBars.Clear();
                barProfilePool.Clear();
                cleanTextCache.Clear();
                ofVwap = null;
                regimeAtr = null;
                riskAtr = null;
                htfEma = null;
                // traces en SESSION_END plutot que supprimes. Le prix de sortie n'est
                // pas fiable ici (serie potentiellement deja liberee) : R = 0.
                try { FlushOpenSignalsAtSessionEnd(0.0); }
                catch (Exception ex) { if (EnableDebugMode) Print("VP_Flush: " + ex.Message); }
                openSignals.Clear();
                // ci-dessus vient d'ecrire des issues SESSION_END : sans cet appel,
                // elles seraient perdues au prochain demarrage.
                try { SavePersistedStats(); }
                catch (Exception ex) { if (EnableDebugMode) Print("VP_Stats: " + ex.Message); }
                // exception survenait pendant le flush. Les deux populations sont
                ClearOpenSniperTrades();
                if (journalWriter != null) { journalWriter.Dispose(); journalWriter = null; }
                // eviter la persistance d'artefacts graphiques lors du retrait de
                // l'indicateur ou de sa reapplication sur le meme chart.
                try
                {
                    RemoveDrawObject("VP_POC_Line");
                    RemoveDrawObject("VP_VAH_Line");
                    RemoveDrawObject("VP_VAL_Line");
                    RemoveDrawObject("VP_POC_Text");
                    RemoveDrawObject("VP_VAH_Text");
                    RemoveDrawObject("VP_VAL_Text");
                    RemoveDrawObject("VP_Dashboard");
                }
                catch (Exception ex) { RegisterRuntimeError("RemoveDrawObject", ex); }
            }
        }

        protected override void OnBarUpdate()
        {
            try
            {
                // mutations d'etat demandees par les callbacks d'envoi Telegram.
                DrainPendingStateActions();

                MarketIntelligenceOnBarUpdate();

                if (BarsInProgress == volumetricBarsIndex)
                {
                    int barIdx = CurrentBars[volumetricBarsIndex];
                    if (barIdx < 0) return;

                    VolumetricBarsType barsType = BarsArray[volumetricBarsIndex].BarsType as VolumetricBarsType;
                    if (barsType == null) return;

                    bool isNewSessionTick = BarsArray[volumetricBarsIndex].IsFirstBarOfSession && IsFirstTickOfBar;

                    // En mode EvaluateOnBarClose, on evalue la derniere barre de la session precedente AVANT de rouler la session.
                    if (EvaluateOnBarClose && isNewSessionTick && barIdx > 0)
                    {
                        evalOffset = 1;

                        UpdateNakedPocs(Highs[volumetricBarsIndex][1], Lows[volumetricBarsIndex][1]);

                        CalculateRollingVolumeProfile(barIdx - 1, barsType);
                        EvaluateVolumeProfileSignal();
                        SniperOnEvaluatedBar();

                        if (State == State.Realtime)
                        {
                            ProcessTelegramAlerts();
                        }

                        prevBarPocPrice = pocPrice;
                        prevBarVahPrice = vahPrice;
                        prevBarValPrice = valPrice;
                        frozenPocPrice = pocPrice;
                        frozenVahPrice = vahPrice;
                        frozenValPrice = valPrice;

                        // implementation unique, partagee avec la branche tick.
                        RollSessionState(barIdx, Closes[volumetricBarsIndex][1]);

                        CalculateRollingVolumeProfile(barIdx, barsType);
                        UpdateTrendFilters();

                        return;
                    }

                    if (isNewSessionTick)
                    {
                        RollSessionState(barIdx, barIdx > 0 ? Closes[volumetricBarsIndex][1] : 0.0);
                    }

                    bool isBarClose = IsFirstTickOfBar && barIdx > 0;

                    if (isBarClose)
                        UpdateNakedPocs(Highs[volumetricBarsIndex][1], Lows[volumetricBarsIndex][1]);

                    if (EvaluateOnBarClose)
                    {
                        if (!isBarClose)
                        {
                            // Profil rafraichi pour le dashboard, aucune evaluation.
                            CalculateRollingVolumeProfile(barIdx, barsType);
                            UpdateTrendFilters();
                            return;
                        }

                        evalOffset = 1;
                        CalculateRollingVolumeProfile(barIdx - 1, barsType);
                        EvaluateVolumeProfileSignal();
                        SniperOnEvaluatedBar();

                        if (State == State.Realtime)
                        {
                            ProcessTelegramAlerts();
                        }

                        prevBarPocPrice = pocPrice;
                        prevBarVahPrice = vahPrice;
                        prevBarValPrice = valPrice;
                        frozenPocPrice = pocPrice;
                        frozenVahPrice = vahPrice;
                        frozenValPrice = valPrice;

                        // Profil complet (barre courante incluse) pour l'affichage.
                        CalculateRollingVolumeProfile(barIdx, barsType);
                        UpdateTrendFilters();
                    }
                    else
                    {
                        if (isBarClose)
                        {
                            prevBarPocPrice = pocPrice;
                            prevBarVahPrice = vahPrice;
                            prevBarValPrice = valPrice;
                            frozenPocPrice = pocPrice;
                            frozenVahPrice = vahPrice;
                            frozenValPrice = valPrice;
                        }

                        evalOffset = 0;
                        CalculateRollingVolumeProfile(barIdx, barsType);
                        EvaluateVolumeProfileSignal();
                        SniperOnEvaluatedBar();

                        if (State == State.Realtime)
                        {
                            ProcessTelegramAlerts();
                        }
                    }
                }

                if (BarsInProgress == 0)
                {
                    if (ShowDashboard && CurrentBars[0] >= 0)
                    {
                        UpdateDashboard();
                    }

                    if (ShowLevelLines && CurrentBars[0] >= 0 && pocPrice != 0)
                    {
                        DrawLevelLines();
                    }
                }
            }
            catch (Exception ex)
            {
                // mais elle est comptee et affichee au dashboard.
                RegisterRuntimeError("OnBarUpdate", ex);
                Print("VP_OnBarUpdate Error: " + ex.GetType().Name + " - " + ex.Message);
            }
        }

        private void RegisterRuntimeError(string origin, Exception ex)
        {
            runtimeErrorCount++;
            lastRuntimeError = (origin ?? "?") + ": " + (ex == null ? "?" : ex.GetType().Name);
            if (EnableDebugMode && ex != null)
                Print("VP_Error[" + origin + "]: " + ex.GetType().Name + " - " + ex.Message);
        }

        // chemin temps reel (premier tick de session) : plus de divergence possible.
        private void RollSessionState(int barIdx, double lastSessionClose)
        {
            FlushOpenSignalsAtSessionEnd(lastSessionClose);
            SniperRollSession(lastSessionClose);
            ArchiveSessionLevels();

            sessionStartBarIndex = barIdx;
            ResetSessionTrackers();

            prevBarPocPrice = 0;
            prevBarVahPrice = 0;
            prevBarValPrice = 0;
            frozenPocPrice = 0;
            frozenVahPrice = 0;
            frozenValPrice = 0;
            signalTriggerBarIndex = -1;
            lastBreakoutBarIndex = -1;
        }

        // Accès sécurisé au temps de la série volumétrique (évite IndexOutOfRange).
        // (et non la barre courante), garantissant des horodatages corrects dans le journal.
        private DateTime GetVolumetricTime()
        {
            if (volumetricBarsIndex < BarsArray.Length
                && CurrentBars[volumetricBarsIndex] >= 0
                && Times[volumetricBarsIndex].Count > 0)
            {
                int offset = Math.Min(evalOffset, Times[volumetricBarsIndex].Count - 1);
                return Times[volumetricBarsIndex][offset];
            }
            // Melangee a DateTime.UtcNow dans les cooldowns, elle decalait les
            // fenetres de la valeur du fuseau horaire et faisait diverger le
            // temps reel du replay. Les appelants court-circuitent sur MinValue.
            return DateTime.MinValue;
        }

        private void UpdateTrendFilters()
        {
            if (ofVwap != null && CurrentBars[volumetricBarsIndex] >= 0)
            {
                int maxBars = Math.Max(0, CurrentBars[volumetricBarsIndex]);
                int off = Math.Min(evalOffset, maxBars);
                if (ofVwap.VWAP.IsValidDataPoint(off))
                    currentVwapPrice = ofVwap.VWAP[off];
            }

            UpdateHtfBias();
        }

        // Confirmation multi-temporelle : la structure du timeframe superieur
        // (prix vs EMA + pente) definit un biais qui filtre les signaux M5.
        private void UpdateHtfBias()
        {
            if (!EnableHtfFilter || htfEma == null || htfBarsIndex < 0 || htfBarsIndex >= BarsArray.Length)
            {
                htfBias = 0;
                htfBiasText = "Desactive";
                return;
            }

            // L'ancienne lecture de l'offset 0 changeait le biais en cours de
            // barre HTF -> alertes non reproductibles en backtest / replay.
            if (CurrentBars[htfBarsIndex] < HtfEmaPeriod + 2)
            {
                htfBias = 0;
                htfBiasText = "Chargement";
                return;
            }

            double htfClose = Closes[htfBarsIndex][1];
            double ema = htfEma[1];
            double emaPrev = htfEma[2];
            // pour éviter les whipsaws autour de l'EMA HTF.
            double atrBuffer = regimeAtr != null && regimeAtr.IsValidDataPoint(1)
                ? regimeAtr[1] * 0.5
                : (adaptiveAvgBarRange > 0 ? adaptiveAvgBarRange * 0.5 : 2.0 * TickSize);
            bool up = htfClose > (ema + atrBuffer) && ema >= emaPrev;
            bool down = htfClose < (ema - atrBuffer) && ema <= emaPrev;

            if (up) { htfBias = 1; htfBiasText = string.Format("Haussier M{0}", HtfMinutes); }
            else if (down) { htfBias = -1; htfBiasText = string.Format("Baissier M{0}", HtfMinutes); }
            else { htfBias = 0; htfBiasText = string.Format("Neutre M{0}", HtfMinutes); }
        }

        // true si le signal est compatible avec le biais HTF.
        private bool IsHtfAligned(bool isBuy)
        {
            if (!EnableHtfFilter || htfEma == null) return true;
            if (htfBias == 0) return !HtfStrictMode;
            return isBuy ? htfBias > 0 : htfBias < 0;
        }

        private bool IsRegimeValid()
        {
            if (!UseRegimeFilter || regimeAtr == null) return true;
            int atrOffset = Math.Min(evalOffset, Math.Max(0, CurrentBars[volumetricBarsIndex]));
            if (!regimeAtr.IsValidDataPoint(atrOffset)) return true;
            double atr = regimeAtr[atrOffset];
            double atrTicks = atr / TickSize;
            if (atrTicks < RegimeMinAtrTicks) return false;
            if (RegimeMaxAtrTicks > 0 && atrTicks > RegimeMaxAtrTicks) return false;
            return true;
        }

        private void ProcessTelegramAlerts()
        {
            // reelles, y compris celles du moteur AMC Pro (absorption, iceberg, etc.).
            // Auparavant seul le buffer Sniper (ProcessSelectionBuffer) etait bloque.
            if (ExecutionMode == SniperExecutionMode.Research) return;

            // fiable ; on ne fabrique plus d'horodatage mural de secours.
            if (GetVolumetricTime() == DateTime.MinValue) return;

            bool moved = false;
            // string += etait un hot path tick-par-tick quand EvaluateOnBarClose
            // vaut false (une allocation par concatenation, a chaque tick).
            StringBuilder chg = alertChangesBuilder;
            chg.Length = 0;
            double tick = TickSize;
            double threshold = EffectiveMovementThreshold();

            if (prevBarPocPrice != 0 && Math.Abs(pocPrice - prevBarPocPrice) >= threshold)
            {
                moved = true;
                string dir = pocPrice > prevBarPocPrice ? "HAUT" : "BAS";
                chg.AppendFormat("POC: {0} de {1} à {2}\n", dir,
                    Instrument.MasterInstrument.FormatPrice(prevBarPocPrice),
                    Instrument.MasterInstrument.FormatPrice(pocPrice));
            }

            if (prevBarVahPrice != 0 && Math.Abs(vahPrice - prevBarVahPrice) >= threshold)
            {
                moved = true;
                string dir = vahPrice > prevBarVahPrice ? "HAUT" : "BAS";
                chg.AppendFormat("VAH: {0} de {1} à {2}\n", dir,
                    Instrument.MasterInstrument.FormatPrice(prevBarVahPrice),
                    Instrument.MasterInstrument.FormatPrice(vahPrice));
            }

            if (prevBarValPrice != 0 && Math.Abs(valPrice - prevBarValPrice) >= threshold)
            {
                moved = true;
                string dir = valPrice > prevBarValPrice ? "HAUT" : "BAS";
                chg.AppendFormat("VAL: {0} de {1} à {2}\n", dir,
                    Instrument.MasterInstrument.FormatPrice(prevBarValPrice),
                    Instrument.MasterInstrument.FormatPrice(valPrice));
            }

            int barIdx = evalBarIndex >= 0 ? evalBarIndex : CurrentBars[volumetricBarsIndex];

            bool isNewAbsorption = (isBullishAbsorptionActive || isBearishAbsorptionActive)
                                   && lastAbsorptionBarIndex == barIdx
                                   && !currentSignal.StartsWith("Pas de trade");
            if (isNewAbsorption)
            {
                if (chg.Length > 0) chg.Append('\n');
                chg.AppendFormat("Absorption : {0}\n", currentAbsorptionStatus);
            }

            bool isNewIceberg = (isIcebergBullish || isIcebergBearish)
                                && lastIcebergBarIndex == barIdx
                                && !currentSignal.StartsWith("Pas de trade");
            if (isNewIceberg)
            {
                chg.AppendFormat("Iceberg : {0}\n", currentIcebergStatus);
            }

            bool isNewImbalance = (isImbalanceBullish || isImbalanceBearish)
                                  && lastImbalanceBarIndex == barIdx
                                  && !currentSignal.StartsWith("Pas de trade");
            if (isNewImbalance)
            {
                chg.AppendFormat("Imbalance : {0}\n", currentImbalanceStatus);
            }

            bool isNewFinishedAuction = (isFinishedAuctionBuy || isFinishedAuctionSell)
                                        && lastFinishedAuctionBarIndex == barIdx
                                        && !currentSignal.StartsWith("Pas de trade");
            if (isNewFinishedAuction)
            {
                chg.AppendFormat("Finished Auction : {0}\n", currentFinishedAuctionStatus);
            }

            if ((isExhaustionBuy || isExhaustionSell) && !currentSignal.StartsWith("Pas de trade"))
            {
                chg.AppendFormat("Exhaustion : {0}\n", currentExhaustionStatus);
            }

            string changes = chg.ToString();

            bool isNewSignal = !currentSignal.StartsWith("Pas de trade")
                               && (currentSignal != lastAlertedSignal || isNewAbsorption || isNewIceberg
                                   || isNewImbalance || isNewFinishedAuction);

            // Filtre de confluence : n'alerter que si le score atteint le pourcentage minimum
            double pctWeighted = CurrentConfluencePercent();
            bool confluenceOk = true;
            if (MinConfluencePercentToAlert > 0 && maxConfluenceScore > 0)
            {
                // declenchement ; le filtre ne comptait que le NOMBRE de
                // contributeurs, donc 3 preuves marginales passaient comme
                // ponderee rapportee au nombre max de contributeurs actifs),
                // tout en exigeant un minimum structurel de contributeurs.
                // somme des poids MAXIMUM des contributeurs actifs, donc bornee
                // a 100 %. Auparavant elle etait divisee par un NOMBRE de
                // contributeurs -> 2 preuves fortes pouvaient depasser 70 %.
                double pctCount = 100.0 * confluenceScore / maxConfluenceScore;
                confluenceOk = pctWeighted >= MinConfluencePercentToAlert
                               && pctCount >= (MinConfluencePercentToAlert * 0.5);
            }

            // Filtre de régime
            bool regimeOk = IsRegimeValid();

            // Expiration du signal
            bool signalExpired = false;
            if (signalTriggerBarIndex >= 0 && SignalValidityBars > 0)
            {
                if (barIdx - signalTriggerBarIndex > SignalValidityBars)
                    signalExpired = true;
            }

            // Filtre de confirmation multi-timeframe
            bool isBuySignal = currentSignal.Contains("BUY");
            bool isSellSignal = currentSignal.Contains("SELL");
            bool htfOk = true;
            if (isBuySignal || isSellSignal)
                htfOk = IsHtfAligned(isBuySignal);

            // Gestion du risque : calcul des niveaux et filtre R:R
            bool riskOk = true;
            bool hasRisk = false;
            if (EnableRiskManagement && (isBuySignal || isSellSignal))
            {
                double entry = Closes[volumetricBarsIndex][evalOffset];
                hasRisk = ComputeRiskLevels(isBuySignal, entry);
                // STRUCTURELLE reelle (bord oppose de la VA / HVN), le filtre
                // MinRiskReward n'est donc plus une tautologie.
                if (hasRisk && MinRiskReward > 0 && lastRiskReward < MinRiskReward)
                    riskOk = false;
                // depasse -> le signal est invalide au lieu d'etre force a 1 lot.
                if (hasRisk && lastPositionSize <= 0)
                    riskOk = false;
                // Sans ce test, ComputeRiskLevels renvoyait false et le signal
                // passait le filtre "riskOk" faute de niveaux a controler.
                if (lastRiskGuardRejected)
                    riskOk = false;
            }

            // Plafond d'alertes par session (anti-spam)
            bool quotaOk = MaxAlertsPerSession <= 0 || sessionAlertsCount < MaxAlertsPerSession;

            // les setups qui dependent de ses bords ne sont pas fiables.
            bool vaOk = !(valueAreaIncomplete
                          && (currentSignal.Contains("VAH") || currentSignal.Contains("VAL")
                              || currentSignal.Contains("BREAKOUT")));

            // Filtre de blackout news (si actif)
            bool newsOk = !NewsHardBlock || !IsSniperNewsBlackout();

            bool shouldAlert = (moved || isNewSignal) && !currentSignal.StartsWith("Pas de trade")
                               && confluenceOk && regimeOk && !signalExpired
                               && htfOk && riskOk && quotaOk && vaOk && newsOk;

            if (EnableDebugMode && (moved || isNewSignal) && !shouldAlert)
                Print(string.Format("VP: signal filtre ({0}) conf={1} regime={2} htf={3} rr={4} quota={5} va={6} news={7}",
                    currentSignal, confluenceOk, regimeOk, htfOk, riskOk, quotaOk, vaOk, newsOk));

            // POINT 4 : journal exhaustif. Les signaux rejetes par un filtre sont
            // suivis dans le CSV (Mode=SHADOW) avec le motif du rejet, afin de
            // disposer d'une population NON biaisee par la selection : c'est le
            // prerequis a toute calibration statistique des ponderations. Ces
            // lignes n'alimentent pas les statistiques affichees.
            if (JournalShadowMode && !shouldAlert)
            {
                string reason = BuildFilterReason(confluenceOk, regimeOk, signalExpired,
                                                  htfOk, riskOk, quotaOk, vaOk, newsOk);
                RegisterShadowSignal(barIdx, reason);
            }


            if (shouldAlert)
            {
                DateTime now = GetVolumetricTime();
                DateTime nowReal = DateTime.UtcNow;

                // faisait taire un signal fort parce qu'un signal faible venait d'etre
                // envoye quelques secondes plus tot.
                string cooldownKey = GetSignalFamily(currentSignal);
                DateTime lastForKey;
                if (lastAlertTimeBySignal.TryGetValue(cooldownKey, out lastForKey)
                    && (nowReal - lastForKey).TotalSeconds < AlertCooldownSeconds)
                {
                    if (EnableDebugMode) Print("VolumeProfile: Cooldown (" + cooldownKey + "), message not sent.");
                    // POINT 4 : un signal valide etouffe par le cooldown est une
                    // information precieuse (il aurait ete tradable) : on le trace.
                    if (JournalShadowMode) RegisterShadowSignal(barIdx, "COOLDOWN");
                    return;
                }

                VolumetricBarsType barsType = BarsArray[volumetricBarsIndex].BarsType as VolumetricBarsType;
                long barVolume = 0;
                long barDelta = 0;
                if (barsType != null && barIdx >= 0 && barIdx < barsType.Volumes.Length)
                {
                    VolumetricData currentBar = barsType.Volumes[barIdx];
                    if (currentBar != null)
                    {
                        barVolume = currentBar.TotalVolume;
                        barDelta = currentBar.BarDelta;
                    }
                }

                // Construction centralisée du message Telegram (Network.cs)
                // Valeurs HTF et Stats conditionnelles
                string amcHtfBias = EnableHtfFilter && !string.IsNullOrEmpty(htfBiasText) ? htfBiasText : null;
                string amcStats = EnableTradeJournal && IncludeStatsInAlert ? GetStatsText(cooldownKey) : null;

                string msg = BuildAmcTelegramAlert(
                    isBuySignal,
                    isSellSignal,
                    currentSignal,
                    confluenceScore,
                    maxConfluenceScore,
                    confluenceWeighted,
                    allSignalsText,
                    hasRisk && lastStopPrice != 0,
                    hasRisk ? lastEntryPrice : 0,
                    lastStopPrice,
                    lastTarget1,
                    lastTarget2,
                    lastRiskTicks,
                    lastPositionSize,
                    RiskPerTradeCurrency,
                    currentInterpretation,
                    barDelta,
                    currentCumulativeDelta,
                    barVolume,
                    currentAbsorptionStatus,
                    currentIcebergStatus,
                    currentImbalanceStatus,
                    vahPrice,
                    pocPrice,
                    valPrice,
                    currentVwapPrice,
                    amcHtfBias,
                    amcStats,
                    now);

                // asynchrone (le callback s'execute plus tard sur le thread UI).
                bool jIsBuy = isBuySignal;
                double jEntry = Closes[volumetricBarsIndex][evalOffset];
                int jBarIdx = barIdx;
                string jSignal = currentSignal;
                int jConfluence = confluenceScore;
                // Si la gestion du risque est desactivee, on calcule quand meme les
                // niveaux pour pouvoir tracer le signal dans le journal.
                if (!hasRisk && EnableTradeJournal && (isBuySignal || isSellSignal))
                    hasRisk = ComputeRiskLevels(isBuySignal, Closes[volumetricBarsIndex][evalOffset]);

                double jStop = lastStopPrice, jT1 = lastTarget1, jT2 = lastTarget2;

                // Routage dynamique vers Canal 1 ou 2 selon le score de confluence
                int targetChannel = (pctWeighted >= ScoreThresholdChat2) ? 2 : 1;

                SendTelegramMessage(msg, success =>
                {
                    if (!success)
                    {
                        // SafePrint, pas Print : on est hors du thread de calcul.
                        if (EnableDebugMode)
                            SafePrint("VolumeProfile: alerte non envoyee (echec Telegram), cooldown non applique.");
                        return;
                    }

                    // Tout est depose dans la file et applique par OnBarUpdate.
                    pendingStateActions.Enqueue(() =>
                    {
                        lastAlertTime = nowReal;
                        lastAlertTimeBySignal[cooldownKey] = nowReal;
                        // qui peut avoir change entre la capture et l'execution du callback.
                        lastAlertedSignal = jSignal;
                        signalsSentCount++;
                        sessionAlertsCount++;

                        // REELLEMENT alertes (tous filtres + cooldown + envoi OK).
                        // Les statistiques decrivent donc la population diffusee.
                        if (jIsBuy || jSignal.Contains("SELL"))
                            RegisterAlertedSignal(jIsBuy, jEntry, jBarIdx, jSignal, jConfluence, jStop, jT1, jT2);

                        if (EnableDebugMode) Print("VolumeProfile: Alert sent. Changes: " + changes);
                    });
                }, targetChannel);
            }
        }

        // Regroupe les variantes d'un meme signal sous une cle de cooldown commune.
        private static string GetSignalFamily(string signal)
        {
            if (string.IsNullOrEmpty(signal)) return "NONE";
            string dir = signal.Contains("BUY") ? "BUY" : (signal.Contains("SELL") ? "SELL" : "NEUTRAL");
            if (signal.Contains("BREAKOUT")) return "BREAKOUT_" + dir;
            if (signal.Contains("ABSORPTION")) return "ABSORPTION_" + dir;
            if (signal.Contains("ICEBERG")) return "ICEBERG_" + dir;
            if (signal.Contains("IMBALANCE")) return "IMBALANCE_" + dir;
            if (signal.Contains("REJET")) return "REJET_" + dir;
            if (signal.Contains("très fort")) return "VALUE_SHIFT_" + dir;
            return "AUTRE_" + dir;
        }

        private static string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private string GetSignalStrengthHeader(string signal)
        {
            if (string.IsNullOrEmpty(signal) || signal.StartsWith("Pas de trade"))
                return "";

            string dot = "";
            if (signal.Contains("BUY")) dot = "[BUY] ";
            else if (signal.Contains("SELL")) dot = "[SELL] ";

            if (signal.Contains("BREAKOUT") || signal.Contains("très fort"))
                return dot + "[SIGNAL TRES FORT]\n\n";

            if (signal.Contains("ABSORPTION") || signal.Contains("ICEBERG") || signal.Contains("IMBALANCE") || signal.Contains("REJET VAL") || signal.Contains("REJET VAH"))
                return dot + "[SIGNAL FORT]\n\n";

            if (signal.Contains("REJET POC") || signal.Contains("potentiel") || signal.Contains("Attendre"))
                return dot + "[SIGNAL MOYEN]\n\n";

            return dot;
        }
    }
    }

