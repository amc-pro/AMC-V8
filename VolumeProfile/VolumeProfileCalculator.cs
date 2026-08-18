#region Using declarations
using System;
using System.Collections.Generic;
using System.Globalization;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.VolumeProfilePro
{
    /// <summary>
    /// Moteur déterministe de calcul du Volume Profile (VAH/POC/VAL) et de détection
    /// mathématique robuste des HVN/LVN par lissage Gaussien et proéminence.
    /// </summary>
    public sealed class VolumeProfileCalculator
    {
        #region Paramètres de calcul

        public int ValueAreaPercent { get; set; }
        public double GaussianSigmaTicks { get; set; }
        public double HvnMinVolumeRatio { get; set; }
        public double LvnMaxVolumeRatio { get; set; }
        public int MinNodeSeparationTicks { get; set; }
        public int MaxNodesPerProfile { get; set; }

        #endregion

        #region Distribution interne

        private readonly Dictionary<long, long> volumeMap = new Dictionary<long, long>(4096);
        public long MinTick { get; private set; }
        public long MaxTick { get; private set; }
        public long TotalVolume { get; private set; }

        public VolumeProfileCalculator()
        {
            ValueAreaPercent = 70;
            GaussianSigmaTicks = 2.5;
            HvnMinVolumeRatio = 1.35;
            LvnMaxVolumeRatio = 0.65;
            MinNodeSeparationTicks = 10;
            MaxNodesPerProfile = 5;
            MinTick = long.MaxValue;
            MaxTick = long.MinValue;
            TotalVolume = 0;
        }

        public void Reset()
        {
            volumeMap.Clear();
            MinTick = long.MaxValue;
            MaxTick = long.MinValue;
            TotalVolume = 0;
        }

        public void AddVolume(long tick, long volume)
        {
            if (volume <= 0) return;
            long current;
            volumeMap.TryGetValue(tick, out current);
            volumeMap[tick] = current + volume;
            TotalVolume += volume;

            if (tick < MinTick) MinTick = tick;
            if (tick > MaxTick) MaxTick = tick;
        }

        public void AddVolumeAtPrice(double price, long volume, double tickSize)
        {
            if (tickSize <= 0) return;
            long tick = (long)Math.Round(price / tickSize);
            AddVolume(tick, volume);
        }

        public void Merge(VolumeProfileCalculator other)
        {
            if (other == null) return;
            foreach (var kv in other.volumeMap)
            {
                AddVolume(kv.Key, kv.Value);
            }
        }

        public long GetVolumeAtTick(long tick)
        {
            long v;
            return volumeMap.TryGetValue(tick, out v) ? v : 0L;
        }

        #endregion

        #region Calcul VAH / POC / VAL & ClosedVolumeProfile

        /// <summary>
        /// Construit un ClosedVolumeProfile complet et immuable pour une période donnée.
        /// </summary>
        public ClosedVolumeProfile BuildProfile(
            string symbol,
            string exchange,
            string sessionTemplate,
            VolumeProfilePeriodType periodType,
            string periodKey,
            DateTime periodStartUtc,
            DateTime periodEndUtc,
            double tickSize)
        {
            var profile = new ClosedVolumeProfile
            {
                Symbol = symbol ?? "",
                Exchange = exchange ?? "",
                SessionTemplate = sessionTemplate ?? "",
                ProfileType = periodType,
                PeriodKey = periodKey ?? "",
                PeriodStartUtc = periodStartUtc,
                PeriodEndUtc = periodEndUtc,
                TickSize = tickSize > 0 ? tickSize : 0.25,
                ValueAreaPercent = this.ValueAreaPercent,
                TotalVolume = this.TotalVolume,
                CreatedAtUtc = DateTime.UtcNow,
                Valid = false
            };

            if (TotalVolume <= 0 || volumeMap.Count == 0 || tickSize <= 0 || MinTick > MaxTick)
            {
                return profile;
            }

            // 1. Recherche du POC
            long pocTick = MinTick;
            long maxVol = -1;
            foreach (var kv in volumeMap)
            {
                if (kv.Value > maxVol)
                {
                    maxVol = kv.Value;
                    pocTick = kv.Key;
                }
            }

            // 2. Calcul de la Value Area (VAH / VAL)
            long targetVolume = (long)(TotalVolume * (ValueAreaPercent / 100.0));
            long accumulatedVolume = GetVolumeAtTick(pocTick);
            long upTick = pocTick;
            long dnTick = pocTick;

            int guard = 0;
            int maxSteps = (int)(MaxTick - MinTick + 10);

            while (accumulatedVolume < targetVolume && guard++ < maxSteps)
            {
                long nextUp = upTick + 1;
                long nextDn = dnTick - 1;

                long vUp = nextUp <= MaxTick ? GetVolumeAtTick(nextUp) : -1;
                long vDn = nextDn >= MinTick ? GetVolumeAtTick(nextDn) : -1;

                if (vUp < 0 && vDn < 0) break;

                if (vUp >= vDn)
                {
                    upTick = nextUp;
                    accumulatedVolume += Math.Max(0, vUp);
                }
                else
                {
                    dnTick = nextDn;
                    accumulatedVolume += Math.Max(0, vDn);
                }
            }

            profile.Poc = pocTick * tickSize;
            profile.Vah = upTick * tickSize;
            profile.Val = dnTick * tickSize;
            profile.Valid = true;

            // 3. Détection des HVN / LVN (pour Weekly et Monthly)
            if (periodType == VolumeProfilePeriodType.Weekly || periodType == VolumeProfilePeriodType.Monthly)
            {
                profile.Nodes = DetectNodes(tickSize);
            }

            return profile;
        }

        #endregion

        #region Détection HVN / LVN avec Lissage Gaussien

        /// <summary>
        /// Extrait les HVN et LVN significatifs à partir d'une distribution lissée par noyau Gaussien.
        /// </summary>
        public List<VolumeProfileNode> DetectNodes(double tickSize)
        {
            var nodes = new List<VolumeProfileNode>();
            if (volumeMap.Count < 5 || MinTick >= MaxTick || TotalVolume <= 0 || tickSize <= 0)
                return nodes;

            int range = (int)(MaxTick - MinTick + 1);
            if (range <= 3) return nodes;

            double[] raw = new double[range];
            for (int i = 0; i < range; i++)
            {
                raw[i] = GetVolumeAtTick(MinTick + i);
            }

            double meanVolume = (double)TotalVolume / range;
            if (meanVolume <= 0) return nodes;

            // 1. Lissage par filtre Gaussien 1D
            double[] smoothed = ApplyGaussianSmoothing(raw, GaussianSigmaTicks);

            // 2. Recherche des extrema locaux
            var hvnCandidates = new List<NodeCandidate>();
            var lvnCandidates = new List<NodeCandidate>();

            for (int i = 1; i < range - 1; i++)
            {
                double val = smoothed[i];
                double prev = smoothed[i - 1];
                double next = smoothed[i + 1];

                // Maximum local (HVN)
                if (val > prev && val >= next)
                {
                    double relVol = val / meanVolume;
                    if (relVol >= HvnMinVolumeRatio)
                    {
                        hvnCandidates.Add(new NodeCandidate
                        {
                            Index = i,
                            Tick = MinTick + i,
                            SmoothedVolume = val,
                            RelativeVolume = relVol,
                            IsHvn = true
                        });
                    }
                }
                // Minimum local (LVN)
                else if (val < prev && val <= next)
                {
                    double relVol = val / meanVolume;
                    if (relVol <= LvnMaxVolumeRatio)
                    {
                        lvnCandidates.Add(new NodeCandidate
                        {
                            Index = i,
                            Tick = MinTick + i,
                            SmoothedVolume = val,
                            RelativeVolume = relVol,
                            IsHvn = false
                        });
                    }
                }
            }

            // 3. Calcul de la proéminence et des zones [ZoneLow, ZoneHigh] à mi-hauteur (FWHM)
            var selectedHvns = FilterAndBuildZones(hvnCandidates, smoothed, raw, range, tickSize, meanVolume, true);
            var selectedLvns = FilterAndBuildZones(lvnCandidates, smoothed, raw, range, tickSize, meanVolume, false);

            nodes.AddRange(selectedHvns);
            nodes.AddRange(selectedLvns);

            // Trier par prix croissant
            nodes.Sort((a, b) => a.PeakPrice.CompareTo(b.PeakPrice));
            return nodes;
        }

        private List<VolumeProfileNode> FilterAndBuildZones(
            List<NodeCandidate> candidates,
            double[] smoothed,
            double[] raw,
            int range,
            double tickSize,
            double meanVolume,
            bool isHvn)
        {
            var result = new List<VolumeProfileNode>();
            if (candidates.Count == 0) return result;

            // Trier par force décroissante (volume relatif)
            if (isHvn)
                candidates.Sort((a, b) => b.RelativeVolume.CompareTo(a.RelativeVolume));
            else
                candidates.Sort((a, b) => a.RelativeVolume.CompareTo(b.RelativeVolume));

            var acceptedIndices = new List<int>();

            foreach (var cand in candidates)
            {
                // Vérifier la séparation minimale
                bool tooClose = false;
                foreach (int acc in acceptedIndices)
                {
                    if (Math.Abs(cand.Index - acc) < MinNodeSeparationTicks)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose) continue;

                // Calcul des bornes de la zone (FWHM / inflection)
                int left = cand.Index;
                int right = cand.Index;
                double peakVal = cand.SmoothedVolume;
                double threshold = isHvn ? (peakVal + meanVolume) * 0.5 : (peakVal + meanVolume) * 0.5;

                // Expansion à gauche
                while (left > 0)
                {
                    if (isHvn && smoothed[left - 1] < threshold) break;
                    if (!isHvn && smoothed[left - 1] > threshold) break;
                    left--;
                }

                // Expansion à droite
                while (right < range - 1)
                {
                    if (isHvn && smoothed[right + 1] < threshold) break;
                    if (!isHvn && smoothed[right + 1] > threshold) break;
                    right++;
                }

                double zoneLowPrice = (MinTick + left) * tickSize;
                double zoneHighPrice = (MinTick + right) * tickSize;
                double peakPrice = cand.Tick * tickSize;
                double prominence = Math.Abs(cand.SmoothedVolume - meanVolume) / meanVolume;

                var nodeType = isHvn ? VolumeProfileNodeType.HVN : VolumeProfileNodeType.LVN;
                result.Add(new VolumeProfileNode(nodeType, zoneLowPrice, zoneHighPrice, peakPrice, cand.RelativeVolume, prominence));
                acceptedIndices.Add(cand.Index);

                if (result.Count >= MaxNodesPerProfile)
                    break;
            }

            return result;
        }

        private static double[] ApplyGaussianSmoothing(double[] input, double sigma)
        {
            int n = input.Length;
            double[] output = new double[n];
            if (sigma <= 0.1 || n <= 2)
            {
                Array.Copy(input, output, n);
                return output;
            }

            int radius = (int)Math.Ceiling(sigma * 3);
            int kernelSize = radius * 2 + 1;
            double[] kernel = new double[kernelSize];
            double sum = 0;

            for (int i = -radius; i <= radius; i++)
            {
                double v = Math.Exp(-(i * i) / (2 * sigma * sigma));
                kernel[i + radius] = v;
                sum += v;
            }

            for (int i = 0; i < kernelSize; i++) kernel[i] /= sum;

            for (int i = 0; i < n; i++)
            {
                double acc = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int idx = i + k;
                    if (idx < 0) idx = 0;
                    else if (idx >= n) idx = n - 1;
                    acc += input[idx] * kernel[k + radius];
                }
                output[i] = acc;
            }

            return output;
        }

        private struct NodeCandidate
        {
            public int Index;
            public long Tick;
            public double SmoothedVolume;
            public double RelativeVolume;
            public bool IsHvn;
        }

        #endregion

        #region Partitionnement Temporel & Clés Déterministes

        // NOTE: Volume Profile is partitioned by the instrument's trading session,
        // not by UTC midnight.  For CME index/metal/energy futures this matters
        // because the electronic session crosses midnight in New York time.
        // We support the two conventions used by this project:
        //   RTH  -> New York calendar trading date, 09:30-16:00 ET
        //   ETH  -> CME Globex trading date, 18:00-17:00 ET next day
        // Custom session templates are mapped conservatively to the RTH calendar
        // date instead of silently pretending that UTC midnight is a session close.

        private static TimeZoneInfo GetNewYorkTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
            catch
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
                catch { return TimeZoneInfo.Utc; }
            }
        }

        private static DateTime ToNewYork(DateTime utc)
        {
            DateTime normalized = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(normalized, GetNewYorkTimeZone());
        }

        private static bool IsEthTemplate(string sessionTemplate)
        {
            string s = (sessionTemplate ?? string.Empty).Trim().ToUpperInvariant();
            return s.Contains("ETH") || s.Contains("GLOBEX") || s.Contains("24");
        }

        /// <summary>
        /// Returns the trading/session date represented by a bar timestamp.
        /// RTH uses the New York calendar date. ETH uses the CME 18:00 ET boundary.
        /// </summary>
        public static DateTime GetTradingSessionDateUtc(string sessionTemplate, DateTime barTimeUtc)
        {
            DateTime ny = ToNewYork(barTimeUtc);
            DateTime sessionDate = ny.Date;

            // CME Globex trade date: the session opening at 18:00 ET belongs to
            // the following business/trading date (Sunday 18:00 -> Monday).
            if (IsEthTemplate(sessionTemplate) && ny.TimeOfDay >= new TimeSpan(18, 0, 0))
                sessionDate = sessionDate.AddDays(1);

            return DateTime.SpecifyKind(sessionDate, DateTimeKind.Utc);
        }

        public static string GetTradingDayKey(string symbol, string exchange, string sessionTemplate, DateTime sessionDate)
        {
            DateTime tradingDate = GetTradingSessionDateUtc(sessionTemplate, sessionDate);
            return string.Format(CultureInfo.InvariantCulture,
                "{0}|{1}|{2}|DAILY|{3:yyyy-MM-dd}",
                symbol ?? "SYM", exchange ?? "EXCH", sessionTemplate ?? "RTH", tradingDate);
        }

        public static string GetTradingWeekKey(string symbol, string exchange, string sessionTemplate, DateTime sessionDate)
        {
            DateTime tradingDate = GetTradingSessionDateUtc(sessionTemplate, sessionDate);
            int weekNum = GetIsoWeekNumber(tradingDate);
            int year = GetIsoWeekYear(tradingDate);
            return string.Format(CultureInfo.InvariantCulture,
                "{0}|{1}|{2}|WEEKLY|{3}-W{4:D2}",
                symbol ?? "SYM", exchange ?? "EXCH", sessionTemplate ?? "RTH", year, weekNum);
        }

        public static int GetIsoWeekNumber(DateTime date)
        {
            DayOfWeek day = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(date);
            if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
                date = date.AddDays(3);
            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        public static int GetIsoWeekYear(DateTime date)
        {
            int week = GetIsoWeekNumber(date);
            if (week == 1 && date.Month == 12) return date.Year + 1;
            if (week >= 52 && date.Month == 1) return date.Year - 1;
            return date.Year;
        }

        public static string GetTradingMonthKey(string symbol, string exchange, string sessionTemplate, DateTime sessionDate)
        {
            DateTime tradingDate = GetTradingSessionDateUtc(sessionTemplate, sessionDate);
            return string.Format(CultureInfo.InvariantCulture,
                "{0}|{1}|{2}|MONTHLY|{3:yyyy-MM}",
                symbol ?? "SYM", exchange ?? "EXCH", sessionTemplate ?? "RTH", tradingDate);
        }

        /// <summary>
        /// Returns the actual session-aware UTC bounds containing referenceDateUtc.
        /// Daily RTH: 09:30-16:00 ET. Daily ETH: 18:00 ET -> 17:00 ET next day.
        /// Weekly RTH: Monday 09:30 -> Friday 16:00 ET.
        /// Weekly ETH: Sunday 18:00 -> Friday 17:00 ET.
        /// Monthly bounds use the same session convention at month boundaries.
        /// </summary>
        public static void GetPeriodBoundsUtc(
            VolumeProfilePeriodType periodType,
            DateTime referenceDateUtc,
            out DateTime startUtc,
            out DateTime endUtc)
        {
            referenceDateUtc = DateTime.SpecifyKind(referenceDateUtc, DateTimeKind.Utc);
            DateTime ny = ToNewYork(referenceDateUtc);
            bool eth = IsEthTemplate("ETH");
            DateTime sessionDate = GetTradingSessionDateUtc(eth ? "ETH" : "RTH", referenceDateUtc);

            if (periodType == VolumeProfilePeriodType.Daily)
            {
                DateTime localStart = eth
                    ? sessionDate.AddHours(18)
                    : sessionDate.AddHours(9).AddMinutes(30);
                DateTime localEnd = eth
                    ? sessionDate.AddDays(1).AddHours(17)
                    : sessionDate.AddHours(16);
                startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified), GetNewYorkTimeZone());
                endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localEnd, DateTimeKind.Unspecified), GetNewYorkTimeZone());
                return;
            }

            if (periodType == VolumeProfilePeriodType.Weekly)
            {
                int diffFromMonday = (7 + (sessionDate.DayOfWeek - DayOfWeek.Monday)) % 7;
                DateTime monday = sessionDate.Date.AddDays(-diffFromMonday);

                DateTime localStart = eth
                    ? monday.AddDays(-1).AddHours(18) // Sunday 18:00 ET
                    : monday.AddHours(9).AddMinutes(30);
                DateTime localEnd = eth
                    ? monday.AddDays(4).AddHours(17) // Friday 17:00 ET
                    : monday.AddDays(4).AddHours(16); // Friday 16:00 ET

                startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified), GetNewYorkTimeZone());
                endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localEnd, DateTimeKind.Unspecified), GetNewYorkTimeZone());
                return;
            }

            // Monthly: use the session calendar month. The profile closes at the
            // final trading session boundary of the month rather than UTC midnight.
            DateTime monthFirst = new DateTime(sessionDate.Year, sessionDate.Month, 1);
            DateTime monthLast = monthFirst.AddMonths(1).AddDays(-1);
            while (monthLast.DayOfWeek == DayOfWeek.Saturday || monthLast.DayOfWeek == DayOfWeek.Sunday)
                monthLast = monthLast.AddDays(-1);

            DateTime monthStartLocal = eth
                ? monthFirst.AddDays(-1).AddHours(18)
                : monthFirst.AddHours(9).AddMinutes(30);
            DateTime monthEndLocal = eth
                ? monthLast.AddHours(17)
                : monthLast.AddHours(16);

            startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(monthStartLocal, DateTimeKind.Unspecified), GetNewYorkTimeZone());
            endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(monthEndLocal, DateTimeKind.Unspecified), GetNewYorkTimeZone());
        }

        /// <summary>
        /// Session-aware bounds using the actual template passed by the manager.
        /// This overload must be used by production code; the legacy overload above
        /// remains for backward-compatible tests/API callers.
        /// </summary>
        public static void GetPeriodBoundsUtc(
            VolumeProfilePeriodType periodType,
            DateTime referenceDateUtc,
            string sessionTemplate,
            out DateTime startUtc,
            out DateTime endUtc)
        {
            referenceDateUtc = DateTime.SpecifyKind(referenceDateUtc, DateTimeKind.Utc);
            DateTime sessionDate = GetTradingSessionDateUtc(sessionTemplate, referenceDateUtc);
            bool eth = IsEthTemplate(sessionTemplate);
            TimeZoneInfo tz = GetNewYorkTimeZone();

            if (periodType == VolumeProfilePeriodType.Daily)
            {
                DateTime startLocal = eth ? sessionDate.AddHours(18) : sessionDate.AddHours(9).AddMinutes(30);
                DateTime endLocal = eth ? sessionDate.AddDays(1).AddHours(17) : sessionDate.AddHours(16);
                startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startLocal, DateTimeKind.Unspecified), tz);
                endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endLocal, DateTimeKind.Unspecified), tz);
                return;
            }

            if (periodType == VolumeProfilePeriodType.Weekly)
            {
                int diff = (7 + (sessionDate.DayOfWeek - DayOfWeek.Monday)) % 7;
                DateTime monday = sessionDate.Date.AddDays(-diff);
                DateTime startLocal = eth ? monday.AddDays(-1).AddHours(18) : monday.AddHours(9).AddMinutes(30);
                DateTime endLocal = eth ? monday.AddDays(4).AddHours(17) : monday.AddDays(4).AddHours(16);
                startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startLocal, DateTimeKind.Unspecified), tz);
                endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endLocal, DateTimeKind.Unspecified), tz);
                return;
            }

            DateTime first = new DateTime(sessionDate.Year, sessionDate.Month, 1);
            DateTime last = first.AddMonths(1).AddDays(-1);
            while (last.DayOfWeek == DayOfWeek.Saturday || last.DayOfWeek == DayOfWeek.Sunday)
                last = last.AddDays(-1);
            DateTime mStart = eth ? first.AddDays(-1).AddHours(18) : first.AddHours(9).AddMinutes(30);
            DateTime mEnd = eth ? last.AddHours(17) : last.AddHours(16);
            startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(mStart, DateTimeKind.Unspecified), tz);
            endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(mEnd, DateTimeKind.Unspecified), tz);
        }

        #endregion
    }
}
