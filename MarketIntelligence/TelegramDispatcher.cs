#region Using declarations
using System;
using System.Collections.Generic;
using System.Threading;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence
{
    /// <summary>
    /// Transport Telegram du module : deduplication, retry avec backoff,
    /// limite d'envois, journalisation. Aucun plantage si Telegram est
    /// indisponible : toute erreur est journalisee puis abandonnee.
    /// L'envoi reel est injecte (delegue vers l'infrastructure existante).
    /// </summary>
    public sealed class TelegramDispatcher
    {
        /// <summary>send(text, onComplete) : onComplete(true) si envoi reussi.</summary>
        private readonly Action<string, Action<bool>> send;
        private readonly IMiLogger logger;
        private readonly Func<DateTime> clock;

        private string lastSentHash;
        private DateTime lastSentUtc = DateTime.MinValue;
        private int inFlight;

        public int MaxAttempts = 3;
        public int MaxInFlight = 2;
        /// <summary>Fenetre anti-doublon : un message identique n'est jamais renvoye.</summary>
        public TimeSpan DuplicateWindow = TimeSpan.FromMinutes(30);
        /// <summary>Delai minimal entre deux envois du module (anti-spam dur).</summary>
        public TimeSpan MinInterval = TimeSpan.FromSeconds(5);

        public TelegramDispatcher(Action<string, Action<bool>> send, IMiLogger logger, Func<DateTime> clock)
        {
            if (send == null) throw new ArgumentNullException("send");
            this.send = send;
            this.logger = logger;
            this.clock = clock ?? (() => DateTime.UtcNow);
        }

        public bool Dispatch(string text) { return Dispatch(text, false); }

        /// <param name="bypassMinInterval">true pour le rapport periodique H4 :
        /// La deduplication 30 min reste active dans tous les cas.</param>
        public bool Dispatch(string text, bool bypassMinInterval)
        {
            if (string.IsNullOrEmpty(text)) return false;

            DateTime now = clock();
            string hash = Hash(text);

            if (hash == lastSentHash && (now - lastSentUtc) < DuplicateWindow)
            {
                Log("message identique ignore (deduplication).");
                return false;
            }

            if (!bypassMinInterval && lastSentUtc != DateTime.MinValue && (now - lastSentUtc) < MinInterval)
            {
                Log("intervalle minimal non atteint, message ignore.");
                return false;
            }

            if (Interlocked.Increment(ref inFlight) > MaxInFlight)
            {
                Interlocked.Decrement(ref inFlight);
                Log("file saturee, message ignore.");
                return false;
            }

            lastSentHash = hash;
            lastSentUtc = now;
            Attempt(text, 1);
            return true;
        }

        private void Attempt(string text, int attempt)
        {
            try
            {
                send(text, ok =>
                {
                    if (ok)
                    {
                        Interlocked.Decrement(ref inFlight);
                        return;
                    }

                    if (attempt >= MaxAttempts)
                    {
                        Interlocked.Decrement(ref inFlight);
                        Log("echec definitif apres " + attempt + " tentatives.");
                        // Le hash est libere pour permettre un renvoi ulterieur.
                        lastSentHash = null;
                        return;
                    }

                    int delayMs = 1000 * (int)Math.Pow(2, attempt - 1); // 1s, 2s, 4s
                    Log("echec d'envoi, nouvelle tentative dans " + delayMs + " ms.");
                    // FIX AUDIT E4 : le timer se dispose et se retire de la liste
                    // apres execution pour eviter une fuite memoire sur les sessions longues.
                    Timer retryTimer = null;
                    retryTimer = new Timer(_ =>
                    {
                        Attempt(text, attempt + 1);
                        lock (timerLock)
                        {
                            if (retryTimer != null) pendingTimers.Remove(retryTimer);
                        }
                        try { if (retryTimer != null) retryTimer.Dispose(); } catch { }
                    }, null, delayMs, Timeout.Infinite);
                    lock (timerLock)
                    {
                        pendingTimers.Add(retryTimer);
                    }
                });
            }
            catch (Exception ex)
            {
                Interlocked.Decrement(ref inFlight);
                Log("exception d'envoi : " + ex.Message);
            }
        }

        private readonly object timerLock = new object();
        private readonly List<Timer> pendingTimers = new List<Timer>();

        public void Dispose()
        {
            lock (timerLock)
            {
                for (int i = 0; i < pendingTimers.Count; i++)
                {
                    try { pendingTimers[i].Dispose(); }
                    catch (Exception) { }
                }
                pendingTimers.Clear();
            }
            inFlight = 0;
            lastSentHash = null;
        }

        private static string Hash(string text)
        {
            if (text == null) return "0:0";
            unchecked
            {
                const ulong fnvOffset = 14695981039346656037UL;
                const ulong fnvPrime = 1099511628211UL;
                ulong hash = fnvOffset;
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= fnvPrime;
                }
                return text.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + hash.ToString("X16");
            }
        }

        private void Log(string message)
        {
            if (logger != null) logger.Log(message);
        }
    }
}
