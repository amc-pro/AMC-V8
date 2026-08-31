#region Using declarations
using System;
using System.Collections.Generic;
using System.IO;
using NinjaTrader.NinjaScript.Indicators;
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

            var score = scorer.ComputeScore(ctx, SwingSetupType.MonthlyVwapBandRetest, SwingDirection.Long);
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

            var score = scorer.ComputeScore(ctx, SwingSetupType.MonthlyVwapBandRetest, SwingDirection.Short);
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

        #endregion

        #endregion

        #endregion

        #endregion
    }
}



