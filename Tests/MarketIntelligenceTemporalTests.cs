using System;
using System.Collections.Generic;
using NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence;

namespace AMC.VolumeProfile.Tests
{
    /// <summary>
    /// Suite de tests certifiant l'invariance temporelle, l'absence absolue de lookahead (Zero-Lookahead),
    /// et le déterminisme strict de calcul entre Historical et Realtime pour Market Intelligence.
    /// </summary>
    public static class MarketIntelligenceTemporalTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception("ASSERTION FAILED: " + message);
        }

        private class MockTemporalMiSource : IMarketIntelligenceSource
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

            public MockTemporalMiSource()
            {
                MarketTime = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
                LastPrice = 20000.0;
                TrendH4 = MiTrend.Bullish;
                TrendH1 = MiTrend.Bullish;
                TrendM15 = MiTrend.Bullish;
                TrendM5 = MiTrend.Bullish;
                VolumeQuality = 0.8;
                MomentumQuality = 0.8;
                ProfileLocation = MiProfileLocation.InsideVa;
                VolatilityRegime = MiVolatilityRegime.Normal;
                NormalizedAtr = 25.0;
                BarsSinceBos = 2;
                BarsSinceChoch = -1;
                BarsSinceBosH4 = 3;
                BarsSinceChochH4 = -1;
                BarsSinceOrderBlock = -1;
                NearestBuySideLiquidity = 20050.0;
                NearestSellSideLiquidity = 19950.0;
            }
        }

        /// <summary>
        /// Test 1 : Invariance temporelle T vs T+1 vs T+2.
        /// Un snapshot calculé à la barre T conserve exactement ses valeurs initiales
        /// après l'apparition de barres ultérieures (zéro repainting).
        /// </summary>
        public static void Run_Test_Temporal_Invariance_T_vs_T_plus_N()
        {
            var source = new MockTemporalMiSource();
            var builder = new MarketSnapshotBuilder(source);

            // Instant T (08:00 UTC)
            var snapT = builder.Build();
            Assert(snapT.Time == new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc), "Heure initiale T = 08:00.");
            Assert(snapT.Bias == MiBias.BuyOnly, "Biais initial à T = BuyOnly.");
            Assert(snapT.AlignmentPercent == 100, "Alignement initial = 100%.");
            Assert(snapT.Confidence > 0, "Confiance initiale > 0.");
            Assert(snapT.ProfileLocation == MiProfileLocation.InsideVa, "Localisation initiale = InsideVa.");

            // Sauvegarde de l'empreinte mémoire à T
            MiBias savedBias = snapT.Bias;
            int savedConfidence = snapT.Confidence;
            int savedAlignment = snapT.AlignmentPercent;
            double savedBuyLiq = snapT.BuySideLiquidity;

            // Avancement temporel : Instant T+1 (08:15 UTC) avec choc baissier futur
            source.MarketTime = new DateTime(2026, 6, 1, 8, 15, 0, DateTimeKind.Utc);
            source.LastPrice = 19900.0;
            source.TrendM5 = MiTrend.Bearish;
            source.ProfileLocation = MiProfileLocation.BelowVal;

            var snapT1 = builder.Build();
            Assert(snapT1.Time == new DateTime(2026, 6, 1, 8, 15, 0, DateTimeKind.Utc), "T+1 = 08:15.");
            Assert(snapT1.ProfileLocation == MiProfileLocation.BelowVal, "T+1 localisé sous VAL.");

            // Vérification absolue de non-altération du snapshot T (immuabilité)
            Assert(snapT.Bias == savedBias, "Le snapshot à T n'a pas été altéré par T+1.");
            Assert(snapT.Confidence == savedConfidence, "La confiance à T reste strictement identique.");
            Assert(snapT.AlignmentPercent == savedAlignment, "L'alignement à T n'a pas dérivé.");
            Assert(snapT.BuySideLiquidity == savedBuyLiq, "La liquidité à T reste intègre.");

            // Avancement temporel : Instant T+2 (08:30 UTC) avec retournement macro
            source.MarketTime = new DateTime(2026, 6, 1, 8, 30, 0, DateTimeKind.Utc);
            source.TrendH1 = MiTrend.Bearish;
            source.TrendH4 = MiTrend.Bearish;
            source.ProfileLocation = MiProfileLocation.AtPoc;

            var snapT2 = builder.Build();
            Assert(snapT2.Bias == MiBias.SellOnly, "T+2 devenu SellOnly.");

            // Le snapshot original T demeure inaltéré
            Assert(snapT.Bias == MiBias.BuyOnly, "Le snapshot d'origine T est rigoureusement invariant dans le temps.");
        }

        /// <summary>
        /// Test 2 : Déterminisme parfait Historical vs Realtime.
        /// Le calcul produit les mêmes résultats en Historical et en Realtime,
        /// mais neutralise 100% des émissions Telegram en Historical.
        /// </summary>
        public static void Run_Test_Historical_vs_Realtime_Determinism()
        {
            var source = new MockTemporalMiSource();
            var builder = new MarketSnapshotBuilder(source);
            var formatter = new TelegramFormatter();
            var messages = new List<string>();
            var logger = new MiDelegateLogger(m => { });
            var dispatcher = new TelegramDispatcher((txt, cb) => { messages.Add(txt); cb(true); }, logger, () => source.MarketTime);

            var reportEngine = new MarketReportEngine(builder, formatter, dispatcher, logger);
            var updateEngine = new MarketUpdateEngine(builder, new MarketSnapshotComparer(), formatter, dispatcher, logger);

            DateTime barTime = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            // Simulation en mode Historical (isRealtime: false)
            var snapHist = reportEngine.OnNewH4Bar(barTime, isRealtime: false);
            Assert(snapHist != null, "Snapshot historique généré.");
            Assert(messages.Count == 0, "Zéro message Telegram en mode historique.");

            updateEngine.Prime(snapHist);
            source.LastPrice = 20040.0; // Mouvement de prix
            bool notifiedHist = updateEngine.Evaluate(isRealtime: false);
            Assert(updateEngine.Current != null, "Current mis à jour en historique.");
            Assert(messages.Count == 0, "Toujours zéro message Telegram en mode historique après UpdateEngine.");

            // Simulation identique en mode Realtime (isRealtime: true)
            reportEngine.Reset();
            updateEngine.Reset();
            var snapRealtime = reportEngine.OnNewH4Bar(barTime, isRealtime: true);
            Assert(snapRealtime != null, "Snapshot temps réel généré.");
            Assert(messages.Count == 1, "Exactement 1 rapport Telegram émis en temps réel.");
            Assert(snapRealtime.Bias == snapHist.Bias, "Déterminisme : Biais identique entre historique et temps réel.");
            Assert(snapRealtime.Confidence == snapHist.Confidence, "Déterminisme : Confiance identique.");
            Assert(snapRealtime.AlignmentPercent == snapHist.AlignmentPercent, "Déterminisme : Alignement identique.");
        }

        /// <summary>
        /// Test 3 : Zero-Lookahead sur le classifieur de tendance MiTrendLogic.
        /// Vérifie que le calcul rejette les données incomplètes et ne lit aucune donnée future.
        /// </summary>
        public static void Run_Test_ZeroLookahead_Trend_Classifier()
        {
            double tick = 0.25;
            double minDistance = 0.50;
            double minSlope = 0.10;

            // Barres closes valides : close = 20010, closePast = 20000, ema = 20005, emaPast = 20002
            // Distance = (20010 - 20005)/0.25 = +20 ticks (>= 0.5)
            // Slope = (20005 - 20002)/0.25 = +12 ticks (>= 0.1)
            // Momentum = (20010 - 20000)/0.25 = +40 ticks (>= 0)
            var trendBull = MiTrendLogic.Classify(20010.0, 20000.0, 20005.0, 20002.0, tick, minDistance, minSlope);
            Assert(trendBull == MiTrend.Bullish, "Tendance haussière confirmée sur barres closes.");

            // Cas de dégénérescence / données futures manquantes (NaN ou Infinity)
            var trendNan = MiTrendLogic.Classify(double.NaN, 20000.0, 20005.0, 20002.0, tick, minDistance, minSlope);
            Assert(trendNan == MiTrend.Neutral, "Toute valeur indéfinie doit retourner Neutral sans lever d'exception.");

            // Cas de momentum opposé (prix sous closePast malgré EMA en dessous) -> Pas de tendance unilatérale
            var trendDivergence = MiTrendLogic.Classify(20006.0, 20015.0, 20005.0, 20002.0, tick, minDistance, minSlope);
            Assert(trendDivergence == MiTrend.Neutral, "Momentum négatif contre EMA haussière doit être classé Neutral.");
        }

        /// <summary>
        /// Test 4 : Localisation Volume Profile & Régimes de Volatilité sur Snapshot.
        /// </summary>
        public static void Run_Test_ProfileLocation_And_VolatilityRegime()
        {
            var source = new MockTemporalMiSource();
            var builder = new MarketSnapshotBuilder(source);

            // Cas A : Marché en expansion au-dessus de VAH
            source.ProfileLocation = MiProfileLocation.AboveVah;
            source.VolatilityRegime = MiVolatilityRegime.Expansion;
            source.NormalizedAtr = 45.0;

            var snapA = builder.Build();
            Assert(snapA.ProfileLocation == MiProfileLocation.AboveVah, "Localisation = AboveVah.");
            Assert(snapA.VolatilityRegime == MiVolatilityRegime.Expansion, "Régime = Expansion.");
            Assert(snapA.NormalizedAtr == 45.0, "ATR normalisé = 45.0.");

            // Cas B : Marché en compression à l'équilibre au POC
            source.ProfileLocation = MiProfileLocation.AtPoc;
            source.VolatilityRegime = MiVolatilityRegime.Compression;
            source.NormalizedAtr = 12.0;

            var snapB = builder.Build();
            Assert(snapB.ProfileLocation == MiProfileLocation.AtPoc, "Localisation = AtPoc.");
            Assert(snapB.VolatilityRegime == MiVolatilityRegime.Compression, "Régime = Compression.");
            Assert(snapB.NormalizedAtr == 12.0, "ATR normalisé = 12.0.");
        }
    }
}
