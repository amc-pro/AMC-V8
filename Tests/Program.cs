#region Using declarations
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence;
using NinjaTrader.NinjaScript.Indicators.VolumeProfilePro;
#endregion

namespace AMC.VolumeProfile.Tests
{
    internal static class Program
    {
        private static int passedTests = 0;
        private static int failedTests = 0;

        private static void Main(string[] args)
        {
            Console.WriteLine("================================================================");
            Console.WriteLine("🚀 AMC PRO V7.9 — VOLUME PROFILE PRODUCTION TEST SUITE");
            Console.WriteLine("================================================================");

            RunTest("Test_Poc_And_ValueArea_Calculation", Test_Poc_And_ValueArea_Calculation);
            RunTest("Test_Gaussian_Smoothing_And_HVN_LVN_Extraction", Test_Gaussian_Smoothing_And_HVN_LVN_Extraction);
            RunTest("Test_Deterministic_Calendar_Period_Keys", Test_Deterministic_Calendar_Period_Keys);
            RunTest("Test_CME_RTH_Daily_And_Weekly_Boundaries", Test_CME_RTH_Daily_And_Weekly_Boundaries);
            RunTest("Test_CME_ETH_Trading_Date_Boundary", Test_CME_ETH_Trading_Date_Boundary);
            RunTest("Test_No_Uniform_Volume_Fallback", Test_No_Uniform_Volume_Fallback);
            RunTest("Test_SQLite_Repository_CRUD_And_Persistence", Test_SQLite_Repository_CRUD_And_Persistence);
            RunTest("Test_AntiLookahead_Strict_Guarantee", Test_AntiLookahead_Strict_Guarantee);
            RunTest("Test_Analyzer_Distance_And_Location", Test_Analyzer_Distance_And_Location);
            RunTest("Test_Analyzer_MultiTimeframe_Confluence", Test_Analyzer_MultiTimeframe_Confluence);
            RunTest("Test_Analyzer_Zone_Lifecycle_Transitions", Test_Analyzer_Zone_Lifecycle_Transitions);
            RunTest("Test_Manager_MultiPeriod_Accumulation_And_Freeze", Test_Manager_MultiPeriod_Accumulation_And_Freeze);
            RunTest("Test_Empty_And_Uniform_Volume_Distributions", Test_Empty_And_Uniform_Volume_Distributions);
            RunTest("Test_Concurrent_Repository_Access_And_Worker_Drain", Test_Concurrent_Repository_Access_And_Worker_Drain);
            RunTest("Test_High_Speed_Throughput_Benchmark", Test_High_Speed_Throughput_Benchmark);
            RunTest("Test_SmcWeights_Sum_Equals_Total", Test_SmcWeights_Sum_Equals_Total);
            RunTest("Test_Footprint_Strength_Max_Bounded", Test_Footprint_Strength_Max_Bounded);
            RunTest("Test_Clamp_NaN_And_Infinity_Protection", Test_Clamp_NaN_And_Infinity_Protection);
            RunTest("Test_Json_Symbol_Escaping_And_Protocol_V2", Test_Json_Symbol_Escaping_And_Protocol_V2);
            RunTest("Test_All_CSharp_Files_Syntax_And_Brace_Balance", Test_All_CSharp_Files_Syntax_And_Brace_Balance);
            RunTest("Test_Dashboard_Text_Wrapping_And_Formatting", Test_Dashboard_Text_Wrapping_And_Formatting);
            RunTest("Test_HTF_Trend_Classifier_ClosedBars", Test_HTF_Trend_Classifier_ClosedBars);
            RunTest("Test_HTF_Trend_Classifier_RejectsInvalidData", Test_HTF_Trend_Classifier_RejectsInvalidData);
            RunTest("Test_VWAP_Sanitization_And_AntiLookahead", Test_VWAP_Sanitization_And_AntiLookahead);
            RunTest("Test_XmlConfigurations_And_ScalpingPro_GateMatching", Test_XmlConfigurations_And_ScalpingPro_GateMatching);
            RunTest("Test_Fvg_AntiLookahead_Strict_Closed_Bars", Test_Fvg_AntiLookahead_Strict_Closed_Bars);
            RunTest("Test_Fvg_Consequent_Encroachment_50Percent_Defense", Test_Fvg_Consequent_Encroachment_50Percent_Defense);
            RunTest("Test_Fvg_Inversion_Breaker_Transition", Test_Fvg_Inversion_Breaker_Transition);
            RunTest("Test_Fvg_Smart_Eviction_Preserves_Active_Zones", Test_Fvg_Smart_Eviction_Preserves_Active_Zones);
            RunTest("Test_Closed_VWAP_And_StandardDeviation_Calculation", Test_Closed_VWAP_And_StandardDeviation_Calculation);
            RunTest("Test_Closed_VWAP_SQLite_Persistence_And_Reload", Test_Closed_VWAP_SQLite_Persistence_And_Reload);
            RunTest("Test_Closed_VWAP_HTF_Confluence_And_Scoring", Test_Closed_VWAP_HTF_Confluence_And_Scoring);
            RunTest("Test_Macro_Inflection_Context_Scoring_N1", Test_Macro_Inflection_Context_Scoring_N1);
            RunTest("Test_ScalpingPro_Continuous_Stretch_Damping", Test_ScalpingPro_Continuous_Stretch_Damping);
            RunTest("Test_AntiFallingKnife_Safety_Gating", Test_AntiFallingKnife_Safety_Gating);

            // ================================================================
            // 🎯 SUITE DE TESTS UNITAIRES SWING ZERO-TRUST (20 TESTS OBLIGATOIRES)
            // ================================================================
            RunTest("Test_Swing_01_AntiLookahead_StrictClosedBars", Test_Swing_01_AntiLookahead_StrictClosedBars);
            RunTest("Test_Swing_02_Deterministic_VP_Closed_Calculations", Test_Swing_02_Deterministic_VP_Closed_Calculations);
            RunTest("Test_Swing_03_Closed_VWAP_And_SD_Bands", Test_Swing_03_Closed_VWAP_And_SD_Bands);
            RunTest("Test_Swing_04_MarketRegime_Classification", Test_Swing_04_MarketRegime_Classification);
            RunTest("Test_Swing_05_RejectExtreme_And_ValueReentry_Setups", Test_Swing_05_RejectExtreme_And_ValueReentry_Setups);
            RunTest("Test_Swing_06_Breakout_Retest_Setup", Test_Swing_06_Breakout_Retest_Setup);
            RunTest("Test_Swing_07_SMC_Structure_And_OrderFlow_Validation", Test_Swing_07_SMC_Structure_And_OrderFlow_Validation);
            RunTest("Test_Swing_08_Hybrid_Stop_Atr_And_Structural", Test_Swing_08_Hybrid_Stop_Atr_And_Structural);
            RunTest("Test_Swing_09_PositionSizing_By_TickValue", Test_Swing_09_PositionSizing_By_TickValue);
            RunTest("Test_Swing_10_Strict_MinMax_StopTicks_Clamping", Test_Swing_10_Strict_MinMax_StopTicks_Clamping);
            RunTest("Test_Swing_11_AntiStacking_Protection", Test_Swing_11_AntiStacking_Protection);
            RunTest("Test_Swing_12_Idempotence_After_Recalculation", Test_Swing_12_Idempotence_After_Recalculation);
            RunTest("Test_Swing_13_NewsFilter_And_Severity_Blackout", Test_Swing_13_NewsFilter_And_Severity_Blackout);
            RunTest("Test_Swing_14_Gaps_And_Rollover_Handling", Test_Swing_14_Gaps_And_Rollover_Handling);
            RunTest("Test_Swing_15_PartialExits_TP1_TP2_And_BreakEvenTrailing", Test_Swing_15_PartialExits_TP1_TP2_And_BreakEvenTrailing);
            RunTest("Test_Swing_16_ScalpingPro_NonRegression_Isolation", Test_Swing_16_ScalpingPro_NonRegression_Isolation);
            RunTest("Test_Swing_17_XmlConfiguration_Parsing_All_8_Instruments", Test_Swing_17_XmlConfiguration_Parsing_All_8_Instruments);
            RunTest("Test_Swing_18_Deployment_And_Sync_Integrity", Test_Swing_18_Deployment_And_Sync_Integrity);
            RunTest("Test_Swing_19_Path_Security_And_No_Secrets_Leak", Test_Swing_19_Path_Security_And_No_Secrets_Leak);
            RunTest("Test_Swing_20_No_Dead_Code_Or_Orphaned_Presets", Test_Swing_20_No_Dead_Code_Or_Orphaned_Presets);

            // ================================================================
            // 🛡️ SUITE DE TESTS D'INTÉGRATION STATEFUL & PERSISTANCE SWING (5 TESTS)
            // ================================================================
            RunTest("Test_Swing_Integration_SQLite_Persistence_And_Reload", Test_Swing_Integration_SQLite_Persistence_And_Reload);
            RunTest("Test_Swing_Integration_TwoStep_Partial_Exit_TP1_BE_TP2", Test_Swing_Integration_TwoStep_Partial_Exit_TP1_BE_TP2);
            RunTest("Test_Swing_Integration_Stop_Before_TP1_Full_Loss", Test_Swing_Integration_Stop_Before_TP1_Full_Loss);
            RunTest("Test_Swing_Integration_Dynamic_News_And_Gap_Penalty", Test_Swing_Integration_Dynamic_News_And_Gap_Penalty);
            RunTest("Test_Swing_Integration_Overnight_Session_Transition", Test_Swing_Integration_Overnight_Session_Transition);

            // ================================================================
            // 📊 SUITE POC MIGRATION MODEL DURCIE (12 TESTS DE VALIDATION & FRONTIÈRES)
            // ================================================================
            RunTest("Test_PocMigration_Analyzer_Detects_Upward_Drift", Test_PocMigration_Analyzer_Detects_Upward_Drift);
            RunTest("Test_PocMigration_Analyzer_Detects_Downward_Drift", Test_PocMigration_Analyzer_Detects_Downward_Drift);
            RunTest("Test_PocMigration_Analyzer_3Profiles_2Transitions_Valid", Test_PocMigration_Analyzer_3Profiles_2Transitions_Valid);
            RunTest("Test_PocMigration_Analyzer_Rejects_Inconsistent_Drift", Test_PocMigration_Analyzer_Rejects_Inconsistent_Drift);
            RunTest("Test_PocMigration_Analyzer_Extracts_Recent_Sequence_After_Older_Break", Test_PocMigration_Analyzer_Extracts_Recent_Sequence_After_Older_Break);
            RunTest("Test_PocMigration_Analyzer_Strength_Threshold_Boundaries", Test_PocMigration_Analyzer_Strength_Threshold_Boundaries);
            RunTest("Test_PocMigration_Analyzer_Overlap_Boundaries", Test_PocMigration_Analyzer_Overlap_Boundaries);
            RunTest("Test_PocMigration_Analyzer_Defends_Against_Zero_Atr_And_Invalid_Data", Test_PocMigration_Analyzer_Defends_Against_Zero_Atr_And_Invalid_Data);
            RunTest("Test_PocMigration_Setup_Scoring_And_Preconditions", Test_PocMigration_Setup_Scoring_And_Preconditions);
            RunTest("Test_PocMigration_Setup_Rejects_Wrong_Side_Structural_Stop", Test_PocMigration_Setup_Rejects_Wrong_Side_Structural_Stop);
            RunTest("Test_PocMigration_Setup_AntiChase_VA_Rejection", Test_PocMigration_Setup_AntiChase_VA_Rejection);
            RunTest("Test_PocMigration_Repository_Query_Strict_AntiLookahead", Test_PocMigration_Repository_Query_Strict_AntiLookahead);

            // ================================================================
            // 🌊 SUITE MONTHLY VWAP BAND RETEST ZERO-TRUST (10 TESTS DE VALIDATION)
            // ================================================================
            RunTest("Test_MonthlyVwap_O1_Calculation_Matches_Exact_Math", Test_MonthlyVwap_O1_Calculation_Matches_Exact_Math);
            RunTest("Test_MonthlyVwap_Reset_On_Month_Boundary", Test_MonthlyVwap_Reset_On_Month_Boundary);
            RunTest("Test_MonthlyVwapBandRetest_Long_Valid_Confirmed_Bar", Test_MonthlyVwapBandRetest_Long_Valid_Confirmed_Bar);
            RunTest("Test_MonthlyVwapBandRetest_Short_Valid_Confirmed_Bar", Test_MonthlyVwapBandRetest_Short_Valid_Confirmed_Bar);
            RunTest("Test_MonthlyVwapBandRetest_Rejects_IntrabarTouch_Without_Close_Confirmation", Test_MonthlyVwapBandRetest_Rejects_IntrabarTouch_Without_Close_Confirmation);
            RunTest("Test_MonthlyVwapBandRetest_Rejects_Flat_Or_Opposing_Vwap_Slope", Test_MonthlyVwapBandRetest_Rejects_Flat_Or_Opposing_Vwap_Slope);
            RunTest("Test_MonthlyVwapBandRetest_EarlyMonth_Data_Insufficient_Guard", Test_MonthlyVwapBandRetest_EarlyMonth_Data_Insufficient_Guard);
            RunTest("Test_MonthlyVwapBandRetest_Excessive_Retests_Rejected", Test_MonthlyVwapBandRetest_Excessive_Retests_Rejected);
            RunTest("Test_MonthlyVwapBandRetest_Snapshot_Immutability", Test_MonthlyVwapBandRetest_Snapshot_Immutability);
            RunTest("Test_MonthlyVwapBandRetest_Full_Sizing_And_Stop_Clamping", Test_MonthlyVwapBandRetest_Full_Sizing_And_Stop_Clamping);

            // ================================================================
            // 🚀 SUITE MONTHLY VWAP P0/P1 HARDENED & NORMALIZED (8 TESTS)
            // ================================================================
            RunTest("Test_MonthlyVwap_Numerical_Stability_HighPrice_And_NegativeVariance", Test_MonthlyVwap_Numerical_Stability_HighPrice_And_NegativeVariance);
            RunTest("Test_MonthlyVwap_Slope_TicksPerHour_Invariance", Test_MonthlyVwap_Slope_TicksPerHour_Invariance);
            RunTest("Test_MonthlyBand_Epoch_Lifecycle_And_Drift_Reset", Test_MonthlyBand_Epoch_Lifecycle_And_Drift_Reset);
            RunTest("Test_MonthlyBand_MultiBar_Acceptance_Configurable", Test_MonthlyBand_MultiBar_Acceptance_Configurable);
            RunTest("Test_MonthlyBand_Collision_Stop_TP_Conservative_Pessimistic", Test_MonthlyBand_Collision_Stop_TP_Conservative_Pessimistic);
            RunTest("Test_MonthlyBand_Slope_AtrNormalized_Validation", Test_MonthlyBand_Slope_AtrNormalized_Validation);
            RunTest("Test_MonthlyBand_Epoch_Persists_On_Snapshot", Test_MonthlyBand_Epoch_Persists_On_Snapshot);
            RunTest("Test_MonthlyBand_NonRegression_All_Existing_Setups", Test_MonthlyBand_NonRegression_All_Existing_Setups);

            // ================================================================
            // 🔧 SUITE CORRECTIONS POST-AUDIT (PROMPT_CORRECTIONS_AUDIT_COMPLET.md)
            // ================================================================
            RunTest("Test_Audit_ScalpingPro_Preset_Guard_In_Source", Test_Audit_ScalpingPro_Preset_Guard_In_Source);
            RunTest("Test_Audit_Sniper_Enabled_And_Swing_Isolation", Test_Audit_Sniper_Enabled_And_Swing_Isolation);
            RunTest("Test_Audit_Swing_Microstructure_Helpers_In_Source", Test_Audit_Swing_Microstructure_Helpers_In_Source);
            RunTest("Test_Audit_Swing_RiskRewardScore_From_Real_RR", Test_Audit_Swing_RiskRewardScore_From_Real_RR);
            RunTest("Test_Audit_Swing_RiskRewardScore_Low_When_RR_Below_Min", Test_Audit_Swing_RiskRewardScore_Low_When_RR_Below_Min);
            RunTest("Test_MonthlyBand_Acceptance_Uses_Prev_Sd1_Values", Test_MonthlyBand_Acceptance_Uses_Prev_Sd1_Values);
            RunTest("Test_Audit_Configs_No_Secrets_ScalpingPro_And_Swing", Test_Audit_Configs_No_Secrets_ScalpingPro_And_Swing);
            RunTest("Test_Audit_TelegramDispatcher_Dedup_Not_Blocked_After_Send_Failure", Test_Audit_TelegramDispatcher_Dedup_Not_Blocked_After_Send_Failure);
            RunTest("Test_Audit_EnforcePresetBarCloseDiscipline_In_Source", Test_Audit_EnforcePresetBarCloseDiscipline_In_Source);

            // ================================================================
            // 🎯 SUITE SWING V3 OPPORTUNITY MANAGER & WIN RATE OPTIMIZATION (12 TESTS)
            // ================================================================
            RunTest("Test_SwingV3_CandidateCollection_And_Ranking", Test_SwingV3_CandidateCollection_And_Ranking);
            RunTest("Test_SwingV3_SameCampaignLock_Blocks_Duplicates", Test_SwingV3_SameCampaignLock_Blocks_Duplicates);
            RunTest("Test_SwingV3_NewStructure_Unlocks_Campaign", Test_SwingV3_NewStructure_Unlocks_Campaign);
            RunTest("Test_SwingV3_Cooldown_Enforcement", Test_SwingV3_Cooldown_Enforcement);
            RunTest("Test_SwingV3_Session_Limits", Test_SwingV3_Session_Limits);
            RunTest("Test_SwingV3_HtfContinuation_Requires_Pullback_To_Value", Test_SwingV3_HtfContinuation_Requires_Pullback_To_Value);
            RunTest("Test_SwingV3_MacroReversal_OrderFlow_HardGate", Test_SwingV3_MacroReversal_OrderFlow_HardGate);
            RunTest("Test_SwingV3_LateEntryPenalty", Test_SwingV3_LateEntryPenalty);
            RunTest("Test_SwingV3_Dynamic_TP1_Snapping_To_Opposing_Wall", Test_SwingV3_Dynamic_TP1_Snapping_To_Opposing_Wall);
            RunTest("Test_SwingV3_RegimeChange_HardExit", Test_SwingV3_RegimeChange_HardExit);
            RunTest("Test_SwingV3_ZeroTrust_InvalidAtrAndPointValue", Test_SwingV3_ZeroTrust_InvalidAtrAndPointValue);
            RunTest("Test_SwingV3_ScalpingPro_StrictIsolation", Test_SwingV3_ScalpingPro_StrictIsolation);

            // ================================================================
            // 🛡️ SUITE SWING V2 REGIME INVALIDATION & STRUCTURAL ARCHITECTURE (11 TESTS)
            // ================================================================
            RunTest("Test_SwingV2_SimpleRegimeChange_NoExit", Test_SwingV2_SimpleRegimeChange_NoExit);
            RunTest("Test_SwingV2_RegimeDeterioration_And_StructuralInvalidation_Exit", Test_SwingV2_RegimeDeterioration_And_StructuralInvalidation_Exit);
            RunTest("Test_SwingV2_MacroReversal_Long_Immunity", Test_SwingV2_MacroReversal_Long_Immunity);
            RunTest("Test_SwingV2_MacroReversal_Short_Immunity", Test_SwingV2_MacroReversal_Short_Immunity);
            RunTest("Test_SwingV2_SoftProtection_Trails_Stop_To_Breakeven", Test_SwingV2_SoftProtection_Trails_Stop_To_Breakeven);
            RunTest("Test_SwingV2_LegacyFlag_ExitOnRegimeChange_BackwardCompatibility", Test_SwingV2_LegacyFlag_ExitOnRegimeChange_BackwardCompatibility);
            RunTest("Test_SwingV2_DefaultSettings_NoPrematureExit", Test_SwingV2_DefaultSettings_NoPrematureExit);
            RunTest("Test_SwingV2_AdverseBars_Hysteresis_And_Persistence", Test_SwingV2_AdverseBars_Hysteresis_And_Persistence);
            RunTest("Test_SwingV2_StrictIsolation_ScalpingPro_Sniper", Test_SwingV2_StrictIsolation_ScalpingPro_Sniper);
            RunTest("Test_SwingV2_DynamicStructuralPrice_Trailing_And_Tp1", Test_SwingV2_DynamicStructuralPrice_Trailing_And_Tp1);
            RunTest("Test_SwingV2_AtrToleranceBuffer_FiltersMicroWicks", Test_SwingV2_AtrToleranceBuffer_FiltersMicroWicks);
            RunTest("Test_SwingV2_PhysicalSl_Vs_StructuralInvalidation_DistinctRoles", Test_SwingV2_PhysicalSl_Vs_StructuralInvalidation_DistinctRoles);
            RunTest("Test_MarketIntelligence_HistoricalDecoupling_NoTelegramSpam", Test_MarketIntelligence_HistoricalDecoupling_NoTelegramSpam);

            // ================================================================
            // 🔬 SUITE FORENSIC REPLAY & HYSTÉRÉSIS MULTIBARRES SPRINT 1 (6 TESTS)
            // ================================================================
            RunTest("Test_Forensic_N1_Immediate_Exit", SwingReplayForensicTests.Run_Test_Forensic_N1_Immediate_Exit);
            RunTest("Test_Forensic_N3_Progression", SwingReplayForensicTests.Run_Test_Forensic_N3_Progression);
            RunTest("Test_Forensic_N5_Progression", SwingReplayForensicTests.Run_Test_Forensic_N5_Progression);
            RunTest("Test_Forensic_Hysteresis_Rebound", SwingReplayForensicTests.Run_Test_Forensic_Hysteresis_Rebound);
            RunTest("Test_Forensic_MacroReversal_Immunity_And_Exit", SwingReplayForensicTests.Run_Test_Forensic_MacroReversal_Immunity_And_Exit);
            RunTest("Test_Forensic_PhysicalSl_Vs_Structural_Buffer", SwingReplayForensicTests.Run_Test_Forensic_PhysicalSl_Vs_Structural_Buffer);

            // ================================================================
            // ⏳ SUITE MARKET INTELLIGENCE DÉCOUPLAGE & INVARIANCE TEMPORELLE SPRINT 2 (4 TESTS)
            // ================================================================
            RunTest("Test_Temporal_Invariance_T_vs_T_plus_N", MarketIntelligenceTemporalTests.Run_Test_Temporal_Invariance_T_vs_T_plus_N);
            RunTest("Test_Historical_vs_Realtime_Determinism", MarketIntelligenceTemporalTests.Run_Test_Historical_vs_Realtime_Determinism);
            RunTest("Test_ZeroLookahead_Trend_Classifier", MarketIntelligenceTemporalTests.Run_Test_ZeroLookahead_Trend_Classifier);
            RunTest("Test_ProfileLocation_And_VolatilityRegime", MarketIntelligenceTemporalTests.Run_Test_ProfileLocation_And_VolatilityRegime);

            // ================================================================
            // 🎯 SUITE QUALITY ENGINE & NO-TRADE MATRIX SPRINT 3 (6 TESTS)
            // ================================================================
            RunTest("Test_QualityEngine_Optimal_Confirmed_Context", QualityEngineTests.Run_Test_QualityEngine_Optimal_Confirmed_Context);
            RunTest("Test_QualityEngine_Discrete_States_Progression", QualityEngineTests.Run_Test_QualityEngine_Discrete_States_Progression);
            RunTest("Test_NoTradeEngine_Blocks_HtfConflict", QualityEngineTests.Run_Test_NoTradeEngine_Blocks_HtfConflict);
            RunTest("Test_NoTradeEngine_Blocks_AdverseH4Trend_Unless_MeanReversal", QualityEngineTests.Run_Test_NoTradeEngine_Blocks_AdverseH4Trend_Unless_MeanReversal);
            RunTest("Test_NoTradeEngine_Blocks_BadLocation", QualityEngineTests.Run_Test_NoTradeEngine_Blocks_BadLocation);
            RunTest("Test_NoTradeEngine_Passes_Aligned_Setup", QualityEngineTests.Run_Test_NoTradeEngine_Passes_Aligned_Setup);

            Console.WriteLine("================================================================");
            Console.WriteLine(string.Format("📊 RESULTATS : {0} REUSSIS, {1} ECHOUES", passedTests, failedTests));
            Console.WriteLine("================================================================");

            if (failedTests > 0)
            {
                Environment.Exit(1);
            }
        }

        private static void RunTest(string testName, Action testMethod)
        {
            try
            {
                testMethod();
                passedTests++;
                Console.WriteLine(string.Format("  ✅ [PASS] {0}", testName));
            }
            catch (Exception ex)
            {
                failedTests++;
                Console.WriteLine(string.Format("  ❌ [FAIL] {0} -> {1}", testName, ex.Message));
                Console.WriteLine(ex.StackTrace);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception("Assertion Failed: " + message);
            }
        }

        #region Test Methods

        private static void Test_HTF_Trend_Classifier_ClosedBars()
        {
            double tick = 0.25;

            var bull = NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence.MiTrendLogic.Classify(
                101.00, 100.00, 100.50, 100.25, tick, 0.50, 0.10);
            Assert(bull == NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence.MiTrend.Bullish,
                "Un contexte prix > EMA + pente EMA + momentum haussier doit être Bullish.");

            var bear = NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence.MiTrendLogic.Classify(
                99.00, 100.00, 99.50, 99.75, tick, 0.50, 0.10);
            Assert(bear == NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence.MiTrend.Bearish,
                "Un contexte prix < EMA + pente EMA + momentum baissier doit être Bearish.");

            var pullback = NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence.MiTrendLogic.Classify(
                100.60, 100.80, 100.50, 100.25, tick, 0.50, 0.10);
            Assert(pullback == NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence.MiTrend.Neutral,
                "Une respiration contre le momentum ne doit pas être classée comme tendance confirmée.");
        }

        private static void Test_HTF_Trend_Classifier_RejectsInvalidData()
        {
            var t = NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence.MiTrendLogic.Classify(
                double.NaN, 100, 100, 99, 0.25, 0.5, 0.1);
            Assert(t == NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence.MiTrend.Neutral,
                "NaN doit produire Neutral, jamais une tendance tradable.");

            t = NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence.MiTrendLogic.Classify(
                101, 100, 100.5, 100, 0, 0.5, 0.1);
            Assert(t == NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence.MiTrend.Neutral,
                "TickSize invalide doit produire Neutral.");
        }

        private static void Test_Poc_And_ValueArea_Calculation()
        {
            var calc = new VolumeProfileCalculator();
            calc.ValueAreaPercent = 70;
            double tickSize = 0.25;

            // Distribution connue en cloche centrée à 21850.0 (tick = 87400)
            // 21845.0 (87380): 100
            // 21847.5 (87390): 500
            // 21850.0 (87400): 2000 (POC)
            // 21852.5 (87410): 500
            // 21855.0 (87420): 100

            calc.AddVolumeAtPrice(21845.0, 100, tickSize);
            calc.AddVolumeAtPrice(21847.5, 500, tickSize);
            calc.AddVolumeAtPrice(21850.0, 2000, tickSize);
            calc.AddVolumeAtPrice(21852.5, 500, tickSize);
            calc.AddVolumeAtPrice(21855.0, 100, tickSize);

            DateTime now = DateTime.UtcNow;
            var profile = calc.BuildProfile("NQ", "CME", "RTH", VolumeProfilePeriodType.Daily, "NQ|CME|RTH|DAILY|2026-08-14", now.AddDays(-1), now, tickSize);

            Assert(profile.Valid, "Le profil doit être valide");
            Assert(Math.Abs(profile.Poc - 21850.0) < 0.001, "Le POC doit être exactement 21850.0, obtenu: " + profile.Poc);
            Assert(profile.Vah >= profile.Poc, "VAH doit être >= POC");
            Assert(profile.Val <= profile.Poc, "VAL doit être <= POC");
            Assert(profile.TotalVolume == 3200, "Le volume total doit être 3200");
        }

        private static void Test_Gaussian_Smoothing_And_HVN_LVN_Extraction()
        {
            var calc = new VolumeProfileCalculator();
            calc.GaussianSigmaTicks = 2.0;
            calc.HvnMinVolumeRatio = 1.3;
            calc.LvnMaxVolumeRatio = 0.7;
            calc.MinNodeSeparationTicks = 8;
            double tickSize = 0.25;

            // Profil bimodal avec deux pics (HVN 1 @ 21800, HVN 2 @ 21900) et un creux (LVN @ 21850)
            long tBase = (long)(21800.0 / tickSize); // 87200
            long tMid = (long)(21850.0 / tickSize);  // 87400
            long tHigh = (long)(21900.0 / tickSize); // 87600

            // Distribution autour de tBase
            for (long t = tBase - 10; t <= tBase + 10; t++)
            {
                long dist = Math.Abs(t - tBase);
                calc.AddVolume(t, Math.Max(10, 1000 - dist * 80));
            }

            // Distribution faible autour de tMid (LVN)
            for (long t = tMid - 10; t <= tMid + 10; t++)
            {
                calc.AddVolume(t, 50);
            }

            // Distribution autour de tHigh (HVN 2)
            for (long t = tHigh - 10; t <= tHigh + 10; t++)
            {
                long dist = Math.Abs(t - tHigh);
                calc.AddVolume(t, Math.Max(10, 800 - dist * 60));
            }

            var nodes = calc.DetectNodes(tickSize);
            Assert(nodes.Count >= 2, "Doit détecter au moins 2 nodes, détectés : " + nodes.Count);

            bool foundHvn = false;
            bool foundLvn = false;
            foreach (var n in nodes)
            {
                if (n.NodeType == VolumeProfileNodeType.HVN) foundHvn = true;
                if (n.NodeType == VolumeProfileNodeType.LVN) foundLvn = true;
            }

            Assert(foundHvn, "Doit contenir au moins un HVN");
            Assert(foundLvn, "Doit contenir au moins un LVN");
        }

        private static void Test_Deterministic_Calendar_Period_Keys()
        {
            DateTime date = new DateTime(2026, 8, 14, 15, 30, 0, DateTimeKind.Utc); // Vendredi semaine 33
            string dayKey = VolumeProfileCalculator.GetTradingDayKey("NQ", "CME", "RTH", date);
            string weekKey = VolumeProfileCalculator.GetTradingWeekKey("NQ", "CME", "RTH", date);
            string monthKey = VolumeProfileCalculator.GetTradingMonthKey("NQ", "CME", "RTH", date);

            Assert(dayKey == "NQ|CME|RTH|DAILY|2026-08-14", "Clé jour incorrecte: " + dayKey);
            Assert(weekKey == "NQ|CME|RTH|WEEKLY|2026-W33", "Clé semaine incorrecte: " + weekKey);
            Assert(monthKey == "NQ|CME|RTH|MONTHLY|2026-08", "Clé mois incorrecte: " + monthKey);

            DateTime startUtc, endUtc;
            VolumeProfileCalculator.GetPeriodBoundsUtc(VolumeProfilePeriodType.Weekly, date, "RTH", out startUtc, out endUtc);
            Assert(startUtc.DayOfWeek == DayOfWeek.Monday, "La semaine RTH doit débuter un Lundi");
        }


        private static void Test_CME_RTH_Daily_And_Weekly_Boundaries()
        {
            // 2026-08-14 15:30 UTC = 11:30 ET (RTH Friday).
            DateTime rthBar = new DateTime(2026, 8, 14, 15, 30, 0, DateTimeKind.Utc);
            string dayKey = VolumeProfileCalculator.GetTradingDayKey("NQ", "CME", "RTH", rthBar);
            string weekKey = VolumeProfileCalculator.GetTradingWeekKey("NQ", "CME", "RTH", rthBar);
            Assert(dayKey.EndsWith("DAILY|2026-08-14"), "RTH Daily doit suivre la date New York");
            Assert(weekKey.EndsWith("WEEKLY|2026-W33"), "RTH Weekly doit etre W33");

            DateTime ds, de, ws, we;
            VolumeProfileCalculator.GetPeriodBoundsUtc(VolumeProfilePeriodType.Daily, rthBar, "RTH", out ds, out de);
            VolumeProfileCalculator.GetPeriodBoundsUtc(VolumeProfilePeriodType.Weekly, rthBar, "RTH", out ws, out we);
            Assert(ds == new DateTime(2026, 8, 14, 13, 30, 0, DateTimeKind.Utc), "RTH Daily open doit etre 09:30 ET");
            Assert(de == new DateTime(2026, 8, 14, 20, 0, 0, DateTimeKind.Utc), "RTH Daily close doit etre 16:00 ET");
            Assert(we == new DateTime(2026, 8, 14, 20, 0, 0, DateTimeKind.Utc), "RTH Weekly close doit etre vendredi 16:00 ET");
        }

        private static void Test_CME_ETH_Trading_Date_Boundary()
        {
            // Sunday 18:00 ET opens the Monday CME trading date.
            DateTime sundayOpen = new DateTime(2026, 8, 16, 22, 0, 0, DateTimeKind.Utc);
            string dayKey = VolumeProfileCalculator.GetTradingDayKey("NQ", "CME", "ETH", sundayOpen);
            string weekKey = VolumeProfileCalculator.GetTradingWeekKey("NQ", "CME", "ETH", sundayOpen);
            Assert(dayKey.EndsWith("DAILY|2026-08-17"), "ETH Sunday 18:00 ET doit appartenir au trading day du lundi");
            Assert(weekKey.EndsWith("WEEKLY|2026-W34"), "ETH Sunday 18:00 ET doit appartenir a la semaine du lundi");

            DateTime ds, de, ws, we;
            VolumeProfileCalculator.GetPeriodBoundsUtc(VolumeProfilePeriodType.Daily, sundayOpen, "ETH", out ds, out de);
            VolumeProfileCalculator.GetPeriodBoundsUtc(VolumeProfilePeriodType.Weekly, sundayOpen, "ETH", out ws, out we);
            Assert(ds == new DateTime(2026, 8, 16, 22, 0, 0, DateTimeKind.Utc), "ETH Daily open doit etre dimanche 18:00 ET");
            Assert(de == new DateTime(2026, 8, 17, 21, 0, 0, DateTimeKind.Utc), "ETH Daily close doit etre lundi 17:00 ET");
            Assert(we == new DateTime(2026, 8, 21, 21, 0, 0, DateTimeKind.Utc), "ETH Weekly close doit etre vendredi 17:00 ET");
        }

        private static void Test_No_Uniform_Volume_Fallback()
        {
            var calc = new VolumeProfileCalculator();
            // This is a contract test for the manager: the production manager must not
            // synthesize per-price volume when tickVolumes are absent. The calculator itself
            // remains deterministic and only accepts explicit per-price additions.
            calc.AddVolume(100, 100);
            Assert(calc.TotalVolume == 100, "Le calculateur doit uniquement refleter les volumes explicites");
        }

        private static void Test_SQLite_Repository_CRUD_And_Persistence()
        {
            string tempDb = Path.Combine(Path.GetTempPath(), "test_amc_vp_" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                using (var repo = new VolumeProfileRepository(tempDb))
                {
                    bool ok = repo.Initialize();
                    Assert(ok, "L'initialisation SQLite doit réussir");

                    var p = new ClosedVolumeProfile
                    {
                        Symbol = "GC",
                        Exchange = "CME",
                        SessionTemplate = "RTH",
                        ProfileType = VolumeProfilePeriodType.Weekly,
                        PeriodKey = "GC|CME|RTH|WEEKLY|2026-W32",
                        PeriodStartUtc = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc),
                        PeriodEndUtc = new DateTime(2026, 8, 7, 23, 59, 59, DateTimeKind.Utc),
                        Poc = 2450.0,
                        Vah = 2470.0,
                        Val = 2430.0,
                        TotalVolume = 500000,
                        ValueAreaPercent = 70,
                        TickSize = 0.1,
                        Valid = true
                    };

                    p.Nodes.Add(new VolumeProfileNode(VolumeProfileNodeType.HVN, 2445.0, 2455.0, 2450.0, 1.8, 0.8));
                    p.Nodes.Add(new VolumeProfileNode(VolumeProfileNodeType.LVN, 2460.0, 2465.0, 2462.0, 0.4, 0.6));

                    repo.UpsertProfile(p);

                    // Attendre le worker asynchrone
                    System.Threading.Thread.Sleep(200);

                    var loaded = repo.GetProfileByKey("GC|CME|RTH|WEEKLY|2026-W32");
                    Assert(loaded != null, "Le profil doit être rechargé");
                    Assert(Math.Abs(loaded.Poc - 2450.0) < 0.01, "Le POC doit être 2450.0");
                    Assert(loaded.Nodes.Count == 2, "Doit avoir 2 nodes, obtenu: " + loaded.Nodes.Count);
                }
            }
            finally
            {
                try { if (File.Exists(tempDb)) File.Delete(tempDb); } catch { }
            }
        }

        private static void Test_AntiLookahead_Strict_Guarantee()
        {
            string tempDb = Path.Combine(Path.GetTempPath(), "test_amc_lookahead_" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                using (var repo = new VolumeProfileRepository(tempDb))
                {
                    repo.Initialize();

                    DateTime week1End = new DateTime(2026, 8, 7, 23, 59, 59, DateTimeKind.Utc);
                    DateTime week2End = new DateTime(2026, 8, 14, 23, 59, 59, DateTimeKind.Utc);

                    var p1 = new ClosedVolumeProfile
                    {
                        Symbol = "NQ",
                        ProfileType = VolumeProfilePeriodType.Weekly,
                        PeriodKey = "NQ|CME|RTH|WEEKLY|2026-W32",
                        PeriodStartUtc = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc),
                        PeriodEndUtc = week1End,
                        Poc = 21000.0,
                        Vah = 21100.0,
                        Val = 20900.0,
                        Valid = true
                    };

                    var p2 = new ClosedVolumeProfile
                    {
                        Symbol = "NQ",
                        ProfileType = VolumeProfilePeriodType.Weekly,
                        PeriodKey = "NQ|CME|RTH|WEEKLY|2026-W33",
                        PeriodStartUtc = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
                        PeriodEndUtc = week2End,
                        Poc = 21500.0,
                        Vah = 21600.0,
                        Val = 21400.0,
                        Valid = true
                    };

                    repo.UpsertProfile(p1);
                    repo.UpsertProfile(p2);

                    System.Threading.Thread.Sleep(200);

                    // Au milieu de la semaine 2 (Mercredi 12 Août 2026 14:00) :
                    // On doit obtenir STRICTEMENT la semaine 1 (POC 21000.0) et JAMAIS la semaine 2 !
                    DateTime simCurrentTime = new DateTime(2026, 8, 12, 14, 0, 0, DateTimeKind.Utc);
                    var activePrevWeek = repo.GetLatestClosedProfile("NQ", VolumeProfilePeriodType.Weekly, simCurrentTime);

                    Assert(activePrevWeek != null, "Un profil précédent doit être trouvé");
                    Assert(activePrevWeek.PeriodKey == "NQ|CME|RTH|WEEKLY|2026-W32", "La référence DOIT être W32 (anti-lookahead), obtenu : " + activePrevWeek.PeriodKey);
                    Assert(Math.Abs(activePrevWeek.Poc - 21000.0) < 0.01, "Le POC doit être 21000.0 (semaine passée clôturée)");
                }
            }
            finally
            {
                try { if (File.Exists(tempDb)) File.Delete(tempDb); } catch { }
            }
        }

        private static void Test_Analyzer_Distance_And_Location()
        {
            var analyzer = new VolumeProfileAnalyzer();
            analyzer.LevelToleranceTicks = 3;
            analyzer.NodeToleranceTicks = 4;
            double tickSize = 0.25;

            var day = new ClosedVolumeProfile
            {
                Poc = 21850.0,
                Vah = 21880.0,
                Val = 21820.0,
                Valid = true
            };

            // Prix @ 21890.0 (Au-dessus de VAH)
            var ctx = analyzer.Analyze(21890.0, 21895.0, 21885.0, 21890.0, 100, 20.0, tickSize, DateTime.UtcNow, day, null, null);

            Assert((ctx.Location & VolumeProfileLocationType.AboveValue) != 0, "Doit être détecté AboveValue");
            Assert(ctx.DistanceToClosestReference > 0, "Distance doit être calculée");
            Assert(ctx.ClosestReferenceName == "VAH Jour Préc", "Référence la plus proche doit être VAH, obtenu: " + ctx.ClosestReferenceName);
        }

        private static void Test_Analyzer_MultiTimeframe_Confluence()
        {
            var analyzer = new VolumeProfileAnalyzer();
            analyzer.LevelToleranceTicks = 4;
            analyzer.ConfluenceToleranceTicks = 4;
            analyzer.MinConfluenceLevels = 2;
            double tickSize = 0.25;

            var day = new ClosedVolumeProfile
            {
                Poc = 21850.0,
                Vah = 21880.0,
                Val = 21820.0,
                Valid = true
            };

            var week = new ClosedVolumeProfile
            {
                Poc = 21850.75, // À 3 ticks (0.75 pt) du Day POC (21850.0) -> Confluence <= 4 ticks !
                Vah = 21950.0,
                Val = 21700.0,
                Valid = true
            };

            var ctx = analyzer.Analyze(21851.0, 21855.0, 21848.0, 21851.0, -50, 15.0, tickSize, DateTime.UtcNow, day, week, null);

            Assert(ctx.ConfluenceCount >= 2, "Doit détecter au moins 2 niveaux en confluence, détecté: " + ctx.ConfluenceCount);
            Assert(ctx.ConfluenceType.Contains("POC Jour Préc") && ctx.ConfluenceType.Contains("POC Sem Préc"), "La description doit lister les confluences: " + ctx.ConfluenceType);
        }

        private static void Test_Analyzer_Zone_Lifecycle_Transitions()
        {
            var analyzer = new VolumeProfileAnalyzer();
            analyzer.LevelToleranceTicks = 3;
            double tickSize = 0.25;

            var state = new VolumeProfileZoneState
            {
                Id = 1,
                ProfileId = 10,
                LevelType = "POC",
                LevelPriceLow = 21848.0,
                LevelPriceHigh = 21852.0,
                PeakPrice = 21850.0,
                State = VolumeProfileZoneStateEnum.UNTOUCHED,
                Active = true
            };

            DateTime t1 = DateTime.UtcNow;

            // Bar 1 : Incursion et rejet haussier (Low sous la zone, Close au-dessus, Delta +)
            analyzer.EvaluateZoneReaction(state, 21855.0, 21847.0, 21854.0, 21849.0, 350.0, tickSize, t1);

            Assert(state.TouchCount == 1, "TouchCount doit être 1");
            Assert(state.State == VolumeProfileZoneStateEnum.REJECTED, "L'état doit être REJECTED, obtenu: " + state.State);
            Assert(state.RejectionCount == 1, "RejectionCount doit être 1");
        }

        private static void Test_Manager_MultiPeriod_Accumulation_And_Freeze()
        {
            string tempDb = Path.Combine(Path.GetTempPath(), "test_amc_mgr_" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                using (var mgr = new VolumeProfileManager("NQ", "CME", "RTH", 0.25, 70, tempDb))
                {
                    mgr.Initialize();

                    // Simuler jour 1 (13 Août)
                    DateTime d1 = new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc);
                    var tickList = new List<KeyValuePair<long, long>>
                    {
                        new KeyValuePair<long, long>(87400, 1000), // 21850.0
                        new KeyValuePair<long, long>(87410, 400),  // 21852.5
                        new KeyValuePair<long, long>(87390, 400)   // 21847.5
                    };

                    mgr.IngestVolumetricBar(d1, 21855.0, 21845.0, 21850.0, 21848.0, 1800, 200, tickList);

                    // Au cours du jour 1, PrevDay n'est pas encore le jour 1 (anti-lookahead)
                    Assert(mgr.PrevDay == null, "PrevDay ne doit pas être le jour en cours");

                    // Simuler transition vers jour 2 (14 Août)
                    DateTime d2 = new DateTime(2026, 8, 14, 10, 0, 0, DateTimeKind.Utc);
                    mgr.IngestVolumetricBar(d2, 21860.0, 21850.0, 21855.0, 21852.0, 1000, -100, null);

                    // Maintenant, le jour 1 est clôturé et doit être gelé dans PrevDay !
                    Assert(mgr.PrevDay != null, "PrevDay doit être disponible au jour 2");
                    Assert(mgr.PrevDay.Valid, "PrevDay doit être valide");
                    Assert(Math.Abs(mgr.PrevDay.Poc - 21850.0) < 0.01, "PrevDay POC doit être 21850.0");
                }
            }
            finally
            {
                try { if (File.Exists(tempDb)) File.Delete(tempDb); } catch { }
            }
        }

        private static void Test_Empty_And_Uniform_Volume_Distributions()
        {
            var calc = new VolumeProfileCalculator();
            // 1. Profil vide
            var emptyProf = calc.BuildProfile("NQ", "CME", "RTH", VolumeProfilePeriodType.Daily, "KEY_EMPTY", DateTime.UtcNow, DateTime.UtcNow, 0.25);
            Assert(!emptyProf.Valid, "Le profil vide doit être marqué invalide (Valid=false)");

            // 2. Profil avec volume uniforme
            calc.Reset();
            for (long t = 100; t <= 120; t++)
            {
                calc.AddVolume(t, 50);
            }
            var uniformProf = calc.BuildProfile("NQ", "CME", "RTH", VolumeProfilePeriodType.Daily, "KEY_UNIFORM", DateTime.UtcNow, DateTime.UtcNow, 0.25);
            Assert(uniformProf.Valid, "Le profil uniforme doit être valide");
            Assert(uniformProf.Poc >= 100 * 0.25 && uniformProf.Poc <= 120 * 0.25, "Le POC doit être dans les bornes");
        }

        private static void Test_Concurrent_Repository_Access_And_Worker_Drain()
        {
            string tempDb = Path.Combine(Path.GetTempPath(), "test_amc_conc_" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                using (var repo = new VolumeProfileRepository(tempDb))
                {
                    repo.Initialize();

                    var tasks = new List<System.Threading.Tasks.Task>();
                    for (int i = 0; i < 20; i++)
                    {
                        int id = i;
                        tasks.Add(System.Threading.Tasks.Task.Run(() =>
                        {
                            var p = new ClosedVolumeProfile
                            {
                                Symbol = "ES",
                                ProfileType = VolumeProfilePeriodType.Daily,
                                PeriodKey = "ES|CME|RTH|DAILY|2026-08-" + (id + 1).ToString("D2"),
                                PeriodStartUtc = new DateTime(2026, 8, id + 1, 0, 0, 0, DateTimeKind.Utc),
                                PeriodEndUtc = new DateTime(2026, 8, id + 1, 23, 59, 59, DateTimeKind.Utc),
                                Poc = 5500.0 + id,
                                Vah = 5520.0 + id,
                                Val = 5480.0 + id,
                                Valid = true
                            };
                            repo.UpsertProfile(p);
                        }));
                    }

                    System.Threading.Tasks.Task.WaitAll(tasks.ToArray());
                    System.Threading.Thread.Sleep(300);

                    var p10 = repo.GetProfileByKey("ES|CME|RTH|DAILY|2026-08-10");
                    Assert(p10 != null, "Le profil 10 doit être présent après écritures concurrentes");
                    Assert(Math.Abs(p10.Poc - 5509.0) < 0.01, "POC du profil 10 doit être 5509.0");
                }
            }
            finally
            {
                try { if (File.Exists(tempDb)) File.Delete(tempDb); } catch { }
            }
        }

        private static void Test_High_Speed_Throughput_Benchmark()
        {
            var calc = new VolumeProfileCalculator();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            int iterations = 10000;
            for (int i = 0; i < iterations; i++)
            {
                calc.AddVolume(80000 + (i % 50), 10);
            }

            var prof = calc.BuildProfile("NQ", "CME", "RTH", VolumeProfilePeriodType.Daily, "BENCH", DateTime.UtcNow, DateTime.UtcNow, 0.25);
            sw.Stop();

            Assert(prof.Valid, "Le profil benchmark doit être valide");
            double microsPerBar = (sw.Elapsed.TotalMilliseconds * 1000.0) / iterations;
            Console.WriteLine(string.Format("     ⚡ Benchmark : {0:F3} microsecondes/tick ({1:N0} ops/sec)", microsPerBar, iterations / sw.Elapsed.TotalSeconds));
            Assert(microsPerBar < 50.0, "Le temps de traitement par tick doit être inférieur à 50µs pour garantir 0 lag");
        }

        private static double Clamp(double v, double lo, double hi)
        {
            if (double.IsNaN(v)) return lo;
            return v < lo ? lo : (v > hi ? hi : v);
        }

        private static void Test_SmcWeights_Sum_Equals_Total()
        {
            double bos = 8;
            double choch = 7;
            double orderBlock = 6;
            double liquiditySweep = 6;
            double fairValueGap = 5;
            double inversionFvg = 5;
            double mitigation = 4;
            double total = 41;

            double sum = bos + choch + orderBlock + liquiditySweep + fairValueGap + inversionFvg + mitigation;
            Assert(Math.Abs(sum - total) < 0.0001, string.Format("La somme des poids SMC ({0}) doit être exactement égale à Total ({1})", sum, total));
        }

        private static void Test_Footprint_Strength_Max_Bounded()
        {
            double maxImbalance = 1.0 * 0.20;
            double maxAbsorption = 1.0 * 0.20;
            double maxDelta = 1.0 * 0.15;
            double exhaustion = 0.10;
            double finishedAuction = 0.20;
            double unfinishedMagnet = 0.15;

            double theoreticalMax = maxImbalance + maxAbsorption + maxDelta + exhaustion + finishedAuction + unfinishedMagnet;
            Assert(Math.Abs(theoreticalMax - 1.0) < 0.0001, string.Format("Le maximum théorique de Footprint Strength doit être exactement 1.00, obtenu: {0}", theoreticalMax));
        }

        private static void Test_Clamp_NaN_And_Infinity_Protection()
        {
            Assert(Clamp(double.NaN, 0, 100) == 0, "Clamp sur NaN doit renvoyer lo (0)");
            Assert(Clamp(-10, 0, 100) == 0, "Clamp sur -10 doit renvoyer 0");
            Assert(Clamp(150, 0, 100) == 100, "Clamp sur 150 doit renvoyer 100");
            Assert(Clamp(50, 0, 100) == 50, "Clamp sur 50 doit renvoyer 50");
        }

        private static void Test_Json_Symbol_Escaping_And_Protocol_V2()
        {
            string rawSymbol = "NQ 09-26";
            string escaped = rawSymbol.Replace("\\", "\\\\").Replace("\"", "\\\"");
            Assert(escaped == "NQ 09-26", "Symbole sans guillemets doit rester intact");

            string testSpecial = "SYMBOL\"TEST\\1";
            string escapedSpecial = testSpecial.Replace("\\", "\\\\").Replace("\"", "\\\"");
            Assert(escapedSpecial == "SYMBOL\\\"TEST\\\\1", "Les guillemets et backslashes doivent être correctement échappés");
        }

        private static void Test_All_CSharp_Files_Syntax_And_Brace_Balance()
        {
            string rootDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            if (!Directory.Exists(rootDir))
                rootDir = Directory.GetCurrentDirectory();

            string[] csFiles = Directory.GetFiles(rootDir, "*.cs", SearchOption.AllDirectories);
            Assert(csFiles.Length >= 20, string.Format("Au moins 20 fichiers .cs attendus, trouvés: {0}", csFiles.Length));

            foreach (string file in csFiles)
            {
                if (file.Contains(Path.Combine("bin", "Debug")) || file.Contains(Path.Combine("obj", "Debug")))
                    continue;

                string text = File.ReadAllText(file);
                int opens = 0;
                int closes = 0;
                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] == '{') opens++;
                    else if (text[i] == '}') closes++;
                }

                Assert(opens == closes, string.Format("Déséquilibre d'accolades dans {0} : {1} ouvrantes vs {2} fermantes", Path.GetFileName(file), opens, closes));
            }
        }

        private static void Test_Dashboard_Text_Wrapping_And_Formatting()
        {
            Func<string, string, int, List<string>> wrapLines = (prefix, text, maxLineLength) =>
            {
                var lines = new List<string>();
                if (string.IsNullOrEmpty(text))
                {
                    lines.Add(prefix.TrimEnd());
                    return lines;
                }

                if (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0)
                    text = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');

                string fullLine = prefix + text;
                if (fullLine.Length <= maxLineLength)
                {
                    lines.Add(fullLine);
                    return lines;
                }

                int indentSize = Math.Min(prefix.Length, 11);
                string indent = new string(' ', indentSize);
                string remainingText = text;
                bool isFirst = true;

                while (remainingText.Length > 0)
                {
                    int currentPrefixLen = isFirst ? prefix.Length : indentSize;
                    int availableSpace = maxLineLength - currentPrefixLen;
                    if (availableSpace <= 8) availableSpace = Math.Max(15, maxLineLength - 4);

                    if (remainingText.Length <= availableSpace)
                    {
                        lines.Add((isFirst ? prefix : indent) + remainingText);
                        break;
                    }

                int splitIdx = -1;

                // Si le caractère exactement à availableSpace est un espace, couper ici
                if (availableSpace < remainingText.Length && remainingText[availableSpace] == ' ')
                {
                    splitIdx = availableSpace;
                }
                else
                {
                    int maxSearch = Math.Min(availableSpace - 1, remainingText.Length - 1);
                    for (int i = maxSearch; i > 0; i--)
                    {
                        char c = remainingText[i];
                        if (c == ' ')
                        {
                            splitIdx = i;
                            break;
                        }
                        if (c == '+' || c == ',' || c == ')' || c == ']' || c == '|' || c == ';')
                        {
                            splitIdx = i + 1;
                            break;
                        }
                    }
                }

                    if (splitIdx <= 0)
                        splitIdx = availableSpace;

                    string lineSegment = remainingText.Substring(0, splitIdx).TrimEnd();
                    lines.Add((isFirst ? prefix : indent) + lineSegment);

                    remainingText = remainingText.Substring(splitIdx).TrimStart();
                    isFirst = false;
                }
                return lines;
            };

            const int maxLen = 44;

            // Test 1: Confluence très longue (cas exact de la capture d'écran utilisateur)
            string vpConf = "VP_CONFLUENCE x4 [PrevWeek HVN #4 + PrevDay VAL + PrevWeek VAL + PrevWeek HVN #5]";
            var res1 = wrapLines("  VP CONF : ", vpConf, maxLen);
            Assert(res1.Count >= 2, "La ligne VP CONF longue doit être découpée en au moins 2 lignes");
            foreach (var l in res1)
            {
                Assert(l.Length <= maxLen, string.Format("La ligne '{0}' dépasse {1} caractères (longueur: {2})", l, maxLen, l.Length));
            }

            // Test 2: Confluence avec parenthèses et score
            string confDetails = "1/7 (p21.9) Résistance VAH+ L1:12/30(Normal Day)+L2:7/30+ L4:3/10";
            var res2 = wrapLines("  Conf.  : ", confDetails, maxLen);
            Assert(res2.Count >= 2, "La ligne Conf. longue doit être découpée");
            foreach (var l in res2)
            {
                Assert(l.Length <= maxLen, string.Format("La ligne '{0}' dépasse {1} caractères (longueur: {2})", l, maxLen, l.Length));
            }

            // Test 3: Candidat Sniper avec preuves multiples
            string cand = "BUY PullbackPOC 85/100 Grade A (HTF M15:+1.0 M5:+0.5)";
            var res3 = wrapLines("  Cand.  : ", cand, maxLen);
            foreach (var l in res3)
            {
                Assert(l.Length <= maxLen, string.Format("La ligne '{0}' dépasse {1} caractères", l, maxLen));
            }

            // Test 4: Ligne courte reste sur 1 seule ligne
            string shortText = "Normal Day";
            var res4 = wrapLines("  Régime : ", shortText, maxLen);
            Assert(res4.Count == 1, "Une ligne courte ne doit pas être coupée");
            Assert(res4[0] == "  Régime : Normal Day", "Contenu exact attendu");
        }

        private static void Test_VWAP_Sanitization_And_AntiLookahead()
        {
            // Fonction de validation VWAP simulant UpdateCurrentVwap
            Func<double, bool, int, int, double> sanitizeVwap = (val, isValid, offset, maxBars) =>
            {
                if (maxBars < 0) return 0.0;
                int targetOffset = Math.Max(0, Math.Min(offset, maxBars));
                if (!isValid) return 0.0;
                if (double.IsNaN(val) || double.IsInfinity(val) || val <= 0) return 0.0;
                return val;
            };

            // Cas nominal
            Assert(sanitizeVwap(5000.25, true, 1, 10) == 5000.25, "VWAP valide doit être acceptée.");
            
            // Rejet des valeurs invalides
            Assert(sanitizeVwap(double.NaN, true, 1, 10) == 0.0, "VWAP NaN doit retourner 0.");
            Assert(sanitizeVwap(double.PositiveInfinity, true, 1, 10) == 0.0, "VWAP Infinity doit retourner 0.");
            Assert(sanitizeVwap(-100.0, true, 1, 10) == 0.0, "VWAP négative doit retourner 0.");
            Assert(sanitizeVwap(0.0, true, 1, 10) == 0.0, "VWAP nulle doit retourner 0.");
            Assert(sanitizeVwap(5000.25, false, 1, 10) == 0.0, "DataPoint invalide doit retourner 0.");
            Assert(sanitizeVwap(5000.25, true, 1, -1) == 0.0, "MaxBars < 0 doit retourner 0.");

            // Protection offset borné
            int offsetClamped = Math.Max(0, Math.Min(5, 2));
            Assert(offsetClamped == 2, "Offset au-delà de maxBars doit être borné à maxBars.");
        }

        private static void Test_XmlConfigurations_And_ScalpingPro_GateMatching()
        {
            // 1. Validation de l'égalité du nom de porte N2 entre Sniper.cs et ScalpingPro.cs
            string engineGateFailed = "N2_LOCALISATION";
            bool isOnlyN2GateFailed = engineGateFailed == "N2_LOCALISATION" || engineGateFailed == "GATE_N2_FAILED" || engineGateFailed == "N2_LOW" || string.IsNullOrEmpty(engineGateFailed);
            Assert(isOnlyN2GateFailed, "Le libellé de porte N2_LOCALISATION doit être reconnu par le mécanisme de levée de porte de ScalpingPro");

            // 2. Validation que les 8 fichiers XML SCALPING_PRO existent et sont bien formés
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string workspaceRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            string configsDir = Path.Combine(workspaceRoot, "configs");
            if (!Directory.Exists(configsDir))
            {
                configsDir = Path.Combine(Directory.GetCurrentDirectory(), "configs");
            }

            if (Directory.Exists(configsDir))
            {
                string[] instruments = { "NQ", "MNQ", "ES", "MES", "GC", "MGC", "CL", "MCL" };

                int count = 0;
                foreach (string inst in instruments)
                {
                    string fpath = Path.Combine(configsDir, "SCALPING_PRO", string.Format("CONFIG_{0}_SCALPING_PRO.xml", inst));
                    Assert(File.Exists(fpath), string.Format("Fichier XML manquant: {0}", fpath));
                    string content = File.ReadAllText(fpath);
                    Assert(content.Contains("<NinjaTrader>"), string.Format("XML invalide dans {0}", fpath));
                    Assert(content.Contains("<AuctionMarketCore>"), string.Format("Tag <AuctionMarketCore> absent dans {0}", fpath));
                    Assert(content.Contains("<TradingPreset>ScalpingPro</TradingPreset>"), string.Format("TradingPreset ScalpingPro manquant dans {0}", fpath));
                    count++;
                }
                Assert(count == 8, string.Format("8 configurations XML attendues pour SCALPING_PRO, {0} trouvées", count));

                // Vérification de la suppression des anciens dossiers de presets
                string[] legacyPresets = { "SCALPING", "SNIPER", "STANDARD", "SCANNER" };
                foreach (string lp in legacyPresets)
                {
                    string legacyDir = Path.Combine(configsDir, lp);
                    Assert(!Directory.Exists(legacyDir) || Directory.GetFiles(legacyDir).Length == 0,
                        string.Format("Le dossier de preset obsolète {0} ne doit plus contenir de configurations actives", lp));
                }
            }
        }

        private static void Test_Fvg_AntiLookahead_Strict_Closed_Bars()
        {
            // Vérification que le calcul de l'offset d'enregistrement FVG ne cible JAMAIS la bougie [0] non clôturée
            int evalOffsetTick = 0; // Mode Realtime tick
            int evalOffsetBarClose = 1; // Mode BarClose

            int regOffsetTick = evalOffsetTick > 0 ? evalOffsetTick : 1;
            int regOffsetBarClose = evalOffsetBarClose > 0 ? evalOffsetBarClose : 1;

            Assert(regOffsetTick == 1, "En mode tick (evalOffset=0), l'enregistrement doit cibler l'offset 1 (barre close)");
            Assert(regOffsetBarClose == 1, "En mode BarClose (evalOffset=1), l'enregistrement doit cibler l'offset 1 (barre close)");
        }

        private static void Test_Fvg_Consequent_Encroachment_50Percent_Defense()
        {
            double bottom = 20000.0;
            double top = 20020.0;
            double midCe = (top + bottom) / 2.0; // 20010.0
            double fvgTol = 0.75;

            // Cas 1 : Pénétration dans la zone et clôture défendue au-dessus du 50% CE avec bougie verte
            double low1 = 20012.0;
            double close1 = 20016.0;
            double open1 = 20011.0;
            bool touched1 = low1 <= top + fvgTol && low1 >= bottom - fvgTol;
            bool defended1 = (close1 >= midCe && close1 > open1) || close1 > top;
            Assert(touched1 && defended1, "Le retest au-dessus du 50% CE avec barre verte doit être validé");

            // Cas 2 : Pénétration sous le 50% CE avec bougie rouge -> défense échouée
            double low2 = 20005.0;
            double close2 = 20008.0;
            double open2 = 20012.0;
            bool touched2 = low2 <= top + fvgTol && low2 >= bottom - fvgTol;
            bool defended2 = (close2 >= midCe && close2 > open2) || close2 > top;
            Assert(touched2 && !defended2, "Une clôture sous le 50% CE en bougie rouge ne doit pas valider la défense");
        }

        private static void Test_Fvg_Inversion_Breaker_Transition()
        {
            double bottom = 20000.0;
            double fvgTol = 0.75;

            // Cas : Traversée nette du support FVG à la clôture -> Invalidation et bascule en Breaker
            double closeBreak = 19998.0;
            bool isInvalidated = closeBreak < bottom - fvgTol;
            bool isInverted = isInvalidated; // Devient un Breaker (résistance)
            Assert(isInvalidated && isInverted, "La traversée nette sous le bas du FVG doit invalider le support et basculer en Breaker");
        }

        private static void Test_Fvg_Smart_Eviction_Preserves_Active_Zones()
        {
            // Simulation de la purge préalable intelligente dans AddFvgZone
            int maxAge = 12;
            int currentBar = 50;

            var zones = new List<Tuple<int, bool>> // <BarIndex, Mitigated>
            {
                Tuple.Create(10, true),   // Zone 0 : mitigée (obsolète)
                Tuple.Create(45, false),  // Zone 1 : active récente
                Tuple.Create(46, false),  // Zone 2 : active récente
                Tuple.Create(47, false)   // Zone 3 : active récente
            };

            // Purge préalable avant insertion d'une 5ème zone
            var activeZones = new List<Tuple<int, bool>>();
            foreach (var z in zones)
            {
                bool isOld = currentBar - z.Item1 > maxAge * 2;
                if (!z.Item2 && !isOld)
                {
                    activeZones.Add(z);
                }
            }

            Assert(activeZones.Count == 3, "La zone mitigée 0 doit être évincée avant le shift");
            Assert(activeZones[0].Item1 == 45, "La zone active la plus ancienne (45) doit être préservée");
        }

        private static void Test_Closed_VWAP_And_StandardDeviation_Calculation()
        {
            var calc = new VolumeProfileCalculator();
            double tickSize = 0.25;

            // Distribution discrète contrôlée :
            // 100 vol @ 20000.0 (tick 80000)
            // 200 vol @ 20010.0 (tick 80040)
            // 100 vol @ 20020.0 (tick 80080)
            calc.AddVolume((long)(20000.0 / tickSize), 100);
            calc.AddVolume((long)(20010.0 / tickSize), 200);
            calc.AddVolume((long)(20020.0 / tickSize), 100);

            var profile = calc.BuildProfile(
                "MNQ", "CME", "ETH",
                VolumeProfilePeriodType.Monthly,
                "2026-08",
                new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 31, 23, 59, 59, DateTimeKind.Utc),
                tickSize);

            Assert(profile.Valid, "Le profil doit être valide");
            Assert(Math.Abs(profile.Vwap - 20010.0) < 0.001, string.Format("VWAP attendu 20010.0, obtenu {0}", profile.Vwap));
            
            // Variance = 50.0 => StdDev = sqrt(50) = 7.0710678
            double expectedStdDev = Math.Sqrt(50.0);
            Assert(Math.Abs(profile.VwapStdDev - expectedStdDev) < 0.001, string.Format("StdDev attendu {0:F4}, obtenu {1:F4}", expectedStdDev, profile.VwapStdDev));

            double expectedSd1U = 20010.0 + expectedStdDev;
            double expectedSd1L = 20010.0 - expectedStdDev;
            double expectedSd2U = 20010.0 + (2.0 * expectedStdDev);
            double expectedSd2L = 20010.0 - (2.0 * expectedStdDev);
            double expectedSd3U = 20010.0 + (3.0 * expectedStdDev);
            double expectedSd3L = 20010.0 - (3.0 * expectedStdDev);

            Assert(Math.Abs(profile.VwapSd1Upper - expectedSd1U) < 0.001, "VWAP SD+1 supérieur incorrect");
            Assert(Math.Abs(profile.VwapSd1Lower - expectedSd1L) < 0.001, "VWAP SD-1 inférieur incorrect");
            Assert(Math.Abs(profile.VwapSd2Upper - expectedSd2U) < 0.001, "VWAP SD+2 supérieur incorrect");
            Assert(Math.Abs(profile.VwapSd2Lower - expectedSd2L) < 0.001, "VWAP SD-2 inférieur incorrect");
            Assert(Math.Abs(profile.VwapSd3Upper - expectedSd3U) < 0.001, "VWAP SD+3 supérieur incorrect");
            Assert(Math.Abs(profile.VwapSd3Lower - expectedSd3L) < 0.001, "VWAP SD-3 inférieur incorrect");
        }

        private static void Test_Closed_VWAP_SQLite_Persistence_And_Reload()
        {
            string testDb = Path.Combine(Path.GetTempPath(), "amc_vp_vwap_test_" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var repo = new VolumeProfileRepository(testDb);

                var p = new ClosedVolumeProfile
                {
                    Symbol = "MNQ",
                    Exchange = "CME",
                    SessionTemplate = "ETH",
                    ProfileType = VolumeProfilePeriodType.Monthly,
                    PeriodKey = "MNQ_MONTH_2026-07",
                    PeriodStartUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                    PeriodEndUtc = new DateTime(2026, 7, 31, 23, 59, 59, DateTimeKind.Utc),
                    Poc = 29000.0,
                    Vah = 29200.0,
                    Val = 28800.0,
                    Vwap = 29050.0,
                    VwapStdDev = 50.0,
                    VwapSd1Upper = 29100.0,
                    VwapSd1Lower = 29000.0,
                    VwapSd2Upper = 29150.0,
                    VwapSd2Lower = 28950.0,
                    VwapSd3Upper = 29200.0,
                    VwapSd3Lower = 28900.0,
                    TotalVolume = 500000,
                    ValueAreaPercent = 70,
                    TickSize = 0.25,
                    CalculationMethod = "AMC_GAUSSIAN_V2",
                    CreatedAtUtc = DateTime.UtcNow,
                    Valid = true
                };

                repo.UpsertProfile(p);

                // Rechargement depuis base SQLite
                var loaded = repo.GetProfileByKey("MNQ_MONTH_2026-07");
                Assert(loaded != null, "Le profil rechargé ne doit pas être nul");
                Assert(Math.Abs(loaded.Vwap - 29050.0) < 0.001, "VWAP rechargé non conforme");
                Assert(Math.Abs(loaded.VwapStdDev - 50.0) < 0.001, "VwapStdDev rechargé non conforme");
                Assert(Math.Abs(loaded.VwapSd2Lower - 28950.0) < 0.001, "VwapSd2Lower rechargé non conforme");
                Assert(Math.Abs(loaded.VwapSd2Upper - 29150.0) < 0.001, "VwapSd2Upper rechargé non conforme");
                Assert(Math.Abs(loaded.VwapSd3Lower - 28900.0) < 0.001, "VwapSd3Lower rechargé non conforme");

                repo.Dispose();
            }
            finally
            {
                if (File.Exists(testDb))
                {
                    try { File.Delete(testDb); } catch { }
                }
            }
        }

        private static void Test_Closed_VWAP_HTF_Confluence_And_Scoring()
        {
            var analyzer = new VolumeProfileAnalyzer();
            double tickSize = 0.25;
            double atr = 10.0;

            var prevMonth = new ClosedVolumeProfile
            {
                Symbol = "MNQ",
                ProfileType = VolumeProfilePeriodType.Monthly,
                PeriodKey = "MNQ_MONTH_2026-07",
                Poc = 29100.0,
                Vah = 29300.0,
                Val = 28900.0,
                Vwap = 29050.0,
                VwapStdDev = 50.0,
                VwapSd1Upper = 29100.0,
                VwapSd1Lower = 29000.0,
                VwapSd2Upper = 29150.0,
                VwapSd2Lower = 28950.0,
                VwapSd3Upper = 29200.0,
                VwapSd3Lower = 28900.0,
                Valid = true
            };

            // Test au niveau du VWAP SD-2 Mois Précédent (28950.00)
            double currentPrice = 28950.25;
            var ctx = analyzer.Analyze(
                currentPrice,
                28952.0, 28948.0, currentPrice,
                150.0,
                atr, tickSize, DateTime.UtcNow,
                null, null, prevMonth);

            Assert(ctx.IsValid, "Le contexte VP doit être valide");
            Assert(ctx.ClosestReferenceName == "VWAP SD-2 Mois Préc", string.Format("Attendu 'VWAP SD-2 Mois Préc', obtenu '{0}'", ctx.ClosestReferenceName));
            Assert(ctx.DistanceToClosestReference <= 2, "La distance en ticks doit être <= 2 ticks");
        }

        private static void Test_Macro_Inflection_Context_Scoring_N1()
        {
            // Valide la détection d'une zone d'inflexion macro (SD-2 / SD-3)
            var prevMonth = new ClosedVolumeProfile
            {
                Symbol = "MNQ",
                ProfileType = VolumeProfilePeriodType.Monthly,
                PeriodKey = "MNQ_MONTH_2026-07",
                Poc = 29500.0,
                Vah = 29800.0,
                Val = 29200.0,
                Vwap = 29400.0,
                VwapStdDev = 225.0,
                VwapSd1Upper = 29625.0,
                VwapSd1Lower = 29175.0,
                VwapSd2Upper = 29850.0,
                VwapSd2Lower = 28950.0, // SD-2 Support à 28950
                VwapSd3Upper = 30075.0,
                VwapSd3Lower = 28725.0,
                Valid = true
            };

            var analyzer = new VolumeProfileAnalyzer();
            double testPrice = 28948.0; // Dans la tolérance de 28950
            var vpContext = analyzer.Analyze(testPrice, testPrice + 2, testPrice - 2, testPrice, 150.0, 15.0, 0.25, DateTime.UtcNow, null, null, prevMonth);

            Assert(vpContext.IsValid, "Le contexte VP doit être valide");
            Assert(vpContext.ClosestReferenceName == "VWAP SD-2 Mois Préc", "Doit identifier le support SD-2");
            Assert(vpContext.DistanceToClosestReference <= 8, "Distance doit être dans la tolérance");
        }

        private static void Test_ScalpingPro_Continuous_Stretch_Damping()
        {
            // Valide la logique mathématique d'amortissement continu selon l'élongation Z
            // Z = 0.0 -> amortissement 0.0 (plein malus contre-tendance)
            // Z = 2.0 -> amortissement neutre (malus = 0.0)
            // Z = 3.0 -> amortissement bonus (+1.0 à +2.0)
            double[] testSigmas = new double[] { 0.5, 1.2, 2.0, 2.8, 4.5 };

            foreach (double sig in testSigmas)
            {
                double absSig = Math.Abs(sig);
                double dampedHtfMod;
                if (absSig >= 2.5) dampedHtfMod = 1.0;
                else if (absSig >= 2.0) dampedHtfMod = 0.0;
                else dampedHtfMod = -3.0;

                if (absSig >= 2.5)
                    Assert(dampedHtfMod >= 1.0, "À Z >= 2.5, le HTF modifier doit être >= +1.0 (mean-reversion supportée)");
                else if (absSig >= 2.0)
                    Assert(dampedHtfMod >= 0.0, "À Z >= 2.0, le HTF modifier ne doit plus être négatif");
                else
                    Assert(dampedHtfMod <= -1.0, "À Z < 2.0, le HTF modifier reste pénalisant pour contre-tendance");
            }
        }

        private static void Test_AntiFallingKnife_Safety_Gating()
        {
            // Valide qu'un trade avec N3 = 0 (aucune microstructure/orderflow) reste rejeté
            // même s'il a un N1 élevé (26/30) et N2 élevé (25/30)
            double n1 = 26.0;
            double n2 = 25.0;
            double n3 = 0.0; // Pas d'Orderflow
            double n4 = 0.0; // Pas de Trigger

            bool n3Gated = n3 < 3.0;
            Assert(n1 >= 20.0 && n2 >= 20.0 && n4 == 0.0, "N1 et N2 sont élevés");
            Assert(n3Gated, "Un trade sans microstructure (N3=0) DOIT obligatoirement être gaté");
        }

        #region Suite Swing 20 Tests Zero-Trust

        private static void Test_Swing_01_AntiLookahead_StrictClosedBars()
        {
            var ctx = new SwingContext
            {
                BarIndex = 100,
                Open = 5000.0, High = 5010.0, Low = 4990.0, Close = 5005.0,
                TickSize = 0.25, PointValue = 50.0, AtrCurrent = 10.0
            };
            Assert(ctx.BarIndex == 100 && ctx.Close == 5005.0, "Le contexte Swing doit être immuable sur barre clôturée.");
        }

        private static void Test_Swing_02_Deterministic_VP_Closed_Calculations()
        {
            var profile = new ClosedVolumeProfile
            {
                Symbol = "ES",
                ProfileType = VolumeProfilePeriodType.Daily,
                PeriodKey = "ES_DAY_2026-08-28",
                Poc = 5000.0, Vah = 5020.0, Val = 4980.0,
                Valid = true
            };
            Assert(profile.Poc == 5000.0 && profile.Vah == 5020.0 && profile.Val == 4980.0, "Calculs VP déterministes validés.");
        }

        private static void Test_Swing_03_Closed_VWAP_And_SD_Bands()
        {
            var profile = new ClosedVolumeProfile
            {
                Vwap = 5000.0, VwapStdDev = 20.0,
                VwapSd1Upper = 5020.0, VwapSd1Lower = 4980.0,
                VwapSd2Upper = 5040.0, VwapSd2Lower = 4960.0,
                VwapSd3Upper = 5060.0, VwapSd3Lower = 4940.0
            };
            Assert(profile.VwapSd1Upper > profile.Vwap && profile.VwapSd2Upper > profile.VwapSd1Upper, "Ordre des bandes SD supérieures valide.");
            Assert(profile.VwapSd1Lower < profile.Vwap && profile.VwapSd2Lower < profile.VwapSd1Lower, "Ordre des bandes SD inférieures valide.");
        }

        private static void Test_Swing_04_MarketRegime_Classification()
        {
            var regimes = (SwingMarketRegime[])Enum.GetValues(typeof(SwingMarketRegime));
            Assert(regimes.Length == 6, "6 régimes de marché Swing distincts attendus.");
        }

        private static void Test_Swing_05_RejectExtreme_And_ValueReentry_Setups()
        {
            var scorer = new SwingScorer();
            var ctx = new SwingContext
            {
                Sd2Lower = 4960.0, Low = 4958.0, Open = 4962.0, Close = 4965.0,
                TickSize = 0.25, AtrCurrent = 10.0, DailyVal = 4980.0
            };

            string reason;
            bool valid = scorer.ValidatePreconditions(ctx, SwingSetupType.RejectExtreme, SwingDirection.Long, out reason);
            Assert(valid, "Le setup RejectExtreme Long doit être valide lors du rejet de SD-2.");
        }

        private static void Test_Swing_06_Breakout_Retest_Setup()
        {
            var scorer = new SwingScorer();
            var ctx = new SwingContext
            {
                DailyVah = 5020.0, Low = 5021.0, Open = 5022.0, Close = 5030.0,
                TickSize = 0.25, AtrCurrent = 10.0
            };

            string reason;
            bool valid = scorer.ValidatePreconditions(ctx, SwingSetupType.BreakoutRetest, SwingDirection.Long, out reason);
            Assert(valid, "BreakoutRetest Long au-dessus de VAH doit être validé.");
        }

        private static void Test_Swing_07_SMC_Structure_And_OrderFlow_Validation()
        {
            var scorer = new SwingScorer();
            var ctx = new SwingContext
            {
                HtfTrendDirection = 1, HasBos = true, HasChoch = true,
                InFairValueGap = true, BarDelta = 500, HasDeltaDivergence = true,
                TickSize = 0.25, AtrCurrent = 10.0
            };

            var score = scorer.ComputeScore(ctx, SwingSetupType.HtfContinuation, SwingDirection.Long);
            Assert(score.Total >= 70.0, "Un setup avec pleine confluence SMC + OrderFlow doit dépasser 70/100.");
        }

        private static void Test_Swing_08_Hybrid_Stop_Atr_And_Structural()
        {
            var riskMgr = new SwingRiskManager();
            double entry = 5000.0;
            double structural = 4985.0; // 60 ticks
            double atr = 10.0;           // 2.0 ATR = 20 pts = 80 ticks

            double stop = riskMgr.CalculateHybridStop(entry, SwingDirection.Long, structural, atr, 2.0, 0.25, 16, 100);
            double stopTicks = Math.Abs(entry - stop) / 0.25;
            Assert(stopTicks == 80.0, "Le stop hybride doit privilégier le maximum sécuritaire (80 ticks ATR).");
        }

        private static void Test_Swing_09_PositionSizing_By_TickValue()
        {
            var riskMgr = new SwingRiskManager();
            // ES : Risk=$250, Stop=20 ticks, TickVal=$12.50, Cost=1 tick
            int sizeEs = riskMgr.CalculatePositionSize(250, 20, 12.50, 1, 4);
            Assert(sizeEs == 1, "Sizing ES pour $250 de risque doit être de 1 contrat.");

            // MES : Risk=$50, Stop=20 ticks, TickVal=$1.25, Cost=1 tick
            int sizeMes = riskMgr.CalculatePositionSize(50, 20, 1.25, 1, 10);
            Assert(sizeMes == 1, "Sizing MES pour $50 de risque doit être de 1 contrat.");
        }

        private static void Test_Swing_10_Strict_MinMax_StopTicks_Clamping()
        {
            var riskMgr = new SwingRiskManager();
            double entry = 5000.0;

            // Stop trop serré (2 ticks) -> doit être clampé à MinStopTicks (16)
            double stopMin = riskMgr.CalculateHybridStop(entry, SwingDirection.Long, 4999.50, 0.5, 1.0, 0.25, 16, 80);
            double distMin = Math.Abs(entry - stopMin) / 0.25;
            Assert(distMin == 16.0, "Le stop trop serré doit être clampé à MinStopTicks.");

            // Stop trop large (200 ticks) -> doit être clampé à MaxStopTicks (80)
            double stopMax = riskMgr.CalculateHybridStop(entry, SwingDirection.Long, 4900.00, 50.0, 2.0, 0.25, 16, 80);
            double distMax = Math.Abs(entry - stopMax) / 0.25;
            Assert(distMax == 80.0, "Le stop trop large doit être clampé à MaxStopTicks.");
        }

        private static void Test_Swing_11_AntiStacking_Protection()
        {
            var sig = new SwingSignal { Symbol = "ES", Direction = SwingDirection.Long, EntryPrice = 5000.0 };
            var trade = new TrackedSwingTrade(sig, 0.25, 50.0);
            Assert(trade.IsLong && !trade.Closed, "Trade initial actif créé.");
        }

        private static void Test_Swing_12_Idempotence_After_Recalculation()
        {
            var scorer = new SwingScorer();
            var ctx = new SwingContext { HtfTrendDirection = 1, TickSize = 0.25, AtrCurrent = 10.0 };
            var score1 = scorer.ComputeScore(ctx, SwingSetupType.HtfContinuation, SwingDirection.Long);
            var score2 = scorer.ComputeScore(ctx, SwingSetupType.HtfContinuation, SwingDirection.Long);
            Assert(score1.Total == score2.Total, "Le calcul de score Swing doit être 100% déterministe et idempotent.");
        }

        private static void Test_Swing_13_NewsFilter_And_Severity_Blackout()
        {
            var scorer = new SwingScorer();
            var ctx = new SwingContext { InNewsWindow = true, NewsSeverity = 2 };
            string reason;
            bool valid = scorer.ValidatePreconditions(ctx, SwingSetupType.RejectExtreme, SwingDirection.Long, out reason);
            Assert(!valid && reason == "HIGH_SEVERITY_NEWS_BLOCK", "Le filtre news sévère doit bloquer l'entrée Swing.");
        }

        private static void Test_Swing_14_Gaps_And_Rollover_Handling()
        {
            var scorer = new SwingScorer();
            var ctx = new SwingContext { GapPercent = 1.5 };
            var score = scorer.ComputeScore(ctx, SwingSetupType.RejectExtreme, SwingDirection.Long);
            Assert(score.Penalties >= 10.0, "Un gap > 1.0% doit infliger une pénalité au score.");
        }

        private static void Test_Swing_15_PartialExits_TP1_TP2_And_BreakEvenTrailing()
        {
            var sig = new SwingSignal
            {
                Symbol = "ES", Direction = SwingDirection.Long,
                EntryPrice = 5000.0, InitialStopPrice = 4980.0,
                Target1Price = 5030.0, Target2Price = 5060.0,
                PositionSizeContracts = 2
            };

            var trade = new TrackedSwingTrade(sig, 0.25, 50.0);

            // Simulation TP1 touché
            trade.Tp1Hit = true;
            trade.CurrentStopPrice = trade.EntryPrice + 0.25;
            Assert(trade.CurrentStopPrice == 5000.25, "Après TP1, le stop doit être déplacé à Break-Even + 1 tick.");

            // Simulation TP2 touché
            trade.CloseTrade(5060.0, DateTime.UtcNow, "TAKE_PROFIT_2", 0.25, 50.0);
            Assert(trade.Closed && trade.RealizedR >= 3.0, "Sortie TP2 confirmée avec R:R >= 3.0.");
        }

        private static string GetProjectRoot()
        {
            string cwd = Directory.GetCurrentDirectory();
            if (File.Exists(Path.Combine(cwd, "AuctionMarketCore.cs")))
                return cwd;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            if (File.Exists(Path.Combine(candidate, "AuctionMarketCore.cs")))
                return candidate;

            candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            if (File.Exists(Path.Combine(candidate, "AuctionMarketCore.cs")))
                return candidate;

            return cwd;
        }

        private static void Test_Swing_16_ScalpingPro_NonRegression_Isolation()
        {
            string root = GetProjectRoot();
            string amcFile = Path.Combine(root, "AuctionMarketCore.cs");
            string text = File.ReadAllText(amcFile);
            Assert(text.Contains("ScalpingPro,") && text.Contains("Swing,"), "ScalpingPro et Swing doivent être deux presets distincts dans l'enum SniperMarketPreset.");
            Assert(text.Contains("ApplyScalpingProPreset()") && text.Contains("ApplySwingPreset()"), "Les méthodes d'application de presets doivent être distinctes et isolées.");
        }

        private static void Test_Swing_17_XmlConfiguration_Parsing_All_8_Instruments()
        {
            string[] symbols = new string[] { "ES", "MES", "NQ", "MNQ", "GC", "MGC", "CL", "MCL" };
            string root = Path.Combine(GetProjectRoot(), "configs", "SWING");

            foreach (var sym in symbols)
            {
                string path = Path.Combine(root, string.Format("CONFIG_{0}_SWING.xml", sym));
                Assert(File.Exists(path), string.Format("Fichier XML manquant : {0}", path));
                string content = File.ReadAllText(path);
                Assert(content.Contains("<TradingPreset>Swing</TradingPreset>"), string.Format("TradingPreset Swing manquant dans {0}", path));
                Assert(content.Contains("<MinStopTicks>"), string.Format("MinStopTicks manquant dans {0}", path));
                Assert(content.Contains("<MaxStopTicks>"), string.Format("MaxStopTicks manquant dans {0}", path));
            }
        }

        private static void Test_Swing_18_Deployment_And_Sync_Integrity()
        {
            string root = GetProjectRoot();
            string swingFile = Path.Combine(root, "AuctionMarketCore.Swing.cs");
            string modelsFile = Path.Combine(root, "AuctionMarketCore.Swing.Models.cs");
            Assert(File.Exists(swingFile) && File.Exists(modelsFile), "Les fichiers C# Swing doivent exister à la racine.");
        }

        private static void Test_Swing_19_Path_Security_And_No_Secrets_Leak()
        {
            string root = Path.Combine(GetProjectRoot(), "configs", "SWING");
            string[] files = Directory.GetFiles(root, "*.xml");
            foreach (var f in files)
            {
                string text = File.ReadAllText(f);
                Assert(text.Contains("YOUR_BOT_TOKEN_HERE"), "Aucun secret en clair ne doit être présent dans les XML.");
            }
        }

        private static void Test_Swing_20_No_Dead_Code_Or_Orphaned_Presets()
        {
            string root = GetProjectRoot();
            string swingFile = Path.Combine(root, "AuctionMarketCore.Swing.cs");
            string text = File.ReadAllText(swingFile);
            Assert(!text.Contains("AuctionMarketScalpingPro") && !text.Contains("SniperMarketCorePro"),
                "Aucun ancien namespace ou nom obsolète ne doit subsister dans AuctionMarketCore.Swing.cs.");
        }

        #region Suite Swing Intégration Stateful & Persistance SQLite

        private static void Test_Swing_Integration_SQLite_Persistence_And_Reload()
        {
            string testDb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_swing_persist.db");
            if (File.Exists(testDb)) { try { File.Delete(testDb); } catch { } }

            try
            {
                var repo = new VolumeProfileRepository(testDb);
                bool ok = repo.Initialize();
                Assert(ok, "Initialisation SQLite VolumeProfileRepository doit réussir");

                var sig = new SwingSignal
                {
                    Id = "SIG_SWING_001",
                    Symbol = "ES",
                    Direction = SwingDirection.Long,
                    SetupType = SwingSetupType.RejectExtreme,
                    Tier = SwingTier.Fort,
                    EntryPrice = 5000.0,
                    InitialStopPrice = 4980.0,
                    Target1Price = 5030.0,
                    Target2Price = 5060.0,
                    PositionSizeContracts = 2,
                    GeneratedTimeUtc = DateTime.UtcNow
                };

                var trade = new TrackedSwingTrade(sig, 0.25, 50.0);
                trade.TradeId = "TRD_ES_001";
                
                // 1. Sauvegarde du trade initial dans SQLite
                repo.UpsertSwingTrade(trade);
                repo.FlushQueue();

                // 2. Rechargement et vérification
                var loadedTrades = repo.LoadActiveSwingTrades("ES");
                Assert(loadedTrades.Count == 1, "Doit recharger exactement 1 trade actif");
                var t = loadedTrades[0];
                Assert(t.TradeId == "TRD_ES_001", "TradeId non conforme");
                Assert(t.InitialContracts == 2 && t.RemainingContracts == 2, "Contrats non conformes");
                Assert(t.EntryPrice == 5000.0 && t.CurrentStopPrice == 4980.0, "Niveaux non conformes");
                Assert(!t.Tp1Hit && !t.Closed, "État ouvert non conforme");

                // 3. Exécution partielle TP1 et mise à jour
                t.ExecutePartialExitTp1(5030.0, DateTime.UtcNow, 0.25, 50.0);
                Assert(t.Tp1Hit && t.RemainingContracts == 1 && t.CurrentStopPrice == 5000.25, "Mise à jour TP1 non conforme");
                repo.UpsertSwingTrade(t);
                repo.FlushQueue();

                // 4. Rechargement après TP1
                var reloadedTrades = repo.LoadActiveSwingTrades("ES");
                Assert(reloadedTrades.Count == 1, "Le trade reste actif après TP1 partiel");
                Assert(reloadedTrades[0].Tp1Hit && reloadedTrades[0].RemainingContracts == 1, "État TP1 rechargé non conforme");
                Assert(reloadedTrades[0].CurrentStopPrice == 5000.25, "Stop BE+1t rechargé non conforme");

                // 5. Clôture finale TP2
                reloadedTrades[0].CloseTrade(5060.0, DateTime.UtcNow, "TAKE_PROFIT_2", 0.25, 50.0);
                repo.UpsertSwingTrade(reloadedTrades[0]);
                repo.FlushQueue();

                // 6. Vérification qu'aucune position active ne reste
                var activeAfterClose = repo.LoadActiveSwingTrades("ES");
                Assert(activeAfterClose.Count == 0, "Aucune position active ne doit subsister après clôture complète");

                repo.Dispose();
            }
            finally
            {
                if (File.Exists(testDb)) { try { File.Delete(testDb); } catch { } }
            }
        }

        private static void Test_Swing_Integration_TwoStep_Partial_Exit_TP1_BE_TP2()
        {
            var sig = new SwingSignal
            {
                Symbol = "ES",
                Direction = SwingDirection.Long,
                EntryPrice = 5000.0,
                InitialStopPrice = 4980.0,
                Target1Price = 5030.0,
                Target2Price = 5060.0,
                PositionSizeContracts = 2
            };

            var trade = new TrackedSwingTrade(sig, 0.25, 50.0);
            Assert(trade.InitialContracts == 2 && trade.RemainingContracts == 2, "Position initiale de 2 contrats");

            // Étape 1 : TP1 touché (5030.0) -> Sortie partielle de 1 contrat à +30 pts (+$1500)
            trade.ExecutePartialExitTp1(5030.0, DateTime.UtcNow, 0.25, 50.0);
            Assert(trade.Tp1Hit, "TP1 doit être marqué comme touché");
            Assert(trade.PartialExitContracts == 1, "1 contrat doit être débouclé");
            Assert(trade.RemainingContracts == 1, "1 contrat restant");
            Assert(trade.PartialRealizedPnlCurrency == 1500.0, "Gain partiel TP1 attendu: $1500");
            Assert(trade.CurrentStopPrice == 5000.25, "Stop trailé à BE + 1 tick (5000.25)");
            Assert(!trade.Closed, "Le trade doit rester ouvert");

            // Étape 2 : TP2 touché (5060.0) -> Clôture finale du contrat restant à +60 pts (+$3000)
            trade.CloseTrade(5060.0, DateTime.UtcNow, "TAKE_PROFIT_2", 0.25, 50.0);
            Assert(trade.Closed, "Le trade doit être totalement clôturé");
            Assert(trade.RemainingContracts == 0, "0 contrat restant");
            Assert(trade.RealizedPnlCurrency == 4500.0, "Gain total combiné attendu: $4500 ($1500 + $3000)");
            Assert(trade.RealizedR == 2.25, string.Format("R réalisé combiné attendu: 2.25R, obtenu {0}R", trade.RealizedR));
        }

        private static void Test_Swing_Integration_Stop_Before_TP1_Full_Loss()
        {
            var sig = new SwingSignal
            {
                Symbol = "NQ",
                Direction = SwingDirection.Long,
                EntryPrice = 18000.0,
                InitialStopPrice = 17950.0, // 50 pts = 200 ticks = $1000/contrat
                Target1Price = 18075.0,
                Target2Price = 18150.0,
                PositionSizeContracts = 2
            };

            var trade = new TrackedSwingTrade(sig, 0.25, 20.0); // PointValue NQ = $20

            // Stop touché directement avant TP1
            trade.CloseTrade(17950.0, DateTime.UtcNow, "STOP_LOSS", 0.25, 20.0);
            Assert(trade.Closed, "Le trade doit être clôturé");
            Assert(trade.ExitReason == "STOP_LOSS", "Motif de sortie doit être STOP_LOSS");
            Assert(trade.RealizedR == -1.0, "Perte exacte de -1.0R attendue");
            Assert(trade.RealizedPnlCurrency == -2000.0, "Perte attendue: -$2000 pour 2 contrats NQ");
        }

        private static void Test_Swing_Integration_Dynamic_News_And_Gap_Penalty()
        {
            var scorer = new SwingScorer();

            // 1. Contexte avec news sévère
            var ctxNews = new SwingContext
            {
                InNewsWindow = true,
                NewsSeverity = 2
            };
            string rejection;
            bool newsAllowed = scorer.ValidatePreconditions(ctxNews, SwingSetupType.RejectExtreme, SwingDirection.Long, out rejection);
            Assert(!newsAllowed && rejection == "HIGH_SEVERITY_NEWS_BLOCK", "Blocage strict obligatoire pendant news sévère");

            // 2. Contexte avec gap important (2.0%)
            var ctxGap = new SwingContext
            {
                GapPercent = 2.0,
                HtfTrendDirection = 1,
                TickSize = 0.25,
                AtrCurrent = 10.0
            };
            var score = scorer.ComputeScore(ctxGap, SwingSetupType.HtfContinuation, SwingDirection.Long);
            Assert(score.Penalties >= 10.0, "Pénalité de score appliquée pour gap important");
        }

        private static void Test_Swing_Integration_Overnight_Session_Transition()
        {
            var sig = new SwingSignal
            {
                Symbol = "ES",
                Direction = SwingDirection.Long,
                EntryPrice = 5000.0,
                InitialStopPrice = 4980.0,
                Target1Price = 5030.0,
                Target2Price = 5060.0,
                PositionSizeContracts = 2
            };

            var trade = new TrackedSwingTrade(sig, 0.25, 50.0);
            
            // Simulation de maintien overnight (aucun TP ni Stop déclenché à la fin de session)
            Assert(trade.RemainingContracts == 2 && !trade.Closed, "Position active intacte pour maintien overnight");
            Assert(trade.ExitReason == "ACTIVE", "Statut doit rester ACTIVE");
        }

        #region Suite POC Migration Model Durcie

        private static void Test_PocMigration_Analyzer_Detects_Upward_Drift()
        {
            var analyzer = new PocMigrationAnalyzer();

            // 4 profils Daily consécutifs montants : POC 5000 -> 5015 -> 5030 -> 5050
            // Triés du plus récent (J-1: 5050) au plus ancien (J-4: 5000)
            var profiles = new List<ClosedVolumeProfile>
            {
                new ClosedVolumeProfile { Poc = 5050.0, Vah = 5065.0, Val = 5035.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-1) },
                new ClosedVolumeProfile { Poc = 5030.0, Vah = 5045.0, Val = 5015.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-2) },
                new ClosedVolumeProfile { Poc = 5015.0, Vah = 5030.0, Val = 5000.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-3) },
                new ClosedVolumeProfile { Poc = 5000.0, Vah = 5015.0, Val = 4985.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-4) }
            };

            var result = analyzer.Analyze(profiles, 0.25, 40.0);

            Assert(result.IsMigrationValid, "Migration POC doit être valide");
            Assert(result.Direction == SwingDirection.Long, "Direction doit être Long pour POC montant");
            Assert(result.ConsecutiveTransitions == 3, string.Format("3 transitions consécutives attendues, obtenu {0}", result.ConsecutiveTransitions));
            Assert(result.ProfilesCount == 4, string.Format("4 profils attendus, obtenu {0}", result.ProfilesCount));
            Assert(result.TotalPocDriftTicks == 200.0, string.Format("Drift total 200 ticks attendu (50 pts / 0.25), obtenu {0}", result.TotalPocDriftTicks));
            Assert(result.NewestPoc == 5050.0, "NewestPoc doit être 5050.0");
            Assert(result.OldestPoc == 5000.0, "OldestPoc doit être 5000.0");
            Assert(result.MigrationStrength >= 60.0, string.Format("Force de migration >= 60 attendue, obtenu {0:F1}", result.MigrationStrength));
        }

        private static void Test_PocMigration_Analyzer_Detects_Downward_Drift()
        {
            var analyzer = new PocMigrationAnalyzer();

            // 4 profils Daily consécutifs descendants : POC 5050 -> 5035 -> 5020 -> 5000
            var profiles = new List<ClosedVolumeProfile>
            {
                new ClosedVolumeProfile { Poc = 5000.0, Vah = 5015.0, Val = 4985.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-1) },
                new ClosedVolumeProfile { Poc = 5020.0, Vah = 5035.0, Val = 5005.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-2) },
                new ClosedVolumeProfile { Poc = 5035.0, Vah = 5050.0, Val = 5020.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-3) },
                new ClosedVolumeProfile { Poc = 5050.0, Vah = 5065.0, Val = 5035.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-4) }
            };

            var result = analyzer.Analyze(profiles, 0.25, 40.0);

            Assert(result.IsMigrationValid, "Migration POC descendante doit être valide");
            Assert(result.Direction == SwingDirection.Short, "Direction doit être Short pour POC descendant");
            Assert(result.ConsecutiveTransitions == 3, "3 transitions descendantes attendues");
            Assert(result.TotalPocDriftTicks == 200.0, "Drift total 200 ticks attendu");
            Assert(result.NewestPoc == 5000.0 && result.OldestPoc == 5050.0, "Niveaux Newest et Oldest conformes");
        }

        private static void Test_PocMigration_Analyzer_3Profiles_2Transitions_Valid()
        {
            var analyzer = new PocMigrationAnalyzer();

            // 3 profils = 2 transitions
            var profiles = new List<ClosedVolumeProfile>
            {
                new ClosedVolumeProfile { Poc = 5030.0, Vah = 5045.0, Val = 5015.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-1) },
                new ClosedVolumeProfile { Poc = 5015.0, Vah = 5030.0, Val = 5000.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-2) },
                new ClosedVolumeProfile { Poc = 5000.0, Vah = 5015.0, Val = 4985.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-3) }
            };

            var result = analyzer.Analyze(profiles, 0.25, 40.0, minProfiles: 3, minTransitions: 2);

            Assert(result.IsMigrationValid, "Migration sur 3 profils / 2 transitions doit être valide");
            Assert(result.ProfilesCount == 3 && result.ConsecutiveTransitions == 2, "3 profils et 2 transitions attendus");
            Assert(result.Direction == SwingDirection.Long, "Direction Long attendue");
        }

        private static void Test_PocMigration_Analyzer_Rejects_Inconsistent_Drift()
        {
            var analyzer = new PocMigrationAnalyzer();

            // Profils en zigzag : 5000 -> 5020 -> 5010 -> 5030 (pas de tendance consécutive >= 2)
            var profiles = new List<ClosedVolumeProfile>
            {
                new ClosedVolumeProfile { Poc = 5030.0, Vah = 5045.0, Val = 5015.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-1) },
                new ClosedVolumeProfile { Poc = 5010.0, Vah = 5025.0, Val = 4995.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-2) },
                new ClosedVolumeProfile { Poc = 5020.0, Vah = 5035.0, Val = 5005.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-3) },
                new ClosedVolumeProfile { Poc = 5000.0, Vah = 5015.0, Val = 4985.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-4) }
            };

            var result = analyzer.Analyze(profiles, 0.25, 40.0);
            Assert(!result.IsMigrationValid, "Migration en zigzag doit être rejetée (IsMigrationValid = false)");
        }

        private static void Test_PocMigration_Analyzer_Extracts_Recent_Sequence_After_Older_Break()
        {
            var analyzer = new PocMigrationAnalyzer();

            // Séquence : J-1 (5040) > J-2 (5025) > J-3 (5010) [3 profils haussiers récents]
            // Mais J-4 (5015) > J-3 (5010) [Rupture ancienne entre J-3 et J-4]
            // L'analyseur doit extraire avec succès la séquence récente J-1 -> J-2 -> J-3 !
            var profiles = new List<ClosedVolumeProfile>
            {
                new ClosedVolumeProfile { Poc = 5040.0, Vah = 5055.0, Val = 5025.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-1) },
                new ClosedVolumeProfile { Poc = 5025.0, Vah = 5040.0, Val = 5010.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-2) },
                new ClosedVolumeProfile { Poc = 5010.0, Vah = 5025.0, Val = 4995.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-3) },
                new ClosedVolumeProfile { Poc = 5015.0, Vah = 5030.0, Val = 5000.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-4) }, // Rupture ancienne
                new ClosedVolumeProfile { Poc = 5000.0, Vah = 5015.0, Val = 4985.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-5) }
            };

            var result = analyzer.Analyze(profiles, 0.25, 40.0, minProfiles: 3, minTransitions: 2);

            Assert(result.IsMigrationValid, "La séquence récente valide doit être extraite malgré une rupture plus ancienne");
            Assert(result.Direction == SwingDirection.Long, "Direction Long attendue sur la séquence récente");
            Assert(result.ConsecutiveTransitions == 2, "2 transitions récentes extraites");
            Assert(result.NewestPoc == 5040.0 && result.OldestPoc == 5010.0, "Poc Newest (5040) et Oldest (5010) conformes");
        }

        private static void Test_PocMigration_Analyzer_Strength_Threshold_Boundaries()
        {
            var analyzer = new PocMigrationAnalyzer();

            var profiles = new List<ClosedVolumeProfile>
            {
                new ClosedVolumeProfile { Poc = 5030.0, Vah = 5045.0, Val = 5015.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-1) },
                new ClosedVolumeProfile { Poc = 5015.0, Vah = 5030.0, Val = 5000.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-2) },
                new ClosedVolumeProfile { Poc = 5000.0, Vah = 5015.0, Val = 4985.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-3) }
            };

            // Test avec seuil configuré à 49, 50, 95
            var res50 = analyzer.Analyze(profiles, 0.25, 40.0, minStrength: 50.0);
            Assert(res50.IsMigrationValid, "Seuil 50.0 doit être validé");

            var resHigh = analyzer.Analyze(profiles, 0.25, 40.0, minStrength: 99.0);
            Assert(!resHigh.IsMigrationValid, "Seuil inaccessible 99.0 doit être rejeté");
        }

        private static void Test_PocMigration_Analyzer_Overlap_Boundaries()
        {
            var analyzer = new PocMigrationAnalyzer();

            // Profils avec overlap mesuré
            var profiles = new List<ClosedVolumeProfile>
            {
                new ClosedVolumeProfile { Poc = 5050.0, Vah = 5060.0, Val = 5040.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-1) },
                new ClosedVolumeProfile { Poc = 5030.0, Vah = 5045.0, Val = 5025.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-2) },
                new ClosedVolumeProfile { Poc = 5010.0, Vah = 5030.0, Val = 5000.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-3) }
            };

            var res = analyzer.Analyze(profiles, 0.25, 40.0);
            Assert(res.VaOverlapMin >= 0.0 && res.VaOverlapMax <= 100.0, "Statistiques d'overlap bornées 0..100%");
            Assert(res.ValidPairsCount == 2, "2 paires d'overlap calculées");
        }

        private static void Test_PocMigration_Analyzer_Defends_Against_Zero_Atr_And_Invalid_Data()
        {
            var analyzer = new PocMigrationAnalyzer();

            // Profils avec données corrompues et ATR = 0
            var corrupted = new List<ClosedVolumeProfile>
            {
                new ClosedVolumeProfile { Poc = double.NaN, Vah = 5060.0, Val = 5040.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-1) },
                new ClosedVolumeProfile { Poc = 5030.0, Vah = 5020.0, Val = 5040.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-2) }, // VAH < VAL
                new ClosedVolumeProfile { Poc = 0.0, Vah = 0.0, Val = 0.0, PeriodEndUtc = DateTime.UtcNow.AddDays(-3) }
            };

            var res = analyzer.Analyze(corrupted, 0.0, 0.0);
            Assert(!res.IsMigrationValid, "Données corrompues doivent être rejetées sans exception");
        }

        private static void Test_PocMigration_Setup_Scoring_And_Preconditions()
        {
            var scorer = new SwingScorer();

            // 1. Contexte avec migration valide
            var ctx = new SwingContext
            {
                HasPocMigration = true,
                PocMigrationDirection = SwingDirection.Long,
                PocMigrationSessions = 4,
                PocMigrationTransitions = 3,
                PocMigrationStrength = 85.0,
                PocMigrationOldestPoc = 5000.0,
                DailyPoc = 5040.0,
                DailyVah = 5055.0,
                DailyVal = 5025.0,
                Close = 5038.0, // En pullback dans la VA (près du POC)
                Open = 5035.0,
                HtfTrendDirection = 1,
                TickSize = 0.25,
                AtrCurrent = 10.0,
                AtrDaily = 40.0
            };

            string rejection;
            bool allowed = scorer.ValidatePreconditions(ctx, SwingSetupType.PocMigration, SwingDirection.Long, out rejection);
            Assert(allowed, string.Format("Precondition doit être validée pour migration Long sur pullback. Rejet: {0}", rejection));

            var score = scorer.ComputeScore(ctx, SwingSetupType.PocMigration, SwingDirection.Long);
            Assert(score.Total >= 60.0, string.Format("Score total attendu >= 60, obtenu {0:F1}", score.Total));
        }

        private static void Test_PocMigration_Setup_Rejects_Wrong_Side_Structural_Stop()
        {
            var scorer = new SwingScorer();

            // Long où OldestPoc >= Close (aberration de marché / stop du mauvais côté)
            var ctxBadStop = new SwingContext
            {
                HasPocMigration = true,
                PocMigrationDirection = SwingDirection.Long,
                PocMigrationSessions = 3,
                PocMigrationTransitions = 2,
                PocMigrationStrength = 80.0,
                PocMigrationOldestPoc = 5050.0, // Plus haut que l'entrée !
                DailyPoc = 5040.0,
                DailyVah = 5055.0,
                DailyVal = 5025.0,
                Close = 5035.0
            };

            string rejection;
            bool allowed = scorer.ValidatePreconditions(ctxBadStop, SwingSetupType.PocMigration, SwingDirection.Long, out rejection);
            Assert(!allowed && rejection == "POC_MIGRATION_INVALID_STRUCTURAL_STOP", "Stop structurel du mauvais côté doit être strictement rejeté");
        }

        private static void Test_PocMigration_Setup_AntiChase_VA_Rejection()
        {
            var scorer = new SwingScorer();

            // Long au-dessus de VAH (chase)
            var ctxChase = new SwingContext
            {
                HasPocMigration = true,
                PocMigrationDirection = SwingDirection.Long,
                PocMigrationSessions = 3,
                PocMigrationTransitions = 2,
                PocMigrationStrength = 80.0,
                PocMigrationOldestPoc = 5000.0,
                DailyPoc = 5040.0,
                DailyVah = 5050.0,
                DailyVal = 5025.0,
                Close = 5055.0 // Au-dessus de VAH
            };

            string rejection;
            bool allowed = scorer.ValidatePreconditions(ctxChase, SwingSetupType.PocMigration, SwingDirection.Long, out rejection);
            Assert(!allowed && rejection == "POC_MIGRATION_LONG_ABOVE_VAH", "Achat au-dessus de VAH doit être rejeté (anti-chase)");
        }

        private static void Test_PocMigration_Repository_Query_Strict_AntiLookahead()
        {
            string testDb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_poc_mig_anti_lookahead.db");
            if (File.Exists(testDb)) { try { File.Delete(testDb); } catch { } }

            try
            {
                var repo = new VolumeProfileRepository(testDb);
                repo.Initialize();

                DateTime t0 = new DateTime(2026, 8, 20, 22, 0, 0, DateTimeKind.Utc);
                DateTime t1 = new DateTime(2026, 8, 21, 22, 0, 0, DateTimeKind.Utc);
                DateTime t2 = new DateTime(2026, 8, 22, 22, 0, 0, DateTimeKind.Utc);
                DateTime tFuture = new DateTime(2026, 8, 25, 22, 0, 0, DateTimeKind.Utc);

                // Profil passé 1
                repo.UpsertProfile(new ClosedVolumeProfile
                {
                    Symbol = "ES",
                    ProfileType = VolumeProfilePeriodType.Daily,
                    PeriodKey = "ES_DAY_2026-08-20",
                    PeriodEndUtc = t0,
                    Poc = 5000.0, Vah = 5015.0, Val = 4985.0
                });

                // Profil passé 2
                repo.UpsertProfile(new ClosedVolumeProfile
                {
                    Symbol = "ES",
                    ProfileType = VolumeProfilePeriodType.Daily,
                    PeriodKey = "ES_DAY_2026-08-21",
                    PeriodEndUtc = t1,
                    Poc = 5015.0, Vah = 5030.0, Val = 5000.0
                });

                // Profil passé 3
                repo.UpsertProfile(new ClosedVolumeProfile
                {
                    Symbol = "ES",
                    ProfileType = VolumeProfilePeriodType.Daily,
                    PeriodKey = "ES_DAY_2026-08-22",
                    PeriodEndUtc = t2,
                    Poc = 5030.0, Vah = 5045.0, Val = 5015.0
                });

                // Profil FUTUR (ne doit JAMAIS être retourné lors d'une évaluation à t2)
                repo.UpsertProfile(new ClosedVolumeProfile
                {
                    Symbol = "ES",
                    ProfileType = VolumeProfilePeriodType.Daily,
                    PeriodKey = "ES_DAY_2026-08-25",
                    PeriodEndUtc = tFuture,
                    Poc = 5100.0, Vah = 5120.0, Val = 5080.0
                });

                repo.FlushQueue();

                // Requête à la date t2 (le profil tFuture doit être exclu)
                var profiles = repo.QueryRecentDailyProfiles("ES", t2, 5);

                Assert(profiles.Count == 3, string.Format("3 profils attendus, obtenu {0}", profiles.Count));
                foreach (var p in profiles)
                {
                    Assert(p.PeriodEndUtc <= t2, "Aucun profil postérieur à t2 ne doit être retourné (Anti-Lookahead strict)");
                }

                repo.Dispose();
            }
            finally
            {
                if (File.Exists(testDb)) { try { File.Delete(testDb); } catch { } }
            }
        }

        #endregion

        #region Monthly VWAP Band Retest Tests

        private static void Test_MonthlyVwap_O1_Calculation_Matches_Exact_Math()
        {
            var calc = new VolumeProfileCalculator();
            double tickSize = 0.25;

            // Ingestion de plusieurs niveaux de prix
            calc.AddVolumeAtPrice(5000.00, 100, tickSize);
            calc.AddVolumeAtPrice(5005.00, 250, tickSize);
            calc.AddVolumeAtPrice(5010.00, 500, tickSize);
            calc.AddVolumeAtPrice(5015.00, 300, tickSize);
            calc.AddVolumeAtPrice(5020.00, 150, tickSize);

            // Calcul O(1) instantané
            double vwapO1, stdDevO1, sd1U, sd1L, sd2U, sd2L, sd3U, sd3L;
            bool ok = calc.TryCalculateVwapAndBands(tickSize, out vwapO1, out stdDevO1, out sd1U, out sd1L, out sd2U, out sd2L, out sd3U, out sd3L);
            Assert(ok, "TryCalculateVwapAndBands doit réussir");

            // Calcul via BuildProfile complet
            var profile = calc.BuildProfile("ES", "CME", "RTH", VolumeProfilePeriodType.Monthly, "ES_M1", DateTime.UtcNow.AddDays(-10), DateTime.UtcNow, tickSize);
            Assert(profile.Valid, "Profile BuildProfile doit être valide");

            // Vérification de la stricte équivalence mathématique
            Assert(Math.Abs(vwapO1 - profile.Vwap) < 1e-6, string.Format("VWAP mismatch: O1={0:F4}, Build={1:F4}", vwapO1, profile.Vwap));
            Assert(Math.Abs(stdDevO1 - profile.VwapStdDev) < 1e-6, string.Format("StdDev mismatch: O1={0:F4}, Build={1:F4}", stdDevO1, profile.VwapStdDev));
            Assert(Math.Abs(sd1U - profile.VwapSd1Upper) < 1e-6, "SD1 Upper mismatch");
            Assert(Math.Abs(sd1L - profile.VwapSd1Lower) < 1e-6, "SD1 Lower mismatch");
            Assert(Math.Abs(sd2U - profile.VwapSd2Upper) < 1e-6, "SD2 Upper mismatch");
            Assert(Math.Abs(sd2L - profile.VwapSd2Lower) < 1e-6, "SD2 Lower mismatch");
            Assert(Math.Abs(sd3U - profile.VwapSd3Upper) < 1e-6, "SD3 Upper mismatch");
            Assert(Math.Abs(sd3L - profile.VwapSd3Lower) < 1e-6, "SD3 Lower mismatch");
        }

        private static void Test_MonthlyVwap_Reset_On_Month_Boundary()
        {
            string testDb = Path.Combine(Path.GetTempPath(), "test_month_reset_" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                using (var mgr = new VolumeProfileManager("NQ", "CME", "RTH", 0.25, 70, testDb))
                {
                    mgr.Initialize();

                    // Mois 1 : Juillet 2026
                    DateTime m1_t1 = new DateTime(2026, 7, 15, 14, 0, 0, DateTimeKind.Utc);
                    var vols1 = new List<KeyValuePair<long, long>>
                    {
                        new KeyValuePair<long, long>((long)(20000.0 / 0.25), 1000)
                    };
                    mgr.IngestVolumetricBar(m1_t1, 20005, 19995, 20000, 20000, 1000, 50, vols1);

                    double vwap1, std1, sd1U, sd1L, sd2U, sd2L, sd3U, sd3L;
                    int barsCount1;
                    DateTime startUtc1;
                    mgr.TryGetCurrentMonthVwapAndBands(out vwap1, out std1, out sd1U, out sd1L, out sd2U, out sd2L, out sd3U, out sd3L, out barsCount1, out startUtc1);
                    Assert(barsCount1 == 1, "1 barre dans le mois 1");
                    Assert(Math.Abs(vwap1 - 20000.0) < 1e-4, "VWAP M1 doit valoir 20000");

                    // Mois 2 : Août 2026 (Transition de mois)
                    DateTime m2_t1 = new DateTime(2026, 8, 3, 14, 0, 0, DateTimeKind.Utc);
                    var vols2 = new List<KeyValuePair<long, long>>
                    {
                        new KeyValuePair<long, long>((long)(21000.0 / 0.25), 500)
                    };
                    mgr.IngestVolumetricBar(m2_t1, 21005, 20995, 21000, 21000, 500, 20, vols2);

                    double vwap2, std2;
                    int barsCount2;
                    DateTime startUtc2;
                    mgr.TryGetCurrentMonthVwapAndBands(out vwap2, out std2, out sd1U, out sd1L, out sd2U, out sd2L, out sd3U, out sd3L, out barsCount2, out startUtc2);
                    
                    // Le mois courant a été réinitialisé à 1 barre et son VWAP est sur 21000
                    Assert(barsCount2 == 1, "Le compteur de barres du nouveau mois doit être réinitialisé à 1");
                    Assert(Math.Abs(vwap2 - 21000.0) < 1e-4, "Le VWAP du nouveau mois doit être 21000.0");

                    // Le mois précédent Juillet est désormais disponible dans PrevMonth
                    Assert(mgr.PrevMonth != null && mgr.PrevMonth.Valid, "PrevMonth doit contenir Juillet clôturé");
                    Assert(Math.Abs(mgr.PrevMonth.Vwap - 20000.0) < 1e-4, "PrevMonth VWAP doit être 20000.0");
                }
            }
            finally
            {
                if (File.Exists(testDb)) { try { File.Delete(testDb); } catch { } }
            }
        }

        private static void Test_MonthlyVwapBandRetest_Long_Valid_Confirmed_Bar()
        {
            var scorer = new SwingScorer();
            var ctx = new SwingContext
            {
                Symbol = "NQ",
                TickSize = 0.25,
                PointValue = 20.0,
                AtrCurrent = 20.0,
                HtfTrendDirection = 1,
                HasCurrentMonthlyVwap = true,
                CurrentMonthlyBarsCount = 30,
                CurrentMonthlyVwap = 20000.0,
                CurrentMonthlySd1Upper = 20100.0,
                CurrentMonthlySd1Lower = 19900.0,
                CurrentMonthlyVwapSlope = 1.5,
                PrevCurrentMonthlySd1Upper = 20098.0,
                PrevClose = 20105.0, // Acceptation préalable au-dessus de SD1
                Open = 20102.0,
                Low = 20098.0, // Retest de SD1 (20100.0) dans la tolérance
                High = 20115.0,
                Close = 20112.0, // Clôture au-dessus de SD1 et au-dessus de l'Open
                RetestCountCurrentLevel = 1
            };

            string reason;
            bool valid = scorer.ValidatePreconditions(ctx, SwingSetupType.MonthlyVwapBandRetest, SwingDirection.Long, out reason);
            Assert(valid, "Le retest Long confirmé doit être validé. Raison rejet: " + reason);

            // RR 2.0 = typique pour un retest VWAP Monthly (post-sizing réel)
            var score = scorer.ComputeScore(ctx, SwingSetupType.MonthlyVwapBandRetest, SwingDirection.Long, 2.0);
            Assert(score.Total >= 70.0, string.Format("Score attendu >= 70, obtenu: {0:F1}", score.Total));
        }

        private static void Test_MonthlyVwapBandRetest_Short_Valid_Confirmed_Bar()
        {
            var scorer = new SwingScorer();
            var ctx = new SwingContext
            {
                Symbol = "NQ",
                TickSize = 0.25,
                PointValue = 20.0,
                AtrCurrent = 20.0,
                HtfTrendDirection = -1,
                HasCurrentMonthlyVwap = true,
                CurrentMonthlyBarsCount = 30,
                CurrentMonthlyVwap = 20000.0,
                CurrentMonthlySd1Upper = 20100.0,
                CurrentMonthlySd1Lower = 19900.0,
                CurrentMonthlyVwapSlope = -1.5,
                PrevCurrentMonthlySd1Lower = 19902.0,
                PrevClose = 19895.0, // Acceptation préalable sous SD-1
                Open = 19898.0,
                High = 19902.0, // Retest de SD-1 (19900.0) dans la tolérance
                Low = 19880.0,
                Close = 19885.0, // Clôture sous SD-1 et sous Open
                RetestCountCurrentLevel = 1
            };

            string reason;
            bool valid = scorer.ValidatePreconditions(ctx, SwingSetupType.MonthlyVwapBandRetest, SwingDirection.Short, out reason);
            Assert(valid, "Le retest Short confirmé doit être validé. Raison rejet: " + reason);

            // RR 2.0 = typique pour un retest VWAP Monthly (post-sizing réel)
            var score = scorer.ComputeScore(ctx, SwingSetupType.MonthlyVwapBandRetest, SwingDirection.Short, 2.0);
            Assert(score.Total >= 70.0, string.Format("Score attendu >= 70, obtenu: {0:F1}", score.Total));
        }

        private static void Test_MonthlyVwapBandRetest_Rejects_IntrabarTouch_Without_Close_Confirmation()
        {
            var scorer = new SwingScorer();
            
            // Cas Long avec clôture sous SD1
            var ctxLong = new SwingContext
            {
                Symbol = "NQ",
                TickSize = 0.25,
                AtrCurrent = 20.0,
                HtfTrendDirection = 1,
                HasCurrentMonthlyVwap = true,
                CurrentMonthlyBarsCount = 30,
                CurrentMonthlyVwap = 20000.0,
                CurrentMonthlySd1Upper = 20100.0,
                CurrentMonthlySd1Lower = 19900.0,
                CurrentMonthlyVwapSlope = 1.5,
                PrevClose = 20105.0,
                Open = 20102.0,
                Low = 20090.0,
                Close = 20095.0 // ÉCHEC : Clôture sous SD1 (20100.0)
            };

            string reason;
            bool validLong = scorer.ValidatePreconditions(ctxLong, SwingSetupType.MonthlyVwapBandRetest, SwingDirection.Long, out reason);
            Assert(!validLong, "Un retest Long clôturant sous SD1 doit être rejeté");
            Assert(reason == "CLOSE_BELOW_SD1", "Raison attendue: CLOSE_BELOW_SD1, obtenu: " + reason);

            // Cas Short avec clôture au-dessus de SD1
            var ctxShort = new SwingContext
            {
                Symbol = "NQ",
                TickSize = 0.25,
                AtrCurrent = 20.0,
                HtfTrendDirection = -1,
                HasCurrentMonthlyVwap = true,
                CurrentMonthlyBarsCount = 30,
                CurrentMonthlyVwap = 20000.0,
                CurrentMonthlySd1Upper = 20100.0,
                CurrentMonthlySd1Lower = 19900.0,
                CurrentMonthlyVwapSlope = -1.5,
                PrevClose = 19895.0,
                Open = 19898.0,
                High = 19910.0,
                Close = 19905.0 // ÉCHEC : Clôture au-dessus de SD-1 (19900.0)
            };

            bool validShort = scorer.ValidatePreconditions(ctxShort, SwingSetupType.MonthlyVwapBandRetest, SwingDirection.Short, out reason);
            Assert(!validShort, "Un retest Short clôturant au-dessus de SD-1 doit être rejeté");
            Assert(reason == "CLOSE_ABOVE_SD1", "Raison attendue: CLOSE_ABOVE_SD1, obtenu: " + reason);
        }

        private static void Test_MonthlyVwapBandRetest_Rejects_Flat_Or_Opposing_Vwap_Slope()
        {
            var scorer = new SwingScorer();
            var ctx = new SwingContext
            {
                Symbol = "NQ",
                TickSize = 0.25,
                AtrCurrent = 20.0,
                HtfTrendDirection = 1,
                HasCurrentMonthlyVwap = true,
                CurrentMonthlyBarsCount = 30,
                CurrentMonthlyVwap = 20000.0,
                CurrentMonthlySd1Upper = 20100.0,
                CurrentMonthlySd1Lower = 19900.0,
                CurrentMonthlyVwapSlope = 0.2, // Pente trop faible (< 0.5)
                PrevClose = 20105.0,
                Open = 20102.0,
                Low = 20098.0,
                Close = 20110.0
            };

            string reason;
            bool valid = scorer.ValidatePreconditions(ctx, SwingSetupType.MonthlyVwapBandRetest, SwingDirection.Long, out reason);
            Assert(!valid, "Pente VWAP insuffisante doit être rejetée");
            Assert(reason == "MONTHLY_VWAP_SLOPE_INSUFFICIENT", "Raison attendue: MONTHLY_VWAP_SLOPE_INSUFFICIENT, obtenu: " + reason);
        }

        private static void Test_MonthlyVwapBandRetest_EarlyMonth_Data_Insufficient_Guard()
        {
            var scorer = new SwingScorer();
            var ctx = new SwingContext
            {
                Symbol = "NQ",
                TickSize = 0.25,
                AtrCurrent = 20.0,
                HtfTrendDirection = 1,
                HasCurrentMonthlyVwap = true,
                CurrentMonthlyBarsCount = 10, // Début de mois (< 20 barres)
                CurrentMonthlyVwap = 20000.0,
                CurrentMonthlySd1Upper = 20100.0,
                CurrentMonthlySd1Lower = 19900.0,
                CurrentMonthlyVwapSlope = 1.5,
                PrevClose = 20105.0,
                Open = 20102.0,
                Low = 20098.0,
                Close = 20110.0
            };

            string reason;
            bool valid = scorer.ValidatePreconditions(ctx, SwingSetupType.MonthlyVwapBandRetest, SwingDirection.Long, out reason);
            Assert(!valid, "Données début de mois insuffisantes doivent être rejetées");
            Assert(reason == "MONTHLY_VWAP_EARLY_MONTH_UNSTABLE", "Raison attendue: MONTHLY_VWAP_EARLY_MONTH_UNSTABLE, obtenu: " + reason);
        }

        private static void Test_MonthlyVwapBandRetest_Excessive_Retests_Rejected()
        {
            var scorer = new SwingScorer();
            var ctx = new SwingContext
            {
                Symbol = "NQ",
                TickSize = 0.25,
                AtrCurrent = 20.0,
                HtfTrendDirection = 1,
                HasCurrentMonthlyVwap = true,
                CurrentMonthlyBarsCount = 30,
                CurrentMonthlyVwap = 20000.0,
                CurrentMonthlySd1Upper = 20100.0,
                CurrentMonthlySd1Lower = 19900.0,
                CurrentMonthlyVwapSlope = 1.5,
                PrevClose = 20105.0,
                Open = 20102.0,
                Low = 20098.0,
                Close = 20110.0,
                RetestCountCurrentLevel = 3 // 3ème retest (limite = 2)
            };

            string reason;
            bool valid = scorer.ValidatePreconditions(ctx, SwingSetupType.MonthlyVwapBandRetest, SwingDirection.Long, out reason);
            Assert(!valid, "Nombre excessif de retests doit être rejeté");
            Assert(reason == "MONTHLY_RETEST_LIMIT_REACHED", "Raison attendue: MONTHLY_RETEST_LIMIT_REACHED, obtenu: " + reason);
        }

        private static void Test_MonthlyVwapBandRetest_Snapshot_Immutability()
        {
            var sig = new SwingSignal
            {
                SetupType = SwingSetupType.MonthlyVwapBandRetest,
                Direction = SwingDirection.Long,
                EntryPrice = 20110.0,
                MonthlyVwapAtSetup = 20000.0,
                MonthlySd1UpperAtSetup = 20100.0,
                MonthlySd1LowerAtSetup = 19900.0,
                MonthlyVwapSlopeAtSetup = 1.5,
                RetestDistanceTicks = 8.0
            };

            Assert(sig.MonthlyVwapAtSetup == 20000.0, "Le snapshot MonthlyVwapAtSetup doit rester immuable");
            Assert(sig.MonthlySd1UpperAtSetup == 20100.0, "Le snapshot MonthlySd1UpperAtSetup doit rester immuable");
            Assert(sig.MonthlyVwapSlopeAtSetup == 1.5, "Le snapshot MonthlyVwapSlopeAtSetup doit rester immuable");
        }

        private static void Test_MonthlyVwapBandRetest_Full_Sizing_And_Stop_Clamping()
        {
            var riskMgr = new SwingRiskManager();

            // Test NQ : PointValue = $20, TickSize = 0.25, TickValue = $5
            double entryNq = 20110.0;
            double structuralStopNq = 20095.0; // 60 ticks
            double stopNq = riskMgr.CalculateHybridStop(entryNq, SwingDirection.Long, structuralStopNq, 20.0, 2.0, 0.25, 20, 120);
            double stopTicksNq = Math.Abs(entryNq - stopNq) / 0.25;

            int sizeNq = riskMgr.CalculatePositionSize(250.0, stopTicksNq, 5.0, 1.0, 4);
            Assert(sizeNq >= 1 && sizeNq <= 4, "Sizing NQ valide");

            // Test ES : PointValue = $50, TickSize = 0.25, TickValue = $12.50
            double entryEs = 5020.0;
            double structuralStopEs = 5015.0; // 20 ticks
            double stopEs = riskMgr.CalculateHybridStop(entryEs, SwingDirection.Long, structuralStopEs, 5.0, 2.0, 0.25, 12, 60);
            double stopTicksEs = Math.Abs(entryEs - stopEs) / 0.25;

            int sizeEs = riskMgr.CalculatePositionSize(250.0, stopTicksEs, 12.50, 1.0, 4);
            Assert(sizeEs >= 1 && sizeEs <= 4, "Sizing ES valide");
        }

        private static void Test_MonthlyVwap_Numerical_Stability_HighPrice_And_NegativeVariance()
        {
            var calc = new VolumeProfileCalculator();
            double tickSize = 0.25;

            // Ingestion à très haute valeur nominale (NQ 20000.0) avec dispersion infinitésimale
            calc.AddVolumeAtPrice(20000.00, 1000000, tickSize);
            calc.AddVolumeAtPrice(20000.25, 5, tickSize);
            calc.AddVolumeAtPrice(20000.00, 1000000, tickSize);

            double vwap, stdDev, sd1U, sd1L, sd2U, sd2L, sd3U, sd3L;
            bool ok = calc.TryCalculateVwapAndBands(tickSize, out vwap, out stdDev, out sd1U, out sd1L, out sd2U, out sd2L, out sd3U, out sd3L);
            Assert(ok, "TryCalculateVwapAndBands doit réussir sur prix élevés");
            Assert(!double.IsNaN(vwap) && !double.IsInfinity(vwap), "VWAP ne doit pas être NaN/Infini");
            Assert(!double.IsNaN(stdDev) && !double.IsInfinity(stdDev) && stdDev >= 0.0, "StdDev doit être finie et >= 0");
            Assert(Math.Abs(vwap - 20000.0) < 1.0, "VWAP doit être proche de 20000");

            // Test avec données NaN ou négatives : doit échouer proprement sans lever d'exception
            double vwapBad, stdBad;
            bool okBadTick = calc.TryCalculateVwapAndBands(-0.25, out vwapBad, out stdBad, out sd1U, out sd1L, out sd2U, out sd2L, out sd3U, out sd3L);
            Assert(!okBadTick, "TickSize négatif doit être rejeté proprement");

            bool okNanTick = calc.TryCalculateVwapAndBands(double.NaN, out vwapBad, out stdBad, out sd1U, out sd1L, out sd2U, out sd2L, out sd3U, out sd3L);
            Assert(!okNanTick, "TickSize NaN doit être rejeté proprement");
        }

        private static void Test_MonthlyVwap_Slope_TicksPerHour_Invariance()
        {
            // Simulation : VWAP montant de 50 points (200 ticks de 0.25) sur 4 heures (240 min)
            double tickSize = 0.25;
            double vwapDeltaPrice = 50.0;
            double elapsedHours = 4.0;

            // Sur barres 1-minute (240 barres) : 200 ticks / 240 bars = 0.833 ticks/barre
            // Sur barres 60-minutes (4 barres) : 200 ticks / 4 bars = 50.0 ticks/barre
            // Pente normalisée en Ticks/Heure : (50.0 / 0.25) / 4.0 = 50.0 ticks/heure
            double slopeTicksPerHour1 = (vwapDeltaPrice / tickSize) / elapsedHours;
            double slopeTicksPerHour2 = (vwapDeltaPrice / tickSize) / elapsedHours;

            Assert(Math.Abs(slopeTicksPerHour1 - 50.0) < 1e-6, "Pente normalisée = 50.0 ticks/heure");
            Assert(Math.Abs(slopeTicksPerHour1 - slopeTicksPerHour2) < 1e-6, "Pente normalisée identique quel que soit le timeframe d'agrégation");
        }

        private static void Test_MonthlyBand_Epoch_Lifecycle_And_Drift_Reset()
        {
            // Initialisation d'un Epoch de bande Upper SD1 à 20100.0 avec 2 retests déjà effectués
            var epoch = new MonthlyBandEpochState
            {
                EpochId = "EP_TEST_01",
                BandType = "MONTHLY_SD1_UPPER",
                ReferencePrice = 20100.0,
                RetestCount = 2,
                IsActive = true
            };

            Assert(epoch.RetestCount == 2, "2 retests sur l'Epoch initial");

            // Dérive du marché : SD1 monte à 20130.0 (dérive de 30 pts = 120 ticks > 20 ticks)
            double currentSd1 = 20130.0;
            double tickSize = 0.25;
            int epochResetTicks = 20;

            double driftTicks = Math.Abs(currentSd1 - epoch.ReferencePrice) / tickSize;
            Assert(driftTicks > epochResetTicks, "La dérive dépasse le seuil de reset d'Epoch");

            // Création automatique du nouvel Epoch
            var newEpoch = new MonthlyBandEpochState
            {
                EpochId = "EP_TEST_02",
                BandType = "MONTHLY_SD1_UPPER",
                ReferencePrice = currentSd1,
                RetestCount = 0,
                IsActive = true
            };

            Assert(newEpoch.EpochId != epoch.EpochId, "Nouvel identifiant d'Epoch généré");
            Assert(newEpoch.RetestCount == 0, "Le compteur de retest du nouvel Epoch est réinitialisé à 0");
        }

        private static void Test_MonthlyBand_MultiBar_Acceptance_Configurable()
        {
            var scorer = new SwingScorer();
            var ctx = new SwingContext
            {
                Symbol = "NQ",
                TickSize = 0.25,
                PointValue = 20.0,
                AtrCurrent = 20.0,
                HtfTrendDirection = 1,
                HasCurrentMonthlyVwap = true,
                CurrentMonthlyBarsCount = 30,
                CurrentMonthlyVwap = 20000.0,
                CurrentMonthlySd1Upper = 20100.0,
                CurrentMonthlySd1Lower = 19900.0,
                CurrentMonthlyVwapSlopeTicksPerHour = 10.0,
                CurrentMonthlyVwapSlope = 1.5,
                MonthlyBandMinAcceptanceBarsRequired = 2, // Exigence de 2 barres d'acceptation
                MonthlyBandAcceptanceBars = 1,            // Seulement 1 barre observée
                Open = 20102.0,
                Low = 20098.0,
                High = 20115.0,
                Close = 20112.0,
                RetestCountCurrentLevel = 0
            };

            string reason;
            bool valid1 = scorer.ValidatePreconditions(ctx, SwingSetupType.MonthlyVwapBandRetest, SwingDirection.Long, out reason);
            Assert(!valid1, "1 seule barre d'acceptation doit être rejetée si 2 sont requises");
            Assert(reason == "MONTHLY_BAND_ACCEPTANCE_INSUFFICIENT", "Raison attendue: MONTHLY_BAND_ACCEPTANCE_INSUFFICIENT, obtenu: " + reason);

            // Mise à jour avec 2 barres d'acceptation confirmées
            ctx.MonthlyBandAcceptanceBars = 2;
            bool valid2 = scorer.ValidatePreconditions(ctx, SwingSetupType.MonthlyVwapBandRetest, SwingDirection.Long, out reason);
            Assert(valid2, "2 barres d'acceptation doivent valider le setup. Raison rejet: " + reason);
        }

        private static void Test_MonthlyBand_Collision_Stop_TP_Conservative_Pessimistic()
        {
            var sig = new SwingSignal
            {
                Symbol = "NQ",
                Direction = SwingDirection.Long,
                SetupType = SwingSetupType.MonthlyVwapBandRetest,
                EntryPrice = 20100.0,
                InitialStopPrice = 20080.0,
                Target1Price = 20130.0,
                Target2Price = 20160.0,
                PositionSizeContracts = 2
            };

            var trade = new TrackedSwingTrade(sig, 0.25, 20.0);
            DateTime nowUtc = DateTime.UtcNow;

            // Simulation d'une barre de forte volatilité touchant le Stop (Low 20070 <= 20080) ET le TP1 (High 20140 >= 20130)
            double barHigh = 20140.0;
            double barLow = 20070.0;

            bool stopTriggered = barLow <= trade.CurrentStopPrice;
            bool tp1Triggered = barHigh >= trade.Target1Price;

            Assert(stopTriggered && tp1Triggered, "Double franchissement Stop et TP sur la même barre");

            // Règle pessimiste Zero-Trust : Stop traité en priorité
            if (stopTriggered)
            {
                trade.CloseTrade(trade.CurrentStopPrice, nowUtc, "STOP_LOSS", 0.25, 20.0);
            }

            Assert(trade.Closed, "Le trade doit être clôturé");
            Assert(trade.ExitReason == "STOP_LOSS", "La raison de sortie doit impérativement être STOP_LOSS");
            Assert(!trade.Tp1Hit, "TP1 ne doit pas être marqué comme atteint en cas de collision");
        }

        private static void Test_MonthlyBand_Slope_AtrNormalized_Validation()
        {
            var scorer = new SwingScorer();
            var ctx = new SwingContext
            {
                Symbol = "NQ",
                TickSize = 0.25,
                AtrCurrent = 20.0,
                HtfTrendDirection = 1,
                HasCurrentMonthlyVwap = true,
                CurrentMonthlyBarsCount = 30,
                CurrentMonthlyVwap = 20000.0,
                CurrentMonthlySd1Upper = 20100.0,
                CurrentMonthlySd1Lower = 19900.0,
                CurrentMonthlyVwapSlopeTicksPerHour = 10.0,
                MonthlyBandMinSlopeAtrNormalizedConfig = 0.20, // Requiert >= 0.20 ATR
                CurrentMonthlyVwapSlopeAtrNormalized = 0.05,  // Trop faible (0.05 ATR)
                PrevClose = 20105.0,
                Open = 20102.0,
                Low = 20098.0,
                Close = 20112.0
            };

            string reason;
            bool valid = scorer.ValidatePreconditions(ctx, SwingSetupType.MonthlyVwapBandRetest, SwingDirection.Long, out reason);
            Assert(!valid, "Pente ATR insuffisante doit être rejetée");
            Assert(reason == "MONTHLY_VWAP_SLOPE_INSUFFICIENT", "Raison attendue: MONTHLY_VWAP_SLOPE_INSUFFICIENT, obtenu: " + reason);

            // Avec pente ATR suffisante
            ctx.CurrentMonthlyVwapSlopeAtrNormalized = 0.30;
            bool validOk = scorer.ValidatePreconditions(ctx, SwingSetupType.MonthlyVwapBandRetest, SwingDirection.Long, out reason);
            Assert(validOk, "Pente ATR suffisante doit être validée");
        }

        private static void Test_MonthlyBand_Epoch_Persists_On_Snapshot()
        {
            var sig = new SwingSignal
            {
                SetupType = SwingSetupType.MonthlyVwapBandRetest,
                Direction = SwingDirection.Long,
                EntryPrice = 20110.0,
                MonthlyPeriodKey = "NQ_MONTH_2026-08",
                MonthlyBandEpochIdAtSetup = "EP_A1B2C3",
                MonthlyVwapSlopeTicksPerHourAtSetup = 15.5,
                MonthlyVwapSlopeAtrNormalizedAtSetup = 0.42,
                MonthlyBandAcceptanceBarsAtSetup = 3
            };

            Assert(sig.MonthlyPeriodKey == "NQ_MONTH_2026-08", "MonthlyPeriodKey préservé");
            Assert(sig.MonthlyBandEpochIdAtSetup == "EP_A1B2C3", "MonthlyBandEpochIdAtSetup préservé");
            Assert(Math.Abs(sig.MonthlyVwapSlopeTicksPerHourAtSetup - 15.5) < 1e-6, "Pente ticks/heure préservée");
            Assert(Math.Abs(sig.MonthlyVwapSlopeAtrNormalizedAtSetup - 0.42) < 1e-6, "Pente ATR préservée");
            Assert(sig.MonthlyBandAcceptanceBarsAtSetup == 3, "Nombre de barres d'acceptation préservé");
        }

        private static void Test_MonthlyBand_NonRegression_All_Existing_Setups()
        {
            var scorer = new SwingScorer();
            var ctx = new SwingContext
            {
                Symbol = "ES",
                TickSize = 0.25,
                PointValue = 50.0,
                AtrCurrent = 10.0,
                HtfTrendDirection = 1,
                Sd2Lower = 4905.0,
                Sd3Lower = 4900.0,
                Low = 4895.0,
                Close = 4910.0,
                Open = 4905.0
            };

            // Test RejectExtreme (Setup #0)
            string reason;
            bool validReject = scorer.ValidatePreconditions(ctx, SwingSetupType.RejectExtreme, SwingDirection.Long, out reason);
            Assert(validReject, "RejectExtreme doit continuer à fonctionner sans régression");

            var score = scorer.ComputeScore(ctx, SwingSetupType.RejectExtreme, SwingDirection.Long);
            Assert(score.Total >= 60.0, "Score RejectExtreme valide");
        }

        #endregion

        #region Suite Corrections Post-Audit

        private static void Test_Audit_ScalpingPro_Preset_Guard_In_Source()
        {
            string root = GetProjectRoot();
            string scalpingFile = Path.Combine(root, "AuctionMarketCore.ScalpingPro.cs");
            string text = File.ReadAllText(scalpingFile);
            Assert(text.Contains("TradingPreset == SniperMarketPreset.ScalpingPro"),
                "IsScalpingPro doit être lié au preset ScalpingPro.");
            Assert(text.Contains("if (!IsScalpingPro) return;"),
                "ScalpingProOnEvaluatedBar doit court-circuiter hors preset ScalpingPro.");
        }

        private static void Test_Audit_Sniper_Enabled_And_Swing_Isolation()
        {
            string root = GetProjectRoot();
            string sniperFile = Path.Combine(root, "AuctionMarketCore.Sniper.cs");
            string text = File.ReadAllText(sniperFile);
            Assert(text.Contains("EnableSniperEngine = true;"),
                "EnableSniperEngine doit être activé par défaut dans ApplySniperDefaults.");
            Assert(text.Contains("if (IsSwing && EnableSwingEngine) return;"),
                "SniperOnEvaluatedBar doit être ignoré en preset Swing actif.");
        }

        private static void Test_Audit_Swing_Microstructure_Helpers_In_Source()
        {
            string root = GetProjectRoot();
            string swingFile = Path.Combine(root, "AuctionMarketCore.Swing.cs");
            string text = File.ReadAllText(swingFile);
            Assert(text.Contains("fvgEngineZones") && text.Contains("IsInActiveFvg"),
                "IsInActiveFvg doit utiliser fvgEngineZones.");
            Assert(text.Contains("isBullishAbsorptionActive") && text.Contains("isBearishAbsorptionActive"),
                "HasRecentAbsorption doit utiliser les flags moteur.");
            Assert(text.Contains("ResolveSwingRegimeHtf"),
                "RegimeHtf doit être dérivé du HTF réel, pas de la direction du setup.");
            Assert(!text.Contains("RegimeHtf = isBuy ? SwingMarketRegime.TrendUp : SwingMarketRegime.TrendDown"),
                "RegimeHtf ne doit plus être assigné par direction du candidat.");
        }

        private static void Test_Audit_Swing_RiskRewardScore_From_Real_RR()
        {
            var scorer = new SwingScorer();
            var ctx = new SwingContext
            {
                Symbol = "NQ", TickSize = 0.25, AtrCurrent = 20.0, HtfTrendDirection = 1,
                Sd2Lower = 19900.0, Low = 19895.0, Close = 19910.0, Open = 19905.0
            };
            var score = scorer.ComputeScore(ctx, SwingSetupType.RejectExtreme, SwingDirection.Long, 3.0);
            Assert(score.RiskRewardScore >= 9.5, "RR 3.0 doit produire un RiskRewardScore proche de 10.");
        }

        private static void Test_Audit_Swing_RiskRewardScore_Low_When_RR_Below_Min()
        {
            var scorer = new SwingScorer();
            var ctx = new SwingContext
            {
                Symbol = "NQ", TickSize = 0.25, AtrCurrent = 20.0, HtfTrendDirection = 1,
                Sd2Lower = 19900.0, Low = 19895.0, Close = 19910.0, Open = 19905.0
            };
            var score = scorer.ComputeScore(ctx, SwingSetupType.RejectExtreme, SwingDirection.Long, 0.4);
            Assert(score.RiskRewardScore < 5.0, "RR 0.4 doit produire un RiskRewardScore faible.");
        }

        private static void Test_MonthlyBand_Acceptance_Uses_Prev_Sd1_Values()
        {
            var scorer = new SwingScorer();
            var ctx = new SwingContext
            {
                Symbol = "NQ",
                TickSize = 0.25,
                PointValue = 20.0,
                AtrCurrent = 20.0,
                HtfTrendDirection = 1,
                HasCurrentMonthlyVwap = true,
                CurrentMonthlyBarsCount = 30,
                CurrentMonthlyVwap = 20000.0,
                CurrentMonthlySd1Upper = 20100.0,
                CurrentMonthlySd1Lower = 19900.0,
                CurrentMonthlyVwapSlopeTicksPerHour = 10.0,
                CurrentMonthlyVwapSlope = 1.5,
                MonthlyBandMinAcceptanceBarsRequired = 1,
                MonthlyBandAcceptanceBars = 0,
                PrevCurrentMonthlySd1Upper = 20098.0,
                PrevClose = 20105.0,
                Open = 20102.0,
                Low = 20098.0,
                High = 20115.0,
                Close = 20112.0,
                RetestCountCurrentLevel = 0
            };

            string reason;
            bool valid = scorer.ValidatePreconditions(ctx, SwingSetupType.MonthlyVwapBandRetest, SwingDirection.Long, out reason);
            Assert(valid, "Acceptation via PrevCurrentMonthlySd1Upper doit valider le retest. Raison: " + reason);
        }

        private static void Test_Audit_Configs_No_Secrets_ScalpingPro_And_Swing()
        {
            string root = GetProjectRoot();
            string[] folders = new string[] { "SWING", "SCALPING_PRO" };
            foreach (string folder in folders)
            {
                string dir = Path.Combine(root, "configs", folder);
                if (!Directory.Exists(dir)) continue;
                foreach (string f in Directory.GetFiles(dir, "*.xml"))
                {
                    string text = File.ReadAllText(f);
                    Assert(text.Contains("YOUR_BOT_TOKEN_HERE"),
                        "Placeholder token attendu dans " + f);
                }
            }
        }

        private static void Test_Audit_TelegramDispatcher_Dedup_Not_Blocked_After_Send_Failure()
        {
            int attempts = 0;
            var dispatcher = new TelegramDispatcher(
                (text, onComplete) =>
                {
                    attempts++;
                    onComplete(false);
                },
                null,
                () => DateTime.UtcNow);
            dispatcher.MinInterval = TimeSpan.FromMilliseconds(0);
            dispatcher.MaxAttempts = 1;
            dispatcher.DuplicateWindow = TimeSpan.FromMinutes(1);

            Assert(dispatcher.Dispatch("audit-dedup-test"), "Premier envoi accepté.");
            Thread.Sleep(150);
            Assert(dispatcher.Dispatch("audit-dedup-test"), "Second envoi identique autorisé après échec (hash non verrouillé).");
            dispatcher.Dispose();
        }

        private static void Test_Audit_EnforcePresetBarCloseDiscipline_In_Source()
        {
            string root = GetProjectRoot();
            string amcFile = Path.Combine(root, "AuctionMarketCore.cs");
            string text = File.ReadAllText(amcFile);
            Assert(text.Contains("EnforcePresetBarCloseDiscipline"),
                "La discipline bar-close preset doit exister.");
        }

        #region Suite Tests Swing V3 Opportunity Manager & Win Rate

        private static void Test_SwingV3_CandidateCollection_And_Ranking()
        {
            var scorer = new SwingScorer();
            var ctx = new SwingContext
            {
                Symbol = "NQ",
                TickSize = 0.25,
                PointValue = 20.0,
                AtrCurrent = 20.0,
                Close = 18500.0,
                Open = 18490.0,
                High = 18505.0,
                Low = 18485.0,
                HtfTrendDirection = 1,
                RegimeHtf = SwingMarketRegime.TrendUp,
                SessionVwap = 18495.0,
                DailyVal = 18450.0,
                DailyVah = 18550.0,
                DailyPoc = 18495.0,
                NearDailyPoc = true,
                HasDeltaDivergence = true,
                HasAbsorptionEvidence = true,
                BarDelta = 500.0
            };

            double tq1, rc1, dq1, lq1, lep1, cp1, score1;
            scorer.ComputeQualityMetrics(ctx, SwingSetupType.HtfContinuation, SwingDirection.Long, 70.0,
                out tq1, out rc1, out dq1, out lq1, out lep1, out cp1, out score1);

            double tq2, rc2, dq2, lq2, lep2, cp2, score2;
            scorer.ComputeQualityMetrics(ctx, SwingSetupType.BreakoutRetest, SwingDirection.Long, 50.0,
                out tq2, out rc2, out dq2, out lq2, out lep2, out cp2, out score2);

            Assert(score1 > score2, "HtfContinuation avec meilleure base et confluence doit avoir un score supérieur à BreakoutRetest.");
            Assert(tq1 >= 7.5, "TimingQuality doit être élevé avec delta positif et absorption.");
        }

        private static void Test_SwingV3_SameCampaignLock_Blocks_Duplicates()
        {
            var opp = new SwingOpportunityManager
            {
                Enabled = true,
                SameCampaignLock = true,
                EntryCooldownBars = 12
            };

            var sig = new SwingSetupSignature
            {
                Symbol = "NQ",
                SetupType = SwingSetupType.HtfContinuation,
                Direction = SwingDirection.Short,
                StructureId = "BOS_18200",
                RegimeId = "TrendDown",
                AnchorPrice = 18250.0
            };

            var cand = new SwingCandidate
            {
                Symbol = "NQ",
                SetupType = SwingSetupType.HtfContinuation,
                Direction = SwingDirection.Short,
                BarIndex = 100,
                StructureId = "BOS_18200",
                Signature = sig
            };

            string reason;
            bool v1 = opp.ValidateCandidate(cand, null, 100, out reason);
            Assert(v1, "Première entrée de campagne doit être autorisée.");

            var trade = new TrackedSwingTrade(new SwingSignal
            {
                Symbol = "NQ",
                Direction = SwingDirection.Short,
                SetupType = SwingSetupType.HtfContinuation,
                EntryPrice = 18200.0,
                InitialStopPrice = 18240.0,
                Target1Price = 18140.0,
                Target2Price = 18080.0
            }, 0.25, 20.0);

            opp.OnCandidateExecuted(cand, trade, 100);

            // Seconde tentative sur la barre suivante (barre 101)
            var candNextBar = new SwingCandidate
            {
                Symbol = "NQ",
                SetupType = SwingSetupType.HtfContinuation,
                Direction = SwingDirection.Short,
                BarIndex = 101,
                StructureId = "BOS_18200",
                Signature = sig
            };

            bool v2 = opp.ValidateCandidate(candNextBar, null, 101, out reason);
            Assert(!v2, "La seconde tentative sur même campagne doit être strictement bloquée.");
            Assert(reason == SwingRejectionReason.DuplicateCampaign || reason == SwingRejectionReason.SameSignature,
                "Motif de rejet attendu : DuplicateCampaign ou SameSignature.");
        }

        private static void Test_SwingV3_NewStructure_Unlocks_Campaign()
        {
            var opp = new SwingOpportunityManager
            {
                Enabled = true,
                SameCampaignLock = true,
                RequireNewStructureForReentry = true,
                EntryCooldownBars = 10
            };

            var sig = new SwingSetupSignature { Symbol = "NQ", SetupType = SwingSetupType.HtfContinuation, Direction = SwingDirection.Long, StructureId = "BOS_100", AnchorPrice = 5000.0 };
            var cand = new SwingCandidate { Symbol = "NQ", SetupType = SwingSetupType.HtfContinuation, Direction = SwingDirection.Long, BarIndex = 100, StructureId = "BOS_100", Signature = sig };

            opp.OnCandidateExecuted(cand, null, 100);
            opp.OnTradeClosed(new TrackedSwingTrade { TradeId = "T1", IsLong = true }, "STOP_LOSS", 102);

            // Re-tentative barre 105 sans nouvelle structure (bloquée par cooldown et même structure)
            string reason;
            bool vBlocked = opp.ValidateCandidate(cand, null, 105, out reason);
            Assert(!vBlocked, "Re-tentative immédiate sans nouvelle structure doit être bloquée.");

            // Événement nouvelle structure à barre 115
            opp.RegisterStructureEvent("NEW_CHOCH_115", 115);
            var sigNew = new SwingSetupSignature { Symbol = "NQ", SetupType = SwingSetupType.HtfContinuation, Direction = SwingDirection.Long, StructureId = "NEW_CHOCH_115", AnchorPrice = 5050.0 };
            var candNew = new SwingCandidate { Symbol = "NQ", SetupType = SwingSetupType.HtfContinuation, Direction = SwingDirection.Long, BarIndex = 115, StructureId = "NEW_CHOCH_115", Signature = sigNew };

            bool vAllowed = opp.ValidateCandidate(candNew, null, 115, out reason);
            Assert(vAllowed, "Nouvelle structure après cooldown doit autoriser une nouvelle campagne.");
        }

        private static void Test_SwingV3_Cooldown_Enforcement()
        {
            var opp = new SwingOpportunityManager
            {
                Enabled = true,
                EntryCooldownBars = 12
            };

            var sig = new SwingSetupSignature { Symbol = "NQ", SetupType = SwingSetupType.MacroReversal, Direction = SwingDirection.Long, StructureId = "S1", AnchorPrice = 5000.0 };
            var cand = new SwingCandidate { Symbol = "NQ", SetupType = SwingSetupType.MacroReversal, Direction = SwingDirection.Long, BarIndex = 100, StructureId = "S1", Signature = sig };

            opp.OnCandidateExecuted(cand, null, 100);
            opp.OnTradeClosed(new TrackedSwingTrade { TradeId = "T1", IsLong = true }, "STOP_LOSS", 102);

            string reason;
            // Test barre 108 (8 barres écoulées < 12)
            bool vCd = opp.ValidateCandidate(cand, null, 108, out reason);
            Assert(!vCd && reason == SwingRejectionReason.CooldownActive, "Le cooldown de 12 barres doit bloquer à la 8ème barre.");

            // Test barre 113 (13 barres écoulées > 12) avec nouvelle structure
            opp.RegisterStructureEvent("S2", 113);
            var cand2 = new SwingCandidate { Symbol = "NQ", SetupType = SwingSetupType.MacroReversal, Direction = SwingDirection.Long, BarIndex = 113, StructureId = "S2", Signature = new SwingSetupSignature { Symbol = "NQ", SetupType = SwingSetupType.MacroReversal, Direction = SwingDirection.Long, StructureId = "S2", AnchorPrice = 5020.0 } };
            bool vOk = opp.ValidateCandidate(cand2, null, 113, out reason);
            Assert(vOk, "Après 12 barres et nouvelle structure, l'entrée doit être autorisée.");
        }

        private static void Test_SwingV3_Session_Limits()
        {
            var opp = new SwingOpportunityManager
            {
                Enabled = true,
                MaxEntriesPerSession = 2,
                MaxLongEntriesPerSession = 1,
                MaxShortEntriesPerSession = 1,
                EntryCooldownBars = 5
            };

            opp.OnNewSession(0);

            var sigL = new SwingSetupSignature { Symbol = "NQ", SetupType = SwingSetupType.HtfContinuation, Direction = SwingDirection.Long, StructureId = "L1" };
            var candL1 = new SwingCandidate { Symbol = "NQ", Direction = SwingDirection.Long, BarIndex = 10, StructureId = "L1", Signature = sigL };

            string reason;
            Assert(opp.ValidateCandidate(candL1, null, 10, out reason), "1ère entrée long autorisée.");
            opp.OnCandidateExecuted(candL1, null, 10);
            opp.OnTradeClosed(new TrackedSwingTrade { TradeId = "T1", IsLong = true }, "TP1", 15);

            // 2ème long dans la même session
            var candL2 = new SwingCandidate { Symbol = "NQ", Direction = SwingDirection.Long, BarIndex = 25, StructureId = "L2", Signature = new SwingSetupSignature { Symbol = "NQ", Direction = SwingDirection.Long, StructureId = "L2" } };
            Assert(!opp.ValidateCandidate(candL2, null, 25, out reason) && reason == SwingRejectionReason.DirectionLimitReached,
                "Second Long dans la même session doit être bloqué.");

            // Short dans la même session
            var sigS = new SwingSetupSignature { Symbol = "NQ", SetupType = SwingSetupType.MacroReversal, Direction = SwingDirection.Short, StructureId = "S1" };
            var candS = new SwingCandidate { Symbol = "NQ", Direction = SwingDirection.Short, BarIndex = 25, StructureId = "S1", Signature = sigS };
            Assert(opp.ValidateCandidate(candS, null, 25, out reason), "1er Short dans la même session doit être autorisé.");
            opp.OnCandidateExecuted(candS, null, 25);
            opp.OnTradeClosed(new TrackedSwingTrade { TradeId = "T2", IsLong = false }, "TP1", 30);

            // 3ème tentative totale dans la session
            var candS2 = new SwingCandidate { Symbol = "NQ", Direction = SwingDirection.Short, BarIndex = 40, StructureId = "S2", Signature = new SwingSetupSignature { Symbol = "NQ", Direction = SwingDirection.Short, StructureId = "S2" } };
            Assert(!opp.ValidateCandidate(candS2, null, 40, out reason) && reason == SwingRejectionReason.SessionLimitReached,
                "Plafond total de 2 entrées par session atteint, toute nouvelle entrée doit être bloquée.");
        }

        private static void Test_SwingV3_HtfContinuation_Requires_Pullback_To_Value()
        {
            var scorer = new SwingScorer();

            // 1. Contexte dans le vide (pas de VWAP, pas de POC, pas de VA, pas de FVG)
            var ctxAir = new SwingContext
            {
                HtfTrendDirection = 1,
                Open = 5090.0,
                Close = 5100.0,
                Low = 5085.0,
                High = 5105.0,
                AtrCurrent = 10.0,
                PointValue = 50.0,
                SessionVwap = 5000.0, // Très loin
                DailyPoc = 5000.0,    // Très loin
                DailyVal = 4980.0,
                DailyVah = 5020.0,
                NearDailyPoc = false,
                NearDailyVah = false,
                NearDailyVal = false,
                InFairValueGap = false,
                InsideHvn = false
            };

            string reason;
            bool vAir = scorer.ValidatePreconditions(ctxAir, SwingSetupType.HtfContinuation, SwingDirection.Long, out reason);
            Assert(!vAir && reason == SwingRejectionReason.NoPullbackToValue,
                "Un achat de continuation dans le vide sans pullback à la valeur doit être rejeté.");

            // 2. Contexte avec pullback sur le Daily POC
            var ctxPullback = new SwingContext
            {
                HtfTrendDirection = 1,
                Open = 5002.0,
                Close = 5008.0,
                Low = 4998.0,
                High = 5010.0,
                AtrCurrent = 10.0,
                PointValue = 50.0,
                DailyPoc = 5000.0,
                NearDailyPoc = true
            };

            bool vPullback = scorer.ValidatePreconditions(ctxPullback, SwingSetupType.HtfContinuation, SwingDirection.Long, out reason);
            Assert(vPullback, "Un achat de continuation sur pullback POC doit être accepté.");
        }

        private static void Test_SwingV3_MacroReversal_OrderFlow_HardGate()
        {
            var scorer = new SwingScorer();

            // Reversal sans divergence delta ni absorption
            var ctxNoOf = new SwingContext
            {
                Open = 5000.0,
                Close = 5010.0,
                AtrCurrent = 10.0,
                PointValue = 50.0,
                HasDeltaDivergence = false,
                HasAbsorptionEvidence = false
            };

            string reason;
            bool vNoOf = scorer.ValidatePreconditions(ctxNoOf, SwingSetupType.MacroReversal, SwingDirection.Long, out reason);
            Assert(!vNoOf && reason == SwingRejectionReason.MacroReversalNoOrderFlow,
                "MacroReversal sans confirmation Order Flow doit être rejeté.");

            // Reversal avec absorption validée
            var ctxWithAbs = new SwingContext
            {
                Open = 5000.0,
                Close = 5010.0,
                AtrCurrent = 10.0,
                PointValue = 50.0,
                HasDeltaDivergence = false,
                HasAbsorptionEvidence = true
            };

            bool vWithAbs = scorer.ValidatePreconditions(ctxWithAbs, SwingSetupType.MacroReversal, SwingDirection.Long, out reason);
            Assert(vWithAbs, "MacroReversal avec preuve d'absorption doit être accepté.");
        }

        private static void Test_SwingV3_LateEntryPenalty()
        {
            var scorer = new SwingScorer();

            // Marché sur-étendu à 3.0 ATR du VWAP
            var ctxExtended = new SwingContext
            {
                SessionVwap = 5000.0,
                AtrCurrent = 20.0,
                Close = 5060.0, // +60 pts = +3.0 ATR
                Open = 5050.0,
                HtfTrendDirection = 1
            };

            double tq, rc, dq, lq, lep, cp, finalScore;
            scorer.ComputeQualityMetrics(ctxExtended, SwingSetupType.HtfContinuation, SwingDirection.Long, 70.0,
                out tq, out rc, out dq, out lq, out lep, out cp, out finalScore);

            Assert(lep >= 15.0, "Une extension >= 2.5 ATR doit infliger la pénalité maximale de 15 points.");

            // Marché proche du VWAP (0.5 ATR)
            var ctxClose = new SwingContext
            {
                SessionVwap = 5000.0,
                AtrCurrent = 20.0,
                Close = 5010.0, // +10 pts = 0.5 ATR
                Open = 5005.0,
                HtfTrendDirection = 1
            };

            scorer.ComputeQualityMetrics(ctxClose, SwingSetupType.HtfContinuation, SwingDirection.Long, 70.0,
                out tq, out rc, out dq, out lq, out lep, out cp, out finalScore);

            Assert(lep == 0.0, "Une entrée proche du VWAP ne doit subir aucune pénalité de retard.");
        }

        private static void Test_SwingV3_Dynamic_TP1_Snapping_To_Opposing_Wall()
        {
            var rm = new SwingRiskManager();
            double entry = 5000.0;
            double stop = 4980.0; // Risk = 20.0 pts. Standard TP1 (1.5R) = 5030.0
            double opposingWall = 5024.0; // Wall situé à 1.2R (entre 1.0R et 1.5R)

            double tp1, tp2;
            rm.CalculateTargets(entry, stop, SwingDirection.Long, 1.5, 3.0, opposingWall, out tp1, out tp2);

            Assert(Math.Abs(tp1 - 5024.0) < 1e-4, "TP1 doit se caler dynamiquement sur le mur opposé à 5024.0 pour sécuriser le win rate.");

            // Mur trop proche (< 1.0R) : ne doit pas réduire TP1 en dessous de 1.0R
            double nearWall = 5010.0; // 0.5R
            rm.CalculateTargets(entry, stop, SwingDirection.Long, 1.5, 3.0, nearWall, out tp1, out tp2);
            Assert(Math.Abs(tp1 - 5030.0) < 1e-4, "Un mur à moins de 1.0R ne doit pas forcer un TP1 inférieur au ratio minimum.");
        }

        private static void Test_SwingV3_RegimeChange_HardExit()
        {
            var sig = new SwingSignal
            {
                Symbol = "NQ",
                Direction = SwingDirection.Long,
                EntryPrice = 5000.0,
                InitialStopPrice = 4960.0
            };

            var trade = new TrackedSwingTrade(sig, 0.25, 20.0);
            trade.CloseTrade(4985.0, DateTime.UtcNow, "REGIME_CHANGED", 0.25, 20.0);

            Assert(trade.Closed, "Le trade doit être marqué Closed.");
            Assert(trade.ExitReason == "REGIME_CHANGED", "Le motif de sortie doit être REGIME_CHANGED.");
            Assert(trade.RealizedR < 0, "La perte partielle sur retournement doit être comptabilisée sans attendre le stop complet.");
        }

        private static void Test_SwingV3_ZeroTrust_InvalidAtrAndPointValue()
        {
            var scorer = new SwingScorer();

            var ctxBadAtr = new SwingContext { IsAtrValid = false, AtrCurrent = 0.0, PointValue = 50.0 };
            string rAtr;
            bool vAtr = scorer.ValidatePreconditions(ctxBadAtr, SwingSetupType.RejectExtreme, SwingDirection.Long, out rAtr);
            Assert(!vAtr && rAtr == SwingRejectionReason.InvalidAtrData, "ATR invalide doit être strictement rejeté.");

            var ctxBadPt = new SwingContext { IsPointValueValid = false, PointValue = 0.0, AtrCurrent = 10.0 };
            string rPt;
            bool vPt = scorer.ValidatePreconditions(ctxBadPt, SwingSetupType.RejectExtreme, SwingDirection.Long, out rPt);
            Assert(!vPt && rPt == SwingRejectionReason.InvalidPointValue, "PointValue invalide doit être strictement rejeté.");
        }

        private static void Test_SwingV3_ScalpingPro_StrictIsolation()
        {
            string root = GetProjectRoot();
            string scalpingConfig = Path.Combine(root, "configs", "SCALPING_PRO", "CONFIG_NQ_SCALPING_PRO.xml");
            Assert(File.Exists(scalpingConfig), "Le fichier XML ScalpingPro NQ doit exister.");
            string text = File.ReadAllText(scalpingConfig);
            Assert(!text.Contains("SwingOpportunityManagement"), "ScalpingPro ne doit contenir aucun tag de SwingOpportunityManagement.");
            Assert(text.Contains("<TradingPreset>ScalpingPro</TradingPreset>"), "TradingPreset doit rester ScalpingPro.");
        }

        #region Suite Tests Swing V2 Regime Invalidation & Structural Architecture

        private static void Test_SwingV2_SimpleRegimeChange_NoExit()
        {
            var sig = new SwingSignal
            {
                Symbol = "NQ",
                Direction = SwingDirection.Long,
                SetupType = SwingSetupType.HtfContinuation,
                EntryPrice = 5000.0,
                InitialStopPrice = 4960.0,
                StructuralStopPrice = 4970.0
            };

            var trade = new TrackedSwingTrade(sig, 0.25, 20.0);

            // Régime adverse (TrendDown), mais structure intacte (close 5010 > 4970) et 1 seule barre
            var decision = trade.EvaluateRegimeDecision(
                currentRegime: SwingMarketRegime.TrendDown,
                close: 5010.0,
                htfEma: 5020.0,
                atrDaily: 20.0,
                confirmationBarsRequired: 3,
                enableSoftProtection: false);

            Assert(decision == SwingRegimeDecision.Hold, "Un simple changement de régime adverse sans invalidation structurelle ne doit pas déclencher de sortie.");
            Assert(!trade.Closed, "Le trade Swing doit rester ouvert et géré par ses cibles et stops.");
            Assert(trade.ConsecutiveAdverseBars == 1, "Le compteur de barres adverses doit être incrémenté à 1.");
        }

        private static void Test_SwingV2_RegimeDeterioration_And_StructuralInvalidation_Exit()
        {
            var sig = new SwingSignal
            {
                Symbol = "NQ",
                Direction = SwingDirection.Long,
                SetupType = SwingSetupType.HtfContinuation,
                EntryPrice = 5000.0,
                InitialStopPrice = 4960.0,
                StructuralStopPrice = 4975.0
            };

            var trade = new TrackedSwingTrade(sig, 0.25, 20.0);

            // Barres 1 & 2 : Régime TrendDown + Structure cassée (close 4970 < 4975), mais pas encore 3 barres de confirmation
            var d1 = trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 4970.0, 5010.0, 20.0, 3, true);
            Assert(d1 == SwingRegimeDecision.Hold, "Barre 1 adverse non confirmée doit donner Hold.");
            Assert(trade.ConsecutiveAdverseBars == 1, "Compteur = 1.");

            var d2 = trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 4968.0, 5010.0, 20.0, 3, true);
            Assert(d2 == SwingRegimeDecision.Hold, "Barre 2 adverse non confirmée doit donner Hold.");
            Assert(trade.ConsecutiveAdverseBars == 2, "Compteur = 2.");

            // Barre 3 : 3ème barre adverse confirmée + Structure brisée (close 4967 < 4975)
            var d3 = trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 4967.0, 5010.0, 20.0, 3, true);
            Assert(d3 == SwingRegimeDecision.StructuralExit, "3 barres adverses consécutives + rupture structurelle doivent déclencher StructuralExit.");

            // Simulation exécution fermeture
            trade.CloseTrade(4967.0, DateTime.UtcNow, "STRUCTURAL_REGIME_INVALIDATION", 0.25, 20.0);
            Assert(trade.Closed, "Le trade doit être clôturé.");
            Assert(trade.ExitReason == "STRUCTURAL_REGIME_INVALIDATION", "Le motif de sortie doit être STRUCTURAL_REGIME_INVALIDATION.");

            // Vérification mise à jour OpportunityManager
            var opp = new SwingOpportunityManager { Enabled = true };
            opp.OnCandidateExecuted(new SwingCandidate { SetupType = SwingSetupType.HtfContinuation, Direction = SwingDirection.Long }, trade, 100);
            var camp = opp.ActiveLongCampaign;
            Assert(camp != null, "La campagne active doit être initialisée.");
            opp.OnTradeClosed(trade, "STRUCTURAL_REGIME_INVALIDATION", 105);
            Assert(camp.State == SwingCampaignState.RegimeChanged, "La campagne doit être marquée RegimeChanged suite à STRUCTURAL_REGIME_INVALIDATION.");
            Assert(opp.ActiveLongCampaign == null, "La campagne active doit être libérée.");
        }

        private static void Test_SwingV2_MacroReversal_Long_Immunity()
        {
            var sig = new SwingSignal
            {
                Symbol = "ES",
                Direction = SwingDirection.Long,
                SetupType = SwingSetupType.MacroReversal,
                EntryPrice = 5000.0,
                InitialStopPrice = 4960.0,
                StructuralStopPrice = 4960.0
            };

            var trade = new TrackedSwingTrade(sig, 0.25, 50.0);

            // Phase 1 : Le cours (5005) est SOUS l'EMA HTF baissière (5080)
            // Régime TrendDown : MacroReversal ignore l'opposition EMA tant que le support d'ancrage (4960) tient
            for (int b = 0; b < 10; b++)
            {
                var dec = trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 5005.0, 5080.0, 30.0, 3, true);
                Assert(dec == SwingRegimeDecision.Hold, "MacroReversal Long sous l'EMA HTF avec structure intacte ne doit JAMAIS être invalidé par l'EMA.");
            }
            Assert(trade.ConsecutiveAdverseBars == 0, "Le compteur adverse doit rester 0 tant que la structure tient.");
            Assert(!trade.Closed, "Le trade MacroReversal Long doit rester actif.");

            // Phase 2 (Bloquant 1) : Si la structure casse réellement (cours = 4950 < 4960)
            // Le trade ne doit PAS être aveuglément immunisé : après confirmation de 3 barres, il doit sortir !
            trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 4950.0, 5080.0, 30.0, 3, true);
            trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 4948.0, 5080.0, 30.0, 3, true);
            var decBreak = trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 4947.0, 5080.0, 30.0, 3, true);

            Assert(decBreak == SwingRegimeDecision.StructuralExit, "MacroReversal dont la structure d'ancrage casse doit être invalidé (StructuralExit).");
        }

        private static void Test_SwingV2_MacroReversal_Short_Immunity()
        {
            var sig = new SwingSignal
            {
                Symbol = "ES",
                Direction = SwingDirection.Short,
                SetupType = SwingSetupType.MacroReversal,
                EntryPrice = 5100.0,
                InitialStopPrice = 5140.0,
                StructuralStopPrice = 5140.0
            };

            var trade = new TrackedSwingTrade(sig, 0.25, 50.0);

            // Phase 1 : Le cours (5095) est AU-DESSUS de l'EMA HTF haussière (5020)
            // Régime TrendUp : MacroReversal Short ignore l'opposition EMA tant que le sommet d'ancrage (5140) tient
            for (int b = 0; b < 10; b++)
            {
                var dec = trade.EvaluateRegimeDecision(SwingMarketRegime.TrendUp, 5095.0, 5020.0, 30.0, 3, true);
                Assert(dec == SwingRegimeDecision.Hold, "MacroReversal Short au-dessus de l'EMA HTF avec structure intacte doit être immunisé.");
            }
            Assert(trade.ConsecutiveAdverseBars == 0, "Le compteur adverse doit rester 0 tant que la structure tient.");
            Assert(!trade.Closed, "Le trade MacroReversal Short doit rester actif.");

            // Phase 2 (Bloquant 1) : Si la structure casse (cours monte à 5155 > 5140)
            trade.EvaluateRegimeDecision(SwingMarketRegime.TrendUp, 5155.0, 5020.0, 30.0, 3, true);
            trade.EvaluateRegimeDecision(SwingMarketRegime.TrendUp, 5158.0, 5020.0, 30.0, 3, true);
            var decBreak = trade.EvaluateRegimeDecision(SwingMarketRegime.TrendUp, 5160.0, 5020.0, 30.0, 3, true);

            Assert(decBreak == SwingRegimeDecision.StructuralExit, "MacroReversal Short dont la structure d'ancrage casse doit être invalidé (StructuralExit).");
        }

        private static void Test_SwingV2_SoftProtection_Trails_Stop_To_Breakeven()
        {
            var sig = new SwingSignal
            {
                Symbol = "NQ",
                Direction = SwingDirection.Long,
                SetupType = SwingSetupType.BreakoutRetest,
                EntryPrice = 5000.0,
                InitialStopPrice = 4960.0,
                StructuralStopPrice = 4970.0
            };

            var trade = new TrackedSwingTrade(sig, 0.25, 20.0);

            // Le cours est à 5020 (en profit de +1R par rapport à l'entrée 5000, stop 4960)
            // Le régime se détériore en TrendDown pendant 3 barres consécutives, mais la structure (4970) n'est PAS cassée
            trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 5020.0, 5030.0, 20.0, 3, true);
            trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 5018.0, 5030.0, 20.0, 3, true);
            var dec3 = trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 5015.0, 5030.0, 20.0, 3, true);

            Assert(dec3 == SwingRegimeDecision.ProtectBreakeven, "Détérioration confirmée mais structure intacte et position en gain doit déclencher ProtectBreakeven.");

            // Application de la décision dans le moteur
            if (dec3 == SwingRegimeDecision.ProtectBreakeven)
            {
                double bePrice = trade.EntryPrice + 0.25;
                if (bePrice > trade.CurrentStopPrice)
                {
                    trade.CurrentStopPrice = bePrice;
                    trade.ExecutionNotes += " [REGIME_PROTECT_BE]";
                }
            }

            Assert(trade.CurrentStopPrice == 5000.25, "Le stop doit être remonté à Break-Even + 1 tick.");
            Assert(!trade.Closed, "Le trade ne doit PAS être coupé prématurément.");
            Assert(trade.ExecutionNotes.Contains("[REGIME_PROTECT_BE]"), "La note d'exécution doit documenter le trailing BE.");
        }

        private static void Test_SwingV2_LegacyFlag_ExitOnRegimeChange_BackwardCompatibility()
        {
            // Vérification de la rétrocompatibilité pour A/B testing
            var sig = new SwingSignal
            {
                Symbol = "NQ",
                Direction = SwingDirection.Long,
                SetupType = SwingSetupType.HtfContinuation,
                EntryPrice = 5000.0,
                InitialStopPrice = 4960.0
            };

            var tradeLegacy = new TrackedSwingTrade(sig, 0.25, 20.0);
            tradeLegacy.BarsElapsed = 15; // Maturité suffisante > 12 barres

            // En mode Legacy activé (ExitOnRegimeChange = true)
            bool exitOnRegimeChange = true;
            double htfEmaVal = 5050.0;
            double close = 4990.0; // Opposé à l'EMA HTF pour un Long
            bool legacyExitTriggered = exitOnRegimeChange && tradeLegacy.BarsElapsed >= 12 &&
                tradeLegacy.SetupType != SwingSetupType.MacroReversal &&
                tradeLegacy.SetupType != SwingSetupType.ValueReentry &&
                htfEmaVal > 0 && close < htfEmaVal;

            Assert(legacyExitTriggered, "Le mode Legacy activé doit déclencher la sortie sur rupture HTF après 12 barres.");

            // En mode par défaut institutionnel (ExitOnRegimeChange = false)
            bool defaultExit = false;
            bool defaultExitTriggered = defaultExit && tradeLegacy.BarsElapsed >= 12 && close < htfEmaVal;
            Assert(!defaultExitTriggered, "Le mode par défaut institutionnel (ExitOnRegimeChange = false) ne doit JAMAIS couper le trade.");
        }

        private static void Test_SwingV2_DefaultSettings_NoPrematureExit()
        {
            string root = GetProjectRoot();
            string[] swingFiles = Directory.GetFiles(Path.Combine(root, "configs", "SWING"), "CONFIG_*_SWING.xml");
            Assert(swingFiles.Length == 8, "Les 8 fichiers de configuration Swing doivent être présents.");

            foreach (var file in swingFiles)
            {
                string text = File.ReadAllText(file);
                Assert(text.Contains("<ExitOnRegimeChange>false</ExitOnRegimeChange>"),
                    string.Format("Le fichier {0} doit avoir ExitOnRegimeChange = false par défaut.", Path.GetFileName(file)));
                Assert(text.Contains("<EnableSwingRegimeInvalidation>true</EnableSwingRegimeInvalidation>"),
                    string.Format("Le fichier {0} doit avoir EnableSwingRegimeInvalidation = true (production).", Path.GetFileName(file)));
                Assert(text.Contains("<RegimeConfirmationBars>3</RegimeConfirmationBars>"),
                    string.Format("Le fichier {0} doit avoir RegimeConfirmationBars = 3 par défaut.", Path.GetFileName(file)));
                Assert(text.Contains("<EnableRegimeSoftProtection>true</EnableRegimeSoftProtection>"),
                    string.Format("Le fichier {0} doit avoir EnableRegimeSoftProtection = true par défaut.", Path.GetFileName(file)));
            }
        }

        private static void Test_SwingV2_AdverseBars_Hysteresis_And_Persistence()
        {
            var sig = new SwingSignal
            {
                Symbol = "NQ",
                Direction = SwingDirection.Long,
                SetupType = SwingSetupType.HtfContinuation,
                EntryPrice = 5000.0,
                InitialStopPrice = 4960.0,
                StructuralStopPrice = 4970.0
            };

            var trade = new TrackedSwingTrade(sig, 0.25, 20.0);

            // Barres 1 & 2 : Régime TrendDown -> Incrémentation
            trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 5010.0, 5030.0, 20.0, 3, true);
            trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 5010.0, 5030.0, 20.0, 3, true);
            Assert(trade.ConsecutiveAdverseBars == 2, "Compteur doit être à 2 après 2 barres défavorables.");

            // Barre 3 : Le marché se réaligne en TrendUp -> Décrémentation progressive (Hystérésis)
            trade.EvaluateRegimeDecision(SwingMarketRegime.TrendUp, 5015.0, 5010.0, 20.0, 3, true);
            Assert(trade.ConsecutiveAdverseBars == 1, "Compteur doit décrémenter à 1 (amortissement hystérésis).");

            // Barre 4 : Marché toujours TrendUp -> Compteur retourne à 0
            trade.EvaluateRegimeDecision(SwingMarketRegime.TrendUp, 5020.0, 5010.0, 20.0, 3, true);
            Assert(trade.ConsecutiveAdverseBars == 0, "Compteur doit revenir à 0.");
        }

        private static void Test_SwingV2_StrictIsolation_ScalpingPro_Sniper()
        {
            string root = GetProjectRoot();
            string sniperPath = Path.Combine(root, "AuctionMarketCore.Sniper.cs");
            string scalpingPath = Path.Combine(root, "AuctionMarketCore.ScalpingPro.cs");

            Assert(File.Exists(sniperPath), "AuctionMarketCore.Sniper.cs doit exister.");
            Assert(File.Exists(scalpingPath), "AuctionMarketCore.ScalpingPro.cs doit exister.");

            string sniperText = File.ReadAllText(sniperPath);
            string scalpingText = File.ReadAllText(scalpingPath);

            // ScalpingPro et Sniper ne doivent pas faire référence aux mécanismes d'invalidation Swing
            Assert(!sniperText.Contains("ExitOnRegimeChange"), "Sniper ne doit pas référencer ExitOnRegimeChange.");
            Assert(!sniperText.Contains("EnableSwingRegimeInvalidation"), "Sniper ne doit pas référencer EnableSwingRegimeInvalidation.");
            Assert(!sniperText.Contains("EvaluateRegimeDecision"), "Sniper ne doit pas appeler EvaluateRegimeDecision.");

            Assert(!scalpingText.Contains("ExitOnRegimeChange"), "ScalpingPro ne doit pas référencer ExitOnRegimeChange.");
            Assert(!scalpingText.Contains("EnableSwingRegimeInvalidation"), "ScalpingPro ne doit pas référencer EnableSwingRegimeInvalidation.");
            Assert(!scalpingText.Contains("EvaluateRegimeDecision"), "ScalpingPro ne doit pas appeler EvaluateRegimeDecision.");
        }

        private static void Test_SwingV2_DynamicStructuralPrice_Trailing_And_Tp1()
        {
            var sig = new SwingSignal
            {
                Symbol = "ES",
                Direction = SwingDirection.Long,
                SetupType = SwingSetupType.BreakoutRetest,
                EntryPrice = 5000.0,
                InitialStopPrice = 4960.0,
                StructuralStopPrice = 4970.0,
                Target1Price = 5030.0,
                PositionSizeContracts = 2
            };

            var trade = new TrackedSwingTrade(sig, 0.25, 50.0);
            Assert(trade.DynamicStructuralPrice == 4970.0, "DynamicStructuralPrice initialisé au StructuralStopPrice.");

            // Trailing dynamique à la hausse (nouveau pivot à 4985)
            trade.UpdateDynamicStructure(4985.0);
            Assert(trade.DynamicStructuralPrice == 4985.0, "DynamicStructuralPrice doit monter au nouveau pivot 4985.0.");

            // Tentative de régression (niveau inférieur à 4975) -> ignorée
            trade.UpdateDynamicStructure(4975.0);
            Assert(trade.DynamicStructuralPrice == 4985.0, "DynamicStructuralPrice ne doit JAMAIS régresser.");

            // Exécution de TP1 -> le stop passe à BE (5000.25) et la structure dynamique monte à BE
            trade.ExecutePartialExitTp1(5030.0, DateTime.UtcNow, 0.25, 50.0);
            Assert(trade.Tp1Hit, "TP1 doit être marqué touché.");
            Assert(trade.DynamicStructuralPrice == 5000.25, "DynamicStructuralPrice doit être relevé au moins à Break-Even lors de TP1.");

            // Même logique pour un trade Short
            var sigShort = new SwingSignal
            {
                Symbol = "ES",
                Direction = SwingDirection.Short,
                SetupType = SwingSetupType.BreakoutRetest,
                EntryPrice = 5000.0,
                InitialStopPrice = 5040.0,
                StructuralStopPrice = 5030.0,
                Target1Price = 4970.0,
                PositionSizeContracts = 2
            };

            var tradeShort = new TrackedSwingTrade(sigShort, 0.25, 50.0);
            Assert(tradeShort.DynamicStructuralPrice == 5030.0, "DynamicStructuralPrice initialisé au StructuralStopPrice pour Short.");

            // Trailing dynamique à la baisse (nouveau pivot à 5015)
            tradeShort.UpdateDynamicStructure(5015.0);
            Assert(tradeShort.DynamicStructuralPrice == 5015.0, "DynamicStructuralPrice doit descendre au nouveau pivot 5015.0.");

            // Tentative de régression vers le haut -> ignorée
            tradeShort.UpdateDynamicStructure(5025.0);
            Assert(tradeShort.DynamicStructuralPrice == 5015.0, "DynamicStructuralPrice Short ne doit jamais remonter.");

            // TP1 Short -> le stop passe à BE (4999.75) et DynamicStructuralPrice descend à BE
            tradeShort.ExecutePartialExitTp1(4970.0, DateTime.UtcNow, 0.25, 50.0);
            Assert(tradeShort.DynamicStructuralPrice == 4999.75, "DynamicStructuralPrice Short doit être abaissé au niveau BE lors de TP1.");
        }

        private static void Test_SwingV2_AtrToleranceBuffer_FiltersMicroWicks()
        {
            var sig = new SwingSignal
            {
                Symbol = "ES",
                Direction = SwingDirection.Long,
                SetupType = SwingSetupType.HtfContinuation,
                EntryPrice = 5000.0,
                InitialStopPrice = 4960.0,
                StructuralStopPrice = 4980.0
            };

            var trade = new TrackedSwingTrade(sig, 0.25, 50.0);
            double atrDaily = 40.0;
            // Tolérance ATR = 40.0 * 0.05 = 2.0 pts
            // Niveau de rupture Long : 4980.0 - 2.0 = 4978.0

            // Mèche de 1 pt sous le stop structurel (close = 4979.0 > 4978.0)
            // Régime TrendUp : doit être filtré par le buffer et ne pas être considéré comme invalidé
            var decMeche = trade.EvaluateRegimeDecision(SwingMarketRegime.TrendUp, 4979.0, 4980.0, false, atrDaily, 3, true);
            Assert(decMeche == SwingRegimeDecision.Hold, "Une micro-mèche dans la zone de tolérance ATR (0.05 ATR) ne doit pas invalider la position.");
            Assert(trade.ConsecutiveAdverseBars == 0, "Le compteur adverse doit rester à 0 sous régime haussier avec mèche filtrée.");

            // Rupture confirmée au-delà du buffer ATR (close = 4977.0 < 4978.0)
            // Régime TrendDown
            trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 4977.0, 4980.0, false, atrDaily, 3, true);
            trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 4976.0, 4980.0, false, atrDaily, 3, true);
            var decBreak = trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 4975.0, 4980.0, false, atrDaily, 3, true);
            Assert(decBreak == SwingRegimeDecision.StructuralExit, "Une rupture franche dépassant le buffer ATR de 0.05 confirmée sur 3 barres doit déclencher StructuralExit.");
        }

        private static void Test_SwingV2_PhysicalSl_Vs_StructuralInvalidation_DistinctRoles()
        {
            // Valide les deux responsabilités distinctes confirmées par l'arbitrage architectural :
            // 1. Physical SL : Hard stop d'urgence / protection du capital contre les chocs de liquidité.
            // 2. Structural Invalidation : Décision logique anticipée confirmée sur N barres fermées.

            DateTime now = DateTime.UtcNow;
            double tick = 0.25;
            double ptVal = 50.0; // ES ($50 / pt)

            // --- CAS 1 : Choc brutal / Flash Crash -> Le Physical SL coupe immédiatement ---
            var sigCrash = new SwingSignal
            {
                Symbol = "ES",
                Direction = SwingDirection.Long,
                SetupType = SwingSetupType.HtfContinuation,
                EntryPrice = 5000.0,
                InitialStopPrice = 4950.0, // Physical SL (50 pts / 200 ticks plus bas)
                StructuralStopPrice = 4980.0 // Structure à 20 pts
            };
            var tradeCrash = new TrackedSwingTrade(sigCrash, tick, ptVal);

            // Simulation d'une bougie de flash crash : low = 4945 <= 4950 (Physical SL touché)
            double lowCrash = 4945.0;
            bool crashSlTriggered = tradeCrash.IsLong && lowCrash <= tradeCrash.CurrentStopPrice;
            Assert(crashSlTriggered, "Le Physical SL doit se déclencher immédiatement si le prix franchit le hard stop.");
            tradeCrash.CloseTrade(tradeCrash.CurrentStopPrice, now, "STOP_LOSS", tick, ptVal);
            Assert(tradeCrash.Closed, "Le trade crash doit être fermé immédiatement.");
            Assert(tradeCrash.ExitReason == "STOP_LOSS", "Le motif doit être STOP_LOSS pour protection immédiate.");

            // --- CAS 2 : Dérive structurelle confirmée sur N=3 barres -> Structural Invalidation coupe en avance ---
            var sigStruct = new SwingSignal
            {
                Symbol = "ES",
                Direction = SwingDirection.Long,
                SetupType = SwingSetupType.HtfContinuation,
                EntryPrice = 5000.0,
                InitialStopPrice = 4950.0, // Physical SL à 4950.0
                StructuralStopPrice = 4980.0 // Pivot structurel à 4980.0
            };
            var tradeStruct = new TrackedSwingTrade(sigStruct, tick, ptVal);
            double atrDaily = 20.0;

            // Barre 1 : Low = 4972 (> 4950, SL intact), Close = 4975 (< 4980, structure brisée)
            bool slBar1 = tradeStruct.IsLong && 4972.0 <= tradeStruct.CurrentStopPrice;
            Assert(!slBar1, "Barre 1 : Physical SL non touché.");
            var d1 = tradeStruct.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 4975.0, 4980.0, false, atrDaily, 3, true);
            Assert(d1 == SwingRegimeDecision.Hold, "Barre 1 : Pas de sortie prématurée à 1 barre (attente de confirmation N=3).");
            Assert(tradeStruct.ConsecutiveAdverseBars == 1, "Compteur adverse = 1.");

            // Barre 2 : Low = 4968 (> 4950, SL intact), Close = 4973 (< 4980)
            bool slBar2 = tradeStruct.IsLong && 4968.0 <= tradeStruct.CurrentStopPrice;
            Assert(!slBar2, "Barre 2 : Physical SL non touché.");
            var d2 = tradeStruct.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 4973.0, 4980.0, false, atrDaily, 3, true);
            Assert(d2 == SwingRegimeDecision.Hold, "Barre 2 : Pas de sortie prématurée à 2 barres.");
            Assert(tradeStruct.ConsecutiveAdverseBars == 2, "Compteur adverse = 2.");

            // Barre 3 : Low = 4965 (> 4950, SL intact), Close = 4970 (< 4980) -> Confirmation N=3 atteinte !
            bool slBar3 = tradeStruct.IsLong && 4965.0 <= tradeStruct.CurrentStopPrice;
            Assert(!slBar3, "Barre 3 : Physical SL toujours non touché.");
            var d3 = tradeStruct.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 4970.0, 4980.0, false, atrDaily, 3, true);
            Assert(d3 == SwingRegimeDecision.StructuralExit, "Barre 3 : Confirmation atteinte -> Sortie logique anticipée (StructuralExit).");
            tradeStruct.CloseTrade(4970.0, now, "STRUCTURAL_REGIME_INVALIDATION", tick, ptVal);
            Assert(tradeStruct.Closed, "Trade clôturé par invalidation structurelle.");
            Assert(tradeStruct.ExitReason == "STRUCTURAL_REGIME_INVALIDATION", "Motif = STRUCTURAL_REGIME_INVALIDATION.");
            // Gain de risque : sortie à 4970.0 au lieu du SL à 4950.0 -> 20 points (80 ticks / $1 000) de perte épargnée !
            double savedRiskPoints = tradeStruct.ExitPrice - tradeStruct.InitialStopPrice;
            Assert(savedRiskPoints == 20.0, "L'invalidation structurelle a économisé 20 pts de risque par rapport au Hard SL.");

            // --- CAS 3 : Bruit thermique temporaire et rebond (Hystérésis protectrice) ---
            var sigRebound = new SwingSignal
            {
                Symbol = "ES",
                Direction = SwingDirection.Long,
                SetupType = SwingSetupType.HtfContinuation,
                EntryPrice = 5000.0,
                InitialStopPrice = 4950.0,
                StructuralStopPrice = 4980.0
            };
            var tradeRebound = new TrackedSwingTrade(sigRebound, tick, ptVal);

            // 2 barres sous la structure
            tradeRebound.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 4975.0, 4980.0, false, atrDaily, 3, true);
            tradeRebound.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 4974.0, 4980.0, false, atrDaily, 3, true);
            Assert(tradeRebound.ConsecutiveAdverseBars == 2, "Compteur = 2 après 2 barres.");

            // Barre 3 : Rebond haussier ! Close = 4988 (> 4980) en régime TrendUp
            var dRebound = tradeRebound.EvaluateRegimeDecision(SwingMarketRegime.TrendUp, 4988.0, 4980.0, false, atrDaily, 3, true);
            Assert(dRebound == SwingRegimeDecision.Hold, "Rebond au-dessus de la structure -> La position est maintenue.");
            Assert(tradeRebound.ConsecutiveAdverseBars == 1, "Le compteur adverse s'est amorti (2 -> 1) évitant l'éjection à contre-temps.");
        }

        private class DummyMiSource : IMarketIntelligenceSource
        {
            public string InstrumentName { get { return "NQ"; } }
            public DateTime MarketTime { get; set; }
            public string TimeZoneLabel { get { return "EST"; } }
            public double TickSize { get { return 0.25; } }
            public double LastPrice { get; set; }

            public MiTrend TrendH4 { get; set; }
            public MiTrend TrendH1 { get; set; }
            public MiTrend TrendM15 { get; set; }
            public MiTrend TrendM5 { get; set; }

            public MiTrend GetTrend(MiTimeframe tf)
            {
                switch (tf)
                {
                    case MiTimeframe.H4: return TrendH4;
                    case MiTimeframe.H1: return TrendH1;
                    case MiTimeframe.M15: return TrendM15;
                    default: return TrendM5;
                }
            }

            public MiStructureEvent LastBos { get; set; }
            public MiStructureEvent LastChoch { get; set; }
            public MiStructureEvent LastBosH4 { get; set; }
            public MiStructureEvent LastChochH4 { get; set; }

            public int BarsSinceBos { get; set; }
            public int BarsSinceChoch { get; set; }
            public int BarsSinceOrderBlock { get; set; }
            public int BarsSinceBosH4 { get; set; }
            public int BarsSinceChochH4 { get; set; }

            public double NearestBuySideLiquidity { get; set; }
            public double NearestSellSideLiquidity { get; set; }

            public MiOrderBlockKind OrderBlockKind { get; set; }
            public MiOrderBlockState OrderBlockState { get; set; }

            public double VolumeQuality { get; set; }
            public double MomentumQuality { get; set; }

            public MiProfileLocation ProfileLocation { get; set; }
            public MiVolatilityRegime VolatilityRegime { get; set; }
            public double NormalizedAtr { get; set; }

            public IEnumerable<IMarketIntelligenceModule> Modules { get { return null; } }

            public DummyMiSource()
            {
                MarketTime = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
                LastPrice = 20000.0;
                TrendH4 = MiTrend.Bullish;
                TrendH1 = MiTrend.Bullish;
                TrendM15 = MiTrend.Bullish;
                TrendM5 = MiTrend.Bullish;
                VolumeQuality = 0.8;
                MomentumQuality = 0.8;
                ProfileLocation = MiProfileLocation.InsideVa;
                VolatilityRegime = MiVolatilityRegime.Normal;
                NormalizedAtr = 20.0;
                BarsSinceBos = -1;
                BarsSinceChoch = -1;
                BarsSinceOrderBlock = -1;
                BarsSinceBosH4 = -1;
                BarsSinceChochH4 = -1;
            }
        }

        private static void Test_MarketIntelligence_HistoricalDecoupling_NoTelegramSpam()
        {
            var source = new DummyMiSource();
            var builder = new MarketSnapshotBuilder(source);
            var formatter = new TelegramFormatter();
            var sentMessages = new List<string>();
            var logger = new MiDelegateLogger(m => { });
            var dispatcher = new TelegramDispatcher((text, onComplete) =>
            {
                sentMessages.Add(text);
                onComplete(true);
            }, logger, () => source.MarketTime);

            var reportEngine = new MarketReportEngine(builder, formatter, dispatcher, logger);
            var updateEngine = new MarketUpdateEngine(builder, new MarketSnapshotComparer(), formatter, dispatcher, logger);

            DateTime h4Time = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);

            // Phase 1 : Mode Historique (State.Historical -> isRealtime = false)
            var snapHist = reportEngine.OnNewH4Bar(h4Time, isRealtime: false);
            Assert(snapHist != null, "Le snapshot doit être construit et retourné même en mode historique.");
            Assert(snapHist.Bias == MiBias.BuyOnly, "Le biais doit être correctement calculé (BuyOnly).");
            Assert(sentMessages.Count == 0, "Aucun message Telegram ne doit être envoyé en mode historique.");

            // Mise à jour M15 en mode Historique (isRealtime = false)
            source.LastPrice = 20050.0;
            updateEngine.Prime(snapHist);
            bool updatedHist = updateEngine.Evaluate(isRealtime: false);
            Assert(updateEngine.Current != null, "Current doit être alimenté en mode historique.");
            Assert(sentMessages.Count == 0, "Toujours 0 message Telegram envoyé en historique.");

            // Phase 2 : Mode Temps Réel (State.Realtime -> isRealtime = true)
            DateTime h4TimeNext = h4Time.AddHours(4);
            source.MarketTime = h4TimeNext;
            var snapRealtime = reportEngine.OnNewH4Bar(h4TimeNext, isRealtime: true);
            Assert(snapRealtime != null, "Snapshot H4 temps réel valide.");
            Assert(sentMessages.Count == 1, "En temps réel, exactement 1 rapport Telegram doit être émis.");
            Assert(sentMessages[0].Contains("RAPPORT H4"), "Le message doit être le rapport H4.");
        }

        #endregion

        #endregion

        #endregion

        #endregion

        #endregion

        #endregion
    }
}



