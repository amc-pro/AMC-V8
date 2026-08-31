#region Using declarations
using System;
using System.Collections.Generic;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.VolumeProfilePro
{
    /// <summary>
    /// Orchestrateur principal du Volume Profile : gère l'accumulation distincte
    /// (Jour, Semaine, Mois), la clôture déterministe et la persistance SQLite.
    /// Garantit formellement le zéro look-ahead bias.
    /// </summary>
    public sealed class VolumeProfileManager : IDisposable
    {
        #region Propriétés & Services

        public string Symbol { get; set; }
        public string Exchange { get; set; }
        public string SessionTemplate { get; set; }
        public double TickSize { get; set; }
        public int ValueAreaPercent { get; set; }

        public VolumeProfileRepository Repository { get; private set; }
        public VolumeProfileAnalyzer Analyzer { get; private set; }

        // Profils clôturés immuables (Seules références exposées aux candidats)
        public ClosedVolumeProfile PrevDay { get; private set; }
        public ClosedVolumeProfile PrevWeek { get; private set; }
        public ClosedVolumeProfile PrevMonth { get; private set; }

        // Accumulateurs en cours (NON exposés aux candidats tant que la période n'est pas clôturée)
        private readonly VolumeProfileCalculator dayAccumulator = new VolumeProfileCalculator();
        private readonly VolumeProfileCalculator weekAccumulator = new VolumeProfileCalculator();
        private readonly VolumeProfileCalculator monthAccumulator = new VolumeProfileCalculator();

        // Suivi des périodes actives
        private string currentDayKey = "";
        private string currentWeekKey = "";
        private string currentMonthKey = "";
        private DateTime currentDayStartUtc = DateTime.MinValue;
        private DateTime currentDayEndUtc = DateTime.MinValue;
        private DateTime currentWeekStartUtc = DateTime.MinValue;
        private DateTime currentWeekEndUtc = DateTime.MinValue;
        private DateTime currentMonthStartUtc = DateTime.MinValue;
        private DateTime currentMonthEndUtc = DateTime.MinValue;

        private readonly Action<string> logAction;
        private bool isDisposed;

        #endregion

        #region Constructeur & Initialisation

        public VolumeProfileManager(
            string symbol,
            string exchange,
            string sessionTemplate,
            double tickSize,
            int valueAreaPercent,
            string dbPath,
            Action<string> logger = null)
        {
            this.Symbol = symbol ?? "SYM";
            this.Exchange = exchange ?? "CME";
            this.SessionTemplate = sessionTemplate ?? "RTH";
            this.TickSize = tickSize > 0 ? tickSize : 0.25;
            this.ValueAreaPercent = valueAreaPercent > 0 ? valueAreaPercent : 70;
            this.logAction = logger ?? (msg => { });

            this.dayAccumulator.ValueAreaPercent = this.ValueAreaPercent;
            this.weekAccumulator.ValueAreaPercent = this.ValueAreaPercent;
            this.monthAccumulator.ValueAreaPercent = this.ValueAreaPercent;

            this.Repository = new VolumeProfileRepository(dbPath, logAction);
            this.Analyzer = new VolumeProfileAnalyzer();
        }

        public void Initialize()
        {
            Repository.Initialize();
        }

        #endregion

        #region Ingestion des Barres & Détection des Clôtures

        /// <summary>
        /// Ingère une barre volumétrique clôturée et gère les transitions de périodes.
        /// </summary>
        public void IngestVolumetricBar(
            DateTime barTimeUtc,
            double barHigh,
            double barLow,
            double barClose,
            double barOpen,
            long barVolume,
            double barDelta,
            IEnumerable<KeyValuePair<long, long>> tickVolumes)
        {
            if (isDisposed || TickSize <= 0) return;

            string barDayKey = VolumeProfileCalculator.GetTradingDayKey(Symbol, Exchange, SessionTemplate, barTimeUtc);
            string barWeekKey = VolumeProfileCalculator.GetTradingWeekKey(Symbol, Exchange, SessionTemplate, barTimeUtc);
            string barMonthKey = VolumeProfileCalculator.GetTradingMonthKey(Symbol, Exchange, SessionTemplate, barTimeUtc);

            // 1. Clôture de Jour
            if (!string.IsNullOrEmpty(currentDayKey) && !string.Equals(currentDayKey, barDayKey, StringComparison.OrdinalIgnoreCase))
            {
                FinalizeDayProfile(currentDayStartUtc, currentDayEndUtc);
                currentDayAccumulatorReset(barDayKey, barTimeUtc);
            }
            else if (string.IsNullOrEmpty(currentDayKey))
            {
                currentDayAccumulatorReset(barDayKey, barTimeUtc);
                // Charger le profil précédent depuis SQLite si disponible
                if (PrevDay == null)
                    PrevDay = Repository.GetLatestClosedProfile(Symbol, VolumeProfilePeriodType.Daily, barTimeUtc);
            }

            // 2. Clôture de Semaine
            if (!string.IsNullOrEmpty(currentWeekKey) && !string.Equals(currentWeekKey, barWeekKey, StringComparison.OrdinalIgnoreCase))
            {
                FinalizeWeekProfile(currentWeekStartUtc, currentWeekEndUtc);
                currentWeekAccumulatorReset(barWeekKey, barTimeUtc);
            }
            else if (string.IsNullOrEmpty(currentWeekKey))
            {
                currentWeekAccumulatorReset(barWeekKey, barTimeUtc);
                if (PrevWeek == null)
                    PrevWeek = Repository.GetLatestClosedProfile(Symbol, VolumeProfilePeriodType.Weekly, barTimeUtc);
            }

            // 3. Clôture de Mois
            if (!string.IsNullOrEmpty(currentMonthKey) && !string.Equals(currentMonthKey, barMonthKey, StringComparison.OrdinalIgnoreCase))
            {
                FinalizeMonthProfile(currentMonthStartUtc, currentMonthEndUtc);
                currentMonthAccumulatorReset(barMonthKey, barTimeUtc);
            }
            else if (string.IsNullOrEmpty(currentMonthKey))
            {
                currentMonthAccumulatorReset(barMonthKey, barTimeUtc);
                if (PrevMonth == null)
                    PrevMonth = Repository.GetLatestClosedProfile(Symbol, VolumeProfilePeriodType.Monthly, barTimeUtc);
            }

            // 4. Ingestion du volume dans les accumulateurs
            if (tickVolumes != null)
            {
                foreach (var kv in tickVolumes)
                {
                    dayAccumulator.AddVolume(kv.Key, kv.Value);
                    weekAccumulator.AddVolume(kv.Key, kv.Value);
                    monthAccumulator.AddVolume(kv.Key, kv.Value);
                }
                dayAccumulator.IncrementBarsCount(1);
                weekAccumulator.IncrementBarsCount(1);
                monthAccumulator.IncrementBarsCount(1);
            }
            else
            {
                // Zero-Trust: a bar-level volume fallback would manufacture an
                // artificial price distribution and can materially distort POC/VAH/VAL.
                // For an institutional Volume Profile, missing per-price volume means
                // the bar is rejected from the profile rather than approximated uniformly.
                logAction("VolumeProfile: tickVolumes indisponible -> barre ignoree (aucun fallback uniforme). ");
            }
        }

        #endregion

        #region Clôtures de Périodes Déterministes

        private void FinalizeDayProfile(DateTime startUtc, DateTime endUtc)
        {
            if (dayAccumulator.TotalVolume <= 0) return;

            var closed = dayAccumulator.BuildProfile(
                Symbol, Exchange, SessionTemplate,
                VolumeProfilePeriodType.Daily, currentDayKey,
                startUtc, endUtc, TickSize);

            if (closed.Valid)
            {
                PrevDay = closed;
                Repository.UpsertProfile(closed);
            }
        }

        private void FinalizeWeekProfile(DateTime startUtc, DateTime endUtc)
        {
            if (weekAccumulator.TotalVolume <= 0) return;

            var closed = weekAccumulator.BuildProfile(
                Symbol, Exchange, SessionTemplate,
                VolumeProfilePeriodType.Weekly, currentWeekKey,
                startUtc, endUtc, TickSize);

            if (closed.Valid)
            {
                PrevWeek = closed;
                Repository.UpsertProfile(closed);
            }
        }

        private void FinalizeMonthProfile(DateTime startUtc, DateTime endUtc)
        {
            if (monthAccumulator.TotalVolume <= 0) return;

            var closed = monthAccumulator.BuildProfile(
                Symbol, Exchange, SessionTemplate,
                VolumeProfilePeriodType.Monthly, currentMonthKey,
                startUtc, endUtc, TickSize);

            if (closed.Valid)
            {
                PrevMonth = closed;
                Repository.UpsertProfile(closed);
            }
        }

        private void currentDayAccumulatorReset(string newKey, DateTime referenceUtc)
        {
            currentDayKey = newKey;
            VolumeProfileCalculator.GetPeriodBoundsUtc(VolumeProfilePeriodType.Daily, referenceUtc, SessionTemplate, out currentDayStartUtc, out currentDayEndUtc);
            dayAccumulator.Reset();
        }

        private void currentWeekAccumulatorReset(string newKey, DateTime referenceUtc)
        {
            currentWeekKey = newKey;
            VolumeProfileCalculator.GetPeriodBoundsUtc(VolumeProfilePeriodType.Weekly, referenceUtc, SessionTemplate, out currentWeekStartUtc, out currentWeekEndUtc);
            weekAccumulator.Reset();
        }

        private void currentMonthAccumulatorReset(string newKey, DateTime referenceUtc)
        {
            currentMonthKey = newKey;
            VolumeProfileCalculator.GetPeriodBoundsUtc(VolumeProfilePeriodType.Monthly, referenceUtc, SessionTemplate, out currentMonthStartUtc, out currentMonthEndUtc);
            monthAccumulator.Reset();
        }

        #endregion

        #region Analyse & Contexte Candidat

        /// <summary>
        /// Extrait en O(1) le VWAP et les bandes d'écart-type SD 1, 2, 3 du mois en cours de formation.
        /// Garantit une évaluation stricte sur barres clôturées ingérées.
        /// </summary>
        public bool TryGetCurrentMonthVwapAndBands(
            out double vwap,
            out double stdDev,
            out double sd1Upper,
            out double sd1Lower,
            out double sd2Upper,
            out double sd2Lower,
            out double sd3Upper,
            out double sd3Lower,
            out int barsCount,
            out DateTime monthStartUtc)
        {
            vwap = 0.0;
            stdDev = 0.0;
            sd1Upper = 0.0;
            sd1Lower = 0.0;
            sd2Upper = 0.0;
            sd2Lower = 0.0;
            sd3Upper = 0.0;
            sd3Lower = 0.0;
            barsCount = monthAccumulator.BarsAccumulated;
            monthStartUtc = currentMonthStartUtc;

            return monthAccumulator.TryCalculateVwapAndBands(
                TickSize,
                out vwap,
                out stdDev,
                out sd1Upper,
                out sd1Lower,
                out sd2Upper,
                out sd2Lower,
                out sd3Upper,
                out sd3Lower);
        }

        /// <summary>
        /// Génère le VolumeProfileContext pour la barre courante basé exclusivement sur les références clôturées.
        /// </summary>
        public VolumeProfileContext GetContext(
            double currentPrice,
            double barHigh,
            double barLow,
            double barClose,
            double barDelta,
            double atr,
            DateTime barTimeUtc)
        {
            return Analyzer.Analyze(
                currentPrice,
                barHigh,
                barLow,
                barClose,
                barDelta,
                atr,
                TickSize,
                barTimeUtc,
                PrevDay,
                PrevWeek,
                PrevMonth);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            if (Repository != null)
            {
                Repository.Dispose();
                Repository = null;
            }
        }

        #endregion
    }
}
