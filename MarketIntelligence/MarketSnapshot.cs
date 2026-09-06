#region Using declarations
using System;
using System.Collections.Generic;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence
{
    /// <summary>
    /// Photographie immuable de l'etat du marche. Un seul exemplaire est
    /// conserve en memoire (le precedent), aucune allocation superflue.
    /// </summary>
    public sealed class MarketSnapshot
    {
        public string Instrument;
        public DateTime Time;
        public string TimeZoneLabel;

        public MiTrend TrendH4;
        public MiTrend TrendH1;
        public MiTrend TrendM15;
        public MiTrend TrendM5;
        public int AlignmentPercent;      // 0 / 25 / 50 / 75 / 100
        public MiTimeframe AlignmentReference;

        public MiStructureEvent LastBos;
        public MiStructureEvent LastChoch;
        public int BarsSinceBos = -1;
        public int BarsSinceChoch = -1;
        public int BarsSinceOrderBlock = -1;

        public MiStructureEvent LastBosH4;
        public MiStructureEvent LastChochH4;
        public int BarsSinceBosH4 = -1;
        public int BarsSinceChochH4 = -1;

        public double BuySideLiquidity;
        public double SellSideLiquidity;
        public double BuySideDistanceTicks;
        public double SellSideDistanceTicks;
        public MiLiquidityTarget Target;

        public MiOrderBlockKind OrderBlockKind;
        public MiOrderBlockState OrderBlockState;

        public MiBias Bias;
        public string BiasReason;
        public int Confidence;            // 0..100

        public MiProfileLocation ProfileLocation;
        public MiVolatilityRegime VolatilityRegime;
        public double NormalizedAtr;

        public List<string> ExtraLines;   // extensions (Delta, VWAP, News...)

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
    }

    /// <summary>Construit un snapshot a partir d'une source injectee.</summary>
    public sealed class MarketSnapshotBuilder
    {
        private readonly IMarketIntelligenceSource source;

        public MarketSnapshotBuilder(IMarketIntelligenceSource source)
        {
            if (source == null) throw new ArgumentNullException("source");
            this.source = source;
        }

        public MarketSnapshot Build()
        {
            var s = new MarketSnapshot();
            s.Instrument = source.InstrumentName;
            s.Time = source.MarketTime;
            s.TimeZoneLabel = source.TimeZoneLabel;

            s.TrendH4 = source.GetTrend(MiTimeframe.H4);
            s.TrendH1 = source.GetTrend(MiTimeframe.H1);
            s.TrendM15 = source.GetTrend(MiTimeframe.M15);
            s.TrendM5 = source.GetTrend(MiTimeframe.M5);
            s.AlignmentPercent = ComputeAlignment(s);

            s.LastBos = source.LastBos;
            s.LastChoch = source.LastChoch;
            s.BarsSinceBos = source.BarsSinceBos;
            s.BarsSinceChoch = source.BarsSinceChoch;
            s.BarsSinceOrderBlock = source.BarsSinceOrderBlock;
            s.LastBosH4 = source.LastBosH4;
            s.LastChochH4 = source.LastChochH4;
            s.BarsSinceBosH4 = source.BarsSinceBosH4;
            s.BarsSinceChochH4 = source.BarsSinceChochH4;

            double tick = source.TickSize > 0 ? source.TickSize : 0.25;
            double price = source.LastPrice;
            s.BuySideLiquidity = source.NearestBuySideLiquidity;
            s.SellSideLiquidity = source.NearestSellSideLiquidity;
            s.BuySideDistanceTicks = s.BuySideLiquidity > 0 ? Math.Abs(s.BuySideLiquidity - price) / tick : -1;
            s.SellSideDistanceTicks = s.SellSideLiquidity > 0 ? Math.Abs(price - s.SellSideLiquidity) / tick : -1;
            s.Target = ComputeTarget(s);

            s.OrderBlockKind = source.OrderBlockKind;
            s.OrderBlockState = source.OrderBlockState;

            s.Bias = ComputeBias(s);
            s.BiasReason = DescribeBias(s);
            s.Confidence = ComputeConfidence(s);

            s.ProfileLocation = source.ProfileLocation;
            s.VolatilityRegime = source.VolatilityRegime;
            s.NormalizedAtr = source.NormalizedAtr;

            if (source.Modules != null)
            {
                foreach (var m in source.Modules)
                {
                    if (m == null) continue;
                    string line = null;
                    try { line = m.Describe(); }
                    catch (Exception) { line = null; }
                    if (string.IsNullOrEmpty(line)) continue;
                    if (s.ExtraLines == null) s.ExtraLines = new List<string>(2);
                    s.ExtraLines.Add(line);
                }
            }

            return s;
        }

        private static int ComputeAlignment(MarketSnapshot s)
        {
            // Hiérarchie MTF : H4 = régime (40%), H1 = confirmation (30%),
            // M15 = contexte d'exécution (20%), M5 = trigger fin (10%).
            // Les timeframes inférieurs ne doivent jamais annuler à eux seuls un H4 sain.
            MiTrend reference = s.TrendH4;
            s.AlignmentReference = MiTimeframe.H4;
            if (reference == MiTrend.Neutral)
            {
                reference = s.TrendH1;
                s.AlignmentReference = MiTimeframe.H1;
            }
            if (reference == MiTrend.Neutral) return 0;

            int aligned = 0;
            if (s.TrendH4 == reference) aligned += 40;
            if (s.TrendH1 == reference) aligned += 30;
            if (s.TrendM15 == reference) aligned += 20;
            if (s.TrendM5 == reference) aligned += 10;
            return aligned;
        }

        private static MiLiquidityTarget ComputeTarget(MarketSnapshot s)
        {
            bool hasBuy = s.BuySideDistanceTicks >= 0;
            bool hasSell = s.SellSideDistanceTicks >= 0;

            // La direction dominante prime : un marche baissier va chercher la
            // liquidite sell side, meme si le buy side est plus proche.
            if (s.TrendH4 == MiTrend.Bearish && hasSell) return MiLiquidityTarget.SellSide;
            if (s.TrendH4 == MiTrend.Bullish && hasBuy) return MiLiquidityTarget.BuySide;

            if (hasBuy && hasSell)
                return s.BuySideDistanceTicks <= s.SellSideDistanceTicks
                    ? MiLiquidityTarget.BuySide
                    : MiLiquidityTarget.SellSide;

            if (hasBuy) return MiLiquidityTarget.BuySide;
            if (hasSell) return MiLiquidityTarget.SellSide;
            return MiLiquidityTarget.None;
        }

        /// <summary>
        /// Le module n'emet JAMAIS d'ordre d'entree : uniquement un contexte
        /// directionnel (BUY ONLY / SELL ONLY / NO TRADE).
        /// </summary>
        private const int H4StructureMaxAgeBars = 6;

        private static MiBias ComputeBias(MarketSnapshot s)
        {
            // H4 = régime principal. H1 = confirmation obligatoire contre-indiquée
            // uniquement lorsqu'elle est explicitement opposée. M15/M5 ne définissent
            // pas le biais global : ils servent à mesurer la qualité d'alignement.
            MiTrend refTrend = s.TrendH4;
            bool h4Neutral = refTrend == MiTrend.Neutral;

            if (h4Neutral)
            {
                refTrend = s.TrendH1;
                if (refTrend == MiTrend.Neutral) return MiBias.NoTrade;
            }
            else if (s.TrendH1 != MiTrend.Neutral && s.TrendH1 != s.TrendH4)
            {
                // Conflit H4/H1 = régime non exploitable. M15/M5 ne peuvent pas le bypasser.
                return MiBias.NoTrade;
            }

            bool bull = refTrend == MiTrend.Bullish;

            if (h4Neutral && s.AlignmentPercent < 30) return MiBias.NoTrade;
            if (!h4Neutral && s.AlignmentPercent < 40) return MiBias.NoTrade;

            if (!h4Neutral)
            {
                if (IsRecentOpposing(s.LastChochH4, s.BarsSinceChochH4, bull, H4StructureMaxAgeBars)
                    || IsRecentOpposing(s.LastBosH4, s.BarsSinceBosH4, bull, H4StructureMaxAgeBars))
                    return MiBias.NoTrade;
            }

            return bull ? MiBias.BuyOnly : MiBias.SellOnly;
        }

        private static bool IsRecentOpposing(MiStructureEvent e, int age, bool bullish, int maxAge)
        {
            if (e == MiStructureEvent.None || age < 0 || age > maxAge) return false;
            return bullish
                ? e == MiStructureEvent.BearishChoch || e == MiStructureEvent.BearishBos
                : e == MiStructureEvent.BullishChoch || e == MiStructureEvent.BullishBos;
        }

        private static double FreshAlignedContribution(MiStructureEvent e, int age, bool bullish, int maxAge)
        {
            if (e == MiStructureEvent.None || age < 0 || age > maxAge) return 0;
            bool aligned = bullish
                ? e == MiStructureEvent.BullishBos || e == MiStructureEvent.BullishChoch
                : e == MiStructureEvent.BearishBos || e == MiStructureEvent.BearishChoch;
            if (!aligned) return 0;
            return (e == MiStructureEvent.BullishBos || e == MiStructureEvent.BearishBos) ? 0.7 : 0.3;
        }

        /// <summary>
        /// Explique le biais SANS le recalculer : memes conditions, dans le meme
        /// </summary>
        private static string DescribeBias(MarketSnapshot s)
        {
            if (s.AlignmentPercent <= 0) return "aucune tendance directrice exploitable";

            MiTrend refTrend = s.TrendH4;
            bool isH4Neutral = false;
            if (refTrend == MiTrend.Neutral)
            {
                if (s.TrendH1 != MiTrend.Neutral)
                {
                    refTrend = s.TrendH1;
                    isH4Neutral = true;
                }
                else
                {
                    return "tendance H4 et H1 neutres";
                }
            }

            if (!isH4Neutral && s.TrendH1 != MiTrend.Neutral && s.TrendH1 != s.TrendH4) return "H1 (" + MiText.Trend(s.TrendH1) + ") en desaccord avec H4";

            bool bull = refTrend == MiTrend.Bullish;
            if (!isH4Neutral)
            {
                if (IsRecentOpposing(s.LastChochH4, s.BarsSinceChochH4, bull, H4StructureMaxAgeBars)) return "CHOCH H4 oppose recent";
                if (IsRecentOpposing(s.LastBosH4, s.BarsSinceBosH4, bull, H4StructureMaxAgeBars)) return "BOS H4 oppose recent";
            }

            bool h1Opposing = IsRecentOpposing(s.LastChoch, s.BarsSinceChoch, bull, H4StructureMaxAgeBars)
                           || IsRecentOpposing(s.LastBos, s.BarsSinceBos, bull, H4StructureMaxAgeBars);

            if (h1Opposing)
            {
                return isH4Neutral 
                    ? "H4 neutre, H1 dominant (" + MiText.Trend(s.TrendH1) + "), CHOCH H1 oppose (conf -15)"
                    : "H4 et H1 alignes, CHOCH H1 oppose (conf -15)";
            }

            return isH4Neutral 
                ? "H4 neutre, H1 dominant (" + MiText.Trend(s.TrendH1) + "), structures H1 non contradictoires"
                : "H4 et H1 alignes, structures recentes non contradictoires";
        }

        /// <summary>
        /// Score 0..100 = QUALITE DU CONTEXTE, pas une probabilite de gain.
        /// Ponderation : Trend 30 / Structure 20 / Liquidity 15 / OB 15 / Volume 10 / Momentum 10.
        /// </summary>
        private int ComputeConfidence(MarketSnapshot s)
        {
            double score = 0;

            score += 30.0 * (s.AlignmentPercent / 100.0);

            MiTrend refTrend = s.TrendH4 != MiTrend.Neutral ? s.TrendH4 : s.TrendH1;
            bool bull = refTrend == MiTrend.Bullish;

            // Structure globale : H4 dominante (60%), H1 secondaire (40%).
            // Les evenements anciens ne contribuent plus au score courant.
            double structureH4 = FreshAlignedContribution(
                s.LastBosH4, s.BarsSinceBosH4, bull, H4StructureMaxAgeBars)
                + FreshAlignedContribution(
                s.LastChochH4, s.BarsSinceChochH4, bull, H4StructureMaxAgeBars);
            double structureH1 = FreshAlignedContribution(
                s.LastBos, s.BarsSinceBos, bull, H4StructureMaxAgeBars)
                + FreshAlignedContribution(
                s.LastChoch, s.BarsSinceChoch, bull, H4StructureMaxAgeBars);
            double structure = 0.60 * Math.Min(1.0, structureH4)
                             + 0.40 * Math.Min(1.0, structureH1);
            score += 20.0 * Math.Min(1.0, structure);

            double liquidity = 0;
            if (s.Target != MiLiquidityTarget.None)
            {
                liquidity = 0.6;
                bool targetAligned = (s.Target == MiLiquidityTarget.BuySide && bull)
                                     || (s.Target == MiLiquidityTarget.SellSide && !bull);
                if (refTrend != MiTrend.Neutral && targetAligned) liquidity = 1.0;
            }
            score += 15.0 * liquidity;

            double ob = 0;
            if (s.OrderBlockState == MiOrderBlockState.Valid)
            {
                bool obBull = s.OrderBlockKind == MiOrderBlockKind.Bullish;
                ob = (refTrend != MiTrend.Neutral && obBull == bull) ? 1.0 : 0.4;
            }
            else if (s.OrderBlockState == MiOrderBlockState.Mitigated) ob = 0.3;
            score += 15.0 * ob;

            score += 10.0 * Clamp01(source.VolumeQuality);
            score += 10.0 * Clamp01(source.MomentumQuality);

            // Extensions : moyenne des contributions valides, appliquee comme
            // modulateur +/-5 points sans changer l'echelle 0..100.
            double sum = 0; int count = 0;
            if (source.Modules != null)
            {
                foreach (var m in source.Modules)
                {
                    if (m == null) continue;
                    double c;
                    try { c = m.ConfidenceContribution; }
                    catch (Exception) { continue; }
                    if (c < 0) continue;
                    sum += Clamp01(c);
                    count++;
                }
            }
            if (count > 0) score += (sum / count - 0.5) * 10.0;

            // Option B : Penalite de 15 points sur la confiance si un CHOCH/BOS H1 oppose est recent
            if (IsRecentOpposing(s.LastChoch, s.BarsSinceChoch, bull, H4StructureMaxAgeBars)
                || IsRecentOpposing(s.LastBos, s.BarsSinceBos, bull, H4StructureMaxAgeBars))
            {
                score -= 15.0;
            }

            if (score < 0) score = 0;
            if (score > 100) score = 100;
            return (int)Math.Round(score);
        }

        private static double Clamp01(double v)
        {
            if (double.IsNaN(v) || v < 0) return 0;
            return v > 1 ? 1 : v;
        }
    }
}
