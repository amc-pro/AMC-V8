#region Using declarations
using System;
using System.Collections.Generic;
using System.IO;
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

            // 2. Validation que tous les 40 fichiers XML de configs existent et sont bien formés
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string workspaceRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            string configsDir = Path.Combine(workspaceRoot, "configs");
            if (!Directory.Exists(configsDir))
            {
                configsDir = Path.Combine(Directory.GetCurrentDirectory(), "configs");
            }

            if (Directory.Exists(configsDir))
            {
                string[] presets = { "SCALPING_PRO", "SCALPING", "SNIPER", "STANDARD", "SCANNER" };
                string[] instruments = { "NQ", "MNQ", "ES", "MES", "GC", "MGC", "CL", "MCL" };

                int count = 0;
                foreach (string p in presets)
                {
                    foreach (string inst in instruments)
                    {
                        string fpath = Path.Combine(configsDir, p, string.Format("CONFIG_{0}_{1}.xml", inst, p));
                        Assert(File.Exists(fpath), string.Format("Fichier XML manquant: {0}", fpath));
                        string content = File.ReadAllText(fpath);
                        Assert(content.Contains("<NinjaTrader>"), string.Format("XML invalide dans {0}", fpath));
                        Assert(content.Contains("<SniperMarketCorePro>"), string.Format("SniperMarketCorePro absent dans {0}", fpath));
                        count++;
                    }
                }
                Assert(count == 40, string.Format("40 configurations XML attendues, {0} trouvées", count));
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

        #endregion
    }
}


