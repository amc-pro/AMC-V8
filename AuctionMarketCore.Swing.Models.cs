#region Using declarations
using System;
using System.Collections.Generic;
using System.Globalization;
using NinjaTrader.NinjaScript.Indicators.VolumeProfilePro;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    #region Énumérations Swing Institutionnelles

    /// <summary>
    /// Famille de setup de trading Swing basée sur Auction Market Theory & SMC.
    /// </summary>
    public enum SwingSetupType
    {
        /// <summary>Rejet d'un extrême statistique (SD ±2 / ±3, Clôture rejet hors VA).</summary>
        RejectExtreme = 0,
        /// <summary>Réintégration confirmée de la Value Area (VAH/VAL).</summary>
        ValueReentry = 1,
        /// <summary>Breakout avec retest confirmé d'un niveau institutionnel (POC, VAH/VAL, HVN).</summary>
        BreakoutRetest = 2,
        /// <summary>Retournement macro structurel avec divergence delta/CVD et absorption.</summary>
        MacroReversal = 3,
        /// <summary>Continuation de tendance HTF après pullback vers FVG, HVN ou VWAP clôturé.</summary>
        HtfContinuation = 4
    }

    /// <summary>
    /// Régime de marché multi-timeframe pour le Swing.
    /// </summary>
    public enum SwingMarketRegime
    {
        TrendUp = 0,
        TrendDown = 1,
        Balance = 2,
        Expansion = 3,
        Compression = 4,
        Transition = 5
    }

    /// <summary>
    /// Statut du cycle de vie d'un signal Swing.
    /// </summary>
    public enum SwingSignalStatus
    {
        Candidate = 0,
        Validated = 1,
        Blocked = 2,
        Expired = 3,
        Entered = 4,
        Exited = 5
    }

    /// <summary>
    /// Grade de qualité institutionnelle pour une opportunité Swing.
    /// </summary>
    public enum SwingTier
    {
        Aucun = 0,
        Moyen = 1,
        Fort = 2,
        TresFort = 3
    }

    /// <summary>
    /// Direction d'exposition Swing.
    /// </summary>
    public enum SwingDirection
    {
        None = 0,
        Long = 1,
        Short = -1
    }

    #endregion

    #region Modèles de Score & Contexte

    /// <summary>
    /// Décomposition transparente et déterministe du score Swing (0 à 100).
    /// </summary>
    public sealed class SwingWeightedScore
    {
        public double HtfContextScore { get; set; }     // 0..20 (Tendance HTF, Alignement 4H/Daily)
        public double AmtLocationScore { get; set; }    // 0..25 (Localisation Auction Market, SD ±2/±3, VA)
        public double VolumeProfileScore { get; set; }  // 0..20 (Confluence POC/VAH/VAL/HVN/LVN clôturés)
        public double StructureSmcScore { get; set; }   // 0..15 (BOS, CHoCH, FVG, Mitigation)
        public double OrderFlowScore { get; set; }      // 0..10 (Delta de barre clôturée, divergence CVD, Absorption)
        public double RiskRewardScore { get; set; }     // 0..10 (Qualité du R/R vers la zone adverse)
        public double Penalties { get; set; }           // Déductions (News, gaps excessifs, volatilité extrême)

        public double Total
        {
            get
            {
                double raw = HtfContextScore + AmtLocationScore + VolumeProfileScore +
                             StructureSmcScore + OrderFlowScore + RiskRewardScore - Penalties;
                if (double.IsNaN(raw) || double.IsInfinity(raw)) return 0.0;
                if (raw < 0.0) return 0.0;
                if (raw > 100.0) return 100.0;
                return raw;
            }
        }

        public string Detail { get; set; }

        public SwingWeightedScore()
        {
            Detail = string.Empty;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "Score={0:F1}/100 [HTF={1:F1} AMT={2:F1} VP={3:F1} SMC={4:F1} OF={5:F1} RR={6:F1} Pen={7:F1}]",
                Total, HtfContextScore, AmtLocationScore, VolumeProfileScore,
                StructureSmcScore, OrderFlowScore, RiskRewardScore, Penalties);
        }
    }

    /// <summary>
    /// Contexte de marché Swing immuable passé à l'évaluateur sur chaque barre clôturée.
    /// </summary>
    public sealed class SwingContext
    {
        public string Symbol { get; set; }
        public int BarIndex { get; set; }
        public DateTime TimeUtc { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }
        public double TickSize { get; set; }
        public double PointValue { get; set; }

        // Volatilité & Risque
        public double AtrCurrent { get; set; }
        public double AtrDaily { get; set; }
        public double RiskPerTradeCurrency { get; set; }

        // Contexte HTF (4H / Daily clôturés)
        public int HtfTrendDirection { get; set; }       // +1 = Haussier, -1 = Baissier, 0 = Neutre
        public double HtfEma { get; set; }
        public SwingMarketRegime RegimeHtf { get; set; }

        // Volume Profile Clôturé (Daily, Weekly, Monthly via SQLite / Memory)
        public double DailyPoc { get; set; }
        public double DailyVah { get; set; }
        public double DailyVal { get; set; }
        public double WeeklyPoc { get; set; }
        public double WeeklyVah { get; set; }
        public double WeeklyVal { get; set; }
        public double MonthlyPoc { get; set; }
        public double MonthlyVah { get; set; }
        public double MonthlyVal { get; set; }

        public bool NearDailyPoc { get; set; }
        public bool NearDailyVah { get; set; }
        public bool NearDailyVal { get; set; }
        public bool NearWeeklyPoc { get; set; }
        public bool NearWeeklyVah { get; set; }
        public bool NearWeeklyVal { get; set; }

        public bool InsideHvn { get; set; }
        public bool InsideLvn { get; set; }
        public double NearestHvnPrice { get; set; }
        public double NearestLvnPrice { get; set; }

        // VWAP Clôturé & Bandes SD
        public double ClosedVwap { get; set; }
        public double Sd1Upper { get; set; }
        public double Sd1Lower { get; set; }
        public double Sd2Upper { get; set; }
        public double Sd2Lower { get; set; }
        public double Sd3Upper { get; set; }
        public double Sd3Lower { get; set; }
        public double CurrentVwapSigmaDistance { get; set; }

        // Structure SMC
        public bool HasBos { get; set; }
        public bool HasChoch { get; set; }
        public bool InFairValueGap { get; set; }
        public double FvgTop { get; set; }
        public double FvgBottom { get; set; }
        public bool HasLiquiditySweep { get; set; }

        // Order Flow & Microstructure
        public double BarDelta { get; set; }
        public double CumulativeDelta { get; set; }
        public bool HasDeltaDivergence { get; set; }
        public bool HasAbsorptionEvidence { get; set; }

        // Macro & Calendrier News
        public bool InNewsWindow { get; set; }
        public int NewsSeverity { get; set; }
        public double GapPercent { get; set; }
        public bool IsOvernightHoldAllowed { get; set; }

        public SwingContext()
        {
            Symbol = "UNKNOWN";
            TimeUtc = DateTime.UtcNow;
            TickSize = 0.25;
            PointValue = 50.0;
            HtfTrendDirection = 0;
            RegimeHtf = SwingMarketRegime.Balance;
        }
    }

    /// <summary>
    /// Signal Swing complet avec traçabilité intégrale, gestion de position et invalidations.
    /// </summary>
    public sealed class SwingSignal
    {
        public string Id { get; set; }
        public DateTime GeneratedTimeUtc { get; set; }
        public string Symbol { get; set; }
        public SwingDirection Direction { get; set; }
        public SwingSetupType SetupType { get; set; }
        public SwingTier Tier { get; set; }
        public SwingSignalStatus Status { get; set; }
        public SwingWeightedScore Score { get; set; }

        // Niveaux de Prix & Gestion du Risque
        public double EntryPrice { get; set; }
        public double InitialStopPrice { get; set; }
        public double StructuralStopPrice { get; set; }
        public double AtrStopPrice { get; set; }
        public double Target1Price { get; set; }
        public double Target2Price { get; set; }

        public double StopDistanceTicks { get; set; }
        public double Target1DistanceTicks { get; set; }
        public double Target2DistanceTicks { get; set; }
        public double RiskRewardRatio1 { get; set; }
        public double RiskRewardRatio2 { get; set; }

        public int PositionSizeContracts { get; set; }
        public double EstimatedRiskCurrency { get; set; }

        // Suivi & Invalidations
        public string InvalidationReason { get; set; }
        public string ExecutionNotes { get; set; }
        public int ValidityBarsMax { get; set; }
        public int BarsElapsed { get; set; }

        public SwingSignal()
        {
            Id = Guid.NewGuid().ToString("N");
            GeneratedTimeUtc = DateTime.UtcNow;
            Symbol = "UNKNOWN";
            Direction = SwingDirection.None;
            SetupType = SwingSetupType.RejectExtreme;
            Tier = SwingTier.Aucun;
            Status = SwingSignalStatus.Candidate;
            Score = new SwingWeightedScore();
            InvalidationReason = string.Empty;
            ExecutionNotes = string.Empty;
            ValidityBarsMax = 12;
            BarsElapsed = 0;
        }

        public string ToCsvHeader()
        {
            return "Id,TimeUtc,Symbol,Direction,SetupType,Tier,Status,TotalScore,Entry,Stop,TP1,TP2,StopTicks,TP1Ticks,RR1,Contracts,RiskUSD,Notes";
        }

        public string ToCsvRow()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0},{1:yyyy-MM-dd HH:mm:ss},{2},{3},{4},{5},{6},{7:F1},{8:F2},{9:F2},{10:F2},{11:F2},{12:F0},{13:F0},{14:F2},{15},{16:F2},\"{17}\"",
                Id, GeneratedTimeUtc, Symbol, Direction, SetupType, Tier, Status, Score != null ? Score.Total : 0.0,
                EntryPrice, InitialStopPrice, Target1Price, Target2Price, StopDistanceTicks, Target1DistanceTicks,
                RiskRewardRatio1, PositionSizeContracts, EstimatedRiskCurrency, ExecutionNotes);
        }
    }

    #endregion

    #region Interfaces Swing (Contrats de Service)

    /// <summary>
    /// Contrat du moteur d'évaluation et de scoring Swing.
    /// </summary>
    public interface ISwingScorer
    {
        bool ValidatePreconditions(SwingContext ctx, SwingSetupType setup, SwingDirection dir, out string rejectionReason);
        SwingWeightedScore ComputeScore(SwingContext ctx, SwingSetupType setup, SwingDirection dir);
        SwingTier ResolveTier(double totalScore, double thresholdMoyen, double thresholdFort, double thresholdTresFort);
    }

    /// <summary>
    /// Contrat du gestionnaire de risque Swing (sizing exact, bornes Min/Max ticks, trailing).
    /// </summary>
    public interface ISwingRiskManager
    {
        int CalculatePositionSize(double riskCurrency, double stopDistanceTicks, double tickValue, double executionCostTicks, int maxContracts);
        double CalculateHybridStop(double entryPrice, SwingDirection dir, double structuralLevel, double atr, double atrMultiple, double tickSize, int minStopTicks, int maxStopTicks);
        void CalculateTargets(double entryPrice, double stopPrice, SwingDirection dir, double target1R, double target2R, double keyOpposingLevel, out double tp1, out double tp2);
    }

    #endregion

    #region Classes Concrètes ISwingScorer, ISwingRiskManager & TrackedSwingTrade

    /// <summary>
    /// Implémentation déterministe du moteur de scoring Swing pour les 5 familles institutionnelles.
    /// </summary>
    public sealed class SwingScorer : ISwingScorer
    {
        public bool ValidatePreconditions(SwingContext ctx, SwingSetupType setup, SwingDirection dir, out string rejectionReason)
        {
            rejectionReason = string.Empty;
            if (ctx == null || dir == SwingDirection.None)
            {
                rejectionReason = "CONTEXT_NULL";
                return false;
            }

            if (ctx.InNewsWindow && ctx.NewsSeverity >= 2)
            {
                rejectionReason = "HIGH_SEVERITY_NEWS_BLOCK";
                return false;
            }

            bool isLong = dir == SwingDirection.Long;

            switch (setup)
            {
                case SwingSetupType.RejectExtreme:
                    if (isLong)
                    {
                        bool testedExtreme = (ctx.Sd2Lower > 0 && ctx.Low <= ctx.Sd2Lower) || (ctx.DailyVal > 0 && ctx.Low <= ctx.DailyVal);
                        bool candleRejection = ctx.Close > ctx.Open && ctx.Close > ctx.Low;
                        if (!testedExtreme || !candleRejection) { rejectionReason = "NO_EXTREME_TEST_LONG"; return false; }
                    }
                    else
                    {
                        bool testedExtreme = (ctx.Sd2Upper > 0 && ctx.High >= ctx.Sd2Upper) || (ctx.DailyVah > 0 && ctx.High >= ctx.DailyVah);
                        bool candleRejection = ctx.Close < ctx.Open && ctx.Close < ctx.High;
                        if (!testedExtreme || !candleRejection) { rejectionReason = "NO_EXTREME_TEST_SHORT"; return false; }
                    }
                    break;

                case SwingSetupType.ValueReentry:
                    if (isLong)
                    {
                        if (ctx.DailyVal <= 0 || ctx.Open >= ctx.DailyVal || ctx.Close <= ctx.DailyVal)
                        { rejectionReason = "NO_VA_REENTRY_LONG"; return false; }
                    }
                    else
                    {
                        if (ctx.DailyVah <= 0 || ctx.Open <= ctx.DailyVah || ctx.Close >= ctx.DailyVah)
                        { rejectionReason = "NO_VA_REENTRY_SHORT"; return false; }
                    }
                    break;

                case SwingSetupType.BreakoutRetest:
                    if (isLong)
                    {
                        if (ctx.DailyVah <= 0 || ctx.Low > ctx.DailyVah + (ctx.TickSize * 10) || ctx.Close < ctx.DailyVah)
                        { rejectionReason = "NO_BREAKOUT_RETEST_LONG"; return false; }
                    }
                    else
                    {
                        if (ctx.DailyVal <= 0 || ctx.High < ctx.DailyVal - (ctx.TickSize * 10) || ctx.Close > ctx.DailyVal)
                        { rejectionReason = "NO_BREAKOUT_RETEST_SHORT"; return false; }
                    }
                    break;

                case SwingSetupType.MacroReversal:
                    if (isLong && (!ctx.HasDeltaDivergence || ctx.Close <= ctx.Open))
                    { rejectionReason = "NO_MACRO_REVERSAL_LONG"; return false; }
                    if (!isLong && (!ctx.HasDeltaDivergence || ctx.Close >= ctx.Open))
                    { rejectionReason = "NO_MACRO_REVERSAL_SHORT"; return false; }
                    break;

                case SwingSetupType.HtfContinuation:
                    if (isLong && (ctx.HtfTrendDirection <= 0 || ctx.Close <= ctx.Open))
                    { rejectionReason = "NO_HTF_CONTINUATION_LONG"; return false; }
                    if (!isLong && (ctx.HtfTrendDirection >= 0 || ctx.Close >= ctx.Open))
                    { rejectionReason = "NO_HTF_CONTINUATION_SHORT"; return false; }
                    break;
            }

            return true;
        }

        public SwingWeightedScore ComputeScore(SwingContext ctx, SwingSetupType setup, SwingDirection dir)
        {
            var s = new SwingWeightedScore();
            bool isLong = dir == SwingDirection.Long;

            // 1. HTF Context Score (0..20)
            if ((isLong && ctx.HtfTrendDirection > 0) || (!isLong && ctx.HtfTrendDirection < 0))
                s.HtfContextScore = 20.0;
            else if (ctx.HtfTrendDirection == 0)
                s.HtfContextScore = 10.0;
            else
                s.HtfContextScore = 4.0; // Contre-tendance pénalisée

            // 2. AMT Location Score (0..25)
            if (setup == SwingSetupType.RejectExtreme)
            {
                if ((isLong && ctx.Low <= ctx.Sd3Lower) || (!isLong && ctx.High >= ctx.Sd3Upper)) s.AmtLocationScore = 25.0;
                else if ((isLong && ctx.Low <= ctx.Sd2Lower) || (!isLong && ctx.High >= ctx.Sd2Upper)) s.AmtLocationScore = 22.0;
                else s.AmtLocationScore = 18.0;
            }
            else if (setup == SwingSetupType.ValueReentry)
            {
                s.AmtLocationScore = 22.0;
            }
            else
            {
                s.AmtLocationScore = 18.0;
            }

            // 3. Volume Profile Score (0..20)
            double vpScore = 0.0;
            if (ctx.NearWeeklyPoc || ctx.NearDailyPoc) vpScore += 8.0;
            if (ctx.NearDailyVah || ctx.NearDailyVal) vpScore += 6.0;
            if (ctx.InsideHvn) vpScore += 6.0;
            s.VolumeProfileScore = Math.Min(20.0, vpScore > 0 ? vpScore : 12.0);

            // 4. Structure SMC Score (0..15)
            double smcScore = 5.0;
            if (ctx.HasBos) smcScore += 4.0;
            if (ctx.HasChoch) smcScore += 4.0;
            if (ctx.InFairValueGap) smcScore += 2.0;
            s.StructureSmcScore = Math.Min(15.0, smcScore);

            // 5. Order Flow Score (0..10)
            double ofScore = 4.0;
            if ((isLong && ctx.BarDelta > 0) || (!isLong && ctx.BarDelta < 0)) ofScore += 3.0;
            if (ctx.HasDeltaDivergence) ofScore += 3.0;
            s.OrderFlowScore = Math.Min(10.0, ofScore);

            // 6. Risk / Reward Score (0..10)
            s.RiskRewardScore = 9.0;

            // 7. Pénalités
            if (ctx.InNewsWindow) s.Penalties += 15.0;
            if (ctx.GapPercent > 1.0) s.Penalties += 10.0;

            s.Detail = string.Format(CultureInfo.InvariantCulture,
                "HTF={0:F0} AMT={1:F0} VP={2:F0} SMC={3:F0} OF={4:F0} RR={5:F0} Pen={6:F0}",
                s.HtfContextScore, s.AmtLocationScore, s.VolumeProfileScore, s.StructureSmcScore, s.OrderFlowScore, s.RiskRewardScore, s.Penalties);

            return s;
        }

        public SwingTier ResolveTier(double totalScore, double thresholdMoyen, double thresholdFort, double thresholdTresFort)
        {
            if (totalScore >= thresholdTresFort) return SwingTier.TresFort;
            if (totalScore >= thresholdFort) return SwingTier.Fort;
            if (totalScore >= thresholdMoyen) return SwingTier.Moyen;
            return SwingTier.Aucun;
        }
    }

    /// <summary>
    /// Gestionnaire de risque quantitatif pour le dimensionnement Swing.
    /// </summary>
    public sealed class SwingRiskManager : ISwingRiskManager
    {
        public int CalculatePositionSize(double riskCurrency, double stopDistanceTicks, double tickValue, double executionCostTicks, int maxContracts)
        {
            if (riskCurrency <= 0 || stopDistanceTicks <= 0 || tickValue <= 0 || maxContracts <= 0)
                return 1;

            double riskPerContract = (stopDistanceTicks + executionCostTicks) * tickValue;
            if (riskPerContract <= 0) return 1;

            int size = (int)Math.Floor(riskCurrency / riskPerContract);
            return Math.Max(1, Math.Min(maxContracts, size));
        }

        public double CalculateHybridStop(double entryPrice, SwingDirection dir, double structuralLevel, double atr, double atrMultiple, double tickSize, int minStopTicks, int maxStopTicks)
        {
            if (tickSize <= 0) tickSize = 0.25;
            bool isLong = dir == SwingDirection.Long;

            double atrTicks = (atr * Math.Max(1.0, atrMultiple)) / tickSize;
            double structuralTicks = Math.Abs(entryPrice - structuralLevel) / tickSize;

            double chosenTicks = Math.Max(atrTicks, structuralTicks);
            double clampedTicks = Math.Max(minStopTicks, Math.Min(maxStopTicks, chosenTicks));

            return isLong ? entryPrice - (clampedTicks * tickSize)
                          : entryPrice + (clampedTicks * tickSize);
        }

        public void CalculateTargets(double entryPrice, double stopPrice, SwingDirection dir, double target1R, double target2R, double keyOpposingLevel, out double tp1, out double tp2)
        {
            bool isLong = dir == SwingDirection.Long;
            double riskDist = Math.Abs(entryPrice - stopPrice);

            double target1Dist = riskDist * Math.Max(1.0, target1R);
            double target2Dist = riskDist * Math.Max(2.0, target2R);

            tp1 = isLong ? entryPrice + target1Dist : entryPrice - target1Dist;
            tp2 = isLong ? entryPrice + target2Dist : entryPrice - target2Dist;

            // Ajustement si un mur opposé est détecté
            if (keyOpposingLevel > 0)
            {
                if (isLong && keyOpposingLevel > entryPrice && keyOpposingLevel < tp2)
                    tp2 = keyOpposingLevel;
                else if (!isLong && keyOpposingLevel < entryPrice && keyOpposingLevel > tp2)
                    tp2 = keyOpposingLevel;
            }
        }
    }

    /// <summary>
    /// Suivi individuel d'un trade Swing virtuel dans le journal Shadow.
    /// </summary>
    public sealed class TrackedSwingTrade
    {
        public string TradeId { get; set; }
        public SwingSignal Signal { get; set; }
        public bool IsLong { get; set; }
        public double EntryPrice { get; set; }
        public double InitialStopPrice { get; set; }
        public double CurrentStopPrice { get; set; }
        public double Target1Price { get; set; }
        public double Target2Price { get; set; }
        public int PositionSizeContracts { get; set; }
        public DateTime EntryTimeUtc { get; set; }
        public DateTime ExitTimeUtc { get; set; }
        public double ExitPrice { get; set; }
        public bool Closed { get; set; }
        public bool Tp1Hit { get; set; }
        public string ExitReason { get; set; }
        public double RealizedR { get; set; }
        public double RealizedPnlCurrency { get; set; }
        public int BarsElapsed { get; set; }
        public string ExecutionNotes { get; set; }

        public TrackedSwingTrade(SwingSignal sig, double tickSize, double pointValue)
        {
            TradeId = Guid.NewGuid().ToString("N").Substring(0, 12);
            Signal = sig;
            IsLong = sig.Direction == SwingDirection.Long;
            EntryPrice = sig.EntryPrice;
            InitialStopPrice = sig.InitialStopPrice;
            CurrentStopPrice = sig.InitialStopPrice;
            Target1Price = sig.Target1Price;
            Target2Price = sig.Target2Price;
            PositionSizeContracts = sig.PositionSizeContracts;
            EntryTimeUtc = sig.GeneratedTimeUtc;
            Closed = false;
            Tp1Hit = false;
            ExitReason = "ACTIVE";
            RealizedR = 0.0;
            RealizedPnlCurrency = 0.0;
            BarsElapsed = 0;
            ExecutionNotes = sig.ExecutionNotes;
        }

        public void CloseTrade(double exitPrice, DateTime exitTimeUtc, string reason, double tickSize, double pointValue)
        {
            Closed = true;
            ExitPrice = exitPrice;
            ExitTimeUtc = exitTimeUtc;
            ExitReason = reason;

            double tickVal = pointValue * tickSize;
            double stopDist = Math.Abs(EntryPrice - InitialStopPrice);

            if (reason == "STOP_LOSS")
            {
                RealizedR = Tp1Hit ? 0.0 : -1.0;
                double exitDistTicks = Math.Abs(EntryPrice - exitPrice) / tickSize;
                RealizedPnlCurrency = Tp1Hit ? 0.0 : -(exitDistTicks * tickVal * PositionSizeContracts);
            }
            else if (reason == "TAKE_PROFIT_2")
            {
                RealizedR = stopDist > 0 ? (Math.Abs(exitPrice - EntryPrice) / stopDist) : 3.0;
                double exitDistTicks = Math.Abs(exitPrice - EntryPrice) / tickSize;
                RealizedPnlCurrency = exitDistTicks * tickVal * PositionSizeContracts;
            }
            else
            {
                RealizedR = stopDist > 0 ? ((IsLong ? exitPrice - EntryPrice : EntryPrice - exitPrice) / stopDist) : 0.0;
                double exitDistTicks = (IsLong ? exitPrice - EntryPrice : EntryPrice - exitPrice) / tickSize;
                RealizedPnlCurrency = exitDistTicks * tickVal * PositionSizeContracts;
            }
        }
    }

    #endregion
}
