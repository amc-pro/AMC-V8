#region Using declarations
using System;
using System.Collections.Generic;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence
{
    /// <summary>
    /// Analyseur SMC autonome (aucune dependance NinjaTrader) : swings,
    /// BOS / CHOCH, liquidites et Order Block actif. Alimente barre CLOTUREE
    /// par barre cloturee -> aucun repaint, aucune allocation par tick.
    /// </summary>
    public sealed class MarketStructureAnalyzer
    {
        private struct Bar
        {
            public double O, H, L, C;
        }

        private readonly int swingStrength;
        private readonly int maxSwings;
        private readonly List<Bar> bars;

        private readonly List<double> swingHighs = new List<double>();
        private readonly List<double> swingLows = new List<double>();

        private double lastSwingHigh = 0;
        private double lastSwingLow = 0;
        private int structureDirection = 0;   // +1 haussier, -1 baissier

        public MiStructureEvent LastBos = MiStructureEvent.None;
        public MiStructureEvent LastChoch = MiStructureEvent.None;

        public MiOrderBlockKind OrderBlockKind = MiOrderBlockKind.None;
        public MiOrderBlockState OrderBlockState = MiOrderBlockState.None;
        private double obHigh, obLow;

        // n'en depend : ils ne servent qu'a la restitution.
        private long barCounter;
        private long bosBar = -1, chochBar = -1, obBar = -1;
        public int BarsSinceBos { get { return bosBar < 0 ? -1 : (int)(barCounter - bosBar); } }
        public int BarsSinceChoch { get { return chochBar < 0 ? -1 : (int)(barCounter - chochBar); } }
        public int BarsSinceOrderBlock { get { return obBar < 0 ? -1 : (int)(barCounter - obBar); } }

        public MarketStructureAnalyzer(int swingStrength = 2, int maxSwings = 12)
        {
            this.swingStrength = Math.Max(1, swingStrength);
            this.maxSwings = Math.Max(4, maxSwings);
            bars = new List<Bar>(this.swingStrength * 2 + 8);
        }

        public void Reset()
        {
            bars.Clear();
            swingHighs.Clear();
            swingLows.Clear();
            lastSwingHigh = 0;
            lastSwingLow = 0;
            structureDirection = 0;
            LastBos = MiStructureEvent.None;
            LastChoch = MiStructureEvent.None;
            OrderBlockKind = MiOrderBlockKind.None;
            OrderBlockState = MiOrderBlockState.None;
            obHigh = obLow = 0;
            barCounter = 0;
            bosBar = chochBar = obBar = -1;
        }

        /// <summary>Liquidite buy side la plus proche AU-DESSUS du prix (0 = aucune).</summary>
        public double NearestBuySide(double price)
        {
            double best = 0;
            for (int i = 0; i < swingHighs.Count; i++)
            {
                double h = swingHighs[i];
                if (h <= price) continue;
                if (best == 0 || h < best) best = h;
            }
            return best;
        }

        /// <summary>Liquidite sell side la plus proche EN DESSOUS du prix (0 = aucune).</summary>
        public double NearestSellSide(double price)
        {
            double best = 0;
            for (int i = 0; i < swingLows.Count; i++)
            {
                double l = swingLows[i];
                if (l >= price) continue;
                if (best == 0 || l > best) best = l;
            }
            return best;
        }

        /// <summary>A appeler une seule fois par barre cloturee.</summary>
        public void OnClosedBar(double open, double high, double low, double close)
        {
            var bar = new Bar { O = open, H = high, L = low, C = close };
            bars.Add(bar);
            barCounter++;

            int window = swingStrength * 2 + 1;
            if (bars.Count > window + 6) bars.RemoveAt(0);   // memoire bornee

            DetectSwing();
            UpdateOrderBlockLifecycle(high, low, close);
            DetectStructureBreak(close);
        }

        private void DetectSwing()
        {
            int window = swingStrength * 2 + 1;
            if (bars.Count < window) return;

            int pivot = bars.Count - 1 - swingStrength;
            if (pivot < 0) return;

            Bar p = bars[pivot];
            bool isHigh = true, isLow = true;
            for (int i = pivot - swingStrength; i <= pivot + swingStrength; i++)
            {
                if (i < 0 || i >= bars.Count || i == pivot) continue;
                if (bars[i].H >= p.H) isHigh = false;
                if (bars[i].L <= p.L) isLow = false;
            }

            if (isHigh)
            {
                lastSwingHigh = p.H;
                Push(swingHighs, p.H);
            }
            if (isLow)
            {
                lastSwingLow = p.L;
                Push(swingLows, p.L);
            }
        }

        private void Push(List<double> list, double value)
        {
            list.Add(value);
            if (list.Count > maxSwings) list.RemoveAt(0);
        }

        private void DetectStructureBreak(double close)
        {
            if (lastSwingHigh > 0 && close > lastSwingHigh)
            {
                if (structureDirection < 0) { LastChoch = MiStructureEvent.BullishChoch; chochBar = barCounter; }
                LastBos = MiStructureEvent.BullishBos;
                bosBar = barCounter;
                structureDirection = 1;
                CaptureOrderBlock(true);
                lastSwingHigh = 0;   // consommee : evite les BOS repetes
            }
            else if (lastSwingLow > 0 && close < lastSwingLow)
            {
                if (structureDirection > 0) { LastChoch = MiStructureEvent.BearishChoch; chochBar = barCounter; }
                LastBos = MiStructureEvent.BearishBos;
                bosBar = barCounter;
                structureDirection = -1;
                CaptureOrderBlock(false);
                lastSwingLow = 0;
            }
        }

        /// <summary>Derniere bougie opposee avant le deplacement qui a casse la structure.</summary>
        private void CaptureOrderBlock(bool bullish)
        {
            for (int i = bars.Count - 2; i >= 0; i--)
            {
                Bar b = bars[i];
                bool bearishCandle = b.C < b.O;
                if (bullish == bearishCandle)
                {
                    obHigh = b.H;
                    obLow = b.L;
                    OrderBlockKind = bullish ? MiOrderBlockKind.Bullish : MiOrderBlockKind.Bearish;
                    OrderBlockState = MiOrderBlockState.Valid;
                    obBar = barCounter;
                    return;
                }
            }
        }

        private void UpdateOrderBlockLifecycle(double high, double low, double close)
        {
            if (OrderBlockState == MiOrderBlockState.None || OrderBlockKind == MiOrderBlockKind.None) return;
            if (obHigh <= 0 && obLow <= 0) return;

            if (OrderBlockKind == MiOrderBlockKind.Bullish)
            {
                if (close < obLow) { OrderBlockState = MiOrderBlockState.Invalid; return; }
                if (OrderBlockState == MiOrderBlockState.Valid && low <= obHigh)
                    OrderBlockState = MiOrderBlockState.Mitigated;
            }
            else
            {
                if (close > obHigh) { OrderBlockState = MiOrderBlockState.Invalid; return; }
                if (OrderBlockState == MiOrderBlockState.Valid && high >= obLow)
                    OrderBlockState = MiOrderBlockState.Mitigated;
            }
        }
    }
}
