#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.BarsTypes;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using System.Windows.Media;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public partial class AuctionMarketCore
    {
        #region Telegram & Logging
        // Remonte une action sur le thread UI NinjaTrader (OnBarUpdate / Print).
        // FIX AUDIT E2 : les blocs catch utilisent Print() directement et NON
        // SafePrint() (qui rappelle RunOnUiThread) pour eviter une recursion
        // infinie si le Dispatcher est defaillant.
        private void RunOnUiThread(Action action)
        {
            if (action == null) return;
            try
            {
                if (ChartControl != null)
                    ChartControl.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { action(); }
                        catch (Exception ex)
                        {
                            // Print direct : pas de SafePrint ici (anti-recursion)
                            try { Print("UI Error: " + ex.Message); } catch { }
                            if (EnableDebugMode)
                                try { Print("UI Stack: " + ex.StackTrace); } catch { }
                        }
                    }));
                else
                    action();
            }
            catch (Exception ex)
            {
                // Print direct : pas de SafePrint ici (anti-recursion)
                try { Print("Dispatcher Error: " + ex.Message); } catch { }
                if (EnableDebugMode)
                    try { Print("Dispatcher Stack: " + ex.StackTrace); } catch { }
            }
        }

        private void SafePrint(string message)
        {
            RunOnUiThread(() => Print(message));
        }

        // Decoupe un message depassant la limite Telegram (4096 caracteres).
        // Ferme proprement les balises HTML ouvertes a la fin de chaque
        // fragment dans l'ordre inverse (LIFO) et les rouvre au debut du suivant,
        // afin d'eviter un rejet HTTP 400 ("Can't parse entities: unclosed tag") par l'API Telegram.
        private static List<string> SplitTelegramMessage(string text)
        {
            var parts = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                parts.Add("");
                return parts;
            }

            if (text.Length <= TelegramMaxMessageLength)
            {
                parts.Add(text);
                return parts;
            }

            var chunks = new List<string>();
            int start = 0;
            while (start < text.Length)
            {
                int chunkMax = TelegramMaxMessageLength - 48;
                int len = Math.Min(chunkMax, text.Length - start);
                if (start + len < text.Length)
                {
                    int breakAt = text.LastIndexOf('\n', start + len - 1, len);
                    if (breakAt > start)
                        len = breakAt - start + 1;
                }

                chunks.Add(text.Substring(start, len));
                start += len;
            }

            int total = chunks.Count;
            for (int i = 0; i < total; i++)
            {
                string header = total > 1 ? string.Format(CultureInfo.InvariantCulture, "<i>({0}/{1})</i>\n", i + 1, total) : "";
                string chunk = chunks[i];

                // Analyse des balises ouvertes dans ce chunk
                var openStack = new List<string>();
                int idx = 0;
                while (idx < chunk.Length)
                {
                    int openTagStart = chunk.IndexOf('<', idx);
                    if (openTagStart < 0) break;
                    int openTagEnd = chunk.IndexOf('>', openTagStart);
                    if (openTagEnd < 0) break;

                    string tagContent = chunk.Substring(openTagStart + 1, openTagEnd - openTagStart - 1).Trim();
                    idx = openTagEnd + 1;

                    if (tagContent.StartsWith("/"))
                    {
                        string closedTag = tagContent.Substring(1).Trim().ToLowerInvariant();
                        for (int s = openStack.Count - 1; s >= 0; s--)
                        {
                            if (openStack[s] == closedTag)
                            {
                                openStack.RemoveAt(s);
                                break;
                            }
                        }
                    }
                    else
                    {
                        string tagName = tagContent.Split(' ')[0].ToLowerInvariant();
                        if (tagName == "b" || tagName == "i" || tagName == "u" || tagName == "s" || tagName == "code" || tagName == "pre" || tagName == "blockquote" || tagName == "a")
                        {
                            openStack.Add(tagName);
                        }
                    }
                }

                // Fermer les balises restantes dans l'ordre inverse (LIFO)
                var suffix = new StringBuilder();
                var prefix = new StringBuilder();
                for (int s = openStack.Count - 1; s >= 0; s--)
                {
                    suffix.Append("</").Append(openStack[s]).Append(">");
                }
                for (int s = 0; s < openStack.Count; s++)
                {
                    prefix.Append("<").Append(openStack[s]).Append(">");
                }

                string part = header + chunk + suffix.ToString();
                if (part.Length > TelegramMaxMessageLength)
                    part = part.Substring(0, TelegramMaxMessageLength);
                parts.Add(part);

                if (prefix.Length > 0 && i + 1 < total)
                    chunks[i + 1] = prefix.ToString() + chunks[i + 1];
            }

            return parts;
        }

        private static string StripHtml(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty)
                .Replace("&amp;", "&")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">");
        }

        // par Telegram (champ parameters.retry_after du corps JSON, ou en-tete HTTP
        // Retry-After) au lieu d'un delai fixe de 2 s. Attendre moins que demande
        // provoque un nouveau 429 et un ban temporaire progressif ; attendre plus
        // longtemps que necessaire retarde inutilement l'alerte.
        // CORRECTION CS1985 : les appels await sont déplacés hors des blocs catch.
        private async Task<bool> PostTelegramMessageAsync(string token, string chatId, string text, CancellationToken ct)
        {
            const int MaxAttempts = 3;
            const int MaxRetryWaitSeconds = 60;

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                bool shouldRetry = false;
                int delayMs = 0;
                HttpResponseMessage response = null;
                string body = null;

                try
                {
                    using (var content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        { "chat_id", chatId },
                        { "text", text },
                        { "parse_mode", "HTML" }
                    }))
                    {
                        try
                        {
                            response = await TelegramClient.PostAsync(
                                string.Format("https://api.telegram.org/bot{0}/sendMessage", token), content, ct)
                                .ConfigureAwait(false);
                        }
                        catch (HttpRequestException ex)
                        {
                            if (attempt == MaxAttempts - 1)
                            {
                                SafePrint("Telegram: echec reseau definitif (" + ex.Message + ").");
                                return false;
                            }
                            delayMs = 1000 << attempt;
                            shouldRetry = true;
                            SafePrint("Telegram: erreur reseau (" + ex.Message + "), reessai dans "
                                + delayMs + " ms.");
                        }
                        catch (TaskCanceledException)
                        {
                            if (ct.IsCancellationRequested) throw;
                            if (attempt == MaxAttempts - 1)
                            {
                                SafePrint("Telegram: timeout definitif.");
                                return false;
                            }
                            delayMs = 1000 << attempt;
                            shouldRetry = true;
                            SafePrint("Telegram: timeout, reessai en " + delayMs + " ms.");
                        }

                        // FIX AUDIT #4: Vérification null avant utilisation de response
                        if (response == null)
                        {
                            SafePrint("Telegram: response null après envoi");
                            return false;
                        }

                        // FIX CS1985: Attendre en dehors des blocs catch
                        if (shouldRetry)
                        {
                            await Task.Delay(delayMs, ct).ConfigureAwait(false);
                            continue;
                        }

                        body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        int status = (int)response.StatusCode;

                        if (response.IsSuccessStatusCode)
                            return true;

                        SafePrint(string.Format("Telegram HTTP {0}: {1}", status, body));

                        // Fallback robuste : si Telegram rejette le HTML (400 Bad Request / parse error),
                        // on renvoie immédiatement le message nettoyé en texte brut pour ne jamais perdre l'alerte.
                        if (status == 400 && !string.IsNullOrEmpty(body) &&
                            (body.IndexOf("parse entities", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             body.IndexOf("unclosed tag", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             body.IndexOf("bad request", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            SafePrint("Telegram: bascule de secours en texte brut (erreur HTML 400).");
                            string plain = StripHtml(text);
                            using (var plainContent = new FormUrlEncodedContent(new Dictionary<string, string>
                            {
                                { "chat_id", chatId },
                                { "text", plain }
                            }))
                            {
                                try
                                {
                                    var plainResp = await TelegramClient.PostAsync(
                                        string.Format("https://api.telegram.org/bot{0}/sendMessage", token), plainContent, ct)
                                        .ConfigureAwait(false);
                                    if (plainResp != null && plainResp.IsSuccessStatusCode)
                                        return true;
                                }
                                catch (Exception pEx)
                                {
                                    SafePrint("Telegram: echec fallback texte brut: " + pEx.Message);
                                }
                            }
                        }

                        // Erreurs 5xx transitoires
                        if (status >= 500 && status < 600 && attempt < MaxAttempts - 1)
                        {
                            delayMs = 1000 << attempt;
                            SafePrint("Telegram: HTTP " + status + ", reessai dans " + delayMs + " ms.");
                            await Task.Delay(delayMs, ct).ConfigureAwait(false);
                            continue;
                        }

                        // Rate limiting (429)
                        if (status == 429 && attempt < MaxAttempts - 1)
                        {
                            int waitSeconds = ExtractRetryAfterSeconds(response, body);
                            if (waitSeconds <= 0) waitSeconds = 2;
                            if (waitSeconds > MaxRetryWaitSeconds)
                            {
                                SafePrint(string.Format("Telegram 429: retry_after {0}s > {1}s, message abandonne.",
                                    waitSeconds, MaxRetryWaitSeconds));
                                return false;
                            }
                            SafePrint(string.Format("Telegram 429: attente {0}s avant reessai.", waitSeconds));
                            await Task.Delay(TimeSpan.FromMilliseconds(waitSeconds * 1000 + 250), ct).ConfigureAwait(false);
                            continue;
                        }

                        // Autres erreurs : abandon
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    SafePrint("Telegram: exception inattendue " + ex.Message);
                    return false;
                }
            }

            return false;
        }

        // Lit le delai impose par Telegram. Priorite au corps JSON
        // ("parameters":{"retry_after":N}), repli sur l'en-tete Retry-After.
        private static int ExtractRetryAfterSeconds(HttpResponseMessage response, string body)
        {
            if (!string.IsNullOrEmpty(body))
            {
                const string key = "\"retry_after\"";
                int k = body.IndexOf(key, StringComparison.OrdinalIgnoreCase);
                if (k >= 0)
                {
                    int i = k + key.Length;
                    while (i < body.Length && (body[i] == ':' || body[i] == ' ')) i++;
                    int start = i;
                    while (i < body.Length && char.IsDigit(body[i])) i++;
                    int parsed;
                    if (i > start && int.TryParse(body.Substring(start, i - start), out parsed) && parsed > 0)
                        return parsed;
                }
            }

            try
            {
                if (response.Headers.RetryAfter != null)
                {
                    if (response.Headers.RetryAfter.Delta.HasValue)
                        return (int)Math.Ceiling(response.Headers.RetryAfter.Delta.Value.TotalSeconds);
                    if (response.Headers.RetryAfter.Date.HasValue)
                    {
                        double secs = (response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow).TotalSeconds;
                        if (secs > 0) return (int)Math.Ceiling(secs);
                    }
                }
            }
            catch { /* en-tete malformee : on retombe sur le delai par defaut */ }

            return 0;
        }


        #region Centralized Telegram Message Formatting
        private string FormatPriceClean(double p)
        {
            if (p <= 0) return "-";
            return Instrument != null && Instrument.MasterInstrument != null
                ? Instrument.MasterInstrument.FormatPrice(p)
                : p.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private string FormatExecutionPlan(double entry, double stop, double target1, double target2, double rr, int positionSize = 0, double riskCurrency = 0)
        {
            if (entry <= 0 || stop <= 0) return "";

            double riskPts = Math.Abs(entry - stop);
            double riskTks = tickSize > 0 ? riskPts / tickSize : 0;
            double r1Ratio = (riskPts > 0 && target1 > 0) ? Math.Abs(target1 - entry) / riskPts : 1.0;
            double r2Ratio = (riskPts > 0 && target2 > 0) ? Math.Abs(target2 - entry) / riskPts : (rr > 0 ? rr : 2.0);

            var sb = new StringBuilder(300);
            sb.Append(string.Format(CultureInfo.InvariantCulture, "🎯 <b>PLAN D'EXÉCUTION</b> (R:R 1:{0:0.00})\n", rr > 0 ? rr : (r2Ratio > 0 ? r2Ratio : 1.0)));
            sb.Append("<code>┌ Entrée : ").Append(FormatPriceClean(entry)).Append("</code>\n");
            sb.Append("<code>├ Stop   : ").Append(FormatPriceClean(stop))
              .Append(string.Format(CultureInfo.InvariantCulture, "  (-{0:0.00} pts / {1:0}t)", riskPts, riskTks)).Append("</code>\n");

            bool hasSizing = positionSize > 0 && riskCurrency > 0;

            if (target1 > 0)
            {
                sb.Append("<code>├ TP1    : ").Append(FormatPriceClean(target1))
                  .Append(string.Format(CultureInfo.InvariantCulture, "  (+{0:0.0}R)", r1Ratio)).Append("</code>\n");
            }

            if (target2 > 0)
            {
                if (hasSizing)
                {
                    sb.Append("<code>├ TP2    : ").Append(FormatPriceClean(target2))
                      .Append(string.Format(CultureInfo.InvariantCulture, "  (+{0:0.0}R)", r2Ratio)).Append("</code>\n");
                    sb.Append(string.Format(CultureInfo.InvariantCulture, "<code>└ Risque : {0} contrat(s) [${1:0}]</code>\n", positionSize, riskCurrency));
                }
                else
                {
                    sb.Append("<code>└ TP2    : ").Append(FormatPriceClean(target2))
                      .Append(string.Format(CultureInfo.InvariantCulture, "  (+{0:0.0}R)", r2Ratio)).Append("</code>\n");
                }
            }
            else if (hasSizing)
            {
                sb.Append(string.Format(CultureInfo.InvariantCulture, "<code>└ Risque : {0} contrat(s) [${1:0}]</code>\n", positionSize, riskCurrency));
            }
            else if (target1 > 0)
            {
                sb.Replace("<code>├ TP1", "<code>└ TP1");
            }
            else
            {
                sb.Replace("<code>├ Stop", "<code>└ Stop");
            }

            sb.Append("\n");
            return sb.ToString();
        }

        private string FormatMarketLevels(double vah, double poc, double val, double vwap, string htfBias, string stats = null)
        {
            var sb = new StringBuilder(250);
            sb.Append("📍 <b>NIVEAUX CLÉS DU MARCHÉ</b>\n");
            sb.Append("▫️ <b>VAH :</b> <code>").Append(FormatPriceClean(vah))
              .Append("</code> │ <b>POC :</b> <code>").Append(FormatPriceClean(poc))
              .Append("</code> │ <b>VAL :</b> <code>").Append(FormatPriceClean(val)).Append("</code>\n");

            if (vwap > 0 && !string.IsNullOrWhiteSpace(htfBias))
            {
                sb.Append("▫️ <b>VWAP :</b> <code>").Append(FormatPriceClean(vwap))
                  .Append("</code> │ <b>Biais HTF :</b> <code>").Append(EscapeHtml(htfBias)).Append("</code>\n");
            }
            else if (vwap > 0)
            {
                sb.Append("▫️ <b>VWAP :</b> <code>").Append(FormatPriceClean(vwap)).Append("</code>\n");
            }
            else if (!string.IsNullOrWhiteSpace(htfBias))
            {
                sb.Append("▫️ <b>Biais HTF :</b> <code>").Append(EscapeHtml(htfBias)).Append("</code>\n");
            }

            if (!string.IsNullOrWhiteSpace(stats))
            {
                sb.Append("▫️ <b>Stats :</b> <code>").Append(EscapeHtml(stats)).Append("</code>\n");
            }

            return sb.ToString();
        }

        private string FormatOrderFlowBlock(long delta, long cumDelta, long volume, string absorption, string iceberg, string imbalance)
        {
            var sb = new StringBuilder(300);
            sb.Append("🌊 <b>ORDER FLOW & AUCTION</b>\n");
            sb.Append(string.Format(CultureInfo.InvariantCulture, "▫️ <b>Delta :</b> <code>{0:+#;-#;0}</code> │ <b>Cumul :</b> <code>{1:+#;-#;0}</code>\n", delta, cumDelta));
            sb.Append(string.Format(CultureInfo.InvariantCulture, "▫️ <b>Volume :</b> <code>{0:N0}</code>\n", volume));

            bool hasAbsorption = !string.IsNullOrWhiteSpace(absorption) 
                && !absorption.Equals("Néant", StringComparison.OrdinalIgnoreCase) 
                && !absorption.Equals("None", StringComparison.OrdinalIgnoreCase)
                && !absorption.Equals("Non", StringComparison.OrdinalIgnoreCase);

            bool hasIceberg = !string.IsNullOrWhiteSpace(iceberg) 
                && !iceberg.Equals("Néant", StringComparison.OrdinalIgnoreCase) 
                && !iceberg.Equals("None", StringComparison.OrdinalIgnoreCase)
                && !iceberg.Equals("Non", StringComparison.OrdinalIgnoreCase);

            bool hasImbalance = !string.IsNullOrWhiteSpace(imbalance) 
                && !imbalance.Equals("Néant", StringComparison.OrdinalIgnoreCase) 
                && !imbalance.Equals("None", StringComparison.OrdinalIgnoreCase)
                && !imbalance.Equals("Non", StringComparison.OrdinalIgnoreCase);

            if (hasAbsorption)
                sb.Append("▫️ <b>Absorption :</b> <code>").Append(EscapeHtml(absorption)).Append("</code>\n");
            if (hasIceberg)
                sb.Append("▫️ <b>Iceberg :</b> <code>").Append(EscapeHtml(iceberg)).Append("</code>\n");
            if (hasImbalance)
                sb.Append("▫️ <b>Imbalance :</b> <code>").Append(EscapeHtml(imbalance)).Append("</code>\n");

            if (!hasAbsorption && !hasIceberg && !hasImbalance)
            {
                sb.Append("▫️ <b>Anomalies OF :</b> <i>Aucune</i>\n");
            }

            sb.Append("\n");
            return sb.ToString();
        }

        private string BuildSniperTelegramAlert(Candidate c)
        {
            if (c == null) return "";

            StringBuilder sb = new StringBuilder(1400);
            string dirEmoji = c.IsBuy ? "🟢" : "🔴";
            string dirText = c.IsBuy ? "BUY LONG" : "SELL SHORT";
            string instName = Instrument != null && Instrument.MasterInstrument != null
                ? Instrument.MasterInstrument.Name
                : (Instrument != null ? Instrument.FullName : "?");
            string gradeIcon = c.Grade == "A+" ? "🏆" : (c.Grade == "A" ? "⭐" : "🎯");

            sb.Append("🎯 <b>SNIPER PRO │ SIGNAL DÉTECTÉ</b>\n");
            sb.Append(dirEmoji).Append(" <b>").Append(dirText).Append("</b> │ <b>")
              .Append(EscapeHtml(instName)).Append("</b> (M").Append(VolumetricTimeframe).Append(")\n");
            sb.Append(gradeIcon).Append(" <b>Grade :</b> <code>").Append(c.Grade)
              .Append(" [").Append(c.Score.ToString("0", CultureInfo.InvariantCulture)).Append("/100]</code>\n");
            sb.Append("━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

            sb.Append("📌 <b>Setup :</b> <code>").Append(EscapeHtml(c.Name)).Append("</code>\n");

            // Filtrage intelligent des confluences : élimine le nom du setup lui-même et les doublons
            var distinctConfluences = new List<string>();
            if (c.EvidenceList != null)
            {
                for (int i = 0; i < c.EvidenceList.Count; i++)
                {
                    string ev = c.EvidenceList[i];
                    if (string.IsNullOrWhiteSpace(ev)) continue;
                    ev = ev.Trim();
                    if (!string.Equals(ev, (c.Name ?? "").Trim(), StringComparison.OrdinalIgnoreCase)
                        && !distinctConfluences.Contains(ev))
                    {
                        distinctConfluences.Add(ev);
                    }
                }
            }

            if (distinctConfluences.Count > 0)
            {
                sb.Append("⚡ <b>Confluences :</b>\n");
                int maxEv = Math.Min(4, distinctConfluences.Count);
                for (int e = 0; e < maxEv; e++)
                {
                    sb.Append("  ▫️ <i>").Append(EscapeHtml(distinctConfluences[e])).Append("</i>\n");
                }
            }

            sb.Append("\n");

            // Plan d'exécution
            string planBlock = FormatExecutionPlan(c.Entry, c.Stop, c.Target1, c.Target2, c.Rr, 0, 0);
            if (!string.IsNullOrEmpty(planBlock))
                sb.Append(planBlock);

            // Niveaux clés du marché
            string htfBias = !string.IsNullOrEmpty(htfBiasText) ? htfBiasText : null;
            sb.Append(FormatMarketLevels(vahPrice, pocPrice, valPrice, currentVwapPrice, htfBias, null));

            sb.Append("━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
            sb.Append("🕒 <i>").Append(c.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
              .Append(" │ ").Append(TradingPreset).Append("</i>");

            return sb.ToString();
        }

        private string BuildAmcTelegramAlert(
            bool isBuySignal,
            bool isSellSignal,
            string currentSignal,
            int confluenceScore,
            int maxConfluenceScore,
            double confluenceWeighted,
            string allSignalsText,
            bool hasRisk,
            double entry,
            double stop,
            double target1,
            double target2,
            double riskTicks,
            int positionSize,
            double riskCurrency,
            string currentInterpretation,
            long barDelta,
            long cumDelta,
            long volume,
            string absorptionStatus,
            string icebergStatus,
            string imbalanceStatus,
            double vah,
            double poc,
            double val,
            double vwap,
            string htfBias,
            string stats,
            DateTime time)
        {
            var sb = new StringBuilder(1400);
            string dirEmoji = isBuySignal ? "🟢" : (isSellSignal ? "🔴" : "⚪");
            string dirText = isBuySignal ? "BUY LONG" : (isSellSignal ? "SELL SHORT" : "NEUTRAL");
            string instName = Instrument != null && Instrument.MasterInstrument != null
                ? Instrument.MasterInstrument.Name
                : (Instrument != null ? Instrument.FullName : "?");

            string strengthIcon = "⚡";
            if (!string.IsNullOrEmpty(currentSignal))
            {
                if (currentSignal.Contains("BREAKOUT") || currentSignal.Contains("très fort")) strengthIcon = "🔥";
                else if (currentSignal.Contains("REJET POC") || currentSignal.Contains("Moyen")) strengthIcon = "⚖️";
            }

            string confluenceText = (confluenceScore > 0 && maxConfluenceScore > 0)
                ? string.Format(CultureInfo.InvariantCulture,
                    "<b>{0:0.0}</b>/10 <code>[{1}/{2} filtres]</code>", confluenceWeighted, confluenceScore, maxConfluenceScore)
                : "N/A";

            sb.Append("⚡ <b>AMC PRO │ SIGNAL D'EXÉCUTION</b>\n");
            sb.Append(dirEmoji).Append(" <b>").Append(dirText).Append("</b> │ <b>")
              .Append(EscapeHtml(instName)).Append("</b> (M").Append(VolumetricTimeframe).Append(")\n");
            sb.Append(strengthIcon).Append(" <b>Confluence :</b> ").Append(confluenceText).Append("\n");
            sb.Append("━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

            sb.Append("📌 <b>Signal :</b> <code>").Append(EscapeHtml(currentSignal)).Append("</code>\n");

            // Signaux simultanés : n'afficher QUE si plusieurs signaux distincts existent
            if (!string.IsNullOrEmpty(allSignalsText) && allSignalsText.Contains(" | "))
            {
                sb.Append("⚡ <b>Signaux simultanés :</b> <code>").Append(EscapeHtml(allSignalsText)).Append("</code>\n");
            }

            sb.Append("\n");

            // Plan d'exécution
            if (hasRisk && stop > 0)
            {
                double rrRatio = (riskTicks > 0 && TargetR2 > 0) ? TargetR2 : 1.0;
                string planBlock = FormatExecutionPlan(entry, stop, target1, target2, rrRatio, positionSize, riskCurrency);
                if (!string.IsNullOrEmpty(planBlock))
                    sb.Append(planBlock);
            }

            // Interprétation optionnelle
            if (!string.IsNullOrWhiteSpace(currentInterpretation))
            {
                sb.Append("<blockquote>💡 <b>Interprétation :</b> ").Append(EscapeHtml(currentInterpretation)).Append("</blockquote>\n\n");
            }

            // Order Flow & Auction
            sb.Append(FormatOrderFlowBlock(barDelta, cumDelta, volume, absorptionStatus, icebergStatus, imbalanceStatus));

            // Niveaux clés du marché
            sb.Append(FormatMarketLevels(vah, poc, val, vwap, htfBias, stats));

            sb.Append("━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
            sb.Append("🕒 <i>").Append(time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
              .Append(" │ AMC Pro</i>");

            return sb.ToString();
        }
        #endregion

        // Le token et le chat id sont lus directement depuis les proprietes de
        // l'indicateur (champs "Telegram Bot Token" / "Telegram Chat ID").
        // Aucune variable d'environnement ni fichier externe n'est utilise.
        private string ResolveBotToken()
        {
            return string.IsNullOrWhiteSpace(BotToken) ? null : BotToken.Trim();
        }

        private string ResolveChatId(int channel = 1)
        {
            string id = ChatId;
            if (channel == 2) id = ChatId2;
            else if (channel == 3) id = ChatId3;
            return string.IsNullOrWhiteSpace(id) ? null : id.Trim();
        }

        private void SendTelegramMessage(string text, Action<bool> onComplete = null, int channel = 1)
        {
            string resolvedToken = ResolveBotToken();
            string resolvedChatId = ResolveChatId(channel);
            
            // Repli sur le canal 1 si le canal cible n'est pas configuré
            if ((channel == 2 || channel == 3) && string.IsNullOrWhiteSpace(resolvedChatId))
                resolvedChatId = ResolveChatId(1);

            if (string.IsNullOrWhiteSpace(resolvedToken) || string.IsNullOrWhiteSpace(resolvedChatId))
            {
                if (EnableDebugMode) Print("VP_Telegram: token ou chat id manquant, message non envoye.");
                if (onComplete != null) RunOnUiThread(() => onComplete(false));
                return;
            }

            if (Interlocked.Increment(ref telegramInFlightCount) > MaxTelegramInFlight)
            {
                Interlocked.Decrement(ref telegramInFlightCount);
                SafePrint("Telegram: file saturee (" + MaxTelegramInFlight + " envois en vol), message ignore.");
                if (onComplete != null) RunOnUiThread(() => onComplete(false));
                return;
            }

            List<string> parts = SplitTelegramMessage(text);
            CancellationToken ct = telegramCts != null ? telegramCts.Token : CancellationToken.None;

            try
            {
                string chatId = resolvedChatId;
                string token = resolvedToken;

                Task.Run(async () =>
                {
                    bool gateEntered = false;
                    bool allOk = false;

                    try
                    {
                        await telegramSendGate.WaitAsync(ct).ConfigureAwait(false);
                        gateEntered = true;

                        allOk = true;

                        for (int i = 0; i < parts.Count; i++)
                        {
                            ct.ThrowIfCancellationRequested();

                            if (!await PostTelegramMessageAsync(token, chatId, parts[i], ct).ConfigureAwait(false))
                            {
                                allOk = false;
                                break;
                            }

                            if (parts.Count > 1 && i < parts.Count - 1)
                                await Task.Delay(300, ct).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        allOk = false;
                        SafePrint("Telegram: envoi annule ou expire (timeout).");
                    }
                    catch (Exception ex)
                    {
                        allOk = false;
                        SafePrint("Telegram send error: " + ex.Message);
                    }
                    finally
                    {
                        if (gateEntered)
                            telegramSendGate.Release();

                        Interlocked.Decrement(ref telegramInFlightCount);

                        if (onComplete != null)
                        {
                            bool result = allOk;
                            RunOnUiThread(() => onComplete(result));
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Interlocked.Decrement(ref telegramInFlightCount);
                if (EnableDebugMode) Print("Telegram setup error: " + ex.Message);
                if (onComplete != null) RunOnUiThread(() => onComplete(false));
            }
        }
        #endregion
    }
}