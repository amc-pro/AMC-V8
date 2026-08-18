#region Using declarations
using System;
using System.Collections.Generic;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence
{
    // Types de base du module Market Intelligence.
    // Aucun type ici ne depend de NinjaTrader : le module est testable et
    // reutilisable, et il peut etre desactive sans impacter le reste.

    public enum MiTrend
    {
        Neutral = 0,
        Bullish = 1,
        Bearish = -1
    }

    public enum MiStructureEvent
    {
        None = 0,
        BullishBos,
        BearishBos,
        BullishChoch,
        BearishChoch
    }

    public enum MiOrderBlockKind
    {
        None = 0,
        Bullish,
        Bearish
    }

    public enum MiOrderBlockState
    {
        None = 0,
        Valid,
        Mitigated,
        Invalid
    }

    public enum MiLiquidityTarget
    {
        None = 0,
        BuySide,
        SellSide
    }

    public enum MiBias
    {
        NoTrade = 0,
        BuyOnly,
        SellOnly
    }

    /// <summary>Timeframes suivis par le module (extensible sans casser l'API).</summary>
    public enum MiTimeframe
    {
        H4 = 0,
        H1 = 1,
        M15 = 2,
        M5 = 3
    }

    public enum MiEventLevel
    {
        Information = 0,
        Important = 1,
        Critical = 2
    }

    /// <summary>
    /// Classification de tendance déterministe, uniquement à partir de bougies clôturées.
    /// La tendance n'est pas définie par un simple croisement prix/EMA : elle exige
    /// simultanément position du prix, pente EMA et momentum directionnel.
    /// </summary>
    public static class MiTrendLogic
    {
        public static MiTrend Classify(
            double close, double closePast,
            double ema, double emaPast,
            double tickSize,
            double minDistanceTicks,
            double minSlopeTicks)
        {
            if (double.IsNaN(close) || double.IsInfinity(close) ||
                double.IsNaN(closePast) || double.IsInfinity(closePast) ||
                double.IsNaN(ema) || double.IsInfinity(ema) ||
                double.IsNaN(emaPast) || double.IsInfinity(emaPast) ||
                tickSize <= 0 || double.IsNaN(tickSize) || double.IsInfinity(tickSize))
                return MiTrend.Neutral;

            double distanceTicks = (close - ema) / tickSize;
            double slopeTicks = (ema - emaPast) / tickSize;
            double momentumTicks = (close - closePast) / tickSize;

            double minDistance = Math.Max(0, minDistanceTicks);
            double minSlope = Math.Max(0, minSlopeTicks);

            bool bullish = distanceTicks >= minDistance
                        && slopeTicks >= minSlope
                        && momentumTicks >= 0;
            bool bearish = distanceTicks <= -minDistance
                        && slopeTicks <= -minSlope
                        && momentumTicks <= 0;

            if (bullish) return MiTrend.Bullish;
            if (bearish) return MiTrend.Bearish;
            return MiTrend.Neutral;
        }
    }

    /// <summary>
    /// Source de donnees consommee par les moteurs. L'indicateur en fournit une
    /// implementation ; les tests peuvent en fournir une autre (injection).
    /// </summary>
    public interface IMarketIntelligenceSource
    {
        string InstrumentName { get; }
        DateTime MarketTime { get; }
        string TimeZoneLabel { get; }
        double TickSize { get; }
        double LastPrice { get; }

        /// <summary>Tendance d'un timeframe, calculee sur barres CLOTUREES.</summary>
        MiTrend GetTrend(MiTimeframe tf);

        /// <summary>Dernier evenement de structure (BOS/CHOCH) detecte en H1.</summary>
        MiStructureEvent LastBos { get; }
        MiStructureEvent LastChoch { get; }

        MiStructureEvent LastBosH4 { get; }
        MiStructureEvent LastChochH4 { get; }

        int BarsSinceBos { get; }
        int BarsSinceChoch { get; }
        int BarsSinceOrderBlock { get; }

        /// <summary>Age en barres des evenements de structure H4 (-1 = inconnu).</summary>
        int BarsSinceBosH4 { get; }
        int BarsSinceChochH4 { get; }

        /// <summary>Liquidites les plus proches (0 = inconnue).</summary>
        double NearestBuySideLiquidity { get; }
        double NearestSellSideLiquidity { get; }

        MiOrderBlockKind OrderBlockKind { get; }
        MiOrderBlockState OrderBlockState { get; }

        /// <summary>Scores normalises 0..1 utilises par la ponderation Confidence.</summary>
        double VolumeQuality { get; }
        double MomentumQuality { get; }

        /// <summary>Extensions futures (Volume Profile, Delta, VWAP, News, DOM...).</summary>
        IEnumerable<IMarketIntelligenceModule> Modules { get; }
    }

    /// <summary>
    /// Point d'extension : un module additionnel enrichit le snapshot sans
    /// modifier l'architecture (Volume Profile, Footprint, Delta, VWAP, News, DOM, Icebergs).
    /// </summary>
    public interface IMarketIntelligenceModule
    {
        string Key { get; }
        /// <summary>Ligne courte affichee dans le rapport (null = rien a dire).</summary>
        string Describe();
        /// <summary>Contribution optionnelle 0..1 au score de confiance (negatif = ignore).</summary>
        double ConfidenceContribution { get; }
    }

    /// <summary>Journalisation minimaliste injectee (aucun couplage avec Print).</summary>
    public interface IMiLogger
    {
        void Log(string message);
    }

    public sealed class MiDelegateLogger : IMiLogger
    {
        private readonly Action<string> sink;
        public MiDelegateLogger(Action<string> sink) { this.sink = sink; }
        public void Log(string message)
        {
            if (sink == null) return;
            try { sink("[MarketIntelligence] " + message); }
            catch (Exception) { }
        }
    }

    public static class MiText
    {
        public static string Trend(MiTrend t)
        {
            if (t == MiTrend.Bullish) return "🟩 Bullish";
            if (t == MiTrend.Bearish) return "🟥 Bearish";
            return "⬜ Neutral";
        }

        public static string Bias(MiBias b)
        {
            if (b == MiBias.BuyOnly) return "BUY ONLY";
            if (b == MiBias.SellOnly) return "SELL ONLY";
            return "NO TRADE";
        }

        public static string BiasEmoji(MiBias b)
        {
            if (b == MiBias.BuyOnly) return "🟢";
            if (b == MiBias.SellOnly) return "🔴";
            return "⚪";
        }

        public static string Structure(MiStructureEvent e)
        {
            switch (e)
            {
                case MiStructureEvent.BullishBos: return "Bullish";
                case MiStructureEvent.BearishBos: return "Bearish";
                case MiStructureEvent.BullishChoch: return "Bullish";
                case MiStructureEvent.BearishChoch: return "Bearish";
                default: return "None";
            }
        }

        public static string OrderBlock(MiOrderBlockKind kind, MiOrderBlockState state)
        {
            if (kind == MiOrderBlockKind.None || state == MiOrderBlockState.None) return "None";
            string k = kind == MiOrderBlockKind.Bullish ? "Bullish" : "Bearish";
            switch (state)
            {
                case MiOrderBlockState.Valid: return k + " VALID";
                case MiOrderBlockState.Mitigated: return k + " MITIGATED";
                default: return k + " INVALID";
            }
        }

        public static string Age(int bars, string unit)
        {
            if (bars < 0) return "";
            string barStr = bars == 1 ? "1 barre " : bars.ToString(System.Globalization.CultureInfo.InvariantCulture) + " barres ";
            return " (il y a " + barStr + unit + ")";
        }

        public static string Timeframe(MiTimeframe tf)
        {
            switch (tf)
            {
                case MiTimeframe.H4: return "H4";
                case MiTimeframe.H1: return "H1";
                case MiTimeframe.M15: return "M15";
                default: return "M5";
            }
        }

        public static string Target(MiLiquidityTarget t)
        {
            if (t == MiLiquidityTarget.BuySide) return "Buy Side Liquidity";
            if (t == MiLiquidityTarget.SellSide) return "Sell Side Liquidity";
            return "None";
        }
    }
}
