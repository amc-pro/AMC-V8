#region Using declarations
using System;
using System.Collections.Generic;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence
{
    /// <summary>
    /// Notifie uniquement lorsqu'un changement MAJEUR est detecte entre deux
    /// snapshots. Si rien d'important ne change : aucun message.
    /// </summary>
    public sealed class MarketUpdateEngine
    {
        private readonly MarketSnapshotBuilder builder;
        private readonly MarketSnapshotComparer comparer;
        private readonly TelegramFormatter formatter;
        private readonly TelegramDispatcher dispatcher;
        private readonly IMiLogger logger;

        private MarketSnapshot last;            // etat precedent
        private MarketSnapshot lastNotified;

        public bool Enabled = true;

        /// <summary>Dernier snapshot construit (consomme par le dashboard graphique).</summary>
        public MarketSnapshot Current { get { return last; } }

        public MarketUpdateEngine(MarketSnapshotBuilder builder,
                                  MarketSnapshotComparer comparer,
                                  TelegramFormatter formatter,
                                  TelegramDispatcher dispatcher,
                                  IMiLogger logger)
        {
            if (builder == null) throw new ArgumentNullException("builder");
            if (comparer == null) throw new ArgumentNullException("comparer");
            if (formatter == null) throw new ArgumentNullException("formatter");
            if (dispatcher == null) throw new ArgumentNullException("dispatcher");
            this.builder = builder;
            this.comparer = comparer;
            this.formatter = formatter;
            this.dispatcher = dispatcher;
            this.logger = logger;
        }

        /// <summary>Alimente l'etat de reference sans notifier (rapport H4).</summary>
        public void Prime(MarketSnapshot snapshot)
        {
            if (snapshot == null) return;
            last = snapshot;
            // Le rapport H4 EST un message recu par le trader : il sert de
            lastNotified = snapshot;
        }

        /// <summary>Evalue un nouvel etat de marche. Retourne true si un message a ete emis.</summary>
        public bool Evaluate()
        {
            if (!Enabled) return false;

            MarketSnapshot current;
            try { current = builder.Build(); }
            catch (Exception ex)
            {
                if (logger != null) logger.Log("evaluation impossible : " + ex.Message);
                return false;
            }

            if (last == null)
            {
                last = current;   // premiere reference, aucune notification
                return false;
            }

            MiAnalysisResult result = comparer.Compare(last, current);
            MarketSnapshot previous = last;
            last = current;       // un seul snapshot conserve en memoire

            // contre le dernier etat que le trader a reellement vu. Quatre
            // glissements sous le seuil ne peuvent plus passer inapercus.
            MarketSnapshot baseline = previous;
            if (!result.ShouldNotify && lastNotified != null && !ReferenceEquals(lastNotified, previous))
            {
                MiAnalysisResult drift = comparer.Compare(lastNotified, current);
                if (drift.ShouldNotify) { result = drift; baseline = lastNotified; }
            }

            if (!result.ShouldNotify)
            {
                if (logger != null && !string.IsNullOrEmpty(result.ReasonSummary))
                    logger.Log(result.ReasonSummary);
                return false;
            }

            string text = formatter.FormatUpdate(baseline, current, result);
            if (string.IsNullOrEmpty(text)) return false;
            bool sent = dispatcher.Dispatch(text);
            if (sent) lastNotified = current;
            return sent;
        }

        public void Reset()
        {
            last = null;
            lastNotified = null;
        }
    }
}
