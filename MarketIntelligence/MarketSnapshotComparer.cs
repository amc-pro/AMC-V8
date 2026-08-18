#region Using declarations
using System;
using System.Collections.Generic;
using System.Linq;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence
{
    public enum MiChangeKind
    {
        TrendH4,
        TrendH1,
        Alignment,
        Bos,
        Choch,
        OrderBlockValid,
        OrderBlockInvalid,
        Bias,
        LiquidityTarget
        // Elles n'ont jamais ete produites ; VolumeQuality / MomentumQuality
        // alimentent deja Confidence (10 + 10 points).
    }

    public sealed class MiChange
    {
        public MiChangeKind Kind;
        public string Label;
        public string From;
        public string To;
        public bool Major;
        public MiEventLevel Level;
        public int Weight;
    }

    public sealed class MiAnalysisResult
    {
        public List<MiChange> Changes = new List<MiChange>();
        public int TotalScore;
        public bool ShouldNotify;
        public string ReasonSummary;
    }

    /// <summary>
    /// Compare deux snapshots et ne retient QUE les changements confirmes et utiles.
    /// Utilise un systeme de score (ChangeScore) et de classification (Critical/Important).
    /// </summary>
    public sealed class MarketSnapshotComparer
    {
        public MiAnalysisResult Compare(MarketSnapshot previous, MarketSnapshot current)
        {
            var result = new MiAnalysisResult();
            if (current == null || previous == null) return result;

            // 1. Detection des changements et attribution des poids
            
            // H4 Trend Flip (Critical)
            if (IsFlip(previous.TrendH4, current.TrendH4))
                result.Changes.Add(Make(MiChangeKind.TrendH4, "H4 Trend Change",
                    MiText.Trend(previous.TrendH4), MiText.Trend(current.TrendH4), 40, MiEventLevel.Critical));
            else if (previous.TrendH4 != current.TrendH4)
                // et doit etre nommee explicitement (poids 25, entre flip H4 et palier H1).
                result.Changes.Add(Make(MiChangeKind.TrendH4, "H4 Trend Update",
                    MiText.Trend(previous.TrendH4), MiText.Trend(current.TrendH4), 25, MiEventLevel.Important));

            // H1 Trend Flip (Critical)
            if (IsFlip(previous.TrendH1, current.TrendH1))
                result.Changes.Add(Make(MiChangeKind.TrendH1, "H1 Trend Change",
                    MiText.Trend(previous.TrendH1), MiText.Trend(current.TrendH1), 25, MiEventLevel.Critical));
            else if (previous.TrendH1 != current.TrendH1)
                result.Changes.Add(Make(MiChangeKind.TrendH1, "H1 Trend Update",
                    MiText.Trend(previous.TrendH1), MiText.Trend(current.TrendH1), 15, MiEventLevel.Important));

            // Alignment (Important)
            if (current.AlignmentPercent != previous.AlignmentPercent)
            {
                int diff = Math.Abs(current.AlignmentPercent - previous.AlignmentPercent);
                if (diff >= 25)
                    result.Changes.Add(Make(MiChangeKind.Alignment, "Alignment Change",
                        previous.AlignmentPercent + "%", current.AlignmentPercent + "%", 15, MiEventLevel.Important));
            }

            if (current.LastBosH4 != MiStructureEvent.None && current.LastBosH4 != previous.LastBosH4)
                result.Changes.Add(Make(MiChangeKind.Bos, "BOS Confirmed (H4)",
                    MiText.Structure(previous.LastBosH4), MiText.Structure(current.LastBosH4), 40, MiEventLevel.Critical));

            // CHOCH H4 (Critical)
            if (current.LastChochH4 != MiStructureEvent.None && current.LastChochH4 != previous.LastChochH4)
                result.Changes.Add(Make(MiChangeKind.Choch, "CHOCH Confirmed (H4)",
                    MiText.Structure(previous.LastChochH4), MiText.Structure(current.LastChochH4), 40, MiEventLevel.Critical));

            if (current.LastBos != MiStructureEvent.None && current.LastBos != previous.LastBos)
                result.Changes.Add(Make(MiChangeKind.Bos, "BOS Confirmed (H1)",
                    MiText.Structure(previous.LastBos), MiText.Structure(current.LastBos), 20, MiEventLevel.Important));

            // CHOCH H1 (Important)
            if (current.LastChoch != MiStructureEvent.None && current.LastChoch != previous.LastChoch)
                result.Changes.Add(Make(MiChangeKind.Choch, "CHOCH Confirmed (H1)",
                    MiText.Structure(previous.LastChoch), MiText.Structure(current.LastChoch), 20, MiEventLevel.Important));

            // Order Block (Important)
            bool wasValid = previous.OrderBlockState == MiOrderBlockState.Valid;
            bool isValid = current.OrderBlockState == MiOrderBlockState.Valid;
            if (isValid && (!wasValid || previous.OrderBlockKind != current.OrderBlockKind))
                result.Changes.Add(Make(MiChangeKind.OrderBlockValid, "New Order Block",
                    MiText.OrderBlock(previous.OrderBlockKind, previous.OrderBlockState),
                    MiText.OrderBlock(current.OrderBlockKind, current.OrderBlockState), 10, MiEventLevel.Important));
            else if (wasValid && current.OrderBlockState == MiOrderBlockState.Invalid)
                result.Changes.Add(Make(MiChangeKind.OrderBlockInvalid, "Order Block Invalidated",
                    MiText.OrderBlock(previous.OrderBlockKind, previous.OrderBlockState),
                    MiText.OrderBlock(current.OrderBlockKind, current.OrderBlockState), 10, MiEventLevel.Important));

            // Bias (Important)
            if (previous.Bias != current.Bias)
                result.Changes.Add(Make(MiChangeKind.Bias, "Bias Change",
                    MiText.Bias(previous.Bias), MiText.Bias(current.Bias), 20, MiEventLevel.Important));

            // Liquidity Target (Information/Important)
            if (previous.Target != current.Target && current.Target != MiLiquidityTarget.None)
                result.Changes.Add(Make(MiChangeKind.LiquidityTarget, "Liquidity Target",
                    MiText.Target(previous.Target), MiText.Target(current.Target), 10, MiEventLevel.Information));

            // 2. Calcul du score total
            result.TotalScore = result.Changes.Sum(c => c.Weight);

            // 3. Decision de notification
            bool hasCritical = result.Changes.Any(c => c.Level == MiEventLevel.Critical);
            bool hasImportant = result.Changes.Any(c => c.Level == MiEventLevel.Important);
            // n'est notifie que si le contexte est suffisamment qualitatif.
            // Un CRITICAL passe toujours, quelle que soit la Confidence.
            bool importantAllowed = current.Confidence >= 60;

            if (hasCritical)
                result.ShouldNotify = true;
            else if (!hasImportant || importantAllowed)
                result.ShouldNotify = (result.TotalScore >= 40)
                                   || (result.TotalScore >= 20 && result.Changes.Count >= 2);
            else
                result.ShouldNotify = false;   // Important seul + Confidence < 60 => silence

            // Regle d'or n°1 : ne jamais se taire sans raison connue.
            if (!result.ShouldNotify && hasImportant && !importantAllowed)
                result.ReasonSummary = "Important ignore : Confidence " + current.Confidence + " < 60.";

            return result;
        }

        private static bool IsFlip(MiTrend a, MiTrend b)
        {
            return (a == MiTrend.Bullish && b == MiTrend.Bearish)
                || (a == MiTrend.Bearish && b == MiTrend.Bullish);
        }

        private static MiChange Make(MiChangeKind kind, string label, string from, string to, int weight, MiEventLevel level)
        {
            return new MiChange { 
                Kind = kind, 
                Label = label, 
                From = from, 
                To = to, 
                Weight = weight, 
                Level = level,
                Major = level >= MiEventLevel.Important 
            };
        }
    }
}
