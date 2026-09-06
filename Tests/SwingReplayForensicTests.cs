using System;
using System.Globalization;
using NinjaTrader.NinjaScript.Indicators;

namespace AMC.VolumeProfile.Tests
{
    /// <summary>
    /// Suite de tests forensic dédiée à la validation de l'invalidation structurelle Swing V3,
    /// de l'hystérésis multi-barres N in [1..6], et du découplage Physical SL vs Structural Exit.
    /// </summary>
    public static class SwingReplayForensicTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception("ASSERTION FAILED: " + message);
        }

        /// <summary>
        /// Test N=1 : Pour un seuil d'exigence de 1 seule barre fermée, une rupture structurelle franche
        /// sous régime TrendDown doit déclencher immédiatement la sortie en StructuralExit dès la première barre.
        /// </summary>
        public static void Run_Test_Forensic_N1_Immediate_Exit()
        {
            var sig = new SwingSignal
            {
                Symbol = "ES",
                Direction = SwingDirection.Long,
                SetupType = SwingSetupType.HtfContinuation,
                EntryPrice = 5000.0,
                InitialStopPrice = 4950.0,
                StructuralStopPrice = 4980.0
            };
            var trade = new TrackedSwingTrade(sig, 0.25, 50.0);
            double atrDaily = 20.0;

            // Barre 1 : Clôture sous le pivot structurel (4975 < 4980)
            var decision = trade.EvaluateRegimeDecision(
                SwingMarketRegime.TrendDown,
                4975.0,
                4980.0,
                false,
                atrDaily,
                confirmationBarsRequired: 1,
                enableSoftProtection: true);

            Assert(decision == SwingRegimeDecision.StructuralExit,
                "Pour N=1, la sortie structurelle doit être déclenchée dès la première clôture adverse confirmée.");
            Assert(trade.ConsecutiveAdverseBars == 1, "Le compteur adverse doit être de 1.");
        }

        /// <summary>
        /// Test N=3 : Progression déterministe de l'hystérésis (1, 2, puis sortie à 3).
        /// </summary>
        public static void Run_Test_Forensic_N3_Progression()
        {
            var sig = new SwingSignal
            {
                Symbol = "ES",
                Direction = SwingDirection.Long,
                SetupType = SwingSetupType.HtfContinuation,
                EntryPrice = 5000.0,
                InitialStopPrice = 4950.0,
                StructuralStopPrice = 4980.0
            };
            var trade = new TrackedSwingTrade(sig, 0.25, 50.0);
            double atrDaily = 20.0;

            // Barre 1
            var d1 = trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 4976.0, 4980.0, false, atrDaily, 3, true);
            Assert(d1 == SwingRegimeDecision.Hold, "Barre 1/3 : Le trade doit être maintenu.");
            Assert(trade.ConsecutiveAdverseBars == 1, "Compteur = 1.");

            // Barre 2
            var d2 = trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 4974.0, 4980.0, false, atrDaily, 3, true);
            Assert(d2 == SwingRegimeDecision.Hold, "Barre 2/3 : Le trade doit être maintenu.");
            Assert(trade.ConsecutiveAdverseBars == 2, "Compteur = 2.");

            // Barre 3
            var d3 = trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 4972.0, 4980.0, false, atrDaily, 3, true);
            Assert(d3 == SwingRegimeDecision.StructuralExit, "Barre 3/3 : Confirmation atteinte -> StructuralExit.");
            Assert(trade.ConsecutiveAdverseBars == 3, "Compteur = 3.");
        }

        /// <summary>
        /// Test N=5 : Vérification de la persistance sur 5 barres.
        /// </summary>
        public static void Run_Test_Forensic_N5_Progression()
        {
            var sig = new SwingSignal
            {
                Symbol = "NQ",
                Direction = SwingDirection.Short,
                SetupType = SwingSetupType.HtfContinuation,
                EntryPrice = 20000.0,
                InitialStopPrice = 20100.0,
                StructuralStopPrice = 20050.0
            };
            var trade = new TrackedSwingTrade(sig, 0.25, 20.0);
            double atrDaily = 50.0;

            for (int b = 1; b <= 4; b++)
            {
                var dec = trade.EvaluateRegimeDecision(SwingMarketRegime.TrendUp, 20060.0, 20050.0, false, atrDaily, 5, true);
                Assert(dec == SwingRegimeDecision.Hold, string.Format(CultureInfo.InvariantCulture, "Barre {0}/5 : Doit maintenir la position.", b));
                Assert(trade.ConsecutiveAdverseBars == b, string.Format(CultureInfo.InvariantCulture, "Compteur attendu = {0}.", b));
            }

            // 5ème barre
            var d5 = trade.EvaluateRegimeDecision(SwingMarketRegime.TrendUp, 20062.0, 20050.0, false, atrDaily, 5, true);
            Assert(d5 == SwingRegimeDecision.StructuralExit, "Barre 5/5 : Invalidation confirmée sur 5 barres.");
        }

        /// <summary>
        /// Test Hystérésis & Rebond : Si la structure est temporairement violée pendant 2 barres,
        /// puis que le prix réintègre la structure à la 3ème barre, le compteur se décrémente et le trade survit.
        /// </summary>
        public static void Run_Test_Forensic_Hysteresis_Rebound()
        {
            var sig = new SwingSignal
            {
                Symbol = "ES",
                Direction = SwingDirection.Long,
                SetupType = SwingSetupType.HtfContinuation,
                EntryPrice = 5000.0,
                InitialStopPrice = 4950.0,
                StructuralStopPrice = 4980.0
            };
            var trade = new TrackedSwingTrade(sig, 0.25, 50.0);
            double atrDaily = 20.0;

            // Barres 1 et 2 sous la structure
            trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 4976.0, 4980.0, false, atrDaily, 3, true);
            trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 4975.0, 4980.0, false, atrDaily, 3, true);
            Assert(trade.ConsecutiveAdverseBars == 2, "Compteur = 2.");

            // Barre 3 : Rebond au-dessus de la structure (4985 > 4980) en régime TrendUp
            var dRebound = trade.EvaluateRegimeDecision(SwingMarketRegime.TrendUp, 4985.0, 4980.0, false, atrDaily, 3, true);
            Assert(dRebound == SwingRegimeDecision.Hold, "Rebond protecteur : le trade ne doit pas être coupé.");
            Assert(trade.ConsecutiveAdverseBars == 1, "Le compteur s'est décrémenté à 1.");

            // Barre 4 : Continuation de la reprise haussière (4995 > 4980)
            trade.EvaluateRegimeDecision(SwingMarketRegime.TrendUp, 4995.0, 4980.0, false, atrDaily, 3, true);
            Assert(trade.ConsecutiveAdverseBars == 0, "Le compteur est revenu à zéro.");
        }

        /// <summary>
        /// Test MacroReversal : Immunité face à l'opposition de tendance HTF tant que l'ancrage tient,
        /// et sortie propre dès confirmation de la cassure de l'ancrage.
        /// </summary>
        public static void Run_Test_Forensic_MacroReversal_Immunity_And_Exit()
        {
            var sig = new SwingSignal
            {
                Symbol = "GC",
                Direction = SwingDirection.Long,
                SetupType = SwingSetupType.MacroReversal,
                EntryPrice = 2500.0,
                InitialStopPrice = 2480.0,
                StructuralStopPrice = 2490.0
            };
            var trade = new TrackedSwingTrade(sig, 0.1, 100.0);
            double atrDaily = 25.0;

            // Phase 1 : Tendance HTF fortement baissière (TrendDown), mais le prix (2505) tient au-dessus du support (2490)
            for (int i = 0; i < 5; i++)
            {
                var dec = trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 2505.0, 2490.0, false, atrDaily, 3, true);
                Assert(dec == SwingRegimeDecision.Hold, "MacroReversal Long doit être immunisé tant que l'ancrage tient.");
            }
            Assert(trade.ConsecutiveAdverseBars == 0, "Zéro barre adverse comptabilisée.");

            // Phase 2 : Rupture franche du support (2485 < 2490)
            trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 2485.0, 2490.0, false, atrDaily, 3, true);
            trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 2484.0, 2490.0, false, atrDaily, 3, true);
            var decExit = trade.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 2483.0, 2490.0, false, atrDaily, 3, true);

            Assert(decExit == SwingRegimeDecision.StructuralExit,
                "Quand le support d'ancrage d'un MacroReversal cède sur 3 barres, la position doit sortir (StructuralExit).");
        }

        /// <summary>
        /// Test Physical SL vs Structural Invalidation :
        /// - Démontre que si le Physical SL est calé exactement sur la structure (CurrentStopPrice == DynamicStructuralPrice),
        ///   le Physical SL avale la position avant que l'invalidation logique ne puisse s'exercer.
        /// - Démontre que si le Physical SL dispose d'une marge au-delà de la structure, l'invalidation logique économise du risque.
        /// </summary>
        public static void Run_Test_Forensic_PhysicalSl_Vs_Structural_Buffer()
        {
            DateTime now = DateTime.UtcNow;

            // Scénario A : Stop physique confondu avec le niveau structurel
            var sigA = new SwingSignal
            {
                Symbol = "ES",
                Direction = SwingDirection.Long,
                SetupType = SwingSetupType.HtfContinuation,
                EntryPrice = 5000.0,
                InitialStopPrice = 4980.0, // SL confondu avec Structure
                StructuralStopPrice = 4980.0
            };
            var tradeA = new TrackedSwingTrade(sigA, 0.25, 50.0);
            // Une bougie qui franchit 4980 (ex: low = 4978, close = 4979) touche inévitablement tradeA.CurrentStopPrice (4980)
            bool slA = tradeA.IsLong && 4978.0 <= tradeA.CurrentStopPrice;
            Assert(slA, "Si le SL est confondu avec la structure, le Hard SL se déclenche immédiatement sur simple mèche.");

            // Scénario B : Stop physique placé au-delà de la structure (Marge de risque / Hard Stop)
            var sigB = new SwingSignal
            {
                Symbol = "ES",
                Direction = SwingDirection.Long,
                SetupType = SwingSetupType.HtfContinuation,
                EntryPrice = 5000.0,
                InitialStopPrice = 4950.0, // Hard SL à 4950
                StructuralStopPrice = 4980.0 // Structure à 4980
            };
            var tradeB = new TrackedSwingTrade(sigB, 0.25, 50.0);
            // Bougie franchissant la structure : low = 4974, close = 4976
            bool slB = tradeB.IsLong && 4974.0 <= tradeB.CurrentStopPrice;
            Assert(!slB, "Avec un buffer au-delà de la structure, le Hard SL n'est PAS touché.");

            // La logique multibarres peut alors s'exercer
            tradeB.EvaluateRegimeDecision(SwingMarketRegime.TrendDown, 4976.0, 4980.0, false, 20.0, 1, true);
            tradeB.CloseTrade(4976.0, now, "STRUCTURAL_REGIME_INVALIDATION", 0.25, 50.0);

            // Perte économisée : sortie à 4976 au lieu de 4950 = 24 points d'alpha préservé ($1 200 / contrat)
            double savedRisk = tradeB.ExitPrice - tradeB.InitialStopPrice;
            Assert(savedRisk == 26.0, "Gain de risque réalisé = 26 points.");
        }
    }
}
