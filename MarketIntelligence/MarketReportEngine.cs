#region Using declarations
using System;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence
{
    /// <summary>
    /// Envoie un rapport complet a chaque ouverture d'une nouvelle bougie H4.
    /// Ne genere aucun signal de trading.
    /// </summary>
    public sealed class MarketReportEngine
    {
        private readonly MarketSnapshotBuilder builder;
        private readonly TelegramFormatter formatter;
        private readonly TelegramDispatcher dispatcher;
        private readonly IMiLogger logger;

        private DateTime lastReportedH4 = DateTime.MinValue;

        public bool Enabled = true;

        public MarketReportEngine(MarketSnapshotBuilder builder,
                                  TelegramFormatter formatter,
                                  TelegramDispatcher dispatcher,
                                  IMiLogger logger)
        {
            if (builder == null) throw new ArgumentNullException("builder");
            if (formatter == null) throw new ArgumentNullException("formatter");
            if (dispatcher == null) throw new ArgumentNullException("dispatcher");
            this.builder = builder;
            this.formatter = formatter;
            this.dispatcher = dispatcher;
            this.logger = logger;
        }

        /// <summary>
        /// Appele a l'ouverture d'une nouvelle bougie H4. Retourne le snapshot
        /// construit (reutilise par le MarketUpdateEngine : aucun recalcul).
        /// </summary>
        public MarketSnapshot OnNewH4Bar(DateTime h4OpenTime)
        {
            if (!Enabled) return null;
            if (h4OpenTime <= lastReportedH4) return null;   // jamais deux fois la meme bougie

            MarketSnapshot snapshot;
            try { snapshot = builder.Build(); }
            catch (Exception ex)
            {
                if (logger != null) logger.Log("construction du snapshot impossible : " + ex.Message);
                return null;
            }

            lastReportedH4 = h4OpenTime;

            string text = formatter.FormatReport(snapshot);
            // jamais etre avale par l'anti-spam 5 s declenche par une alerte M15.
            if (!string.IsNullOrEmpty(text)) dispatcher.Dispatch(text, true);
            return snapshot;
        }

        public void Reset()
        {
            lastReportedH4 = DateTime.MinValue;
        }
    }
}
