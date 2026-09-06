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
        HtfContinuation = 4,
        /// <summary>Migration directionnelle du POC sur N sessions consécutives (Auction Market Theory pure).</summary>
        PocMigration = 5,
        /// <summary>Retest confirmé de la bande SD±1 du VWAP Monthly en cours de formation (dynamique).</summary>
        MonthlyVwapBandRetest = 6
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
    /// Santé du régime de marché par rapport à la direction d'un trade Swing (Architecture V2).
    /// </summary>
    public enum SwingRegimeHealth
    {
        Aligned = 0,       // Régime favorable au trade
        Neutral = 1,       // Balance ou transition tolérée
        Deteriorated = 2   // Régime franchement adverse persistant
    }

    /// <summary>
    /// Décision d'arbitrage de régime basée sur la confirmation structurelle (Architecture V2 Structure-First).
    /// </summary>
    public enum SwingRegimeDecision
    {
        Hold = 0,              // Maintenir sans altération
        ProtectBreakeven = 1,  // Sécuriser le profit (resserrement du stop à BE si en gain)
        StructuralExit = 2     // Sortie autorisée uniquement si la structure est invalidée avec confirmation
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

    /// <summary>
    /// Cycle de vie d'une campagne Swing institutionnelle complète.
    /// </summary>
    public enum SwingCampaignState
    {
        Idle = 0,
        Armed = 1,
        Candidate = 2,
        Validated = 3,
        Entered = 4,
        Active = 5,
        Tp1Hit = 6,
        BreakEven = 7,
        Runner = 8,
        Completed = 9,
        Invalidated = 10,
        Timeout = 11,
        RegimeChanged = 12,
        Cooldown = 13
    }

    #endregion

    #region Modèles d'Opportunité & Campagne Swing V3

    /// <summary>
    /// Motifs structurés et déterministes de rejet pour l'audit d'opportunités Swing.
    /// </summary>
    public static class SwingRejectionReason
    {
        public const string None = "NONE";
        public const string ContextNull = "CONTEXT_NULL";
        public const string HighSeverityNewsBlock = "HIGH_SEVERITY_NEWS_BLOCK";
        public const string DuplicateCampaign = "DUPLICATE_CAMPAIGN";
        public const string SameSignature = "SAME_SIGNATURE";
        public const string CooldownActive = "COOLDOWN_ACTIVE";
        public const string SessionLimitReached = "SESSION_LIMIT_REACHED";
        public const string DirectionLimitReached = "DIRECTION_LIMIT_REACHED";
        public const string RegimeConflict = "REGIME_CONFLICT";
        public const string HtfConflict = "HTF_CONFLICT";
        public const string LateEntryExtended = "LATE_ENTRY_EXTENDED";
        public const string LowTimingQuality = "LOW_TIMING_QUALITY";
        public const string LowFinalScore = "LOW_FINAL_SCORE";
        public const string NoPullbackToValue = "NO_PULLBACK_TO_VALUE";
        public const string MacroReversalNoOrderFlow = "MACRO_REVERSAL_NO_ORDER_FLOW";
        public const string RiskRewardInsufficient = "RISK_REWARD_INSUFFICIENT";
        public const string PositionSizingFailed = "POSITION_SIZING_FAILED";
        public const string InvalidAtrData = "INVALID_ATR_DATA";
        public const string InvalidPointValue = "INVALID_POINT_VALUE";
    }

    /// <summary>
    /// Signature déterministe d'une opportunité Swing pour déduplication et même campagne.
    /// </summary>
    public sealed class SwingSetupSignature : IEquatable<SwingSetupSignature>
    {
        public string Symbol { get; set; }
        public SwingSetupType SetupType { get; set; }
        public SwingDirection Direction { get; set; }
        public string StructureId { get; set; }
        public string RegimeId { get; set; }
        public double AnchorPrice { get; set; }

        public SwingSetupSignature()
        {
            Symbol = string.Empty;
            StructureId = string.Empty;
            RegimeId = string.Empty;
        }

        public string FormattedKey
        {
            get
            {
                return string.Format(CultureInfo.InvariantCulture,
                    "{0}|{1}|{2}|{3}|{4}|{5:F2}",
                    Symbol ?? "SYM", SetupType, Direction,
                    string.IsNullOrEmpty(StructureId) ? "NO_STRUCT" : StructureId,
                    string.IsNullOrEmpty(RegimeId) ? "NO_REGIME" : RegimeId,
                    AnchorPrice);
            }
        }

        public bool Equals(SwingSetupSignature other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(FormattedKey, other.FormattedKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SwingSetupSignature);
        }

        public override int GetHashCode()
        {
            return FormattedKey != null ? FormattedKey.GetHashCode() : 0;
        }

        public override string ToString()
        {
            return FormattedKey;
        }
    }

    /// <summary>
    /// Candidat d'opportunité Swing avant sélection finale par ranking multi-critères.
    /// </summary>
    public sealed class SwingCandidate
    {
        public string Id { get; set; }
        public string Symbol { get; set; }
        public SwingDirection Direction { get; set; }
        public SwingSetupType SetupType { get; set; }
        public int BarIndex { get; set; }
        public DateTime TimeUtc { get; set; }
        public string StructureId { get; set; }
        public string RegimeId { get; set; }
        public SwingSetupSignature Signature { get; set; }

        // Décomposition des scores et pénalités
        public double BaseScore { get; set; }
        public double TimingQuality { get; set; }         // 0..10
        public double RegimeCompatibility { get; set; }   // 0..10
        public double DirectionalQuality { get; set; }    // 0..10
        public double LocationQuality { get; set; }       // 0..10
        public double LateEntryPenalty { get; set; }      // 0..15
        public double ConflictPenalty { get; set; }       // 0..15
        public double FinalQualityScore { get; set; }     // Score consolidé pondéré

        // Gestion du risque & niveaux
        public double EntryPrice { get; set; }
        public double StopPrice { get; set; }
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

        // Détails et statut
        public SwingWeightedScore ScoreDetails { get; set; }
        public SwingTier Tier { get; set; }
        public string RejectionReason { get; set; }
        public bool IsValid { get; set; }
        public string ExecutionNotes { get; set; }

        public SwingCandidate()
        {
            Id = Guid.NewGuid().ToString("N");
            Symbol = "SYM";
            Direction = SwingDirection.None;
            SetupType = SwingSetupType.RejectExtreme;
            TimeUtc = DateTime.UtcNow;
            StructureId = string.Empty;
            RegimeId = string.Empty;
            Signature = new SwingSetupSignature();
            RejectionReason = SwingRejectionReason.None;
            IsValid = true;
            ExecutionNotes = string.Empty;
        }
    }

    /// <summary>
    /// Représentation d'une Campagne Swing active regroupant une ou plusieurs phases sur la même opportunité.
    /// </summary>
    public sealed class SwingCampaign
    {
        public string CampaignId { get; set; }
        public string Symbol { get; set; }
        public SwingDirection Direction { get; set; }
        public SwingSetupType SetupType { get; set; }
        public SwingSetupSignature Signature { get; set; }
        public SwingCampaignState State { get; set; }
        public int InitialEntryBarIndex { get; set; }
        public DateTime InitialEntryTimeUtc { get; set; }
        public int LastActionBarIndex { get; set; }
        public DateTime LastActionTimeUtc { get; set; }
        public int TradesCount { get; set; }
        public double TotalRealizedR { get; set; }
        public double TotalRealizedCurrency { get; set; }
        public string InitialStructureId { get; set; }
        public string CurrentStructureId { get; set; }

        public SwingCampaign()
        {
            CampaignId = Guid.NewGuid().ToString("N");
            Symbol = "SYM";
            Direction = SwingDirection.None;
            State = SwingCampaignState.Idle;
            Signature = new SwingSetupSignature();
            InitialEntryTimeUtc = DateTime.UtcNow;
            LastActionTimeUtc = DateTime.UtcNow;
            InitialStructureId = string.Empty;
            CurrentStructureId = string.Empty;
        }
    }

    #endregion

    #region Modèles d'Epoch & Identité de Bandes Mobiles

    /// <summary>
    /// Cycle de vie et identité d'un niveau de bande mensuelle dynamique (SD+1 / SD-1).
    /// Évite les faux retests sur bandes mobiles et permet un comptage déterministe par Epoch.
    /// </summary>
    public sealed class MonthlyBandEpochState
    {
        public string EpochId { get; set; }
        public string BandType { get; set; } // "MONTHLY_SD1_UPPER" ou "MONTHLY_SD1_LOWER"
        public double ReferencePrice { get; set; }
        public int ReferenceBarIndex { get; set; }
        public DateTime ReferenceTimeUtc { get; set; }
        public int RetestCount { get; set; }
        public int AcceptanceBarsCount { get; set; }
        public bool IsActive { get; set; }

        public MonthlyBandEpochState()
        {
            EpochId = Guid.NewGuid().ToString("N");
            BandType = string.Empty;
            ReferencePrice = 0.0;
            ReferenceBarIndex = 0;
            ReferenceTimeUtc = DateTime.MinValue;
            RetestCount = 0;
            AcceptanceBarsCount = 0;
            IsActive = true;
        }
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

        // VWAP Monthly Courant & Bandes Dynamiques (En cours de mois)
        public bool HasCurrentMonthlyVwap { get; set; }
        public string MonthlyPeriodKey { get; set; }
        public double CurrentMonthlyVwap { get; set; }
        public double CurrentMonthlyVwapStdDev { get; set; }
        public double CurrentMonthlySd1Upper { get; set; }
        public double CurrentMonthlySd1Lower { get; set; }
        public double CurrentMonthlySd2Upper { get; set; }
        public double CurrentMonthlySd2Lower { get; set; }
        public double CurrentMonthlySd3Upper { get; set; }
        public double CurrentMonthlySd3Lower { get; set; }
        public double CurrentMonthlyVwapSlope { get; set; }
        public double CurrentMonthlyVwapSlopeTicksPerHour { get; set; }
        public double CurrentMonthlyVwapSlopeAtrNormalized { get; set; }
        public int CurrentMonthlyBarsCount { get; set; }
        public DateTime CurrentMonthlyStartUtc { get; set; }

        // Identité d'Epoch et Acceptation Multi-Barres
        public string MonthlyBandEpochId { get; set; }
        public int MonthlyBandAcceptanceBars { get; set; }
        public double MonthlyBandEpochReferencePrice { get; set; }
        public double MonthlyBandEpochDriftTicks { get; set; }
        public int MonthlyBandMinAcceptanceBarsRequired { get; set; }
        public double MonthlyBandMinSlopeTicksPerHourConfig { get; set; }
        public double MonthlyBandMinSlopeAtrNormalizedConfig { get; set; }

        // Barres précédentes pour détection de retest / acceptation
        public double PrevClose { get; set; }
        public double PrevHigh { get; set; }
        public double PrevLow { get; set; }
        public double PrevOpen { get; set; }
        public double PrevCurrentMonthlySd1Upper { get; set; }
        public double PrevCurrentMonthlySd1Lower { get; set; }
        public int RetestCountCurrentLevel { get; set; }

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

        // POC Migration Multi-Session
        public bool HasPocMigration { get; set; }
        public SwingDirection PocMigrationDirection { get; set; }
        public int PocMigrationSessions { get; set; }
        public int PocMigrationTransitions { get; set; }
        public double PocMigrationStrength { get; set; }
        public double PocMigrationDriftTotalTicks { get; set; }
        public double PocMigrationVaOverlap { get; set; }
        public double PocMigrationVaOverlapMin { get; set; }
        public double PocMigrationVaOverlapMax { get; set; }
        public double PocMigrationOldestPoc { get; set; }
        public double PocMigrationNewestPoc { get; set; }
        public double PocMigrationAvgDriftPerSession { get; set; }
        public double PocMigrationNormalizedDrift { get; set; }

        // Zero-Trust Data Integrity & Clés de Structure V3
        public bool IsAtrValid { get; set; }
        public bool IsPointValueValid { get; set; }
        public double SessionVwap { get; set; }
        public double SwingAnchorPrice { get; set; }
        public string ActiveStructureId { get; set; }
        public string ActiveRegimeId { get; set; }

        public SwingContext()
        {
            Symbol = "UNKNOWN";
            TimeUtc = DateTime.UtcNow;
            TickSize = 0.25;
            PointValue = 50.0;
            HtfTrendDirection = 0;
            RegimeHtf = SwingMarketRegime.Balance;
            PocMigrationDirection = SwingDirection.None;
            HasCurrentMonthlyVwap = false;
            MonthlyPeriodKey = string.Empty;
            CurrentMonthlyBarsCount = 0;
            CurrentMonthlyStartUtc = DateTime.MinValue;
            MonthlyBandEpochId = string.Empty;
            MonthlyBandAcceptanceBars = 0;
            MonthlyBandMinAcceptanceBarsRequired = 1;
            MonthlyBandMinSlopeTicksPerHourConfig = 2.0;
            MonthlyBandMinSlopeAtrNormalizedConfig = 0.0;
            RetestCountCurrentLevel = 0;

            IsAtrValid = true;
            IsPointValueValid = true;
            AtrCurrent = 10.0;
            SessionVwap = 0.0;
            SwingAnchorPrice = 0.0;
            ActiveStructureId = string.Empty;
            ActiveRegimeId = string.Empty;
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

        // Snapshot immuable au signal (Current Monthly VWAP Retest)
        public string MonthlyPeriodKey { get; set; }
        public double MonthlyVwapAtSetup { get; set; }
        public double MonthlySd1UpperAtSetup { get; set; }
        public double MonthlySd1LowerAtSetup { get; set; }
        public double MonthlyVwapSlopeAtSetup { get; set; }
        public double MonthlyVwapSlopeTicksPerHourAtSetup { get; set; }
        public double MonthlyVwapSlopeAtrNormalizedAtSetup { get; set; }
        public string MonthlyBandEpochIdAtSetup { get; set; }
        public int MonthlyBandAcceptanceBarsAtSetup { get; set; }
        public double RetestDistanceTicks { get; set; }

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
        SwingWeightedScore ComputeScore(SwingContext ctx, SwingSetupType setup, SwingDirection dir, double riskRewardRatio);
        SwingTier ResolveTier(double totalScore, double thresholdMoyen, double thresholdFort, double thresholdTresFort);
        void ComputeQualityMetrics(
            SwingContext ctx,
            SwingSetupType setup,
            SwingDirection dir,
            double baseScore,
            out double timingQuality,
            out double regimeCompatibility,
            out double directionalQuality,
            out double locationQuality,
            out double lateEntryPenalty,
            out double conflictPenalty,
            out double finalQualityScore);
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
                rejectionReason = SwingRejectionReason.ContextNull;
                return false;
            }

            if (ctx.InNewsWindow && ctx.NewsSeverity >= 2)
            {
                rejectionReason = "HIGH_SEVERITY_NEWS_BLOCK";
                return false;
            }

            if (!ctx.IsAtrValid || ctx.AtrCurrent <= 0)
            {
                rejectionReason = SwingRejectionReason.InvalidAtrData;
                return false;
            }

            if (!ctx.IsPointValueValid || ctx.PointValue <= 0)
            {
                rejectionReason = SwingRejectionReason.InvalidPointValue;
                return false;
            }

            bool isLong = dir == SwingDirection.Long;

            switch (setup)
            {
                case SwingSetupType.RejectExtreme:
                    if (isLong)
                    {
                        // Anti-couteau tombant : Pas d'achat aux extrêmes si tendance HTF baissière
                        if (ctx.HtfTrendDirection < 0)
                        { rejectionReason = "REJECT_EXTREME_COUNTER_HTF_BEARISH"; return false; }

                        bool testedExtreme = (ctx.Sd2Lower > 0 && ctx.Low <= ctx.Sd2Lower) || (ctx.DailyVal > 0 && ctx.Low <= ctx.DailyVal);
                        bool candleRejection = ctx.Close > ctx.Open && ctx.Close > ctx.Low;
                        if (!testedExtreme || !candleRejection) { rejectionReason = "NO_EXTREME_TEST_LONG"; return false; }

                        // Confirmation Order Flow : pas d'achat si delta vendeur strict sans divergence ni absorption
                        if (ctx.BarDelta < 0 && !ctx.HasDeltaDivergence && !ctx.HasAbsorptionEvidence)
                        { rejectionReason = "REJECT_EXTREME_OPPOSING_DELTA"; return false; }
                    }
                    else
                    {
                        // Pas de vente aux extrêmes si tendance HTF haussière
                        if (ctx.HtfTrendDirection > 0)
                        { rejectionReason = "REJECT_EXTREME_COUNTER_HTF_BULLISH"; return false; }

                        bool testedExtreme = (ctx.Sd2Upper > 0 && ctx.High >= ctx.Sd2Upper) || (ctx.DailyVah > 0 && ctx.High >= ctx.DailyVah);
                        bool candleRejection = ctx.Close < ctx.Open && ctx.Close < ctx.High;
                        if (!testedExtreme || !candleRejection) { rejectionReason = "NO_EXTREME_TEST_SHORT"; return false; }

                        // Confirmation Order Flow : pas de vente si delta acheteur strict sans divergence ni absorption
                        if (ctx.BarDelta > 0 && !ctx.HasDeltaDivergence && !ctx.HasAbsorptionEvidence)
                        { rejectionReason = "REJECT_EXTREME_OPPOSING_DELTA"; return false; }
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
                    if (isLong)
                    {
                        if (ctx.Close <= ctx.Open)
                        { rejectionReason = "NO_MACRO_REVERSAL_LONG"; return false; }
                        if (!ctx.HasDeltaDivergence && !ctx.HasAbsorptionEvidence)
                        { rejectionReason = SwingRejectionReason.MacroReversalNoOrderFlow; return false; }
                    }
                    else
                    {
                        if (ctx.Close >= ctx.Open)
                        { rejectionReason = "NO_MACRO_REVERSAL_SHORT"; return false; }
                        if (!ctx.HasDeltaDivergence && !ctx.HasAbsorptionEvidence)
                        { rejectionReason = SwingRejectionReason.MacroReversalNoOrderFlow; return false; }
                    }
                    break;

                case SwingSetupType.HtfContinuation:
                    if (isLong)
                    {
                        if (ctx.HtfTrendDirection <= 0 || ctx.Close <= ctx.Open)
                        { rejectionReason = "NO_HTF_CONTINUATION_LONG"; return false; }

                        // Pullback à la valeur obligatoire (anti-chase) : test requis d'une zone institutionnelle
                        bool hasValuePullback = ctx.InFairValueGap
                            || ctx.NearDailyPoc || ctx.NearWeeklyPoc
                            || ctx.NearDailyVah || ctx.NearDailyVal
                            || ctx.InsideHvn
                            || (ctx.DailyPoc > 0 && Math.Abs(ctx.Low - ctx.DailyPoc) <= (ctx.AtrCurrent * 1.5))
                            || (ctx.ClosedVwap > 0 && Math.Abs(ctx.Low - ctx.ClosedVwap) <= (ctx.AtrCurrent * 1.5))
                            || (ctx.SessionVwap > 0 && Math.Abs(ctx.Low - ctx.SessionVwap) <= (ctx.AtrCurrent * 1.5))
                            || (ctx.CurrentMonthlyVwap > 0 && Math.Abs(ctx.Low - ctx.CurrentMonthlyVwap) <= (ctx.AtrCurrent * 1.5))
                            || (ctx.DailyVal > 0 && ctx.Low <= ctx.DailyVah && ctx.Low >= ctx.DailyVal);

                        if (!hasValuePullback)
                        { rejectionReason = SwingRejectionReason.NoPullbackToValue; return false; }
                    }
                    else
                    {
                        if (ctx.HtfTrendDirection >= 0 || ctx.Close >= ctx.Open)
                        { rejectionReason = "NO_HTF_CONTINUATION_SHORT"; return false; }

                        // Pullback à la valeur obligatoire (anti-chase) : test requis d'une zone institutionnelle
                        bool hasValuePullback = ctx.InFairValueGap
                            || ctx.NearDailyPoc || ctx.NearWeeklyPoc
                            || ctx.NearDailyVah || ctx.NearDailyVal
                            || ctx.InsideHvn
                            || (ctx.DailyPoc > 0 && Math.Abs(ctx.High - ctx.DailyPoc) <= (ctx.AtrCurrent * 1.5))
                            || (ctx.ClosedVwap > 0 && Math.Abs(ctx.High - ctx.ClosedVwap) <= (ctx.AtrCurrent * 1.5))
                            || (ctx.SessionVwap > 0 && Math.Abs(ctx.High - ctx.SessionVwap) <= (ctx.AtrCurrent * 1.5))
                            || (ctx.CurrentMonthlyVwap > 0 && Math.Abs(ctx.High - ctx.CurrentMonthlyVwap) <= (ctx.AtrCurrent * 1.5))
                            || (ctx.DailyVah > 0 && ctx.High >= ctx.DailyVal && ctx.High <= ctx.DailyVah);

                        if (!hasValuePullback)
                        { rejectionReason = SwingRejectionReason.NoPullbackToValue; return false; }
                    }
                    break;

                case SwingSetupType.PocMigration:
                    if (!ctx.HasPocMigration || ctx.PocMigrationSessions < 3 || ctx.PocMigrationTransitions < 2)
                    { rejectionReason = "NO_POC_MIGRATION_DETECTED"; return false; }
                    if (ctx.PocMigrationDirection != dir)
                    { rejectionReason = "POC_MIGRATION_DIRECTION_MISMATCH"; return false; }
                    
                    // Entrée sur pullback obligatoire (anti-chase) & Stop structurel OldestPoc
                    if (isLong)
                    {
                        if (ctx.DailyVah > 0 && ctx.Close > ctx.DailyVah)
                        { rejectionReason = "POC_MIGRATION_LONG_ABOVE_VAH"; return false; }
                        if (ctx.PocMigrationOldestPoc >= ctx.Close && ctx.PocMigrationOldestPoc > 0)
                        { rejectionReason = "POC_MIGRATION_INVALID_STRUCTURAL_STOP"; return false; }
                    }
                    else
                    {
                        if (ctx.DailyVal > 0 && ctx.Close < ctx.DailyVal)
                        { rejectionReason = "POC_MIGRATION_SHORT_BELOW_VAL"; return false; }
                        if (ctx.PocMigrationOldestPoc <= ctx.Close && ctx.PocMigrationOldestPoc > 0)
                        { rejectionReason = "POC_MIGRATION_INVALID_STRUCTURAL_STOP"; return false; }
                    }
                    break;

                case SwingSetupType.MonthlyVwapBandRetest:
                    if (!ctx.HasCurrentMonthlyVwap || ctx.CurrentMonthlyVwap <= 0 || ctx.CurrentMonthlySd1Upper <= 0 || ctx.CurrentMonthlySd1Lower <= 0)
                    { rejectionReason = "NO_CURRENT_MONTHLY_VWAP_DATA"; return false; }

                    if (ctx.CurrentMonthlyBarsCount < 20)
                    { rejectionReason = "MONTHLY_VWAP_EARLY_MONTH_UNSTABLE"; return false; }

                    if (ctx.RetestCountCurrentLevel > 2)
                    { rejectionReason = "MONTHLY_RETEST_LIMIT_REACHED"; return false; }

                    // Tolérance adaptative ATR (min(8 ticks, ATR * 0.30))
                    double tolTicks = Math.Min(8.0, (ctx.AtrCurrent / Math.Max(0.01, ctx.TickSize)) * 0.30);
                    double tolPrice = tolTicks * ctx.TickSize;

                    // Contrôle d'acceptation multi-barres paramétrable (défaut : 1 barre minimum)
                    int requiredAcceptance = ctx.MonthlyBandMinAcceptanceBarsRequired > 0 ? ctx.MonthlyBandMinAcceptanceBarsRequired : 1;

                    if (isLong)
                    {
                        // Long : Tendance HTF haussière requise
                        if (ctx.HtfTrendDirection <= 0)
                        { rejectionReason = "HTF_TREND_NOT_BULLISH"; return false; }

                        // Prix au-dessus du VWAP Monthly
                        if (ctx.Close <= ctx.CurrentMonthlyVwap)
                        { rejectionReason = "PRICE_BELOW_MONTHLY_VWAP"; return false; }

                        // Pente du VWAP haussière (Vérification normalisée Ticks/h ou brute)
                        double minSlopePerHour = ctx.MonthlyBandMinSlopeTicksPerHourConfig > 0 ? ctx.MonthlyBandMinSlopeTicksPerHourConfig : 2.0;
                        bool slopeValid = false;
                        if (ctx.CurrentMonthlyVwapSlopeTicksPerHour != 0.0)
                            slopeValid = ctx.CurrentMonthlyVwapSlopeTicksPerHour >= minSlopePerHour;
                        else
                            slopeValid = ctx.CurrentMonthlyVwapSlope >= 0.5;

                        if (!slopeValid)
                        { rejectionReason = "MONTHLY_VWAP_SLOPE_INSUFFICIENT"; return false; }

                        // Contrôle de pente ATR si configuré
                        if (ctx.MonthlyBandMinSlopeAtrNormalizedConfig > 0 && ctx.CurrentMonthlyVwapSlopeAtrNormalized < ctx.MonthlyBandMinSlopeAtrNormalizedConfig)
                        { rejectionReason = "MONTHLY_VWAP_SLOPE_INSUFFICIENT"; return false; }

                        // Acceptation préalable au-dessus de SD+1 (Multi-barres ou 1 barre précédente)
                        if (ctx.MonthlyBandAcceptanceBars > 0)
                        {
                            if (ctx.MonthlyBandAcceptanceBars < requiredAcceptance)
                            { rejectionReason = "MONTHLY_BAND_ACCEPTANCE_INSUFFICIENT"; return false; }
                        }
                        else
                        {
                            double prevSd1 = ctx.PrevCurrentMonthlySd1Upper > 0 ? ctx.PrevCurrentMonthlySd1Upper : ctx.CurrentMonthlySd1Upper;
                            if (ctx.PrevClose <= prevSd1 && ctx.Open <= ctx.CurrentMonthlySd1Upper)
                            { rejectionReason = "NO_PRIOR_ACCEPTANCE_ABOVE_SD1"; return false; }
                        }

                        // Retest de SD+1 : Low touche ou pénètre dans la tolérance
                        if (ctx.Low > ctx.CurrentMonthlySd1Upper + tolPrice)
                        { rejectionReason = "NO_SD1_RETEST_TOUCH"; return false; }

                        // Clôture confirmée au-dessus de SD+1
                        if (ctx.Close <= ctx.CurrentMonthlySd1Upper)
                        { rejectionReason = "CLOSE_BELOW_SD1"; return false; }

                        // Bougie de confirmation haussière
                        if (ctx.Close <= ctx.Open)
                        { rejectionReason = "BEARISH_CONFIRMATION_CANDLE"; return false; }
                    }
                    else
                    {
                        // Short : Tendance HTF baissière requise
                        if (ctx.HtfTrendDirection >= 0)
                        { rejectionReason = "HTF_TREND_NOT_BEARISH"; return false; }

                        // Prix sous le VWAP Monthly
                        if (ctx.Close >= ctx.CurrentMonthlyVwap)
                        { rejectionReason = "PRICE_ABOVE_MONTHLY_VWAP"; return false; }

                        // Pente du VWAP baissière (Vérification normalisée Ticks/h ou brute)
                        double minSlopePerHour = ctx.MonthlyBandMinSlopeTicksPerHourConfig > 0 ? ctx.MonthlyBandMinSlopeTicksPerHourConfig : 2.0;
                        bool slopeValid = false;
                        if (ctx.CurrentMonthlyVwapSlopeTicksPerHour != 0.0)
                            slopeValid = ctx.CurrentMonthlyVwapSlopeTicksPerHour <= -minSlopePerHour;
                        else
                            slopeValid = ctx.CurrentMonthlyVwapSlope <= -0.5;

                        if (!slopeValid)
                        { rejectionReason = "MONTHLY_VWAP_SLOPE_INSUFFICIENT"; return false; }

                        // Contrôle de pente ATR si configuré
                        if (ctx.MonthlyBandMinSlopeAtrNormalizedConfig > 0 && ctx.CurrentMonthlyVwapSlopeAtrNormalized < ctx.MonthlyBandMinSlopeAtrNormalizedConfig)
                        { rejectionReason = "MONTHLY_VWAP_SLOPE_INSUFFICIENT"; return false; }

                        // Acceptation préalable sous SD-1 (Multi-barres ou 1 barre précédente)
                        if (ctx.MonthlyBandAcceptanceBars > 0)
                        {
                            if (ctx.MonthlyBandAcceptanceBars < requiredAcceptance)
                            { rejectionReason = "MONTHLY_BAND_ACCEPTANCE_INSUFFICIENT"; return false; }
                        }
                        else
                        {
                            double prevSd1 = ctx.PrevCurrentMonthlySd1Lower > 0 ? ctx.PrevCurrentMonthlySd1Lower : ctx.CurrentMonthlySd1Lower;
                            if (ctx.PrevClose >= prevSd1 && ctx.Open >= ctx.CurrentMonthlySd1Lower)
                            { rejectionReason = "NO_PRIOR_ACCEPTANCE_BELOW_SD1"; return false; }
                        }

                        // Retest de SD-1 : High touche ou pénètre dans la tolérance
                        if (ctx.High < ctx.CurrentMonthlySd1Lower - tolPrice)
                        { rejectionReason = "NO_SD1_RETEST_TOUCH"; return false; }

                        // Clôture confirmée sous SD-1
                        if (ctx.Close >= ctx.CurrentMonthlySd1Lower)
                        { rejectionReason = "CLOSE_ABOVE_SD1"; return false; }

                        // Bougie de confirmation baissière
                        if (ctx.Close >= ctx.Open)
                        { rejectionReason = "BULLISH_CONFIRMATION_CANDLE"; return false; }
                    }
                    break;
            }

            return true;
        }

        public SwingWeightedScore ComputeScore(SwingContext ctx, SwingSetupType setup, SwingDirection dir)
        {
            return ComputeScore(ctx, setup, dir, 0.0);
        }

        public SwingWeightedScore ComputeScore(SwingContext ctx, SwingSetupType setup, SwingDirection dir, double riskRewardRatio)
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
            else if (setup == SwingSetupType.PocMigration)
            {
                // Prix en pullback dans la VA migrée = entrée optimale
                bool nearPoc = ctx.DailyPoc > 0 && Math.Abs(ctx.Close - ctx.DailyPoc) <= ctx.AtrCurrent * 0.5;
                bool insideVa = ctx.DailyVal > 0 && ctx.DailyVah > 0 && ctx.Close >= ctx.DailyVal && ctx.Close <= ctx.DailyVah;
                if (nearPoc) s.AmtLocationScore = 25.0;
                else if (insideVa) s.AmtLocationScore = 22.0;
                else s.AmtLocationScore = 15.0;
            }
            else if (setup == SwingSetupType.MonthlyVwapBandRetest)
            {
                // Retest de SD1 : précis = 25 pts, pénétration légère = 22 pts
                double bandLevel = isLong ? ctx.CurrentMonthlySd1Upper : ctx.CurrentMonthlySd1Lower;
                double testDist = Math.Abs((isLong ? ctx.Low : ctx.High) - bandLevel);
                if (testDist <= ctx.TickSize * 2) s.AmtLocationScore = 25.0;
                else if (testDist <= ctx.TickSize * 6) s.AmtLocationScore = 22.0;
                else s.AmtLocationScore = 18.0;
            }
            else
            {
                s.AmtLocationScore = 18.0;
            }

            // 3. Volume Profile Score (0..20)
            double vpScore = 0.0;
            if (setup == SwingSetupType.PocMigration)
            {
                // Score basé sur la force de migration du POC
                vpScore = Math.Min(20.0, ctx.PocMigrationStrength * 0.2);
            }
            else if (setup == SwingSetupType.MonthlyVwapBandRetest)
            {
                // Score basé sur la magnitude de la pente du VWAP (0..20 pts)
                double absSlope = Math.Abs(ctx.CurrentMonthlyVwapSlope);
                if (absSlope >= 2.0) vpScore = 20.0;
                else if (absSlope >= 1.0) vpScore = 16.0;
                else vpScore = 12.0;
            }
            else
            {
                if (ctx.NearWeeklyPoc || ctx.NearDailyPoc) vpScore += 8.0;
                if (ctx.NearDailyVah || ctx.NearDailyVal) vpScore += 6.0;
                if (ctx.InsideHvn) vpScore += 6.0;
            }
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

            // 6. Risk / Reward Score (0..10) — basé sur le RR réel post-sizing si disponible
            s.RiskRewardScore = MapRiskRewardScore(riskRewardRatio);

            // 7. Pénalités
            if (ctx.InNewsWindow) s.Penalties += 15.0;
            if (ctx.GapPercent > 1.0) s.Penalties += 10.0;

            s.Detail = string.Format(CultureInfo.InvariantCulture,
                "HTF={0:F0} AMT={1:F0} VP={2:F0} SMC={3:F0} OF={4:F0} RR={5:F0} Pen={6:F0}",
                s.HtfContextScore, s.AmtLocationScore, s.VolumeProfileScore, s.StructureSmcScore, s.OrderFlowScore, s.RiskRewardScore, s.Penalties);

            return s;
        }

        /// <summary>RR 1.0→5 pts, 2.0→8 pts, ≥3.0→10 pts ; RR inconnu → score neutre 4.</summary>
        internal static double MapRiskRewardScore(double riskRewardRatio)
        {
            if (riskRewardRatio <= 0) return 4.0;
            if (riskRewardRatio >= 3.0) return 10.0;
            if (riskRewardRatio >= 2.0) return 8.0;
            if (riskRewardRatio >= 1.0) return 5.0;
            return Math.Max(1.0, riskRewardRatio * 5.0);
        }

        public SwingTier ResolveTier(double totalScore, double thresholdMoyen, double thresholdFort, double thresholdTresFort)
        {
            if (totalScore >= thresholdTresFort) return SwingTier.TresFort;
            if (totalScore >= thresholdFort) return SwingTier.Fort;
            if (totalScore >= thresholdMoyen) return SwingTier.Moyen;
            return SwingTier.Aucun;
        }

        public void ComputeQualityMetrics(
            SwingContext ctx,
            SwingSetupType setup,
            SwingDirection dir,
            double baseScore,
            out double timingQuality,
            out double regimeCompatibility,
            out double directionalQuality,
            out double locationQuality,
            out double lateEntryPenalty,
            out double conflictPenalty,
            out double finalQualityScore)
        {
            timingQuality = 5.0;
            regimeCompatibility = 5.0;
            directionalQuality = 5.0;
            locationQuality = 5.0;
            lateEntryPenalty = 0.0;
            conflictPenalty = 0.0;

            if (ctx == null)
            {
                finalQualityScore = baseScore;
                return;
            }

            bool isLong = dir == SwingDirection.Long;

            // 1. Timing Quality (0..10) : Qualité d'exécution micro-structurelle
            double tq = 5.0;
            if ((isLong && ctx.BarDelta > 0) || (!isLong && ctx.BarDelta < 0)) tq += 2.5;
            if (ctx.HasDeltaDivergence) tq += 2.5;
            if (ctx.HasAbsorptionEvidence) tq += 2.0;
            timingQuality = Math.Max(0.0, Math.Min(10.0, tq));

            // 2. Regime Compatibility (0..10)
            double rc = 5.0;
            bool trendMatchesDir = (isLong && ctx.RegimeHtf == SwingMarketRegime.TrendUp) || (!isLong && ctx.RegimeHtf == SwingMarketRegime.TrendDown);
            if (trendMatchesDir)
            {
                if (setup == SwingSetupType.HtfContinuation || setup == SwingSetupType.BreakoutRetest || setup == SwingSetupType.MonthlyVwapBandRetest)
                    rc += 5.0;
                else if (setup == SwingSetupType.MacroReversal)
                    rc -= 2.0;
            }
            else if (ctx.RegimeHtf == SwingMarketRegime.Balance)
            {
                if (setup == SwingSetupType.ValueReentry || setup == SwingSetupType.RejectExtreme)
                    rc += 5.0;
                else if (setup == SwingSetupType.BreakoutRetest)
                    rc -= 2.0;
            }
            regimeCompatibility = Math.Max(0.0, Math.Min(10.0, rc));

            // 3. Directional Quality (0..10) : Alignement HTF
            if ((isLong && ctx.HtfTrendDirection > 0) || (!isLong && ctx.HtfTrendDirection < 0))
                directionalQuality = 10.0;
            else if (ctx.HtfTrendDirection == 0)
                directionalQuality = 5.0;
            else
                directionalQuality = 1.0;

            // 4. Location Quality (0..10) : Proximité avec niveaux institutionnels
            double lq = 4.0;
            if (ctx.NearDailyPoc || ctx.NearWeeklyPoc || (ctx.SessionVwap > 0 && Math.Abs(ctx.Close - ctx.SessionVwap) <= ctx.AtrCurrent * 0.75))
                lq += 3.0;
            if (ctx.NearDailyVah || ctx.NearDailyVal || ctx.InFairValueGap)
                lq += 3.0;
            locationQuality = Math.Max(0.0, Math.Min(10.0, lq));

            // 5. Late Entry Penalty (0..15) : Pénalise les entrées chassant un marché étendu
            double meanPrice = ctx.SessionVwap > 0 ? ctx.SessionVwap : (ctx.ClosedVwap > 0 ? ctx.ClosedVwap : ctx.DailyPoc);
            if (meanPrice > 0 && ctx.AtrCurrent > 0)
            {
                double distAtr = Math.Abs(ctx.Close - meanPrice) / ctx.AtrCurrent;
                if (distAtr >= 2.5)
                    lateEntryPenalty = 15.0;
                else if (distAtr >= 1.8)
                    lateEntryPenalty = 10.0;
                else if (distAtr >= 1.2)
                    lateEntryPenalty = 5.0;
            }

            // 6. Conflict Penalty (0..15) : Pénalise les conflits majeurs d'Order Flow ou News
            if (ctx.InNewsWindow) conflictPenalty += 10.0;
            if ((isLong && ctx.BarDelta < 0 && !ctx.HasDeltaDivergence && !ctx.HasAbsorptionEvidence) ||
                (!isLong && ctx.BarDelta > 0 && !ctx.HasDeltaDivergence && !ctx.HasAbsorptionEvidence))
            {
                conflictPenalty += 5.0;
            }
            conflictPenalty = Math.Min(15.0, conflictPenalty);

            // Calcul du FinalQualityScore
            double rawFinal = baseScore + timingQuality + regimeCompatibility + directionalQuality + locationQuality - lateEntryPenalty - conflictPenalty;
            if (double.IsNaN(rawFinal) || double.IsInfinity(rawFinal))
                finalQualityScore = 0.0;
            else
                finalQualityScore = Math.Max(0.0, Math.Min(150.0, rawFinal));
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
            if (keyOpposingLevel > 0 && riskDist > 0)
            {
                double distToWall = Math.Abs(keyOpposingLevel - entryPrice);
                double min1R = riskDist * 1.0;
                // Si un mur institutionnel opposé est détecté entre 1.0R et TP1, on cale TP1 dessus pour maximiser le win rate
                if (distToWall >= min1R && distToWall < target1Dist)
                {
                    if (isLong && keyOpposingLevel > entryPrice)
                        tp1 = keyOpposingLevel;
                    else if (!isLong && keyOpposingLevel < entryPrice)
                        tp1 = keyOpposingLevel;
                }
                else if (isLong && keyOpposingLevel > entryPrice && keyOpposingLevel < tp2)
                {
                    tp2 = keyOpposingLevel;
                }
                else if (!isLong && keyOpposingLevel < entryPrice && keyOpposingLevel > tp2)
                {
                    tp2 = keyOpposingLevel;
                }
            }
        }
    }

    /// <summary>
    /// Suivi individuel d'un trade Swing virtuel dans le journal Shadow et la base SQLite.
    /// Gère la machine d'états à sorties partielles (TP1 partiel, Stop Break-Even, TP2 clôture finale).
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
        
        public int InitialContracts { get; set; }
        public int RemainingContracts { get; set; }
        public int PositionSizeContracts { get { return RemainingContracts; } set { RemainingContracts = value; } }

        public DateTime EntryTimeUtc { get; set; }
        public DateTime ExitTimeUtc { get; set; }
        public double ExitPrice { get; set; }
        public bool Closed { get; set; }
        public bool Tp1Hit { get; set; }
        public string ExitReason { get; set; }
        
        public double PartialExitPrice { get; set; }
        public DateTime PartialExitTimeUtc { get; set; }
        public int PartialExitContracts { get; set; }
        public double PartialRealizedPnlCurrency { get; set; }
        public double PartialRealizedR { get; set; }

        public double RealizedR { get; set; }
        public double RealizedPnlCurrency { get; set; }
        public int BarsElapsed { get; set; }
        public int ConsecutiveAdverseBars { get; set; }
        public string ExecutionNotes { get; set; }

        public double StructuralStopPrice
        {
            get { return Signal != null ? Signal.StructuralStopPrice : InitialStopPrice; }
        }

        public SwingSetupType SetupType
        {
            get { return Signal != null ? Signal.SetupType : SwingSetupType.RejectExtreme; }
        }

        private double dynamicStructuralPrice;
        public double DynamicStructuralPrice
        {
            get { return dynamicStructuralPrice > 0 ? dynamicStructuralPrice : StructuralStopPrice; }
            set { dynamicStructuralPrice = value; }
        }

        public void UpdateDynamicStructure(double newLevel)
        {
            if (newLevel <= 0) return;
            if (IsLong)
            {
                if (newLevel > DynamicStructuralPrice)
                    DynamicStructuralPrice = newLevel;
            }
            else
            {
                if (newLevel < DynamicStructuralPrice)
                    DynamicStructuralPrice = newLevel;
            }
        }

        public TrackedSwingTrade()
        {
            TradeId = Guid.NewGuid().ToString("N").Substring(0, 12);
            Closed = false;
            Tp1Hit = false;
            ExitReason = "ACTIVE";
            EntryTimeUtc = DateTime.UtcNow;
            ExecutionNotes = string.Empty;
            ConsecutiveAdverseBars = 0;
            dynamicStructuralPrice = 0.0;
        }

        public TrackedSwingTrade(SwingSignal sig, double tickSize, double pointValue)
        {
            TradeId = Guid.NewGuid().ToString("N").Substring(0, 12);
            Signal = sig;
            IsLong = sig != null && sig.Direction == SwingDirection.Long;
            EntryPrice = sig != null ? sig.EntryPrice : 0.0;
            InitialStopPrice = sig != null ? sig.InitialStopPrice : 0.0;
            CurrentStopPrice = sig != null ? sig.InitialStopPrice : 0.0;
            Target1Price = sig != null ? sig.Target1Price : 0.0;
            Target2Price = sig != null ? sig.Target2Price : 0.0;
            InitialContracts = sig != null ? Math.Max(1, sig.PositionSizeContracts) : 1;
            RemainingContracts = InitialContracts;
            EntryTimeUtc = sig != null ? sig.GeneratedTimeUtc : DateTime.UtcNow;
            Closed = false;
            Tp1Hit = false;
            ExitReason = "ACTIVE";
            RealizedR = 0.0;
            RealizedPnlCurrency = 0.0;
            BarsElapsed = 0;
            ConsecutiveAdverseBars = 0;
            ExecutionNotes = sig != null ? sig.ExecutionNotes : string.Empty;
            dynamicStructuralPrice = sig != null ? sig.StructuralStopPrice : 0.0;
        }

        /// <summary>
        /// Surcharge de compatibilité pour l'évaluation de régime Swing V2.
        /// </summary>
        public SwingRegimeDecision EvaluateRegimeDecision(
            SwingMarketRegime currentRegime,
            double close,
            double htfEma,
            double atrDaily,
            int confirmationBarsRequired,
            bool enableSoftProtection)
        {
            return EvaluateRegimeDecision(
                currentRegime,
                close,
                DynamicStructuralPrice,
                false,
                atrDaily,
                confirmationBarsRequired,
                enableSoftProtection);
        }

        /// <summary>
        /// Évalue la santé du régime et détermine si une action de protection ou d'invalidation structurelle dynamique est requise (Architecture V2).
        /// Principe : Régime = Contexte, Structure = Validation, Risk Management = Protection.
        /// </summary>
        public SwingRegimeDecision EvaluateRegimeDecision(
            SwingMarketRegime currentRegime,
            double close,
            double dynamicStructurePrice,
            bool hasOpposingChoch,
            double atrDaily,
            int confirmationBarsRequired,
            bool enableSoftProtection)
        {
            if (Closed) return SwingRegimeDecision.Hold;

            double structLevel = dynamicStructurePrice > 0 ? dynamicStructurePrice : DynamicStructuralPrice;
            double tol = atrDaily > 0 ? atrDaily * 0.05 : 0.0;
            bool isStructureBreached = structLevel > 0
                ? (IsLong ? (close < structLevel - tol) : (close > structLevel + tol))
                : false;

            bool isStructureInvalidated = isStructureBreached || hasOpposingChoch;

            // 1. RÈGLE CRITIQUE POUR MACROREVERSAL (Résolution Bloquant 1) :
            // Un setup de mean-reversion entre volontairement contre l'EMA HTF précédente.
            // L'opposition avec l'EMA HTF est IGNORE (ne compte pas comme détérioration tant que l'ancrage tient).
            // En revanche, si la structure d'ancrage est brisée (isStructureInvalidated), la tentative de retournement
            // a échoué et passe en état Deteriorated pour permettre la confirmation et la sortie en StructuralExit !
            bool isMacroReversal = SetupType == SwingSetupType.MacroReversal;

            SwingRegimeHealth health = SwingRegimeHealth.Neutral;
            if (isMacroReversal)
            {
                if (isStructureInvalidated)
                    health = SwingRegimeHealth.Deteriorated;
                else
                    health = SwingRegimeHealth.Neutral;
            }
            else
            {
                if (IsLong)
                {
                    if (isStructureInvalidated || currentRegime == SwingMarketRegime.TrendDown)
                        health = SwingRegimeHealth.Deteriorated;
                    else if (currentRegime == SwingMarketRegime.TrendUp || currentRegime == SwingMarketRegime.Expansion)
                        health = SwingRegimeHealth.Aligned;
                    else
                        health = SwingRegimeHealth.Neutral;
                }
                else
                {
                    if (isStructureInvalidated || currentRegime == SwingMarketRegime.TrendUp)
                        health = SwingRegimeHealth.Deteriorated;
                    else if (currentRegime == SwingMarketRegime.TrendDown || currentRegime == SwingMarketRegime.Compression)
                        health = SwingRegimeHealth.Aligned;
                    else
                        health = SwingRegimeHealth.Neutral;
                }
            }

            // 2. Suivi de la persistance (Hystérésis)
            if (health == SwingRegimeHealth.Deteriorated)
            {
                ConsecutiveAdverseBars++;
            }
            else
            {
                if (ConsecutiveAdverseBars > 0)
                    ConsecutiveAdverseBars = Math.Max(0, ConsecutiveAdverseBars - 1);
            }

            // 3. Test de confirmation temporelle (multibarres)
            int minBars = Math.Max(1, confirmationBarsRequired);
            bool isDeteriorationConfirmed = ConsecutiveAdverseBars >= minBars;

            // 4. Prise de décision
            // Cas A : Invalidation structurelle confirmée sous régime adverse persistant -> EXIT
            if (isDeteriorationConfirmed && isStructureInvalidated)
            {
                return SwingRegimeDecision.StructuralExit;
            }

            // Cas B : Détérioration persistante MAIS structure intacte -> Protection Break-Even (si en profit)
            if (isDeteriorationConfirmed && enableSoftProtection && !isStructureInvalidated)
            {
                bool inProfit = IsLong ? (close > EntryPrice) : (close < EntryPrice);
                if (inProfit || Tp1Hit)
                {
                    return SwingRegimeDecision.ProtectBreakeven;
                }
            }

            // Cas C : Par défaut, le trade reste géré par son Stop Loss et ses cibles naturelles
            return SwingRegimeDecision.Hold;
        }

        /// <summary>
        /// Exécute la sortie partielle à TP1 (généralement 50% de la position) et trail le stop à Break-Even (+ 1 tick).
        /// </summary>
        public void ExecutePartialExitTp1(double exitPrice, DateTime exitTimeUtc, double tickSize, double pointValue)
        {
            if (Tp1Hit || Closed) return;

            Tp1Hit = true;
            PartialExitPrice = exitPrice;
            PartialExitTimeUtc = exitTimeUtc;

            // Débouclage de la moitié (arrondi vers le bas si impair, min 1)
            PartialExitContracts = InitialContracts > 1 ? (InitialContracts / 2) : 1;
            RemainingContracts = Math.Max(0, InitialContracts - PartialExitContracts);

            double tickVal = pointValue * tickSize;
            double stopDist = Math.Abs(EntryPrice - InitialStopPrice);
            double profitDist = IsLong ? (exitPrice - EntryPrice) : (EntryPrice - exitPrice);

            PartialRealizedR = stopDist > 0 ? (profitDist / stopDist) : 1.5;
            double exitDistTicks = profitDist / tickSize;
            PartialRealizedPnlCurrency = exitDistTicks * tickVal * PartialExitContracts;

            // Déplacement du Stop à Break-Even (+ 1 tick dans le sens du gain)
            CurrentStopPrice = EntryPrice + (IsLong ? tickSize : -tickSize);
            UpdateDynamicStructure(CurrentStopPrice);

            // Si tous les contrats ont été soldés à TP1 (ex: 1 seul contrat initial)
            if (RemainingContracts <= 0)
            {
                Closed = true;
                ExitPrice = exitPrice;
                ExitTimeUtc = exitTimeUtc;
                ExitReason = "TAKE_PROFIT_1_FULL";
                RealizedR = PartialRealizedR;
                RealizedPnlCurrency = PartialRealizedPnlCurrency;
            }
        }

        /// <summary>
        /// Clôture finale complète des contrats restants (TP2, Break-Even Stop ou Stop Loss initial).
        /// </summary>
        public void CloseTrade(double exitPrice, DateTime exitTimeUtc, string reason, double tickSize, double pointValue)
        {
            if (Closed) return;

            Closed = true;
            ExitPrice = exitPrice;
            ExitTimeUtc = exitTimeUtc;
            ExitReason = reason;

            double tickVal = pointValue * tickSize;
            double stopDist = Math.Abs(EntryPrice - InitialStopPrice);
            int closingContracts = RemainingContracts > 0 ? RemainingContracts : InitialContracts;

            double finalPnl = 0.0;
            double finalR = 0.0;

            if (reason == "STOP_LOSS")
            {
                if (Tp1Hit)
                {
                    // Sortie au stop après TP1 = Sortie au Break-Even (+1 tick)
                    double beDist = IsLong ? (exitPrice - EntryPrice) : (EntryPrice - exitPrice);
                    double beTicks = beDist / tickSize;
                    finalPnl = beTicks * tickVal * closingContracts;
                    finalR = stopDist > 0 ? (beDist / stopDist) : 0.0;
                    ExitReason = "BREAK_EVEN_STOP";
                }
                else
                {
                    // Perte totale -1R sur la totalité des contrats
                    finalR = -1.0;
                    double lossTicks = Math.Abs(EntryPrice - exitPrice) / tickSize;
                    finalPnl = -(lossTicks * tickVal * closingContracts);
                }
            }
            else if (reason == "TAKE_PROFIT_2")
            {
                double tp2Dist = IsLong ? (exitPrice - EntryPrice) : (EntryPrice - exitPrice);
                finalR = stopDist > 0 ? (tp2Dist / stopDist) : 3.0;
                double tp2Ticks = tp2Dist / tickSize;
                finalPnl = tp2Ticks * tickVal * closingContracts;
            }
            else
            {
                double dist = IsLong ? (exitPrice - EntryPrice) : (EntryPrice - exitPrice);
                finalR = stopDist > 0 ? (dist / stopDist) : 0.0;
                double distTicks = dist / tickSize;
                finalPnl = distTicks * tickVal * closingContracts;
            }

            // PnL & R globaux combinés (Partiel TP1 + Final)
            RealizedPnlCurrency = PartialRealizedPnlCurrency + finalPnl;
            RealizedR = Tp1Hit ? ((PartialRealizedR * PartialExitContracts + finalR * closingContracts) / InitialContracts) : finalR;
            RemainingContracts = 0;
        }
    }

    #endregion

    #region POC Migration Model

    /// <summary>
    /// Résultat complet et auditable de l'analyse de migration du POC sur N sessions historiques.
    /// Objet immuable retourné par PocMigrationAnalyzer.
    /// </summary>
    public sealed class PocMigrationResult
    {
        public SwingDirection Direction { get; set; }
        public int ProfilesCount { get; set; }
        public int ConsecutiveTransitions { get; set; }
        public double TotalPocDriftTicks { get; set; }
        public double AveragePocDriftPerSession { get; set; }
        public double NormalizedDriftAtr { get; set; }
        public double ValueAreaOverlapPercent { get; set; }
        public double VaOverlapAverage { get; set; }
        public double VaOverlapMin { get; set; }
        public double VaOverlapMax { get; set; }
        public int ValidPairsCount { get; set; }
        public bool IsMigrationValid { get; set; }
        public double MigrationStrength { get; set; }
        public double NewestPoc { get; set; }
        public double OldestPoc { get; set; }
        public DateTime NewestProfileTimeUtc { get; set; }
        public DateTime OldestProfileTimeUtc { get; set; }
        public string InvalidationReason { get; set; }

        public PocMigrationResult()
        {
            Direction = SwingDirection.None;
            IsMigrationValid = false;
            InvalidationReason = "INIT";
        }
    }

    /// <summary>
    /// Analyseur déterministe et pur (sans état) de la migration directionnelle du POC.
    /// Recherche la séquence directionnelle valide la plus récente dans la fenêtre de lookback.
    /// </summary>
    public sealed class PocMigrationAnalyzer
    {
        public PocMigrationResult Analyze(
            List<ClosedVolumeProfile> recentProfiles,
            double tickSize,
            double atrDaily,
            int minProfiles = 3,
            int minTransitions = 2,
            double minStrength = 50.0)
        {
            var result = new PocMigrationResult();

            // 1. Validation défensive des entrées
            if (recentProfiles == null || recentProfiles.Count == 0)
            {
                result.InvalidationReason = "PROFILES_NULL_OR_EMPTY";
                return result;
            }

            if (minProfiles < 3) minProfiles = 3;
            if (minTransitions < 2) minTransitions = 2;
            if (minTransitions > minProfiles - 1) minTransitions = minProfiles - 1;

            if (tickSize <= 0.0 || double.IsNaN(tickSize) || double.IsInfinity(tickSize))
                tickSize = 0.25;

            if (atrDaily <= 0.0 || double.IsNaN(atrDaily) || double.IsInfinity(atrDaily))
                atrDaily = tickSize * 40.0;

            // 2. Normalisation et tri des profils (du plus récent au plus ancien)
            var validProfiles = new List<ClosedVolumeProfile>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < recentProfiles.Count; i++)
            {
                var p = recentProfiles[i];
                if (p == null || p.Poc <= 0 || p.Vah <= p.Val || p.Val <= 0 || double.IsNaN(p.Poc) || double.IsInfinity(p.Poc))
                    continue;

                string key = !string.IsNullOrEmpty(p.PeriodKey) ? p.PeriodKey : (p.PeriodEndUtc != DateTime.MinValue ? p.PeriodEndUtc.ToString("o") : i.ToString());
                if (seenKeys.Add(key))
                {
                    validProfiles.Add(p);
                }
            }

            // Tri par date si renseignée, sinon conserve l'ordre de la collection fournie
            bool hasEndDates = false;
            for (int i = 0; i < validProfiles.Count; i++)
            {
                if (validProfiles[i].PeriodEndUtc != DateTime.MinValue)
                {
                    hasEndDates = true;
                    break;
                }
            }
            if (hasEndDates)
            {
                validProfiles.Sort((a, b) => b.PeriodEndUtc.CompareTo(a.PeriodEndUtc));
            }

            if (validProfiles.Count < minProfiles)
            {
                result.InvalidationReason = string.Format(CultureInfo.InvariantCulture, "INSUFFICIENT_PROFILES_{0}_LT_{1}", validProfiles.Count, minProfiles);
                return result;
            }

            // 3. Recherche de la dernière séquence directionnelle valide la plus récente
            PocMigrationResult bestCandidate = null;

            for (int startIndex = 0; startIndex <= validProfiles.Count - minProfiles; startIndex++)
            {
                int consecutiveUp = 0;
                int consecutiveDown = 0;
                double seqDriftTicks = 0.0;
                var pairOverlaps = new List<double>();
                double seqNewestPoc = validProfiles[startIndex].Poc;
                double seqOldestPoc = validProfiles[startIndex].Poc;
                DateTime seqNewestTime = validProfiles[startIndex].PeriodEndUtc;
                DateTime seqOldestTime = validProfiles[startIndex].PeriodEndUtc;

                for (int i = startIndex; i < validProfiles.Count - 1; i++)
                {
                    var newer = validProfiles[i];
                    var older = validProfiles[i + 1];

                    double drift = newer.Poc - older.Poc;
                    double driftTicks = drift / tickSize;

                    // Si drift nul (< 1 tick), rupture de tendance
                    if (Math.Abs(driftTicks) < 1.0)
                        break;

                    if (i == startIndex)
                    {
                        if (driftTicks > 0) consecutiveUp = 1;
                        else consecutiveDown = 1;
                    }
                    else
                    {
                        if (driftTicks > 0 && consecutiveUp > 0) consecutiveUp++;
                        else if (driftTicks < 0 && consecutiveDown > 0) consecutiveDown++;
                        else break; // Rupture directionnelle
                    }

                    seqDriftTicks += driftTicks;
                    seqOldestPoc = older.Poc;
                    seqOldestTime = older.PeriodEndUtc;

                    // Calcul de l'overlap de la paire
                    double overlapLow = Math.Max(newer.Val, older.Val);
                    double overlapHigh = Math.Min(newer.Vah, older.Vah);
                    double overlapRange = Math.Max(0.0, overlapHigh - overlapLow);
                    double maxRange = Math.Max(newer.Vah - newer.Val, older.Vah - older.Val);
                    double overlapPct = maxRange > 0.0 ? (overlapRange / maxRange) * 100.0 : 0.0;
                    pairOverlaps.Add(overlapPct);
                }

                int transitions = Math.Max(consecutiveUp, consecutiveDown);
                if (transitions >= minTransitions)
                {
                    var candidate = new PocMigrationResult
                    {
                        Direction = consecutiveUp > consecutiveDown ? SwingDirection.Long : SwingDirection.Short,
                        ProfilesCount = transitions + 1,
                        ConsecutiveTransitions = transitions,
                        TotalPocDriftTicks = Math.Abs(seqDriftTicks),
                        AveragePocDriftPerSession = Math.Abs(seqDriftTicks) / transitions,
                        NewestPoc = seqNewestPoc,
                        OldestPoc = seqOldestPoc,
                        NewestProfileTimeUtc = seqNewestTime,
                        OldestProfileTimeUtc = seqOldestTime,
                        ValidPairsCount = pairOverlaps.Count
                    };

                    // Calcul des statistiques d'overlap
                    double totalOverlap = 0.0;
                    double minOverlap = 100.0;
                    double maxOverlap = 0.0;

                    for (int o = 0; o < pairOverlaps.Count; o++)
                    {
                        double ov = pairOverlaps[o];
                        totalOverlap += ov;
                        if (ov < minOverlap) minOverlap = ov;
                        if (ov > maxOverlap) maxOverlap = ov;
                    }

                    candidate.VaOverlapAverage = pairOverlaps.Count > 0 ? totalOverlap / pairOverlaps.Count : 0.0;
                    candidate.VaOverlapMin = pairOverlaps.Count > 0 ? minOverlap : 0.0;
                    candidate.VaOverlapMax = pairOverlaps.Count > 0 ? maxOverlap : 0.0;
                    candidate.ValueAreaOverlapPercent = candidate.VaOverlapAverage;

                    // Drift normalisé par rapport à l'ATR Daily
                    double atrDailyTicks = atrDaily / tickSize;
                    candidate.NormalizedDriftAtr = atrDailyTicks > 0.0 ? candidate.TotalPocDriftTicks / atrDailyTicks : 1.0;

                    // 4. Calcul de MigrationStrength (0..100)
                    double strength = 0.0;

                    // a) Consistance de direction de base (30 pts)
                    strength += 30.0;

                    // b) Magnitude normalisée vs ATR (0..25 pts)
                    if (candidate.NormalizedDriftAtr >= 1.0) strength += 25.0;
                    else if (candidate.NormalizedDriftAtr >= 0.5) strength += 18.0;
                    else strength += Math.Max(0.0, candidate.NormalizedDriftAtr * 36.0);

                    // c) Qualité d'overlap Value Area (0..20 pts)
                    if (candidate.VaOverlapAverage >= 30.0 && candidate.VaOverlapAverage <= 80.0)
                    {
                        strength += 20.0;
                        if (candidate.VaOverlapMin < 30.0) strength -= 5.0;
                        if (candidate.VaOverlapMax > 80.0) strength -= 5.0;
                    }
                    else if (candidate.VaOverlapAverage > 80.0)
                    {
                        strength += 10.0;
                    }
                    else
                    {
                        strength += 5.0;
                    }

                    // d) Durée / Nombre de transitions (0..15 pts)
                    if (transitions >= 4) strength += 15.0;
                    else if (transitions == 3) strength += 10.0;
                    else strength += 5.0;

                    // e) Régularité du drift par session (0..10 pts)
                    if (candidate.AveragePocDriftPerSession >= 4.0) strength += 10.0;
                    else if (candidate.AveragePocDriftPerSession >= 2.0) strength += 6.0;
                    else strength += 2.0;

                    candidate.MigrationStrength = Math.Max(0.0, Math.Min(100.0, strength));
                    candidate.IsMigrationValid = candidate.MigrationStrength >= minStrength;

                    if (candidate.IsMigrationValid)
                    {
                        candidate.InvalidationReason = "VALID";
                        bestCandidate = candidate;
                        break;
                    }
                    else if (bestCandidate == null)
                    {
                        candidate.InvalidationReason = string.Format(CultureInfo.InvariantCulture, "STRENGTH_BELOW_THRESHOLD_{0:F1}_LT_{1:F1}", candidate.MigrationStrength, minStrength);
                        bestCandidate = candidate;
                    }
                }
            }

            if (bestCandidate != null)
                return bestCandidate;

            result.InvalidationReason = "NO_VALID_DIRECTIONAL_SEQUENCE";
            return result;
        }
    }

    #endregion

    #region Swing Opportunity Manager & Campaign Engine V3

    /// <summary>
    /// Gestionnaire institutionnel d'opportunités Swing V3.
    /// Empêche le sur-trading, verrouille les campagnes redondantes (SameCampaignLock),
    /// contrôle le cooldown et les plafonds d'exposition par session.
    /// </summary>
    public sealed class SwingOpportunityManager
    {
        public bool Enabled { get; set; }
        public bool SameCampaignLock { get; set; }
        public bool RequireNewStructureForReentry { get; set; }
        public int EntryCooldownBars { get; set; }
        public int MaxEntriesPerSession { get; set; }
        public int MaxLongEntriesPerSession { get; set; }
        public int MaxShortEntriesPerSession { get; set; }

        public SwingCampaign ActiveLongCampaign { get; set; }
        public SwingCampaign ActiveShortCampaign { get; set; }
        public Dictionary<string, int> RecentSignatures { get; private set; }

        public int LastEntryBarLong { get; set; }
        public int LastEntryBarShort { get; set; }
        public int SessionEntryCount { get; set; }
        public int SessionLongCount { get; set; }
        public int SessionShortCount { get; set; }
        public int LastSessionStartBar { get; set; }
        public string LastStructureEvent { get; set; }
        public int LastStructureEventBar { get; set; }

        public SwingOpportunityManager()
        {
            Enabled = true;
            SameCampaignLock = true;
            RequireNewStructureForReentry = true;
            EntryCooldownBars = 12;
            MaxEntriesPerSession = 0;
            MaxLongEntriesPerSession = 0;
            MaxShortEntriesPerSession = 0;

            RecentSignatures = new Dictionary<string, int>(StringComparer.Ordinal);
            LastEntryBarLong = -1;
            LastEntryBarShort = -1;
            LastSessionStartBar = -1;
            LastStructureEvent = string.Empty;
            LastStructureEventBar = -1;
        }

        public void OnNewSession(int sessionStartBar)
        {
            SessionEntryCount = 0;
            SessionLongCount = 0;
            SessionShortCount = 0;
            LastSessionStartBar = sessionStartBar;
        }

        public void RegisterStructureEvent(string structureId, int barIndex)
        {
            LastStructureEvent = structureId;
            LastStructureEventBar = barIndex;
        }

        public bool ValidateCandidate(SwingCandidate candidate, SwingContext ctx, int currentBar, out string rejectionReason)
        {
            rejectionReason = SwingRejectionReason.None;
            if (!Enabled || candidate == null) return true;

            bool isLong = candidate.Direction == SwingDirection.Long;

            // 1. Verrouillage de la même campagne active (SameCampaignLock)
            SwingCampaign activeCampaign = isLong ? ActiveLongCampaign : ActiveShortCampaign;
            if (activeCampaign != null && activeCampaign.State == SwingCampaignState.Active)
            {
                if (SameCampaignLock)
                {
                    rejectionReason = SwingRejectionReason.DuplicateCampaign;
                    return false;
                }

                if (candidate.Signature != null && activeCampaign.Signature != null &&
                    string.Equals(candidate.Signature.FormattedKey, activeCampaign.Signature.FormattedKey, StringComparison.Ordinal))
                {
                    rejectionReason = SwingRejectionReason.SameSignature;
                    return false;
                }
            }

            // 2. Cooldown d'entrée directionnel
            int lastEntryBar = isLong ? LastEntryBarLong : LastEntryBarShort;
            if (EntryCooldownBars > 0 && lastEntryBar >= 0 && (currentBar - lastEntryBar) < EntryCooldownBars)
            {
                rejectionReason = SwingRejectionReason.CooldownActive;
                return false;
            }

            // 3. Exigence de nouvelle structure après clôture d'une campagne
            if (RequireNewStructureForReentry && lastEntryBar >= 0)
            {
                bool hasNewStructure = (ctx != null && (ctx.HasBos || ctx.HasChoch)) || (LastStructureEventBar > lastEntryBar);
                int cdStructure = EntryCooldownBars > 0 ? EntryCooldownBars * 2 : 24;
                if (!hasNewStructure && (currentBar - lastEntryBar) < cdStructure)
                {
                    rejectionReason = SwingRejectionReason.DuplicateCampaign;
                    return false;
                }
            }

            // 4. Déduplication par signature récente sur la même structure
            if (candidate.Signature != null)
            {
                int priorBar;
                if (RecentSignatures.TryGetValue(candidate.Signature.FormattedKey, out priorBar))
                {
                    int cd = EntryCooldownBars > 0 ? EntryCooldownBars : 12;
                    if (currentBar - priorBar < cd)
                    {
                        rejectionReason = SwingRejectionReason.SameSignature;
                        return false;
                    }
                }
            }

            // 5. Limites de session
            if (MaxEntriesPerSession > 0 && SessionEntryCount >= MaxEntriesPerSession)
            {
                rejectionReason = SwingRejectionReason.SessionLimitReached;
                return false;
            }

            if (isLong && MaxLongEntriesPerSession > 0 && SessionLongCount >= MaxLongEntriesPerSession)
            {
                rejectionReason = SwingRejectionReason.DirectionLimitReached;
                return false;
            }

            if (!isLong && MaxShortEntriesPerSession > 0 && SessionShortCount >= MaxShortEntriesPerSession)
            {
                rejectionReason = SwingRejectionReason.DirectionLimitReached;
                return false;
            }

            return true;
        }

        public void OnCandidateExecuted(SwingCandidate candidate, TrackedSwingTrade trade, int currentBar)
        {
            if (candidate == null) return;

            bool isLong = candidate.Direction == SwingDirection.Long;
            SessionEntryCount++;

            if (isLong)
            {
                SessionLongCount++;
                LastEntryBarLong = currentBar;
            }
            else
            {
                SessionShortCount++;
                LastEntryBarShort = currentBar;
            }

            var campaign = new SwingCampaign
            {
                CampaignId = trade != null ? trade.TradeId : Guid.NewGuid().ToString("N"),
                Symbol = candidate.Symbol,
                Direction = candidate.Direction,
                SetupType = candidate.SetupType,
                Signature = candidate.Signature,
                State = SwingCampaignState.Active,
                InitialEntryBarIndex = currentBar,
                InitialEntryTimeUtc = candidate.TimeUtc,
                LastActionBarIndex = currentBar,
                LastActionTimeUtc = candidate.TimeUtc,
                TradesCount = 1,
                InitialStructureId = candidate.StructureId,
                CurrentStructureId = candidate.StructureId
            };

            if (isLong)
                ActiveLongCampaign = campaign;
            else
                ActiveShortCampaign = campaign;

            if (candidate.Signature != null)
            {
                RecentSignatures[candidate.Signature.FormattedKey] = currentBar;
            }
        }

        public void OnTradeClosed(TrackedSwingTrade trade, string exitReason, int currentBar)
        {
            if (trade == null) return;

            bool isRegimeExit = exitReason == "REGIME_CHANGED" || exitReason == "STRUCTURAL_REGIME_INVALIDATION";

            if (trade.IsLong)
            {
                if (ActiveLongCampaign != null)
                {
                    ActiveLongCampaign.State = isRegimeExit
                        ? SwingCampaignState.RegimeChanged
                        : SwingCampaignState.Completed;
                    ActiveLongCampaign.LastActionBarIndex = currentBar;
                    ActiveLongCampaign = null;
                }
            }
            else
            {
                if (ActiveShortCampaign != null)
                {
                    ActiveShortCampaign.State = isRegimeExit
                        ? SwingCampaignState.RegimeChanged
                        : SwingCampaignState.Completed;
                    ActiveShortCampaign.LastActionBarIndex = currentBar;
                    ActiveShortCampaign = null;
                }
            }
        }
    }

    #endregion
}
