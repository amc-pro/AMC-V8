#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.BarsTypes;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Indicators.VolumeProfilePro;
using SMI = NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public partial class AuctionMarketCore
    {
        #region Paramètres Swing Dédiés

        [Display(Name = "Activer Moteur Swing", Order = 1, GroupName = "Swing 01. Moteur")]
        public bool EnableSwingEngine { get; set; }

        [Range(10, 100)]
        [Display(Name = "Seuil Alerte Swing (Min Score)", Order = 2, GroupName = "Swing 01. Moteur")]
        public double SwingMinScoreToAlert { get; set; }

        [Range(10, 100)]
        [Display(Name = "Seuil Tier Moyen (Silver)", Order = 3, GroupName = "Swing 01. Moteur")]
        public double SwingTierSilverScore { get; set; }

        [Range(10, 100)]
        [Display(Name = "Seuil Tier Fort (Gold)", Order = 4, GroupName = "Swing 01. Moteur")]
        public double SwingTierGoldScore { get; set; }

        [Range(10, 100)]
        [Display(Name = "Seuil Tier Très Fort", Order = 5, GroupName = "Swing 01. Moteur")]
        public double SwingTierTresFortScore { get; set; }

        [Display(Name = "Autoriser Maintien Overnight", Order = 6, GroupName = "Swing 02. Risque")]
        public bool SwingAllowOvernightHold { get; set; }

        [Range(1, 10)]
        [Display(Name = "Max Positions Swing Simultanées", Order = 7, GroupName = "Swing 02. Risque")]
        public int SwingMaxActiveTrades { get; set; }

        [Display(Name = "Filtre News Actif", Order = 8, GroupName = "Swing 02. Risque")]
        public bool EnableNewsFilter { get; set; }

        [Display(Name = "Fichier Journal Swing (Vide = Auto shadow/swing_trades.csv)", Order = 12, GroupName = "Swing 03. Journal")]
        public string SwingJournalFilePath { get; set; }

        [Display(Name = "Activer Alertes Telegram Swing", Order = 13, GroupName = "Swing 03. Alertes")]
        public bool EnableSwingTelegramAlerts { get; set; }

        [Display(Name = "Activer POC Migration", Order = 14, GroupName = "Swing 01. Moteur")]
        public bool EnablePocMigration { get; set; }

        [Range(2, 10)]
        [Display(Name = "Sessions Min Migration POC", Order = 15, GroupName = "Swing 01. Moteur")]
        public int PocMigrationMinSessions { get; set; }

        [Range(3, 20)]
        [Display(Name = "Lookback Sessions Migration", Order = 16, GroupName = "Swing 01. Moteur")]
        public int PocMigrationLookbackSessions { get; set; }

        [Display(Name = "Activer Monthly VWAP Retest", Order = 17, GroupName = "Swing 01. Moteur")]
        public bool EnableMonthlyVwapRetest { get; set; }

        [Range(1, 30)]
        [Display(Name = "Tolérance Retest Monthly (Ticks)", Order = 18, GroupName = "Swing 01. Moteur")]
        public int MonthlyBandRetestToleranceTicks { get; set; }

        [Range(0.05, 1.0)]
        [Display(Name = "Fraction ATR Max Tolérance", Order = 19, GroupName = "Swing 01. Moteur")]
        public double MonthlyBandMaxRetestAtrFraction { get; set; }

        [Range(5, 100)]
        [Display(Name = "Barres Min Mois Courant", Order = 20, GroupName = "Swing 01. Moteur")]
        public int MonthlyBandMinBarsLookback { get; set; }

        [Range(1, 20)]
        [Display(Name = "Lookback Pente VWAP (Barres)", Order = 21, GroupName = "Swing 01. Moteur")]
        public int MonthlyBandSlopeLookbackBars { get; set; }

        [Range(0.0, 10.0)]
        [Display(Name = "Pente Min VWAP Monthly (Ticks/b)", Order = 22, GroupName = "Swing 01. Moteur")]
        public double MonthlyBandMinVwapSlope { get; set; }

        [Range(1, 5)]
        [Display(Name = "Max Retests Autorisés", Order = 23, GroupName = "Swing 01. Moteur")]
        public int MonthlyBandMaxRetestsAllowed { get; set; }

        [Range(1, 5)]
        [Display(Name = "Barres Min Acceptation Bande", Order = 24, GroupName = "Swing 01. Moteur")]
        public int MonthlyBandMinAcceptanceBars { get; set; }

        [Range(5, 100)]
        [Display(Name = "Reset Epoch Drift (Ticks)", Order = 25, GroupName = "Swing 01. Moteur")]
        public int MonthlyBandEpochResetTicks { get; set; }

        [Range(0.0, 50.0)]
        [Display(Name = "Pente Min VWAP (Ticks/Heure)", Order = 26, GroupName = "Swing 01. Moteur")]
        public double MonthlyBandMinSlopeTicksPerHour { get; set; }

        [Range(0.0, 2.0)]
        [Display(Name = "Pente Min VWAP / ATR", Order = 27, GroupName = "Swing 01. Moteur")]
        public double MonthlyBandMinSlopeAtrNormalized { get; set; }

        [Range(15, 1440)]
        [Display(Name = "Lookback Pente VWAP (Minutes)", Order = 28, GroupName = "Swing 01. Moteur")]
        public int MonthlyBandSlopeLookbackMinutes { get; set; }

        #endregion

        #region État Interne Swing

        [Browsable(false)]
        [XmlIgnore]
        public bool IsSwing
        {
            get { return TradingPreset == SniperMarketPreset.Swing; }
        }

        private VolumeProfileManager volumeProfileManager
        {
            get { return vpManager; }
        }

        private ISwingScorer swingScorer = new SwingScorer();
        private ISwingRiskManager swingRiskManager = new SwingRiskManager();
        private PocMigrationAnalyzer pocMigrationAnalyzer = new PocMigrationAnalyzer();
        private readonly List<SwingSignal> activeSwingSignals = new List<SwingSignal>();
        private readonly List<TrackedSwingTrade> openSwingTrades = new List<TrackedSwingTrade>();
        private readonly List<TrackedSwingTrade> closedSwingTrades = new List<TrackedSwingTrade>();
        private readonly List<double> monthlyVwapHistory = new List<double>();
        private readonly List<KeyValuePair<DateTime, double>> monthlyVwapTimeHistory = new List<KeyValuePair<DateTime, double>>();
        private MonthlyBandEpochState currentUpperBandEpoch = new MonthlyBandEpochState { BandType = "MONTHLY_SD1_UPPER" };
        private MonthlyBandEpochState currentLowerBandEpoch = new MonthlyBandEpochState { BandType = "MONTHLY_SD1_LOWER" };
        private int consecutiveAboveSd1Bars = 0;
        private int consecutiveBelowSd1Bars = 0;
        private int monthlyBandRetestCount = 0;
        private string resolvedSwingJournalPath;
        private bool swingJournalHeaderWritten;
        private int swingLastEvaluatedBar = -1;
        private double swingPrevMonthlySd1Upper;
        private double swingPrevMonthlySd1Lower;
        private readonly object swingJournalLock = new object();
        private const int MaxClosedSwingTrades = 500;

        #endregion

        #region Initialisation & Defaults Swing

        private void ApplySwingDefaults()
        {
            EnableSwingEngine = true;
            SwingMinScoreToAlert = 50.0;
            SwingTierSilverScore = 50.0;
            SwingTierGoldScore = 70.0;
            SwingTierTresFortScore = 85.0;
            SwingAllowOvernightHold = true;
            SwingMaxActiveTrades = 2;
            EnableNewsFilter = true;
            NewsBlackoutMinutes = 15;
            NewsWindowPenalty = 20;
            NewsHardBlock = true;
            EnablePocMigration = true;
            PocMigrationMinSessions = 3;
            PocMigrationLookbackSessions = 5;
            EnableMonthlyVwapRetest = true;
            MonthlyBandRetestToleranceTicks = 8;
            MonthlyBandMaxRetestAtrFraction = 0.30;
            MonthlyBandMinBarsLookback = 20;
            MonthlyBandSlopeLookbackBars = 5;
            MonthlyBandMinVwapSlope = 0.5;
            MonthlyBandMaxRetestsAllowed = 2;
            MonthlyBandMinAcceptanceBars = 2;
            MonthlyBandEpochResetTicks = 20;
            MonthlyBandMinSlopeTicksPerHour = 2.0;
            MonthlyBandMinSlopeAtrNormalized = 0.10;
            MonthlyBandSlopeLookbackMinutes = 240;

            // Niveaux de score pour catégorisation des Tier
            SwingJournalFilePath = string.Empty;
            EnableSwingTelegramAlerts = true;
        }

        /// <summary>
        /// Applique la configuration Swing institutionnelle par défaut.
        /// </summary>
        private void ApplySwingPreset()
        {
            EvaluateOnBarClose = true;
            EnableSniperEngine = false;
            UseSessionProfile = true;
            EnableClosedVolumeProfile = true;
            EnableSQLiteVolumeProfileHistory = true;
            EnableMarketIntelligence = true;

            // Paramètres de Risque Swing Macro
            MinRiskReward = 1.5;
            TargetR1 = 1.5;
            TargetR2 = 3.0;
            StopAtrMultiple = 2.0;
            StopBufferTicks = 4;
            RiskPerTradeCurrency = 250;
            MaxContracts = 4;
            ExecutionCostTicks = 1;

            // Anti-Lookahead & Filtrage HTF
            EnableHtfFilter = true;
            HtfMinutes = 240; // 4 Heures
            HtfEmaPeriod = 50;
            HtfStrictMode = false;
            HtfSoftMode = true;

            ApplySwingDefaults();
        }

        private void InitSwingEngine()
        {
            if (swingScorer == null)
                swingScorer = new SwingScorer();

            if (swingRiskManager == null)
                swingRiskManager = new SwingRiskManager();

            if (pocMigrationAnalyzer == null)
                pocMigrationAnalyzer = new PocMigrationAnalyzer();

            activeSwingSignals.Clear();
            openSwingTrades.Clear();
            closedSwingTrades.Clear();
            monthlyVwapHistory.Clear();
            monthlyVwapTimeHistory.Clear();
            currentUpperBandEpoch = new MonthlyBandEpochState { BandType = "MONTHLY_SD1_UPPER" };
            currentLowerBandEpoch = new MonthlyBandEpochState { BandType = "MONTHLY_SD1_LOWER" };
            consecutiveAboveSd1Bars = 0;
            consecutiveBelowSd1Bars = 0;
            monthlyBandRetestCount = 0;
            resolvedSwingJournalPath = ResolveSwingJournalPath();
            swingJournalHeaderWritten = false;
            swingLastEvaluatedBar = -1;
            swingPrevMonthlySd1Upper = 0;
            swingPrevMonthlySd1Lower = 0;

            // Reprise d'état persistant SQLite si actif (Survie aux redémarrages / overnight)
            if (EnableSQLiteVolumeProfileHistory && volumeProfileManager != null && volumeProfileManager.Repository != null)
            {
                try
                {
                    string sym = Instrument != null && Instrument.MasterInstrument != null ? Instrument.MasterInstrument.Name : "SYM";
                    var persistedTrades = volumeProfileManager.Repository.LoadActiveSwingTrades(sym);
                    if (persistedTrades != null && persistedTrades.Count > 0)
                    {
                        openSwingTrades.AddRange(persistedTrades);
                        if (EnableDebugMode)
                            Print(string.Format("VP_Swing: {0} positions actives rechargées depuis SQLite.", persistedTrades.Count));
                    }
                }
                catch (Exception ex)
                {
                    RegisterRuntimeError("InitSwingEngine.LoadActiveSwingTrades", ex);
                }
            }
        }

        private void SwingTerminated()
        {
            try
            {
                // Si maintien overnight autorisé, sauvegarder et flusher vers SQLite sans fermer
                if (SwingAllowOvernightHold && volumeProfileManager != null && volumeProfileManager.Repository != null)
                {
                    for (int i = 0; i < openSwingTrades.Count; i++)
                    {
                        volumeProfileManager.Repository.UpsertSwingTrade(openSwingTrades[i]);
                    }
                    volumeProfileManager.Repository.FlushQueue();
                }
                else
                {
                    // Clôture des trades swing en fin de session / déchargement si overnight interdit
                    DateTime nowUtc = DateTime.UtcNow;
                    for (int i = openSwingTrades.Count - 1; i >= 0; i--)
                    {
                        TrackedSwingTrade t = openSwingTrades[i];
                        t.CloseTrade(snClose > 0 ? snClose : 0.0, nowUtc, "SESSION_TERMINATED", TickSize, ResolvePointValue());
                        if (volumeProfileManager != null && volumeProfileManager.Repository != null)
                            volumeProfileManager.Repository.UpsertSwingTrade(t);
                        LogSwingTrade(t);
                    }
                }
            }
            catch (Exception ex)
            {
                RegisterRuntimeError("SwingTerminated", ex);
            }

            openSwingTrades.Clear();
            activeSwingSignals.Clear();
            closedSwingTrades.Clear();
        }

        private string ResolveSwingJournalPath()
        {
            if (!string.IsNullOrEmpty(SwingJournalFilePath))
                return SwingJournalFilePath;

            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string shadowDir = Path.Combine(docs, "NinjaTrader 8", "shadow");
                if (!Directory.Exists(shadowDir))
                {
                    Directory.CreateDirectory(shadowDir);
                }
                return Path.Combine(shadowDir, "swing_trades.csv");
            }
            catch
            {
                return "swing_trades.csv";
            }
        }

        private double ResolvePointValue()
        {
            try
            {
                if (Instrument != null && Instrument.MasterInstrument != null)
                    return Instrument.MasterInstrument.PointValue;
            }
            catch (Exception ex)
            {
                RegisterRuntimeError("ResolvePointValue", ex);
            }
            if (EnableDebugMode)
                Print("VP_Swing: PointValue indisponible, fallback 50.0");
            return 50.0;
        }

        #endregion

        #region Moteur d'Évaluation Swing sur Barre Clôturée

        /// <summary>
        /// Point d'entrée Swing exécuté sur chaque barre clôturée (anti-lookahead strict).
        /// </summary>
        private void SwingOnEvaluatedBar()
        {
            if (!IsSwing || !EnableSwingEngine) return;

            if (swingScorer == null || swingRiskManager == null || pocMigrationAnalyzer == null || currentUpperBandEpoch == null || currentLowerBandEpoch == null)
            {
                InitSwingEngine();
            }

            if (evalBarIndex < 0 || evalBarIndex == swingLastEvaluatedBar) return;
            swingLastEvaluatedBar = evalBarIndex;

            if (CurrentBars == null || volumetricBarsIndex < 0 || volumetricBarsIndex >= CurrentBars.Length || CurrentBars[volumetricBarsIndex] < 5) return;

            if (snClose <= 0 && volumetricBarsIndex < BarsArray.Length)
            {
                try { CacheEvaluatedBar(); }
                catch (Exception ex) { RegisterRuntimeError("Swing.CacheBar", ex); return; }
            }

            // 1. Mise à jour des trades ouverts (vérification des Stops et Take Profits)
            try { UpdateOpenSwingTrades(); }
            catch (Exception ex) { RegisterRuntimeError("Swing.UpdateTrades", ex); }

            // 2. Construction du contexte de marché Swing immuable
            SwingContext ctxLong = null;
            SwingContext ctxShort = null;
            try { ctxLong = BuildSwingContext(true); }
            catch (Exception ex) { RegisterRuntimeError("Swing.BuildCtxLong", ex); }

            try { ctxShort = BuildSwingContext(false); }
            catch (Exception ex) { RegisterRuntimeError("Swing.BuildCtxShort", ex); }

            // 3. Détection et évaluation des signaux sur les 5 familles institutionnelles
            try { EvaluateSwingDirection(ctxLong, SwingDirection.Long); }
            catch (Exception ex) { RegisterRuntimeError("Swing.EvalLong", ex); }

            try { EvaluateSwingDirection(ctxShort, SwingDirection.Short); }
            catch (Exception ex) { RegisterRuntimeError("Swing.EvalShort", ex); }
        }

        private SwingContext BuildSwingContext(bool isBuy)
        {
            bool inNewsWindow;
            int newsSeverity;
            CheckNewsConditions(out inNewsWindow, out newsSeverity);

            double gapPercent = CalculateSessionGapPercent();

            DateTime rawBarTime = GetVolumetricTime();
            DateTime barTimeUtc = rawBarTime != DateTime.MinValue
                ? (rawBarTime.Kind == DateTimeKind.Utc ? rawBarTime : rawBarTime.ToUniversalTime())
                : DateTime.UtcNow;

            var ctx = new SwingContext
            {
                Symbol = Instrument != null && Instrument.MasterInstrument != null ? Instrument.MasterInstrument.Name : "SYM",
                BarIndex = evalBarIndex,
                TimeUtc = barTimeUtc,
                Open = snOpen,
                High = snHigh,
                Low = snLow,
                Close = snClose,
                Volume = snVolume,
                TickSize = TickSize,
                PointValue = ResolvePointValue(),
                AtrCurrent = riskAtr != null && riskAtr.IsValidDataPoint(0) ? riskAtr[0] : TickSize * 10,
                AtrDaily = regimeAtr != null && regimeAtr.IsValidDataPoint(0) ? regimeAtr[0] : TickSize * 40,
                RiskPerTradeCurrency = RiskPerTradeCurrency,
                HtfTrendDirection = htfEma != null && htfEma.IsValidDataPoint(0) ? (snClose > htfEma[0] ? 1 : -1) : 0,
                HtfEma = htfEma != null && htfEma.IsValidDataPoint(0) ? htfEma[0] : 0.0,
                RegimeHtf = ResolveSwingRegimeHtf(
                    snClose,
                    htfEma != null && htfEma.IsValidDataPoint(0) ? htfEma[0] : 0.0,
                    htfEma != null && htfEma.IsValidDataPoint(0) ? (snClose > htfEma[0] ? 1 : -1) : 0,
                    regimeAtr != null && regimeAtr.IsValidDataPoint(0) ? regimeAtr[0] : TickSize * 40),
                IsOvernightHoldAllowed = SwingAllowOvernightHold,
                InNewsWindow = inNewsWindow,
                NewsSeverity = newsSeverity,
                GapPercent = gapPercent,
                PrevCurrentMonthlySd1Upper = swingPrevMonthlySd1Upper,
                PrevCurrentMonthlySd1Lower = swingPrevMonthlySd1Lower
            };

            // Ingestion des données Volume Profile V2 Clôturées
            if (currentVpContext != null)
            {
                int vpTol = VolumeProfileLevelToleranceTicks > 0 ? VolumeProfileLevelToleranceTicks : 3;
                double vpPrice = snClose;

                if (currentVpContext.PrevDay != null && currentVpContext.PrevDay.Valid)
                {
                    var day = currentVpContext.PrevDay;
                    ctx.DailyPoc = day.Poc;
                    ctx.DailyVah = day.Vah;
                    ctx.DailyVal = day.Val;
                    ctx.ClosedVwap = day.Vwap;
                    ctx.Sd1Upper = day.VwapSd1Upper;
                    ctx.Sd1Lower = day.VwapSd1Lower;
                    ctx.Sd2Upper = day.VwapSd2Upper;
                    ctx.Sd2Lower = day.VwapSd2Lower;
                    ctx.Sd3Upper = day.VwapSd3Upper;
                    ctx.Sd3Lower = day.VwapSd3Lower;
                    ctx.NearDailyPoc = IsNearVpLevel(vpPrice, day.Poc, TickSize, vpTol);
                    ctx.NearDailyVah = IsNearVpLevel(vpPrice, day.Vah, TickSize, vpTol);
                    ctx.NearDailyVal = IsNearVpLevel(vpPrice, day.Val, TickSize, vpTol);
                }

                if (currentVpContext.PrevWeek != null && currentVpContext.PrevWeek.Valid)
                {
                    var week = currentVpContext.PrevWeek;
                    ctx.WeeklyPoc = week.Poc;
                    ctx.WeeklyVah = week.Vah;
                    ctx.WeeklyVal = week.Val;
                    ctx.NearWeeklyPoc = IsNearVpLevel(vpPrice, week.Poc, TickSize, vpTol);
                    ctx.NearWeeklyVah = IsNearVpLevel(vpPrice, week.Vah, TickSize, vpTol);
                    ctx.NearWeeklyVal = IsNearVpLevel(vpPrice, week.Val, TickSize, vpTol);
                }

                if (currentVpContext.PrevMonth != null && currentVpContext.PrevMonth.Valid)
                {
                    var month = currentVpContext.PrevMonth;
                    ctx.MonthlyPoc = month.Poc;
                    ctx.MonthlyVah = month.Vah;
                    ctx.MonthlyVal = month.Val;
                }

                ctx.InsideHvn = (currentVpContext.Location & VolumeProfileLocationType.InsideHvn) != 0;
                ctx.InsideLvn = (currentVpContext.Location & VolumeProfileLocationType.InsideLvn) != 0;
            }

            // Order Flow & Microstructure
            ctx.BarDelta = currentBarDelta;
            ctx.CumulativeDelta = currentCumulativeDelta;
            ctx.HasDeltaDivergence = HasCumDeltaDivergence(isBuy);
            ctx.HasAbsorptionEvidence = HasRecentAbsorption(isBuy);

            // Structure SMC (FVG, BOS, CHoCH) vivants
            ctx.InFairValueGap = IsInActiveFvg(snClose, isBuy);
            ctx.HasBos = HasRecentBos(isBuy);
            ctx.HasChoch = HasRecentChoch(isBuy);

            // Current Monthly VWAP & Bandes Dynamiques (En cours de mois)
            if (vpManager != null)
            {
                double curVwap, curStdDev, sd1U, sd1L, sd2U, sd2L, sd3U, sd3L;
                int monthBars;
                DateTime monthStart, monthEnd;
                string monthPeriodKey;
                if (vpManager.TryGetCurrentMonthVwapAndBands(
                    out curVwap, out curStdDev, out sd1U, out sd1L, out sd2U, out sd2L, out sd3U, out sd3L,
                    out monthBars, out monthStart, out monthEnd, out monthPeriodKey))
                {
                    ctx.HasCurrentMonthlyVwap = true;
                    ctx.MonthlyPeriodKey = monthPeriodKey;
                    ctx.CurrentMonthlyVwap = curVwap;
                    ctx.CurrentMonthlyVwapStdDev = curStdDev;
                    ctx.CurrentMonthlySd1Upper = sd1U;
                    ctx.CurrentMonthlySd1Lower = sd1L;
                    ctx.CurrentMonthlySd2Upper = sd2U;
                    ctx.CurrentMonthlySd2Lower = sd2L;
                    ctx.CurrentMonthlySd3Upper = sd3U;
                    ctx.CurrentMonthlySd3Lower = sd3L;
                    ctx.CurrentMonthlyBarsCount = monthBars;
                    ctx.CurrentMonthlyStartUtc = monthStart;

                    // Maintien de l'historique temporel VWAP pour le calcul de pente normalisée
                    DateTime curTimeUtc = barTimeUtc;
                    if (monthlyVwapTimeHistory.Count == 0 || Math.Abs(monthlyVwapTimeHistory[monthlyVwapTimeHistory.Count - 1].Value - curVwap) > 1e-6)
                    {
                        monthlyVwapTimeHistory.Add(new KeyValuePair<DateTime, double>(curTimeUtc, curVwap));
                        monthlyVwapHistory.Add(curVwap);
                        if (monthlyVwapTimeHistory.Count > 100)
                            monthlyVwapTimeHistory.RemoveAt(0);
                        if (monthlyVwapHistory.Count > 100)
                            monthlyVwapHistory.RemoveAt(0);
                    }

                    // Calcul de la pente normalisée en Ticks/Heure
                    int lookbackMins = MonthlyBandSlopeLookbackMinutes > 0 ? MonthlyBandSlopeLookbackMinutes : 240;
                    DateTime targetLookbackTime = curTimeUtc.AddMinutes(-lookbackMins);
                    double oldVwap = curVwap;
                    DateTime oldTime = curTimeUtc;

                    for (int h = monthlyVwapTimeHistory.Count - 1; h >= 0; h--)
                    {
                        if (monthlyVwapTimeHistory[h].Key <= targetLookbackTime || h == 0)
                        {
                            oldVwap = monthlyVwapTimeHistory[h].Value;
                            oldTime = monthlyVwapTimeHistory[h].Key;
                            break;
                        }
                    }

                    double elapsedHours = Math.Max(0.25, (curTimeUtc - oldTime).TotalHours);
                    double vwapDiffTicks = (curVwap - oldVwap) / (ctx.TickSize > 0 ? ctx.TickSize : 0.25);
                    ctx.CurrentMonthlyVwapSlopeTicksPerHour = vwapDiffTicks / elapsedHours;
                    ctx.CurrentMonthlyVwapSlopeAtrNormalized = ctx.AtrCurrent > 0 ? Math.Abs(curVwap - oldVwap) / ctx.AtrCurrent : 0.0;

                    // Pente par barre (rétro-compatibilité)
                    int slopeLookbackBars = MonthlyBandSlopeLookbackBars > 0 ? MonthlyBandSlopeLookbackBars : 5;
                    if (monthlyVwapHistory.Count > slopeLookbackBars)
                    {
                        double oldBarVwap = monthlyVwapHistory[monthlyVwapHistory.Count - 1 - slopeLookbackBars];
                        ctx.CurrentMonthlyVwapSlope = ((curVwap - oldBarVwap) / (ctx.TickSize > 0 ? ctx.TickSize : 0.25)) / slopeLookbackBars;
                    }
                    else if (monthlyVwapHistory.Count > 1)
                    {
                        double oldBarVwap = monthlyVwapHistory[0];
                        ctx.CurrentMonthlyVwapSlope = ((curVwap - oldBarVwap) / (ctx.TickSize > 0 ? ctx.TickSize : 0.25)) / (monthlyVwapHistory.Count - 1);
                    }
                    else
                    {
                        ctx.CurrentMonthlyVwapSlope = 0.0;
                    }

                    // Suivi de l'acceptation multi-barres
                    if (snClose > sd1U)
                        consecutiveAboveSd1Bars++;
                    else
                        consecutiveAboveSd1Bars = 0;

                    if (snClose < sd1L)
                        consecutiveBelowSd1Bars++;
                    else
                        consecutiveBelowSd1Bars = 0;

                    // Gestion du cycle de vie des Epochs de bandes dynamiques
                    int epochResetTicks = MonthlyBandEpochResetTicks > 0 ? MonthlyBandEpochResetTicks : 20;
                    double upperDriftTicks = (currentUpperBandEpoch != null && currentUpperBandEpoch.ReferencePrice > 0) ? Math.Abs(sd1U - currentUpperBandEpoch.ReferencePrice) / (ctx.TickSize > 0 ? ctx.TickSize : 0.25) : 0.0;
                    if (currentUpperBandEpoch == null || currentUpperBandEpoch.ReferencePrice <= 0 || upperDriftTicks > epochResetTicks)
                    {
                        currentUpperBandEpoch = new MonthlyBandEpochState
                        {
                            EpochId = Guid.NewGuid().ToString("N").Substring(0, 8),
                            BandType = "MONTHLY_SD1_UPPER",
                            ReferencePrice = sd1U,
                            ReferenceBarIndex = CurrentBar,
                            ReferenceTimeUtc = curTimeUtc,
                            RetestCount = 0,
                            AcceptanceBarsCount = consecutiveAboveSd1Bars,
                            IsActive = true
                        };
                    }
                    else
                    {
                        currentUpperBandEpoch.AcceptanceBarsCount = consecutiveAboveSd1Bars;
                    }

                    double lowerDriftTicks = (currentLowerBandEpoch != null && currentLowerBandEpoch.ReferencePrice > 0) ? Math.Abs(sd1L - currentLowerBandEpoch.ReferencePrice) / (ctx.TickSize > 0 ? ctx.TickSize : 0.25) : 0.0;
                    if (currentLowerBandEpoch == null || currentLowerBandEpoch.ReferencePrice <= 0 || lowerDriftTicks > epochResetTicks)
                    {
                        currentLowerBandEpoch = new MonthlyBandEpochState
                        {
                            EpochId = Guid.NewGuid().ToString("N").Substring(0, 8),
                            BandType = "MONTHLY_SD1_LOWER",
                            ReferencePrice = sd1L,
                            ReferenceBarIndex = CurrentBar,
                            ReferenceTimeUtc = curTimeUtc,
                            RetestCount = 0,
                            AcceptanceBarsCount = consecutiveBelowSd1Bars,
                            IsActive = true
                        };
                    }
                    else
                    {
                        currentLowerBandEpoch.AcceptanceBarsCount = consecutiveBelowSd1Bars;
                    }

                    // Attribution au contexte
                    MonthlyBandEpochState activeEpoch = (isBuy ? currentUpperBandEpoch : currentLowerBandEpoch)
                        ?? new MonthlyBandEpochState { BandType = isBuy ? "MONTHLY_SD1_UPPER" : "MONTHLY_SD1_LOWER" };
                    ctx.MonthlyBandEpochId = activeEpoch.EpochId;
                    ctx.MonthlyBandAcceptanceBars = isBuy ? consecutiveAboveSd1Bars : consecutiveBelowSd1Bars;
                    ctx.MonthlyBandEpochReferencePrice = activeEpoch.ReferencePrice;
                    ctx.MonthlyBandEpochDriftTicks = isBuy ? upperDriftTicks : lowerDriftTicks;
                    ctx.MonthlyBandMinAcceptanceBarsRequired = MonthlyBandMinAcceptanceBars > 0 ? MonthlyBandMinAcceptanceBars : 1;
                    ctx.MonthlyBandMinSlopeTicksPerHourConfig = MonthlyBandMinSlopeTicksPerHour;
                    ctx.MonthlyBandMinSlopeAtrNormalizedConfig = MonthlyBandMinSlopeAtrNormalized;
                    ctx.RetestCountCurrentLevel = activeEpoch.RetestCount;

                    swingPrevMonthlySd1Upper = sd1U;
                    swingPrevMonthlySd1Lower = sd1L;
                }
            }

            // Récupération des données de la barre précédente
            try
            {
                if (volumetricBarsIndex >= 0
                    && CurrentBars != null
                    && volumetricBarsIndex < CurrentBars.Length
                    && CurrentBars[volumetricBarsIndex] >= 1
                    && Closes != null && volumetricBarsIndex < Closes.Length && Closes[volumetricBarsIndex].Count > 1
                    && Opens != null && volumetricBarsIndex < Opens.Length && Opens[volumetricBarsIndex].Count > 1
                    && Highs != null && volumetricBarsIndex < Highs.Length && Highs[volumetricBarsIndex].Count > 1
                    && Lows != null && volumetricBarsIndex < Lows.Length && Lows[volumetricBarsIndex].Count > 1)
                {
                    ctx.PrevClose = Closes[volumetricBarsIndex][1];
                    ctx.PrevOpen = Opens[volumetricBarsIndex][1];
                    ctx.PrevHigh = Highs[volumetricBarsIndex][1];
                    ctx.PrevLow = Lows[volumetricBarsIndex][1];
                }
                else
                {
                    ctx.PrevClose = snOpen;
                    ctx.PrevOpen = snOpen;
                    ctx.PrevHigh = snHigh;
                    ctx.PrevLow = snLow;
                }
            }
            catch
            {
                ctx.PrevClose = snOpen;
                ctx.PrevOpen = snOpen;
                ctx.PrevHigh = snHigh;
                ctx.PrevLow = snLow;
            }

            ctx.RetestCountCurrentLevel = monthlyBandRetestCount;

            // Analyse de la migration directionnelle du POC (Multi-Session)
            if (EnablePocMigration && volumeProfileManager != null && volumeProfileManager.Repository != null && pocMigrationAnalyzer != null)
            {
                try
                {
                    string sym = Instrument != null && Instrument.MasterInstrument != null ? Instrument.MasterInstrument.Name : "SYM";
                    int lookback = PocMigrationLookbackSessions > 0 ? PocMigrationLookbackSessions : 5;
                    int minProfiles = PocMigrationMinSessions > 0 ? PocMigrationMinSessions : 3;
                    var recentDailies = volumeProfileManager.Repository.QueryRecentDailyProfiles(sym, ctx.TimeUtc, lookback);
                    if (recentDailies != null && recentDailies.Count >= minProfiles)
                    {
                        var mig = pocMigrationAnalyzer.Analyze(recentDailies, ctx.TickSize, ctx.AtrDaily, minProfiles, minProfiles - 1, 50.0);
                        if (mig != null && mig.IsMigrationValid)
                        {
                            ctx.HasPocMigration = true;
                            ctx.PocMigrationDirection = mig.Direction;
                            ctx.PocMigrationSessions = mig.ProfilesCount;
                            ctx.PocMigrationTransitions = mig.ConsecutiveTransitions;
                            ctx.PocMigrationStrength = mig.MigrationStrength;
                            ctx.PocMigrationDriftTotalTicks = mig.TotalPocDriftTicks;
                            ctx.PocMigrationVaOverlap = mig.VaOverlapAverage;
                            ctx.PocMigrationVaOverlapMin = mig.VaOverlapMin;
                            ctx.PocMigrationVaOverlapMax = mig.VaOverlapMax;
                            ctx.PocMigrationOldestPoc = mig.OldestPoc;
                            ctx.PocMigrationNewestPoc = mig.NewestPoc;
                            ctx.PocMigrationAvgDriftPerSession = mig.AveragePocDriftPerSession;
                            ctx.PocMigrationNormalizedDrift = mig.NormalizedDriftAtr;
                        }
                    }
                }
                catch (Exception ex)
                {
                    RegisterRuntimeError("BuildSwingContext.PocMigration", ex);
                }
            }

            return ctx;
        }

        private void CheckNewsConditions(out bool inNewsWindow, out int severity)
        {
            inNewsWindow = false;
            severity = 0;
            if (!EnableNewsFilter) return;

            DateTime rawTime = GetVolumetricTime();
            if (rawTime == DateTime.MinValue) return;
            DateTime barTime = rawTime.Kind == DateTimeKind.Utc ? rawTime : rawTime.ToUniversalTime();

            int hourUtc = barTime.Hour;
            int minute = barTime.Minute;

            // Fenêtre 13h25 - 13h45 UTC (CPI / NFP / PPI CME)
            if (hourUtc == 13 && minute >= 25 && minute <= 45)
            {
                inNewsWindow = true;
                severity = 2; // Haute sévérité
            }
            // Fenêtre 18h50 - 19h30 UTC (FOMC Statement & Conference)
            else if ((hourUtc == 18 && minute >= 50) || (hourUtc == 19 && minute <= 30))
            {
                inNewsWindow = true;
                severity = 2; // Haute sévérité
            }
        }

        private double CalculateSessionGapPercent()
        {
            try
            {
                if (sessionStartBarIndex >= 0
                    && volumetricBarsIndex >= 0
                    && volumetricBarsIndex < BarsArray.Length
                    && CurrentBars != null
                    && volumetricBarsIndex < CurrentBars.Length
                    && CurrentBars[volumetricBarsIndex] >= sessionStartBarIndex)
                {
                    int sessionStartOffset = CurrentBars[volumetricBarsIndex] - sessionStartBarIndex;
                    if (Opens != null && volumetricBarsIndex < Opens.Length && sessionStartOffset >= 0 && sessionStartOffset < Opens[volumetricBarsIndex].Count
                        && Closes != null && volumetricBarsIndex < Closes.Length && (sessionStartOffset + 1) < Closes[volumetricBarsIndex].Count)
                    {
                        double sessionOpen = Opens[volumetricBarsIndex][sessionStartOffset];
                        double priorSessionClose = Closes[volumetricBarsIndex][sessionStartOffset + 1];
                        if (priorSessionClose > 0)
                        {
                            return Math.Abs(sessionOpen - priorSessionClose) / priorSessionClose * 100.0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RegisterRuntimeError("CalculateSessionGapPercent", ex);
            }
            return 0.0;
        }

        private void EvaluateSwingDirection(SwingContext ctx, SwingDirection dir)
        {
            if (ctx == null || dir == SwingDirection.None) return;

            // Filtre Anti-Stacking : Pas plus d'une position Swing active dans la même direction
            if (HasOpenTradeInDirection(dir)) return;
            if (openSwingTrades.Count >= SwingMaxActiveTrades) return;

            var setupList = new List<SwingSetupType>
            {
                SwingSetupType.RejectExtreme,
                SwingSetupType.ValueReentry,
                SwingSetupType.BreakoutRetest,
                SwingSetupType.MacroReversal,
                SwingSetupType.HtfContinuation
            };
            if (EnablePocMigration)
                setupList.Add(SwingSetupType.PocMigration);
            if (EnableMonthlyVwapRetest)
                setupList.Add(SwingSetupType.MonthlyVwapBandRetest);

            var setupTypes = setupList.ToArray();

            foreach (var setup in setupTypes)
            {
                string rejectionReason;
                if (!swingScorer.ValidatePreconditions(ctx, setup, dir, out rejectionReason))
                    continue;

                SwingSignal signal = BuildAndSizeSignal(ctx, setup, dir, null, SwingTier.Aucun);
                if (signal == null)
                    continue;

                SwingWeightedScore score = swingScorer.ComputeScore(ctx, setup, dir, signal.RiskRewardRatio1);
                SwingTier tier = swingScorer.ResolveTier(score.Total, SwingTierSilverScore, SwingTierGoldScore, SwingTierTresFortScore);

                if (tier == SwingTier.Aucun || score.Total < SwingMinScoreToAlert)
                    continue;

                signal.Score = score;
                signal.Tier = tier;
                signal.ExecutionNotes = string.Format(CultureInfo.InvariantCulture, "{0} | {1} | Score={2:F1}", setup, tier, score.Total);
                ExecuteSwingSignal(signal);
                break; // Un seul setup prioritaire par barre
            }
        }

        private SwingSignal BuildAndSizeSignal(SwingContext ctx, SwingSetupType setup, SwingDirection dir, SwingWeightedScore score, SwingTier tier)
        {
            if (ctx == null) return null;

            double tick = ctx.TickSize > 0 ? ctx.TickSize : (TickSize > 0 ? TickSize : 0.25);
            double entry = ctx.Close;
            bool isLong = dir == SwingDirection.Long;

            // Calcul du niveau structurel de référence
            double structuralLevel = isLong ? ctx.Low - (StopBufferTicks * tick) : ctx.High + (StopBufferTicks * tick);
            if (setup == SwingSetupType.RejectExtreme && ctx.Sd2Lower > 0 && isLong)
                structuralLevel = Math.Min(structuralLevel, ctx.Sd2Lower - (StopBufferTicks * tick));
            else if (setup == SwingSetupType.RejectExtreme && ctx.Sd2Upper > 0 && !isLong)
                structuralLevel = Math.Max(structuralLevel, ctx.Sd2Upper + (StopBufferTicks * tick));
            else if (setup == SwingSetupType.PocMigration && ctx.PocMigrationOldestPoc > 0)
            {
                structuralLevel = isLong ? ctx.PocMigrationOldestPoc - (StopBufferTicks * tick)
                                         : ctx.PocMigrationOldestPoc + (StopBufferTicks * tick);
            }
            else if (setup == SwingSetupType.MonthlyVwapBandRetest)
            {
                if (isLong)
                {
                    double bandRef = ctx.CurrentMonthlySd1Upper;
                    double lowRef = Math.Min(ctx.Low, bandRef);
                    structuralLevel = lowRef - (StopBufferTicks * tick);
                }
                else
                {
                    double bandRef = ctx.CurrentMonthlySd1Lower;
                    double highRef = Math.Max(ctx.High, bandRef);
                    structuralLevel = highRef + (StopBufferTicks * tick);
                }
            }

            // Calcul du Stop hybride (ATR + Structurel borné par Min/MaxStopTicks)
            double stop = swingRiskManager != null
                ? swingRiskManager.CalculateHybridStop(entry, dir, structuralLevel, ctx.AtrCurrent, StopAtrMultiple, tick, MinStopTicks, MaxStopTicks)
                : (isLong ? entry - (tick * 20) : entry + (tick * 20));

            double stopDistTicks = Math.Abs(entry - stop) / tick;
            if (stopDistTicks < MinStopTicks || stopDistTicks > MaxStopTicks)
                return null;

            // Calcul des objectifs Take Profit
            double opposingLevel = isLong ? (ctx.DailyVah > entry ? ctx.DailyVah : (ctx.Sd2Upper > entry ? ctx.Sd2Upper : 0.0))
                                          : (ctx.DailyVal < entry ? ctx.DailyVal : (ctx.Sd2Lower < entry ? ctx.Sd2Lower : 0.0));

            double tp1, tp2;
            if (swingRiskManager != null)
            {
                swingRiskManager.CalculateTargets(entry, stop, dir, TargetR1, TargetR2, opposingLevel, out tp1, out tp2);
            }
            else
            {
                tp1 = isLong ? entry + (stopDistTicks * tick * TargetR1) : entry - (stopDistTicks * tick * TargetR1);
                tp2 = isLong ? entry + (stopDistTicks * tick * TargetR2) : entry - (stopDistTicks * tick * TargetR2);
            }

            double tp1DistTicks = Math.Abs(entry - tp1) / tick;
            double tp2DistTicks = Math.Abs(entry - tp2) / tick;
            double rr1 = stopDistTicks > 0 ? tp1DistTicks / stopDistTicks : 0.0;
            double rr2 = stopDistTicks > 0 ? tp2DistTicks / stopDistTicks : 0.0;

            if (rr1 < MinRiskReward) return null;

            // Dimensionnement exact de la position selon la valeur du tick
            double ptVal = ctx.PointValue > 0 ? ctx.PointValue : ResolvePointValue();
            double tickVal = ptVal * tick;
            int contracts = swingRiskManager != null
                ? swingRiskManager.CalculatePositionSize(ctx.RiskPerTradeCurrency, stopDistTicks, tickVal, ExecutionCostTicks, MaxContracts)
                : 1;

            if (contracts <= 0) return null;

            double totalScoreVal = score != null ? score.Total : 0.0;

            var signal = new SwingSignal
            {
                Symbol = ctx.Symbol,
                GeneratedTimeUtc = ctx.TimeUtc,
                Direction = dir,
                SetupType = setup,
                Tier = tier,
                Status = SwingSignalStatus.Validated,
                Score = score,
                EntryPrice = entry,
                InitialStopPrice = stop,
                StructuralStopPrice = structuralLevel,
                AtrStopPrice = isLong ? entry - (ctx.AtrCurrent * StopAtrMultiple) : entry + (ctx.AtrCurrent * StopAtrMultiple),
                Target1Price = tp1,
                Target2Price = tp2,
                StopDistanceTicks = stopDistTicks,
                Target1DistanceTicks = tp1DistTicks,
                Target2DistanceTicks = tp2DistTicks,
                RiskRewardRatio1 = rr1,
                RiskRewardRatio2 = rr2,
                PositionSizeContracts = contracts,
                EstimatedRiskCurrency = (stopDistTicks + ExecutionCostTicks) * tickVal * contracts,
                ExecutionNotes = string.Format(CultureInfo.InvariantCulture, "{0} | {1} | Score={2:F1}", setup, tier, totalScoreVal),
                MonthlyPeriodKey = ctx.MonthlyPeriodKey,
                MonthlyVwapAtSetup = ctx.CurrentMonthlyVwap,
                MonthlySd1UpperAtSetup = ctx.CurrentMonthlySd1Upper,
                MonthlySd1LowerAtSetup = ctx.CurrentMonthlySd1Lower,
                MonthlyVwapSlopeAtSetup = ctx.CurrentMonthlyVwapSlope,
                MonthlyVwapSlopeTicksPerHourAtSetup = ctx.CurrentMonthlyVwapSlopeTicksPerHour,
                MonthlyVwapSlopeAtrNormalizedAtSetup = ctx.CurrentMonthlyVwapSlopeAtrNormalized,
                MonthlyBandEpochIdAtSetup = ctx.MonthlyBandEpochId,
                MonthlyBandAcceptanceBarsAtSetup = ctx.MonthlyBandAcceptanceBars,
                RetestDistanceTicks = isLong ? Math.Abs(ctx.Low - ctx.CurrentMonthlySd1Upper) / tick
                                             : Math.Abs(ctx.High - ctx.CurrentMonthlySd1Lower) / tick
            };

            return signal;
        }

        private void ExecuteSwingSignal(SwingSignal sig)
        {
            if (sig == null) return;

            var trade = new TrackedSwingTrade(sig, TickSize > 0 ? TickSize : 0.25, ResolvePointValue());
            if (openSwingTrades != null) openSwingTrades.Add(trade);
            if (activeSwingSignals != null) activeSwingSignals.Add(sig);

            if (sig.SetupType == SwingSetupType.MonthlyVwapBandRetest)
            {
                if (sig.Direction == SwingDirection.Long)
                {
                    if (currentUpperBandEpoch != null) currentUpperBandEpoch.RetestCount++;
                }
                else
                {
                    if (currentLowerBandEpoch != null) currentLowerBandEpoch.RetestCount++;
                }
                monthlyBandRetestCount++;
            }

            // Persistance SQLite
            if (volumeProfileManager != null && volumeProfileManager.Repository != null)
            {
                try { volumeProfileManager.Repository.UpsertSwingTrade(trade); }
                catch (Exception ex) { RegisterRuntimeError("ExecuteSwingSignal.UpsertSwingTrade", ex); }
            }

            // Log d'entrée dans le journal Shadow
            try { LogSwingTrade(trade); }
            catch (Exception ex) { RegisterRuntimeError("ExecuteSwingSignal.LogSwingTrade", ex); }

            // Notification Telegram si activée
            if (EnableSwingTelegramAlerts && (sig.Tier == SwingTier.Fort || sig.Tier == SwingTier.TresFort))
            {
                try
                {
                    string msg = string.Format(CultureInfo.InvariantCulture,
                        "🚨 <b>SWING {0} {1}</b>\n" +
                        "Instrument: <code>{2}</code> | Tier: <b>{3}</b>\n" +
                        "Setup: <b>{4}</b> | Score: <b>{5:F1}/100</b>\n" +
                        "Entrée: <code>{6:F2}</code>\n" +
                        "Stop: <code>{7:F2}</code> ({8:F0} ticks)\n" +
                        "TP1: <code>{9:F2}</code> ({10:F1}R) | TP2: <code>{11:F2}</code> ({12:F1}R)\n" +
                        "Taille: <b>{13} contrat(s)</b> | Risque: <b>${14:F2}</b>\n" +
                        "Epoch: <code>{15}</code> | Pente: <b>{16:F1} t/h</b> ({17:F2} ATR)",
                        sig.Direction == SwingDirection.Long ? "ACHAT (LONG)" : "VENTE (SHORT)",
                        sig.Symbol ?? "SYM", sig.Symbol ?? "SYM", sig.Tier, sig.SetupType, sig.Score != null ? sig.Score.Total : 0.0,
                        sig.EntryPrice, sig.InitialStopPrice, sig.StopDistanceTicks,
                        sig.Target1Price, sig.RiskRewardRatio1, sig.Target2Price, sig.RiskRewardRatio2,
                        sig.PositionSizeContracts, sig.EstimatedRiskCurrency,
                        !string.IsNullOrEmpty(sig.MonthlyBandEpochIdAtSetup) ? sig.MonthlyBandEpochIdAtSetup : "N/A",
                        sig.MonthlyVwapSlopeTicksPerHourAtSetup,
                        sig.MonthlyVwapSlopeAtrNormalizedAtSetup);

                    SendTelegramMessage(msg, null, MiTelegramChannel);
                }
                catch { }
            }
        }

        #endregion

        #region Suivi des Trades Shadow Swing & Idempotence

        private void UpdateOpenSwingTrades()
        {
            if (openSwingTrades == null || openSwingTrades.Count == 0) return;

            DateTime rawNow = GetVolumetricTime();
            DateTime nowUtc = rawNow != DateTime.MinValue
                ? (rawNow.Kind == DateTimeKind.Utc ? rawNow : rawNow.ToUniversalTime())
                : DateTime.UtcNow;

            double high = snHigh;
            double low = snLow;
            double close = snClose;
            double tick = TickSize > 0 ? TickSize : 0.25;
            double ptVal = ResolvePointValue();

            for (int i = openSwingTrades.Count - 1; i >= 0; i--)
            {
                TrackedSwingTrade t = openSwingTrades[i];
                if (t == null || t.Closed)
                {
                    openSwingTrades.RemoveAt(i);
                    continue;
                }

                t.BarsElapsed++;

                // 1. Vérification du Stop Loss
                bool stopTriggered = (t.IsLong && low <= t.CurrentStopPrice) || (!t.IsLong && high >= t.CurrentStopPrice);
                if (stopTriggered)
                {
                    t.CloseTrade(t.CurrentStopPrice, nowUtc, "STOP_LOSS", tick, ptVal);
                    if (volumeProfileManager != null && volumeProfileManager.Repository != null)
                    {
                        try { volumeProfileManager.Repository.UpsertSwingTrade(t); }
                        catch { }
                    }
                    LogSwingTrade(t);
                    if (closedSwingTrades != null) closedSwingTrades.Add(t);
                    openSwingTrades.RemoveAt(i);
                    continue;
                }

                // 2. Vérification de TP1 (Sortie partielle + passage à Break-Even)
                if (!t.Tp1Hit)
                {
                    bool tp1Triggered = (t.IsLong && high >= t.Target1Price) || (!t.IsLong && low <= t.Target1Price);
                    if (tp1Triggered)
                    {
                        t.ExecutePartialExitTp1(t.Target1Price, nowUtc, tick, ptVal);
                        t.ExecutionNotes += string.Format(CultureInfo.InvariantCulture, " [TP1_HIT ({0}c) -> BE]", t.PartialExitContracts);
                        if (volumeProfileManager != null && volumeProfileManager.Repository != null)
                        {
                            try { volumeProfileManager.Repository.UpsertSwingTrade(t); }
                            catch { }
                        }
                        LogSwingTrade(t);

                        // Si le trade a été clôturé intégralement à TP1 (ex: 1 seul contrat initial)
                        if (t.Closed)
                        {
                            if (closedSwingTrades != null) closedSwingTrades.Add(t);
                            openSwingTrades.RemoveAt(i);
                            continue;
                        }
                    }
                }

                // 3. Vérification de TP2 (Sortie finale des contrats restants)
                if (t.Tp1Hit && !t.Closed)
                {
                    bool tp2Triggered = (t.IsLong && high >= t.Target2Price) || (!t.IsLong && low <= t.Target2Price);
                    if (tp2Triggered)
                    {
                        t.CloseTrade(t.Target2Price, nowUtc, "TAKE_PROFIT_2", tick, ptVal);
                        if (volumeProfileManager != null && volumeProfileManager.Repository != null)
                        {
                            try { volumeProfileManager.Repository.UpsertSwingTrade(t); }
                            catch { }
                        }
                        LogSwingTrade(t);
                        if (closedSwingTrades != null) closedSwingTrades.Add(t);
                        openSwingTrades.RemoveAt(i);
                        continue;
                    }
                }
            }
        }

        private bool HasOpenTradeInDirection(SwingDirection dir)
        {
            bool isLong = dir == SwingDirection.Long;
            for (int i = 0; i < openSwingTrades.Count; i++)
            {
                if (openSwingTrades[i].IsLong == isLong && !openSwingTrades[i].Closed)
                    return true;
            }
            return false;
        }

        private void LogSwingTrade(TrackedSwingTrade t)
        {
            if (t == null) return;

            try
            {
                if (string.IsNullOrEmpty(resolvedSwingJournalPath))
                    resolvedSwingJournalPath = ResolveSwingJournalPath();

                lock (swingJournalLock)
                {
                    if (!swingJournalHeaderWritten && !File.Exists(resolvedSwingJournalPath))
                    {
                        File.WriteAllText(resolvedSwingJournalPath,
                            "TradeId,SignalId,Symbol,Direction,SetupType,Tier,Status,EntryTimeUtc,ExitTimeUtc,EntryPrice,ExitPrice,StopPrice,TP1,TP2,InitialContracts,RemainingContracts,RealizedR,RealizedUSD,ExitReason,Notes\n",
                            System.Text.Encoding.UTF8);
                        swingJournalHeaderWritten = true;
                    }

                    string line = string.Format(CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4},{5},{6},{7:yyyy-MM-dd HH:mm:ss},{8},{9:F2},{10:F2},{11:F2},{12:F2},{13:F2},{14},{15},{16:F2},{17:F2},{18},\"{19}\"\n",
                        t.TradeId, t.Signal != null ? t.Signal.Id : "", t.Signal != null ? t.Signal.Symbol : "", t.Signal != null ? t.Signal.Direction.ToString() : "", t.Signal != null ? t.Signal.SetupType.ToString() : "", t.Signal != null ? t.Signal.Tier.ToString() : "",
                        t.Closed ? "CLOSED" : "OPEN",
                        t.EntryTimeUtc,
                        t.Closed ? t.ExitTimeUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "ACTIVE",
                        t.EntryPrice, t.Closed ? t.ExitPrice : 0.0, t.CurrentStopPrice, t.Target1Price, t.Target2Price,
                        t.InitialContracts, t.RemainingContracts, t.RealizedR, t.RealizedPnlCurrency, t.ExitReason, t.ExecutionNotes);

                    File.AppendAllText(resolvedSwingJournalPath, line, System.Text.Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                RegisterRuntimeError("LogSwingTrade", ex);
            }
        }

        #endregion

        #region Helpers & Vérifications Microstructure

        private static SwingMarketRegime ResolveSwingRegimeHtf(double close, double htfEmaVal, int htfTrendDir, double atrDaily)
        {
            if (htfEmaVal <= 0 || close <= 0)
                return SwingMarketRegime.Transition;

            double distAtr = atrDaily > 0 ? Math.Abs(close - htfEmaVal) / atrDaily : 0.0;
            if (htfTrendDir > 0 && close > htfEmaVal)
                return distAtr < 0.35 ? SwingMarketRegime.Expansion : SwingMarketRegime.TrendUp;
            if (htfTrendDir < 0 && close < htfEmaVal)
                return distAtr < 0.35 ? SwingMarketRegime.Compression : SwingMarketRegime.TrendDown;
            if (distAtr < 0.25)
                return SwingMarketRegime.Balance;
            return SwingMarketRegime.Transition;
        }

        private void TrackClosedSwingTrade(TrackedSwingTrade t)
        {
            if (t == null) return;
            closedSwingTrades.Add(t);
            while (closedSwingTrades.Count > MaxClosedSwingTrades)
                closedSwingTrades.RemoveAt(0);
        }

        private static bool IsNearVpLevel(double price, double level, double tickSize, int toleranceTicks)
        {
            return level > 0 && tickSize > 0 && Math.Abs(price - level) / tickSize <= toleranceTicks;
        }

        private bool HasCumDeltaDivergence(bool isBuy)
        {
            if (isBuy)
                return currentBarDelta > 0 && snLow <= prevBarValPrice && snClose > snOpen;
            return currentBarDelta < 0 && snHigh >= prevBarVahPrice && snClose < snOpen;
        }

        private bool HasRecentAbsorption(bool isBuy)
        {
            if (isBuy && isBullishAbsorptionActive) return true;
            if (!isBuy && isBearishAbsorptionActive) return true;
            double z = ZDeltaCurrent();
            return isBuy ? z >= 1.0 : z <= -1.0;
        }

        // FVG : même source que le moteur Sniper (fvgEngineZones / Engine).
        private bool IsInActiveFvg(double price, bool isBuy)
        {
            if (fvgEngineZones.Count == 0) return false;
            double tol = TickSize > 0 ? TickSize * 2.0 : 0.5;
            int maxAge = FvgZoneMemoryBars > 0 ? FvgZoneMemoryBars : 200;

            for (int i = 0; i < fvgEngineZones.Count; i++)
            {
                FvgEngineZone fz = fvgEngineZones[i];
                if (fz.Invalidated || fz.IsBull != isBuy) continue;
                if (evalBarIndex - fz.BarIndex > maxAge) continue;

                if (price >= fz.Bottom - tol && price <= fz.Top + tol)
                    return true;
                if (isBuy && snLow <= fz.Top + tol && snClose >= fz.Bottom - tol)
                    return true;
                if (!isBuy && snHigh >= fz.Bottom - tol && snClose <= fz.Top + tol)
                    return true;
            }
            return false;
        }

        private bool HasRecentBos(bool isBuy)
        {
            if (miAnalyzer != null)
            {
                SMI.MiStructureEvent bos = miAnalyzer.LastBos;
                if (bos != SMI.MiStructureEvent.None)
                {
                    bool dirMatch = isBuy
                        ? bos == SMI.MiStructureEvent.BullishBos
                        : bos == SMI.MiStructureEvent.BearishBos;
                    if (dirMatch && miAnalyzer.BarsSinceBos >= 0 && miAnalyzer.BarsSinceBos <= SmcEventMaxAgeBars)
                        return true;
                }
            }
            return isBuy ? snClose > snOpen && snHigh > prevBarVahPrice
                         : snClose < snOpen && snLow < prevBarValPrice;
        }

        private bool HasRecentChoch(bool isBuy)
        {
            if (miAnalyzerH4 != null)
            {
                SMI.MiStructureEvent choch = miAnalyzerH4.LastChoch;
                if (choch != SMI.MiStructureEvent.None)
                {
                    bool dirMatch = isBuy
                        ? choch == SMI.MiStructureEvent.BullishChoch
                        : choch == SMI.MiStructureEvent.BearishChoch;
                    if (dirMatch && miAnalyzerH4.BarsSinceChoch >= 0 && miAnalyzerH4.BarsSinceChoch <= SmcEventMaxAgeBars)
                        return true;
                }
            }
            return isBuy ? snClose > prevBarPocPrice && snOpen < prevBarPocPrice
                         : snClose < prevBarPocPrice && snOpen > prevBarPocPrice;
        }

        #endregion
    }
}
