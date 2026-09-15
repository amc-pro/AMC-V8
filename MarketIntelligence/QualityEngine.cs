#region Using declarations
using System;
using System.Globalization;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence
{
    /// <summary>
    /// États discrets de qualité du contexte de marché (Sprint 3).
    /// </summary>
    public enum ContextQualityState
    {
        Invalidated = 0,   // Score < 40 ou conflit critique
        Degraded = 1,      // 40 <= Score < 55 (contexte médiocre, taille réduite ou abstention)
        Watch = 2,         // 55 <= Score < 70 (contexte neutre/acceptable)
        Ready = 3,         // 70 <= Score < 85 (contexte favorable avec confluence)
        Confirmed = 4      // Score >= 85 (contexte institutionnel optimal aligné)
    }

    /// <summary>
    /// Résultat détaillé et explicable de l'évaluation contextuelle.
    /// </summary>
    public sealed class QualityEvaluation
    {
        public double TotalScore { get; set; }
        public ContextQualityState State { get; set; }

        public double TrendScore { get; set; }
        public double StructureScore { get; set; }
        public double LocationScore { get; set; }
        public double VolatilityScore { get; set; }
        public double Penalties { get; set; }

        public string Summary { get; set; }

        public bool IsTradeable
        {
            get { return State >= ContextQualityState.Watch && TotalScore >= 50.0; }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "[{0}] Score={1:F1} (Trend={2:F1}, Struct={3:F1}, Loc={4:F1}, Vol={5:F1}, Pen={6:F1}) | {7}",
                State, TotalScore, TrendScore, StructureScore, LocationScore, VolatilityScore, Penalties, Summary);
        }
    }

    /// <summary>
    /// Moteur d'évaluation quantitative de la qualité du contexte de marché (Sprint 3).
    /// Calcule un score explicable déterministe (0 à 100) pour une direction donnée (Long ou Short).
    /// </summary>
    public sealed class QualityEngine
    {
        // Poids du modèle de scoring contextuel (Somme = 100)
        private const double WeightTrend = 35.0;
        private const double WeightStructure = 25.0;
        private const double WeightLocation = 25.0;
        private const double WeightVolatility = 15.0;

        /// <summary>
        /// Évalue la qualité du contexte de marché pour une direction donnée.
        /// </summary>
        public QualityEvaluation Evaluate(MarketSnapshot snapshot, bool isBuy)
        {
            var eval = new QualityEvaluation();

            if (snapshot == null)
            {
                eval.TotalScore = 0.0;
                eval.State = ContextQualityState.Invalidated;
                eval.Summary = "SNAPSHOT_NULL";
                return eval;
            }

            // 1. SCORING DE TENDANCE MULTI-TIMEFRAME (Max 35 pts)
            // H4 = 18 pts, H1 = 12 pts, M15 = 5 pts
            double trendPts = 0.0;
            MiTrend expectedTrend = isBuy ? MiTrend.Bullish : MiTrend.Bearish;
            MiTrend opposingTrend = isBuy ? MiTrend.Bearish : MiTrend.Bullish;

            if (snapshot.TrendH4 == expectedTrend) trendPts += 18.0;
            else if (snapshot.TrendH4 == MiTrend.Neutral) trendPts += 8.0;

            if (snapshot.TrendH1 == expectedTrend) trendPts += 12.0;
            else if (snapshot.TrendH1 == MiTrend.Neutral) trendPts += 5.0;

            if (snapshot.TrendM15 == expectedTrend) trendPts += 5.0;

            eval.TrendScore = trendPts;

            // 2. SCORING DE STRUCTURE SMC (Max 25 pts)
            // BOS favorable = 15 pts, CHOCH récent = 10 pts
            double structPts = 0.0;
            MiStructureEvent expectedBos = isBuy ? MiStructureEvent.BullishBos : MiStructureEvent.BearishBos;
            MiStructureEvent opposingBos = isBuy ? MiStructureEvent.BearishBos : MiStructureEvent.BullishBos;
            MiStructureEvent expectedChoch = isBuy ? MiStructureEvent.BullishChoch : MiStructureEvent.BearishChoch;
            MiStructureEvent opposingChoch = isBuy ? MiStructureEvent.BearishChoch : MiStructureEvent.BullishChoch;

            if (snapshot.LastBosH4 == expectedBos && snapshot.BarsSinceBosH4 >= 0 && snapshot.BarsSinceBosH4 <= 6)
                structPts += 15.0;
            else if (snapshot.LastBos == expectedBos && snapshot.BarsSinceBos >= 0 && snapshot.BarsSinceBos <= 6)
                structPts += 10.0;
            else if (snapshot.LastBos != opposingBos)
                structPts += 5.0;

            if (snapshot.LastChochH4 == expectedChoch && snapshot.BarsSinceChochH4 >= 0 && snapshot.BarsSinceChochH4 <= 6)
                structPts += 10.0;
            else if (snapshot.LastChoch == expectedChoch && snapshot.BarsSinceChoch >= 0 && snapshot.BarsSinceChoch <= 6)
                structPts += 6.0;
            else if (snapshot.LastChoch != opposingChoch)
                structPts += 3.0;

            eval.StructureScore = Math.Min(WeightStructure, structPts);

            // 3. SCORING DE LOCALISATION VOLUME PROFILE (Max 25 pts)
            double locPts = 0.0;
            switch (snapshot.ProfileLocation)
            {
                case MiProfileLocation.AboveVah:
                    // Achat au-dessus de VAH = breakout/trend confirmé (25 pts), Vente = contre-tendance risquée (5 pts)
                    locPts = isBuy ? 25.0 : 5.0;
                    break;
                case MiProfileLocation.BelowVal:
                    // Vente sous VAL = trend baissier confirmé (25 pts), Achat = contre-tendance risquée (5 pts)
                    locPts = isBuy ? 5.0 : 25.0;
                    break;
                case MiProfileLocation.InsideVa:
                    // Dans la Value Area : marché de rotation normale (18 pts)
                    locPts = 18.0;
                    break;
                case MiProfileLocation.AtPoc:
                    // Au Point of Control : zone d'équilibre neutre (15 pts)
                    locPts = 15.0;
                    break;
                case MiProfileLocation.NearHvn:
                    // Près d'un HVN : support/résistance institutionnel (20 pts)
                    locPts = 20.0;
                    break;
                case MiProfileLocation.InsideLvn:
                    // Dans un LVN : zone d'accélération directionnelle (18 pts)
                    locPts = 18.0;
                    break;
                default:
                    locPts = 12.0;
                    break;
            }
            eval.LocationScore = locPts;

            // 4. SCORING DE VOLATILITÉ (Max 15 pts)
            double volPts = 0.0;
            switch (snapshot.VolatilityRegime)
            {
                case MiVolatilityRegime.Normal:
                    volPts = 15.0;
                    break;
                case MiVolatilityRegime.Expansion:
                    volPts = 13.0; // Bonne dynamique mais risque de slippage accru
                    break;
                case MiVolatilityRegime.Compression:
                    volPts = 8.0;  // Marché comprimé, énergie latente mais risque d'essoufflement
                    break;
                default:
                    volPts = 10.0;
                    break;
            }
            eval.VolatilityScore = volPts;

            // 5. PÉNALITÉS ET CONFLITS (-Pts)
            double penalties = 0.0;

            // Conflit direct H4 vs direction voulue
            if (snapshot.TrendH4 == opposingTrend)
                penalties += 20.0;

            // Conflit entre H4 et H1
            if (snapshot.TrendH4 != MiTrend.Neutral && snapshot.TrendH1 != MiTrend.Neutral && snapshot.TrendH4 != snapshot.TrendH1)
                penalties += 15.0;

            // CHOCH adverse récent
            if (snapshot.LastChochH4 == opposingChoch && snapshot.BarsSinceChochH4 >= 0 && snapshot.BarsSinceChochH4 <= 4)
                penalties += 12.0;

            eval.Penalties = penalties;

            // CALCUL DU SCORE FINAL NORMALISÉ (0..100)
            double rawScore = eval.TrendScore + eval.StructureScore + eval.LocationScore + eval.VolatilityScore - eval.Penalties;
            eval.TotalScore = Math.Max(0.0, Math.Min(100.0, rawScore));

            // DÉTERMINATION DE L'ÉTAT DISCRET
            if (eval.TotalScore >= 85.0 && eval.Penalties <= 5.0)
                eval.State = ContextQualityState.Confirmed;
            else if (eval.TotalScore >= 70.0 && eval.Penalties <= 12.0)
                eval.State = ContextQualityState.Ready;
            else if (eval.TotalScore >= 55.0)
                eval.State = ContextQualityState.Watch;
            else if (eval.TotalScore >= 40.0)
                eval.State = ContextQualityState.Degraded;
            else
                eval.State = ContextQualityState.Invalidated;

            eval.Summary = string.Format(CultureInfo.InvariantCulture,
                "Score={0:F1} | Trend={1} | Loc={2} | Vol={3} | Pen={4:F1}",
                eval.TotalScore, snapshot.GetTrend(MiTimeframe.H4), snapshot.ProfileLocation, snapshot.VolatilityRegime, eval.Penalties);

            return eval;
        }
    }
}
