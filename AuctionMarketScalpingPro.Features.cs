#region Using declarations
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    // Ce fichier n'ajoute AUCUNE regle de trading et ne modifie aucun seuil.
    // Il fournit trois briques indispensables aux phases suivantes :
    //   1. FeatureSnapshot : photographie complete du vecteur de features au
    //      moment exact ou un signal est enregistre (alerte OU shadow).
    //   3. Persistance des statistiques : statsByFamily / globalStats survivent
    // Regle de conception : tout est defensif (try/catch + valeurs neutres).
    // Une panne d'ecriture ne doit jamais interrompre le moteur de signaux.
    public partial class AuctionMarketScalpingPro
    {
        #region V5 Phase 0 - Vecteur de features

        /// <summary>
        /// Photographie immuable de l'etat du moteur au moment de l'enregistrement
        /// d'un signal. Aucune valeur n'est recalculee a la cloture du trade : ce
        /// sont bien les conditions D'ENTREE qui sont apprises, pas les conditions
        /// de sortie (sinon fuite de donnees futures dans l'apprentissage).
        /// </summary>
        private sealed class FeatureSnapshot
        {
            public string DayType = "-";
            public int DayTypeScore;
            public string SniperDay = "-";
            public double IbExtensionRatio;
            public bool IbComplete;
            public bool InsideProfile;
            public bool HtfAligned;
            public int HtfBias;
            public bool RegimeValid;
            public double AtrTicks;
            public bool IsRth;
            public int HourOfDay;
            public int MinuteOfHour;

            public bool NearKeyLevel;
            public bool SharedKeyLevel;
            public bool NearPriorLevel;
            public bool NakedPriorLevel;
            public bool VwapAligned;
            public double VwapDistanceTicks;
            public bool NearVaBorder;
            public double PocDistanceTicks;
            public double VahDistanceTicks;
            public double ValDistanceTicks;
            public bool IsLvn;
            public bool IsHvn;
            public double VaOverlap;
            public double ValueAreaCompleteness;
            public bool ValueAreaIncomplete;

            public bool Absorption;
            public bool AbsorptionStrong;
            public double AbsorptionQuality;
            public long AbsorptionVolume;
            public bool Iceberg;
            public bool Imbalance;
            public bool CumDeltaDiv;
            public double CumDeltaDivStrength;
            public bool DeltaFlip;
            public double DeltaFlipStrength;

            public bool FinishedAuction;
            public double WickRatio;
            public long BarDelta;
            public int DeltaThreshold;
            public bool DeltaConfirms;
            public bool Exhaustion;

            public double VolumeRank;
            public long BarVolume;
            public long SessionVolume;
            public long CumulativeDelta;
            public double BarRangeTicks;

            public int ConfluenceCount;
            public int ConfluenceMaxCount;
            public double ConfluencePercent;
            public double BuySideWeight;
            public double SellSideWeight;
            public double RiskTicks;
            public double RewardTicks;
        }

        private long featureSignalCounter = 0;

        /// <summary>
        /// Capture le vecteur de features courant. Ne leve jamais : en cas d'erreur,
        /// renvoie un instantane partiel plutot que d'interrompre l'enregistrement.
        /// </summary>
        private FeatureSnapshot CaptureFeatures(bool isBuy, double entry, double stop, double target1)
        {
            FeatureSnapshot f = new FeatureSnapshot();
            try
            {
                double tick = tickSize > 0 ? tickSize : TickSize;
                if (tick <= 0) tick = 1.0;

                double high = Highs[volumetricBarsIndex][evalOffset];
                double low = Lows[volumetricBarsIndex][evalOffset];
                double close = Closes[volumetricBarsIndex][evalOffset];
                double range = high - low;
                DateTime t = GetVolumetricTime();

                f.DayType = currentDayType;
                f.DayTypeScore = dayTypeScore;
                f.SniperDay = sniperDayType.ToString();
                f.IbExtensionRatio = ibExtensionRatio;
                f.IbComplete = isIbComplete;
                f.InsideProfile = IsPriceInsideProfile(close);
                f.HtfAligned = IsHtfAligned(isBuy);
                f.HtfBias = htfBias;
                f.RegimeValid = IsRegimeValid();
                f.AtrTicks = (regimeAtr != null && regimeAtr.IsValidDataPoint(0)) ? regimeAtr[0] / tick : 0.0;
                f.IsRth = currentBucketIsRth;
                f.HourOfDay = t.Hour;
                f.MinuteOfHour = t.Minute;

                bool shared;
                f.NearKeyLevel = IsNearKeyLevel(close, 5 * tick, out shared);
                f.SharedKeyLevel = shared;

                bool naked;
                f.NearPriorLevel = IsNearPriorSessionLevel(close, 5 * tick, out naked);
                f.NakedPriorLevel = naked;

                f.VwapAligned = UseVwapFilter && currentVwapPrice != 0
                                && (isBuy ? close > currentVwapPrice : close < currentVwapPrice);
                f.VwapDistanceTicks = currentVwapPrice != 0 ? (close - currentVwapPrice) / tick : 0.0;
                f.NearVaBorder = (vahPrice > 0 && Math.Abs(close - vahPrice) <= 5 * tick)
                                 || (valPrice > 0 && Math.Abs(close - valPrice) <= 5 * tick);
                f.PocDistanceTicks = pocPrice > 0 ? (close - pocPrice) / tick : 0.0;
                f.VahDistanceTicks = vahPrice > 0 ? (close - vahPrice) / tick : 0.0;
                f.ValDistanceTicks = valPrice > 0 ? (close - valPrice) / tick : 0.0;
                f.IsLvn = IsLowVolumeNode(close, NodeToleranceTicks);
                f.IsHvn = hvnVolumeThreshold > 0 && VolumeAtPrice(close) >= hvnVolumeThreshold;
                f.VaOverlap = sniperVaOverlap;
                f.ValueAreaCompleteness = valueAreaCompleteness;
                f.ValueAreaIncomplete = valueAreaIncomplete;

                f.Absorption = isBuy ? isBullishAbsorptionActive : isBearishAbsorptionActive;
                f.AbsorptionStrong = isAbsorptionStrong;
                f.AbsorptionQuality = absorptionQualityFactor;
                f.AbsorptionVolume = currentAbsorptionVolume;
                f.Iceberg = isBuy ? isIcebergBullish : isIcebergBearish;
                f.Imbalance = isBuy ? isImbalanceBullish : isImbalanceBearish;
                f.CumDeltaDiv = isBuy ? isCumDeltaDivBullish : isCumDeltaDivBearish;
                f.CumDeltaDivStrength = cumDeltaDivStrength;
                f.DeltaFlip = isBuy ? isDeltaFlipBullish : isDeltaFlipBearish;
                f.DeltaFlipStrength = deltaFlipStrength;

                f.FinishedAuction = isBuy ? isFinishedAuctionBuy : isFinishedAuctionSell;
                f.WickRatio = range > 0 ? (isBuy ? (close - low) / range : (high - close) / range) : 0.0;
                f.BarDelta = currentBarDelta;
                f.DeltaThreshold = EffectiveAbsorptionDeltaThreshold();
                f.DeltaConfirms = Math.Abs(currentBarDelta) >= f.DeltaThreshold;
                f.Exhaustion = isBuy ? isExhaustionBuy : isExhaustionSell;

                f.VolumeRank = VolumeRankCurrent();
                f.BarVolume = Volumes[volumetricBarsIndex][evalOffset] > 0
                    ? (long)Volumes[volumetricBarsIndex][evalOffset] : 0L;
                f.SessionVolume = sessionTotalVolume;
                f.CumulativeDelta = currentCumulativeDelta;
                f.BarRangeTicks = range / tick;

                f.ConfluenceCount = confluenceScore;
                f.ConfluenceMaxCount = maxConfluenceScore;
                f.ConfluencePercent = CurrentConfluencePercent();
                f.BuySideWeight = buySideWeight;
                f.SellSideWeight = sellSideWeight;
                f.RiskTicks = (entry > 0 && stop > 0) ? Math.Abs(entry - stop) / tick : 0.0;
                f.RewardTicks = (entry > 0 && target1 > 0) ? Math.Abs(target1 - entry) / tick : 0.0;
            }
            catch (Exception ex)
            {
                if (EnableDebugMode) Print("VP_Features: capture partielle (" + ex.Message + ")");
            }
            return f;
        }

        #endregion

        #region V5 Phase 0 - Journal v2 (export du vecteur de features)

        private string featureJournalPathResolved = null;
        private bool featureHeaderWritten = false;

        private const string FeatureJournalHeader =
            "SignalId;Date;Instrument;Signal;Famille;Sens;Mode;Motif;Resultat;R;" +
            "Entree;Stop;Cible1;Cible2;RiskTicks;RewardTicks;" +
            "DayType;DayTypeScore;SniperDay;IbExt;IbComplete;InsideProfile;HtfAligned;HtfBias;RegimeValid;AtrTicks;IsRth;Hour;Minute;" +
            "NearKey;SharedKey;NearPrior;NakedPrior;VwapAligned;VwapDistTicks;NearVaBorder;PocDistTicks;VahDistTicks;ValDistTicks;IsLvn;IsHvn;VaOverlap;VaCompleteness;VaIncomplete;" +
            "Absorption;AbsorptionStrong;AbsorptionQuality;AbsorptionVolume;Iceberg;Imbalance;CumDeltaDiv;CumDeltaDivStrength;DeltaFlip;DeltaFlipStrength;" +
            "FinishedAuction;WickRatio;BarDelta;DeltaThreshold;DeltaConfirms;Exhaustion;" +
            "VolumeRank;BarVolume;SessionVolume;CumDelta;BarRangeTicks;" +
            "ConfluenceCount;ConfluenceMax;ConfluencePct;BuyWeight;SellWeight\n";

        private string ResolveFeatureJournalPath()
        {
            try
            {
                if (string.IsNullOrEmpty(journalPathResolved)) return null;
                string dir = Path.GetDirectoryName(journalPathResolved);
                string baseName = Path.GetFileNameWithoutExtension(journalPathResolved);
                string suffix = string.IsNullOrEmpty(instrumentRoot) ? "" : "_" + instrumentRoot;
                return Path.Combine(dir, baseName + "_features" + suffix + ".csv");
            }
            catch (Exception ex)
            {
                if (EnableDebugMode) Print("VP_FeatureJournal: chemin invalide (" + ex.Message + ")");
                return null;
            }
        }

        private static string Bit(bool v) { return v ? "1" : "0"; }

        /// <summary>
        /// Ecrit une ligne large : identite du signal + issue + vecteur complet.
        /// </summary>
        private void WriteFeatureJournalLine(TrackedSignal t, string outcome, double rMultiple)
        {
            if (t == null || t.Features == null) return;
            if (string.IsNullOrEmpty(featureJournalPathResolved)) return;

            try
            {
                FeatureSnapshot f = t.Features;
                CultureInfo ci = CultureInfo.InvariantCulture;
                StringBuilder sb = new StringBuilder(1024);

                sb.AppendFormat(ci, "{0};{1:yyyy-MM-dd HH:mm:ss};{2};{3};{4};{5};{6};{7};{8};{9:F3};",
                    t.SignalId, t.Time, instrumentRoot, t.Signal, t.Family,
                    t.IsBuy ? "BUY" : "SELL", t.Shadow ? "SHADOW" : "ALERTE",
                    string.IsNullOrEmpty(t.Reason) ? "-" : t.Reason, outcome, rMultiple);

                sb.AppendFormat(ci, "{0:F5};{1:F5};{2:F5};{3:F5};{4:F1};{5:F1};",
                    t.Entry, t.Stop, t.Target1, t.Target2, f.RiskTicks, f.RewardTicks);

                sb.AppendFormat(ci, "{0};{1};{2};{3:F3};{4};{5};{6};{7};{8};{9:F2};{10};{11};{12};",
                    f.DayType, f.DayTypeScore, f.SniperDay, f.IbExtensionRatio, Bit(f.IbComplete),
                    Bit(f.InsideProfile), Bit(f.HtfAligned), f.HtfBias, Bit(f.RegimeValid), f.AtrTicks,
                    Bit(f.IsRth), f.HourOfDay, f.MinuteOfHour);

                sb.AppendFormat(ci, "{0};{1};{2};{3};{4};{5:F2};{6};{7:F2};{8:F2};{9:F2};{10};{11};{12:F3};{13:F3};{14};",
                    Bit(f.NearKeyLevel), Bit(f.SharedKeyLevel), Bit(f.NearPriorLevel), Bit(f.NakedPriorLevel),
                    Bit(f.VwapAligned), f.VwapDistanceTicks, Bit(f.NearVaBorder), f.PocDistanceTicks,
                    f.VahDistanceTicks, f.ValDistanceTicks, Bit(f.IsLvn), Bit(f.IsHvn), f.VaOverlap,
                    f.ValueAreaCompleteness, Bit(f.ValueAreaIncomplete));

                sb.AppendFormat(ci, "{0};{1};{2:F3};{3};{4};{5};{6};{7:F3};{8};{9:F3};",
                    Bit(f.Absorption), Bit(f.AbsorptionStrong), f.AbsorptionQuality, f.AbsorptionVolume,
                    Bit(f.Iceberg), Bit(f.Imbalance), Bit(f.CumDeltaDiv), f.CumDeltaDivStrength,
                    Bit(f.DeltaFlip), f.DeltaFlipStrength);

                sb.AppendFormat(ci, "{0};{1:F3};{2};{3};{4};{5};",
                    Bit(f.FinishedAuction), f.WickRatio, f.BarDelta, f.DeltaThreshold,
                    Bit(f.DeltaConfirms), Bit(f.Exhaustion));

                sb.AppendFormat(ci, "{0:F1};{1};{2};{3};{4:F1};",
                    f.VolumeRank, f.BarVolume, f.SessionVolume, f.CumulativeDelta, f.BarRangeTicks);

                sb.AppendFormat(ci, "{0};{1};{2:F2};{3:F2};{4:F2}\n",
                    f.ConfluenceCount, f.ConfluenceMaxCount, f.ConfluencePercent,
                    f.BuySideWeight, f.SellSideWeight);

                // Le thread de donnees ne fait plus ni File I/O ni lock.
                featureHeaderWritten = true;
                if (journalWriter != null)
                    journalWriter.Enqueue(featureJournalPathResolved, FeatureJournalHeader, sb.ToString());
            }
            catch (Exception ex)
            {
                if (EnableDebugMode) Print("VP_FeatureJournal: " + ex.Message);
            }
        }

        #endregion

        #region V5 Phase 0 - Persistance des statistiques

        private string statsPathResolved = null;

        private string ResolveStatsPath()
        {
            try
            {
                if (string.IsNullOrEmpty(journalPathResolved)) return null;
                string dir = Path.GetDirectoryName(journalPathResolved);
                string baseName = Path.GetFileNameWithoutExtension(journalPathResolved);
                string suffix = string.IsNullOrEmpty(instrumentRoot) ? "" : "_" + instrumentRoot;
                return Path.Combine(dir, baseName + "_stats" + suffix + ".csv");
            }
            catch (Exception ex)
            {
                if (EnableDebugMode) Print("VP_Stats: chemin invalide (" + ex.Message + ")");
                return null;
            }
        }

        /// <summary>Remet a zero les compteurs en memoire (globalStats est readonly).</summary>
        private void ResetStatsInMemory()
        {
            statsByFamily.Clear();
            globalStats.Wins = 0;
            globalStats.Losses = 0;
            globalStats.Timeouts = 0;
            globalStats.SumR = 0.0;
        }

        /// <summary>
        /// changement de timeframe repartait d'un echantillon vide : aucune
        /// calibration statistique n'etait possible entre deux sessions.
        /// Seuls les signaux ALERTES sont comptes (identique au comportement live).
        /// </summary>
        private void LoadPersistedStats()
        {
            ResetStatsInMemory();
            if (string.IsNullOrEmpty(statsPathResolved)) return;

            try
            {
                if (!File.Exists(statsPathResolved)) return;

                string[] lines;
                lock (journalLock) { lines = File.ReadAllLines(statsPathResolved); }

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.StartsWith("Famille", StringComparison.OrdinalIgnoreCase)) continue;

                    string[] p = line.Split(';');
                    if (p.Length < 5) continue;
                    // R = 0 par convention (trade encore ouvert en fin de session) ;
                    // les agreger sous-estime mecaniquement le win rate et l'E[R].
                    if (line.IndexOf("SESSION_END", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                    int wins, losses, timeouts;
                    double sumR;
                    if (!int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out wins)) continue;
                    if (!int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out losses)) continue;
                    if (!int.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out timeouts)) continue;
                    if (!double.TryParse(p[4], NumberStyles.Float, CultureInfo.InvariantCulture, out sumR)) continue;

                    string family = p[0];
                    if (string.IsNullOrWhiteSpace(family)) continue;

                    // Le total global est reconstruit par somme des familles : pas de
                    // ligne agregee a maintenir en coherence (source unique de verite).
                    FamilyStats fs;
                    if (!statsByFamily.TryGetValue(family, out fs))
                    {
                        fs = new FamilyStats();
                        statsByFamily[family] = fs;
                    }
                    fs.Wins += wins;
                    fs.Losses += losses;
                    fs.Timeouts += timeouts;
                    fs.SumR += sumR;

                    globalStats.Wins += wins;
                    globalStats.Losses += losses;
                    globalStats.Timeouts += timeouts;
                    globalStats.SumR += sumR;
                }

                if (EnableDebugMode)
                    Print(string.Format(CultureInfo.InvariantCulture,
                        "VP_Stats: historique recharge ({0} trades, {1:F0}% WR, {2:F2}R).",
                        globalStats.Total, globalStats.WinRate, globalStats.SumR));
            }
            catch (Exception ex)
            {
                // Un fichier corrompu ne doit pas empecher l'indicateur de demarrer :
                // on repart d'un etat vierge plutot que de bloquer.
                ResetStatsInMemory();
                if (EnableDebugMode) Print("VP_Stats: rechargement impossible (" + ex.Message + ")");
            }
        }

        /// <summary>
        /// Reecrit integralement le fichier de statistiques (fichier court : une
        /// ligne par famille). Ecriture atomique via fichier temporaire + Replace
        /// pour ne jamais laisser un fichier tronque en cas d'arret brutal.
        /// </summary>
        private void SavePersistedStats()
        {
            if (string.IsNullOrEmpty(statsPathResolved)) return;

            try
            {
                StringBuilder sb = new StringBuilder(512);
                sb.Append("Famille;Wins;Losses;Timeouts;SumR\n");
                foreach (KeyValuePair<string, FamilyStats> kv in statsByFamily)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0};{1};{2};{3};{4:F4}\n",
                        kv.Key, kv.Value.Wins, kv.Value.Losses, kv.Value.Timeouts, kv.Value.SumR);
                }

                lock (journalLock)
                {
                    string tmp = statsPathResolved + ".tmp";
                    File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
                    if (File.Exists(statsPathResolved))
                        File.Replace(tmp, statsPathResolved, null);
                    else
                        File.Move(tmp, statsPathResolved);
                }
            }
            catch (Exception ex)
            {
                if (EnableDebugMode) Print("VP_Stats: sauvegarde impossible (" + ex.Message + ")");
            }
        }

        private void InitializeFeatureInfrastructure()
        {
            featureJournalPathResolved = ResolveFeatureJournalPath();
            featureHeaderWritten = false;
            statsPathResolved = ResolveStatsPath();
            featureSignalCounter = 0;
            LoadPersistedStats();
        }

        #endregion
    }
}
