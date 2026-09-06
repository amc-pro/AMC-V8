#region Using declarations
using System;
using System.Globalization;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence
{
    /// <summary>
    /// Motifs auditables de rejet du No-Trade Engine (Sprint 3).
    /// </summary>
    public enum NoTradeReason
    {
        None = 0,
        LowContextQuality,
        HtfConflict,
        HtfOpposedDirection,
        BadLocation,
        AdverseChochRecent,
        ExtremeCompression,
        SnapshotUnavailable
    }

    /// <summary>
    /// Résultat de filtrage émis par le NoTradeEngine.
    /// </summary>
    public sealed class NoTradeDecision
    {
        public bool IsRejected { get; set; }
        public NoTradeReason Reason { get; set; }
        public string Explanation { get; set; }
        public double QualityScore { get; set; }

        public override string ToString()
        {
            return IsRejected
                ? string.Format(CultureInfo.InvariantCulture, "REJECT [{0}] : {1} (Score={2:F1})", Reason, Explanation, QualityScore)
                : string.Format(CultureInfo.InvariantCulture, "PASS (Score={0:F1})", QualityScore);
        }
    }

    /// <summary>
    /// Moteur déterministe de filtrage No-Trade protégeant le capital contre les environnements dégradés.
    /// Vérifie les filtres d'invalidation contextuelle avant toute entrée en position.
    /// </summary>
    public sealed class NoTradeEngine
    {
        private readonly QualityEngine qualityEngine;

        public double MinRequiredScore { get; set; }
        public bool BlockOnHtfConflict { get; set; }
        public bool BlockOnAdverseH4Trend { get; set; }
        public bool BlockOnBadLocation { get; set; }

        public NoTradeEngine(QualityEngine qualityEngine = null)
        {
            this.qualityEngine = qualityEngine ?? new QualityEngine();
            MinRequiredScore = 50.0;
            BlockOnHtfConflict = true;
            BlockOnAdverseH4Trend = true;
            BlockOnBadLocation = true;
        }

        /// <summary>
        /// Évalue l'éligibilité d'un trade selon le contexte de marché.
        /// </summary>
        public NoTradeDecision EvaluateTradeEligibility(MarketSnapshot snapshot, bool isBuy, bool isMeanReversal = false)
        {
            var decision = new NoTradeDecision { IsRejected = false, Reason = NoTradeReason.None };

            if (snapshot == null)
            {
                decision.IsRejected = true;
                decision.Reason = NoTradeReason.SnapshotUnavailable;
                decision.Explanation = "Snapshot Market Intelligence non disponible.";
                decision.QualityScore = 0.0;
                return decision;
            }

            var qual = qualityEngine.Evaluate(snapshot, isBuy);
            decision.QualityScore = qual.TotalScore;

            // Filtre 1 : Conflit direct H4 vs H1 (Régime macro désynchronisé)
            if (BlockOnHtfConflict && snapshot.TrendH4 != MiTrend.Neutral &&
                snapshot.TrendH1 != MiTrend.Neutral && snapshot.TrendH4 != snapshot.TrendH1)
            {
                decision.IsRejected = true;
                decision.Reason = NoTradeReason.HtfConflict;
                decision.Explanation = string.Format(CultureInfo.InvariantCulture,
                    "Conflit de tendance macro : H4 est {0} alors que H1 est {1}.",
                    snapshot.TrendH4, snapshot.TrendH1);
                return decision;
            }

            // Filtre 2 : Opposition frontale avec la tendance H4 (sauf si Mean-Reversal autorisé)
            MiTrend opposingTrend = isBuy ? MiTrend.Bearish : MiTrend.Bullish;
            if (BlockOnAdverseH4Trend && !isMeanReversal && snapshot.TrendH4 == opposingTrend)
            {
                decision.IsRejected = true;
                decision.Reason = NoTradeReason.HtfOpposedDirection;
                decision.Explanation = string.Format(CultureInfo.InvariantCulture,
                    "Tentative d'entrée {0} alors que la tendance H4 est fermement {1}.",
                    isBuy ? "Achat" : "Vente", snapshot.TrendH4);
                return decision;
            }

            // Filtre 3 : Mauvaise localisation Volume Profile (Achat sous VAL baissier ou Vente au-dessus de VAH)
            if (BlockOnBadLocation && !isMeanReversal)
            {
                if (isBuy && snapshot.ProfileLocation == MiProfileLocation.BelowVal && snapshot.TrendH4 == MiTrend.Bearish)
                {
                    decision.IsRejected = true;
                    decision.Reason = NoTradeReason.BadLocation;
                    decision.Explanation = "Achat rejeté : Prix sous VAL dans un régime baissier (chute libre).";
                    return decision;
                }
                else if (!isBuy && snapshot.ProfileLocation == MiProfileLocation.AboveVah && snapshot.TrendH4 == MiTrend.Bullish)
                {
                    decision.IsRejected = true;
                    decision.Reason = NoTradeReason.BadLocation;
                    decision.Explanation = "Vente rejetée : Prix au-dessus de VAH dans un régime haussier (extension forte).";
                    return decision;
                }
            }

            // Filtre 4 : CHOCH adverse récent sur H4
            MiStructureEvent adverseChoch = isBuy ? MiStructureEvent.BearishChoch : MiStructureEvent.BullishChoch;
            if (snapshot.LastChochH4 == adverseChoch && snapshot.BarsSinceChochH4 >= 0 && snapshot.BarsSinceChochH4 <= 3)
            {
                decision.IsRejected = true;
                decision.Reason = NoTradeReason.AdverseChochRecent;
                decision.Explanation = string.Format(CultureInfo.InvariantCulture,
                    "Présence d'un {0} H4 récent ({1} barres). Risque élevé de retournement.",
                    adverseChoch, snapshot.BarsSinceChochH4);
                return decision;
            }

            // Filtre 5 : Qualité de contexte insuffisante (Score global < Seuil minimum)
            if (qual.TotalScore < MinRequiredScore)
            {
                decision.IsRejected = true;
                decision.Reason = NoTradeReason.LowContextQuality;
                decision.Explanation = string.Format(CultureInfo.InvariantCulture,
                    "Score de qualité contextuelle insuffisant ({0:F1} < {1:F1}). État: {2}.",
                    qual.TotalScore, MinRequiredScore, qual.State);
                return decision;
            }

            decision.Explanation = "Contexte favorable validé par le No-Trade Engine.";
            return decision;
        }
    }
}
