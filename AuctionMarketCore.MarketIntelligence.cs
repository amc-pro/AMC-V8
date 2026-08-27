#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
using SMI = NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    /// <summary>
    /// Pont entre l'indicateur NinjaTrader et le module Market Intelligence.
    /// Aucun moteur existant n'est modifie : ce fichier ne fait qu'alimenter le
    /// module et peut etre desactive via EnableMarketIntelligence.
    /// </summary>
    public partial class AuctionMarketCore
    {
        #region Market Intelligence — Parametres

        [Display(Name = "Activer Market Intelligence", GroupName = "13. Market Intelligence", Order = 1)]
        public bool EnableMarketIntelligence { get; set; }

        [Display(Name = "Market Report H4", GroupName = "13. Market Intelligence", Order = 2)]
        public bool EnableMarketReport { get; set; }

        [Display(Name = "Market Update (changements majeurs)", GroupName = "13. Market Intelligence", Order = 3)]
        public bool EnableMarketUpdate { get; set; }

        [Range(2, 200)]
        [Display(Name = "Periode EMA de tendance", GroupName = "13. Market Intelligence", Order = 4)]
        public int MiTrendEmaPeriod { get; set; }

        [Range(1, 10)]
        [Display(Name = "Force des swings (SMC)", GroupName = "13. Market Intelligence", Order = 5)]
        public int MiSwingStrength { get; set; }

        [Range(0.0, 10.0)]
        [Display(Name = "Distance minimale prix/EMA (ticks)", GroupName = "13. Market Intelligence", Order = 6)]
        public double MiTrendMinDistanceTicks { get; set; }

        [Range(0.0, 10.0)]
        [Display(Name = "Pente minimale EMA (ticks/barre)", GroupName = "13. Market Intelligence", Order = 7)]
        public double MiTrendMinSlopeTicks { get; set; }

        [Display(Name = "Canal Telegram (1, 2 ou 3)", GroupName = "13. Market Intelligence", Order = 8)]
        [Range(1, 3)]
        public int MiTelegramChannel { get; set; }

        #endregion

        #region Market Intelligence — Etat interne

        private int miH4Index = -1, miH1Index = -1, miM15Index = -1, miM5Index = -1;
        private EMA miEmaH4, miEmaH1, miEmaM15, miEmaM5;
        private SMI.MarketStructureAnalyzer miAnalyzer;      // structure H1
        private SMI.MarketStructureAnalyzer miAnalyzerH4;
        private SMI.MarketReportEngine miReportEngine;
        private SMI.MarketUpdateEngine miUpdateEngine;
        private SMI.TelegramDispatcher miDispatcher;
        private ScalpingProMarketIntelligenceSource miSource;
        private DateTime miLastH4Open = DateTime.MinValue;
        private readonly List<SMI.IMarketIntelligenceModule> miModules = new List<SMI.IMarketIntelligenceModule>();
        private string miDisabledReason;
        /// <summary>Dernier snapshot connu, pour la ligne de statut du dashboard.</summary>
        private SMI.MarketSnapshot miLastSnapshot;

        private void MarketIntelligenceSetDefaults()
        {
            EnableMarketIntelligence = false;
            EnableMarketReport = true;
            EnableMarketUpdate = true;
            MiTrendEmaPeriod = 21;
            MiSwingStrength = 2;
            MiTrendMinDistanceTicks = 0.50;
            MiTrendMinSlopeTicks = 0.10;
            MiTelegramChannel = 3;
        }

        /// <summary>Appele depuis State.Configure (apres les series existantes).</summary>
        private void MarketIntelligenceConfigure()
        {
            // [MOD V7.8.1] Toujours ajouter H1 et M15 pour le filtre obligatoire HTF aligne.
            int next = BarsArray != null ? BarsArray.Length : 1;
            AddDataSeries(Instrument.FullName, BarsPeriodType.Minute, 60, MarketDataType.Last); miH1Index = next++;
            AddDataSeries(Instrument.FullName, BarsPeriodType.Minute, 15, MarketDataType.Last); miM15Index = next++;

            if (!EnableMarketIntelligence) return;
            if (hostedSeriesDeclared)
            {
                EnableMarketIntelligence = false;
                miDisabledReason = "mode heberge incompatible avec AddDataSeries";
                SafePrint("MarketIntelligence DESACTIVE automatiquement : " + miDisabledReason + ".");
                return;
            }

            AddDataSeries(Instrument.FullName, BarsPeriodType.Minute, 240, MarketDataType.Last); miH4Index = next++;
            AddDataSeries(Instrument.FullName, BarsPeriodType.Minute, 5, MarketDataType.Last); miM5Index = next++;
        }

        /// <summary>Appele depuis State.DataLoaded.</summary>
        private void MarketIntelligenceDataLoaded()
        {
            MarketIntelligenceDispose();

            // [MOD V7.8.1] Toujours initialiser H1 et M15 pour le filtre obligatoire.
            if (miH1Index >= 0 && miH1Index < BarsArray.Length)
                miEmaH1 = EMA(BarsArray[miH1Index], MiTrendEmaPeriod);
            if (miM15Index >= 0 && miM15Index < BarsArray.Length)
                miEmaM15 = EMA(BarsArray[miM15Index], MiTrendEmaPeriod);

            if (!EnableMarketIntelligence) return;
            if (miH4Index < 0 || miH4Index >= BarsArray.Length)
            {
                EnableMarketIntelligence = false;
                miDisabledReason = "serie H4 indisponible (index " + miH4Index + ")";
                SafePrint("MarketIntelligence DESACTIVE automatiquement : " + miDisabledReason + ".");
                return;
            }

            miEmaH4 = EMA(BarsArray[miH4Index], MiTrendEmaPeriod);
            // miEmaH1 et miEmaM15 deja faits au dessus
            miEmaM5 = EMA(BarsArray[miM5Index], MiTrendEmaPeriod);

            miAnalyzer = new SMI.MarketStructureAnalyzer(MiSwingStrength);
            miAnalyzerH4 = new SMI.MarketStructureAnalyzer(MiSwingStrength);
            var logger = new SMI.MiDelegateLogger(msg =>
            {
                SafePrint(msg);
                if (msg != null && msg.IndexOf("echec definitif", StringComparison.OrdinalIgnoreCase) >= 0)
                    RegisterRuntimeError("MarketIntelligence/Telegram", new Exception(msg));
            });

            int channel = MiTelegramChannel;
            miDispatcher = new SMI.TelegramDispatcher(
                (text, onComplete) => SendTelegramMessage(text, onComplete, channel),
                logger,
                () => DateTime.UtcNow);

            miSource = new ScalpingProMarketIntelligenceSource(this);
            var builder = new SMI.MarketSnapshotBuilder(miSource);
            var formatter = new SMI.TelegramFormatter();

            miReportEngine = new SMI.MarketReportEngine(builder, formatter, miDispatcher, logger) { Enabled = EnableMarketReport };
            miUpdateEngine = new SMI.MarketUpdateEngine(builder, new SMI.MarketSnapshotComparer(), formatter, miDispatcher, logger) { Enabled = EnableMarketUpdate };
            miLastH4Open = DateTime.MinValue;
            miLastSnapshot = null;
            miDisabledReason = null;
            SafePrint("MarketIntelligence actif (regime H4/H1 + execution M15/M5). Trend = prix/EMA + pente + momentum sur barres cloturees. Rapport H4 emis en temps reel "
                    + "uniquement : aucun rapport sur donnees historiques.");
        }

        private void MarketIntelligenceDispose()
        {
            if (miDispatcher != null) { miDispatcher.Dispose(); miDispatcher = null; }
            if (miAnalyzer != null) miAnalyzer.Reset();
            if (miAnalyzerH4 != null) miAnalyzerH4.Reset();
            if (miReportEngine != null) miReportEngine.Reset();
            if (miUpdateEngine != null) miUpdateEngine.Reset();
            miReportEngine = null;
            miUpdateEngine = null;
            miSource = null;
            miLastSnapshot = null;
        }

        /// <summary>Appele au debut de OnBarUpdate, quelle que soit la serie.</summary>
        private void MarketIntelligenceOnBarUpdate()
        {
            if (!EnableMarketIntelligence || miReportEngine == null) return;

            try
            {
                // Structure SMC sur H1, une seule fois par barre cloturee.
                if (BarsInProgress == miH1Index && IsFirstTickOfBar && CurrentBars[miH1Index] > 1)
                {
                    miAnalyzer.OnClosedBar(
                        Opens[miH1Index][1], Highs[miH1Index][1],
                        Lows[miH1Index][1], Closes[miH1Index][1]);
                }

                if (BarsInProgress == miH4Index && IsFirstTickOfBar && CurrentBars[miH4Index] > 1)
                {
                    miAnalyzerH4.OnClosedBar(
                        Opens[miH4Index][1], Highs[miH4Index][1],
                        Lows[miH4Index][1], Closes[miH4Index][1]);
                }

                // Nouvelle bougie H4 -> rapport complet.
                if (BarsInProgress == miH4Index && IsFirstTickOfBar && CurrentBars[miH4Index] > MiTrendEmaPeriod + 2)
                {
                    DateTime open = Times[miH4Index][0];
                    if (open > miLastH4Open)
                    {
                        miLastH4Open = open;
                        if (State == State.Realtime)
                        {
                            var snap = miReportEngine.OnNewH4Bar(open);
                            if (snap != null) { miUpdateEngine.Prime(snap); miLastSnapshot = snap; }
                        }
                    }
                }

                // Detection des changements majeurs : cloture M15 (peu couteux, pas de spam).
                if (BarsInProgress == miM15Index && IsFirstTickOfBar
                    && State == State.Realtime && CurrentBars[miM15Index] > MiTrendEmaPeriod + 2)
                {
                    miUpdateEngine.Evaluate();
                    if (miUpdateEngine.Current != null) miLastSnapshot = miUpdateEngine.Current;
                }
            }
            catch (Exception ex)
            {
                RegisterRuntimeError("MarketIntelligence", ex);
            }
        }

        private SMI.MiBias GetMarketIntelligenceBias()
        {
            return miLastSnapshot != null ? miLastSnapshot.Bias : SMI.MiBias.NoTrade;
        }

        private int GetMarketIntelligenceConfidence()
        {
            return miLastSnapshot != null ? miLastSnapshot.Confidence : 0;
        }

        /// <summary>
        /// Penalite experimentale MI -> Scalping Pro. Le contexte ne bloque pas
        /// encore les retournements intraday : il modifie seulement le score.
        /// 
        /// MODIF UTILISATEUR: Suppression pénalité NO TRADE, ajout bonus +10 si tous HTF alignés.
        /// FIX BUG 5: Annulation pénalité lors de transitions de biais récentes si signal aligné avec nouveau biais.
        /// </summary>
        private int GetMarketIntelligenceDirectionalPenalty(bool isBuy)
        {
            if (!EnableMarketIntelligence || miLastSnapshot == null) return 0;
            
            // MODIF: Suppression de la pénalité pour NO TRADE (était -12)
            if (miLastSnapshot.Bias == SMI.MiBias.NoTrade) return 0;
            
            bool aligned = (isBuy && miLastSnapshot.Bias == SMI.MiBias.BuyOnly)
                        || (!isBuy && miLastSnapshot.Bias == SMI.MiBias.SellOnly);
            
            // FIX BUG 5: Si confiance faible mais signal aligné, réduire pénalité pour éviter blocage excessif
            // lors de transitions de tendance
            if (aligned && miLastSnapshot.Confidence < 50)
            {
                // Signal aligné mais confiance faible: pénalité réduite au lieu de blocage complet
                return -2;
            }
            
            // MODIF: Ajout bonus +10 si tous les HTF sont alignés (AlignmentPercent >= 80)
            if (aligned && miLastSnapshot.AlignmentPercent >= 80)
                return 10;
            
            return aligned ? 0 : -8;
        }

        /// <summary>
        /// [MOD V7.8.1] Verifie l'alignement strict H1 et M15. 
        /// Utilise par le gate de signal sur tous les presets.
        /// </summary>
        private bool IsH1M15Aligned(bool isBuy)
        {
            // Zero-Trust : un filtre HTF indisponible ne doit JAMAIS être considéré
            // comme aligné. Sinon une série manquante ou un indicateur non prêt
            // devient un bypass implicite du gate HTF.
            if (miH1Index < 0 || miM15Index < 0 || miEmaH1 == null || miEmaM15 == null) return false;
            if (CurrentBars[miH1Index] < MiTrendEmaPeriod + 4 || CurrentBars[miM15Index] < MiTrendEmaPeriod + 4) return false;
            
            SMI.MiTrend h1 = MiComputeTrend(miH1Index, miEmaH1);
            SMI.MiTrend m15 = MiComputeTrend(miM15Index, miEmaM15);
            
            if (isBuy) return h1 == SMI.MiTrend.Bullish && m15 == SMI.MiTrend.Bullish;
            return h1 == SMI.MiTrend.Bearish && m15 == SMI.MiTrend.Bearish;
        }

        /// <summary>Ligne compacte pour le dashboard graphique. Jamais null (§6.7).</summary>
        private string BuildMarketIntelligenceStatusLine(int maxLen = 44)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(miDisabledReason))
            {
                AppendWrappedLine(sb, "  MI     : ", "OFF (" + miDisabledReason + ")", maxLen);
                return sb.ToString();
            }
            if (!EnableMarketIntelligence)
            {
                sb.AppendLine("  MI     : Désactivé");
                return sb.ToString();
            }
            if (miReportEngine == null || miUpdateEngine == null)
            {
                sb.AppendLine("  MI     : Initialisation...");
                return sb.ToString();
            }

            SMI.MarketSnapshot s = miLastSnapshot;
            if (s == null)
            {
                sb.AppendLine("  MI     : Attente clôture H4");
                return sb.ToString();
            }

            string text = SMI.MiText.Bias(s.Bias) + " " + s.Confidence + "/100"
                 + " (Align " + s.AlignmentPercent + "% ref " + SMI.MiText.Timeframe(s.AlignmentReference)
                 + " | struct H4+H1) "
                 + s.Time.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

            AppendWrappedLine(sb, "  MI     : ", CleanTextForDashboard(text), maxLen);
            return sb.ToString();
        }

        /// <summary>Contribution du bloc MI a l'empreinte de rafraichissement du dashboard.</summary>
        private long MarketIntelligenceFingerprint()
        {
            unchecked
            {
                long h = EnableMarketIntelligence ? 7 : 3;
                h = (h * 31) ^ (miDisabledReason == null ? 0 : miDisabledReason.GetHashCode());
                SMI.MarketSnapshot s = miLastSnapshot;
                if (s != null)
                {
                    h = (h * 31) ^ ((long)s.Bias * 397 + s.Confidence * 31 + s.AlignmentPercent);
                    h = (h * 31) ^ (long)s.AlignmentReference;
                    h = (h * 31) ^ s.Time.Ticks;
                }
                return h;
            }
        }

        private SMI.MiTrend MiComputeTrend(int barsIndex, EMA ema)
        {
            // Zero-Trust: tendance calculée uniquement sur des barres clôturées.
            // On utilise [1] comme dernière clôture confirmée et [3] pour mesurer
            // pente/momentum sur deux intervalles, ce qui évite les flips causés par
            // un seul tick ou une seule bougie de respiration.
            if (barsIndex < 0 || barsIndex >= BarsArray.Length || ema == null) return SMI.MiTrend.Neutral;
            if (CurrentBars[barsIndex] < MiTrendEmaPeriod + 4) return SMI.MiTrend.Neutral;
            if (!ema.IsValidDataPoint(1) || !ema.IsValidDataPoint(3)) return SMI.MiTrend.Neutral;

            double close = Closes[barsIndex][1];
            double closePast = Closes[barsIndex][3];
            double e = ema[1];
            double ePast = ema[3];

            return SMI.MiTrendLogic.Classify(
                close, closePast, e, ePast, TickSize,
                MiTrendMinDistanceTicks, MiTrendMinSlopeTicks);
        }

        /// <summary>Adaptateur NinjaTrader de la source de donnees du module.</summary>
        private sealed class ScalpingProMarketIntelligenceSource : SMI.IMarketIntelligenceSource
        {
            private readonly AuctionMarketCore o;
            public ScalpingProMarketIntelligenceSource(AuctionMarketCore owner) { o = owner; }

            public string InstrumentName { get { return o.Instrument != null ? o.Instrument.FullName : "N/A"; } }
            public DateTime MarketTime
            {
                get
                {
                    return (o.miH1Index >= 0 && o.CurrentBars[o.miH1Index] >= 0)
                        ? o.Times[o.miH1Index][0]
                        : DateTime.UtcNow;
                }
            }
            public string TimeZoneLabel
            {
                get
                {
                    if (o.miH1Index >= 0 && o.CurrentBars[o.miH1Index] >= 0)
                    {
                        try
                        {
                            NinjaTrader.Data.Bars b = o.BarsArray[o.miH1Index];
                            if (b != null && b.TradingHours != null && b.TradingHours.TimeZoneInfo != null)
                                return b.TradingHours.TimeZoneInfo.StandardName;
                        }
                        catch (Exception) { }
                        return TimeZoneInfo.Local.StandardName;
                    }
                    return "UTC";   // seul cas ou le repli DateTime.UtcNow s'applique
                }
            }
            public double TickSize { get { return o.TickSize; } }
            public double LastPrice
            {
                get
                {
                    return (o.miM5Index >= 0 && o.CurrentBars[o.miM5Index] >= 0)
                        ? o.Closes[o.miM5Index][0]
                        : o.Close[0];
                }
            }

            public SMI.MiTrend GetTrend(SMI.MiTimeframe tf)
            {
                switch (tf)
                {
                    case SMI.MiTimeframe.H4: return o.MiComputeTrend(o.miH4Index, o.miEmaH4);
                    case SMI.MiTimeframe.H1: return o.MiComputeTrend(o.miH1Index, o.miEmaH1);
                    case SMI.MiTimeframe.M15: return o.MiComputeTrend(o.miM15Index, o.miEmaM15);
                    default: return o.MiComputeTrend(o.miM5Index, o.miEmaM5);
                }
            }

            public SMI.MiStructureEvent LastBos { get { return o.miAnalyzer != null ? o.miAnalyzer.LastBos : SMI.MiStructureEvent.None; } }
            public SMI.MiStructureEvent LastChoch { get { return o.miAnalyzer != null ? o.miAnalyzer.LastChoch : SMI.MiStructureEvent.None; } }
            public SMI.MiStructureEvent LastBosH4 { get { return o.miAnalyzerH4 != null ? o.miAnalyzerH4.LastBos : SMI.MiStructureEvent.None; } }
            public SMI.MiStructureEvent LastChochH4 { get { return o.miAnalyzerH4 != null ? o.miAnalyzerH4.LastChoch : SMI.MiStructureEvent.None; } }
            public int BarsSinceBos { get { return o.miAnalyzer != null ? o.miAnalyzer.BarsSinceBos : -1; } }
            public int BarsSinceChoch { get { return o.miAnalyzer != null ? o.miAnalyzer.BarsSinceChoch : -1; } }
            public int BarsSinceOrderBlock { get { return o.miAnalyzer != null ? o.miAnalyzer.BarsSinceOrderBlock : -1; } }
            public int BarsSinceBosH4 { get { return o.miAnalyzerH4 != null ? o.miAnalyzerH4.BarsSinceBos : -1; } }
            public int BarsSinceChochH4 { get { return o.miAnalyzerH4 != null ? o.miAnalyzerH4.BarsSinceChoch : -1; } }
            public double NearestBuySideLiquidity { get { return o.miAnalyzer != null ? o.miAnalyzer.NearestBuySide(LastPrice) : 0; } }
            public double NearestSellSideLiquidity { get { return o.miAnalyzer != null ? o.miAnalyzer.NearestSellSide(LastPrice) : 0; } }
            public SMI.MiOrderBlockKind OrderBlockKind { get { return o.miAnalyzer != null ? o.miAnalyzer.OrderBlockKind : SMI.MiOrderBlockKind.None; } }
            public SMI.MiOrderBlockState OrderBlockState { get { return o.miAnalyzer != null ? o.miAnalyzer.OrderBlockState : SMI.MiOrderBlockState.None; } }

            public double VolumeQuality
            {
                get
                {
                    if (o.miM15Index < 0 || o.CurrentBars[o.miM15Index] < 21) return 0.5;
                    double sum = 0;
                    for (int i = 1; i <= 20; i++) sum += o.Volumes[o.miM15Index][i];
                    double avg = sum / 20.0;
                    if (avg <= 0) return 0.5;
                    double ratio = o.Volumes[o.miM15Index][1] / avg;
                    return Math.Max(0, Math.Min(1, ratio / 2.0));
                }
            }

            public double MomentumQuality
            {
                get
                {
                    if (o.miM15Index < 0 || o.CurrentBars[o.miM15Index] < 6) return 0.5;
                    double c = o.Closes[o.miM15Index][1];
                    double past = o.Closes[o.miM15Index][5];
                    if (past <= 0) return 0.5;
                    double move = Math.Abs(c - past) / (o.TickSize > 0 ? o.TickSize : 0.25);
                    return Math.Max(0, Math.Min(1, move / 40.0));
                }
            }

            public IEnumerable<SMI.IMarketIntelligenceModule> Modules { get { return o.miModules; } }
        }

        #endregion
    }
}
