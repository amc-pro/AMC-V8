using System;
using NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence;

namespace AMC.VolumeProfile.Tests
{
    /// <summary>
    /// Suite de tests unitaires pour le QualityEngine et le NoTradeEngine (Sprint 3).
    /// </summary>
    public static class QualityEngineTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception("ASSERTION FAILED: " + message);
        }

        private static MarketSnapshot CreateBaseSnapshot()
        {
            return new MarketSnapshot
            {
                Instrument = "NQ",
                Time = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc),
                TrendH4 = MiTrend.Bullish,
                TrendH1 = MiTrend.Bullish,
                TrendM15 = MiTrend.Bullish,
                TrendM5 = MiTrend.Bullish,
                LastBosH4 = MiStructureEvent.BullishBos,
                BarsSinceBosH4 = 2,
                LastChochH4 = MiStructureEvent.BullishChoch,
                BarsSinceChochH4 = 3,
                ProfileLocation = MiProfileLocation.AboveVah,
                VolatilityRegime = MiVolatilityRegime.Normal,
                NormalizedAtr = 25.0
            };
        }

        /// <summary>
        /// Test 1 : Contexte haussier optimal -> Score élevé (>= 85) et État Confirmed.
        /// </summary>
        public static void Run_Test_QualityEngine_Optimal_Confirmed_Context()
        {
            var qe = new QualityEngine();
            var snap = CreateBaseSnapshot();

            var eval = qe.Evaluate(snap, isBuy: true);

            Assert(eval.TotalScore >= 85.0, "Score attendu >= 85 pour un contexte optimal.");
            Assert(eval.State == ContextQualityState.Confirmed, "État attendu = Confirmed.");
            Assert(eval.IsTradeable, "Le contexte doit être tradeable.");
            Assert(eval.Penalties == 0.0, "Zéro pénalité attendue.");
        }

        /// <summary>
        /// Test 2 : Dégradation progressive des états discrets (Confirmed -> Ready -> Watch -> Degraded -> Invalidated).
        /// </summary>
        public static void Run_Test_QualityEngine_Discrete_States_Progression()
        {
            var qe = new QualityEngine();

            // 1. Ready (H4 Bullish, H1 Neutral, InsideVa)
            var snapReady = CreateBaseSnapshot();
            snapReady.TrendH1 = MiTrend.Neutral;
            snapReady.ProfileLocation = MiProfileLocation.InsideVa;
            var evalReady = qe.Evaluate(snapReady, isBuy: true);
            Assert(evalReady.State == ContextQualityState.Ready || evalReady.State == ContextQualityState.Confirmed,
                "État attendu = Ready ou Confirmed pour H4 Bullish + InsideVa.");

            // 2. Watch (H4 Neutral, H1 Bullish, InsideVa)
            var snapWatch = CreateBaseSnapshot();
            snapWatch.TrendH4 = MiTrend.Neutral;
            snapWatch.ProfileLocation = MiProfileLocation.AtPoc;
            var evalWatch = qe.Evaluate(snapWatch, isBuy: true);
            Assert(evalWatch.TotalScore >= 50.0 && evalWatch.TotalScore < 85.0, "Score Watch attendu entre 50 et 85.");

            // 3. Degraded / Invalidated (H4 Bearish pour un achat Long)
            var snapInvalid = CreateBaseSnapshot();
            snapInvalid.TrendH4 = MiTrend.Bearish;
            snapInvalid.TrendH1 = MiTrend.Bearish;
            snapInvalid.ProfileLocation = MiProfileLocation.BelowVal;
            var evalInvalid = qe.Evaluate(snapInvalid, isBuy: true);
            Assert(evalInvalid.State == ContextQualityState.Invalidated || evalInvalid.State == ContextQualityState.Degraded,
                "Achat contre tendance H4+H1 baissière doit être Degraded ou Invalidated.");
            Assert(!evalInvalid.IsTradeable, "Ne doit pas être tradeable.");
        }

        /// <summary>
        /// Test 3 : NoTradeEngine rejette un trade lors d'un conflit macro H4 vs H1.
        /// </summary>
        public static void Run_Test_NoTradeEngine_Blocks_HtfConflict()
        {
            var nte = new NoTradeEngine();
            var snap = CreateBaseSnapshot();

            // H4 Bullish mais H1 Bearish -> Conflit macro
            snap.TrendH4 = MiTrend.Bullish;
            snap.TrendH1 = MiTrend.Bearish;

            var decision = nte.EvaluateTradeEligibility(snap, isBuy: true);

            Assert(decision.IsRejected, "Le trade doit être rejeté en cas de conflit H4 vs H1.");
            Assert(decision.Reason == NoTradeReason.HtfConflict, "Motif attendu = HtfConflict.");
            Assert(decision.Explanation.Contains("Conflit de tendance macro"), "Explication motivée attendue.");
        }

        /// <summary>
        /// Test 4 : NoTradeEngine rejette une position opposée à la tendance H4 sauf si Mean-Reversal autorisé.
        /// </summary>
        public static void Run_Test_NoTradeEngine_Blocks_AdverseH4Trend_Unless_MeanReversal()
        {
            var nte = new NoTradeEngine();
            var snap = CreateBaseSnapshot();

            // Marché H4 Bearish
            snap.TrendH4 = MiTrend.Bearish;
            snap.TrendH1 = MiTrend.Bearish;

            // Tentative d'achat continuation -> REJET
            var decContinuation = nte.EvaluateTradeEligibility(snap, isBuy: true, isMeanReversal: false);
            Assert(decContinuation.IsRejected, "Achat continuation contre H4 Bearish doit être rejeté.");
            Assert(decContinuation.Reason == NoTradeReason.HtfOpposedDirection, "Motif = HtfOpposedDirection.");

            // Même achat configuré en Mean-Reversal explicite -> AUTORISÉ (exemption)
            snap.ProfileLocation = MiProfileLocation.InsideVa;
            var decReversal = nte.EvaluateTradeEligibility(snap, isBuy: true, isMeanReversal: true);
            // S'il n'y a pas d'autre filtre bloquant
            Assert(decReversal.Reason != NoTradeReason.HtfOpposedDirection, "Mean-Reversal ne doit pas être bloqué par HtfOpposedDirection.");
        }

        /// <summary>
        /// Test 5 : NoTradeEngine rejette un achat en chute libre (BelowVal sous tendance baissière).
        /// </summary>
        public static void Run_Test_NoTradeEngine_Blocks_BadLocation()
        {
            var nte = new NoTradeEngine();
            var snap = CreateBaseSnapshot();

            snap.TrendH4 = MiTrend.Bearish;
            snap.ProfileLocation = MiProfileLocation.BelowVal;

            var decision = nte.EvaluateTradeEligibility(snap, isBuy: true);
            Assert(decision.IsRejected, "Achat sous VAL en marché baissier doit être rejeté.");
        }

        /// <summary>
        /// Test 6 : Validation positive d'un setup aligné par le NoTradeEngine.
        /// </summary>
        public static void Run_Test_NoTradeEngine_Passes_Aligned_Setup()
        {
            var nte = new NoTradeEngine();
            var snap = CreateBaseSnapshot();

            var decision = nte.EvaluateTradeEligibility(snap, isBuy: true);
            Assert(!decision.IsRejected, "Un setup sain et aligné doit passer sans rejet.");
            Assert(decision.Reason == NoTradeReason.None, "Reason = None.");
            Assert(decision.QualityScore >= 70.0, "Score >= 70.");
        }
    }
}
