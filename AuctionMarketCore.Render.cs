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
        #region Dashboard
        private void UpdateDashboard()
        {
            try
            {
                if (dashboardFont == null)
                {
                    Print("VP_Dashboard: dashboardFont est null, abandon.");
                    return;
                }

                long fingerprint = ComputeDashboardFingerprint();
                if (fingerprint == lastDashboardFingerprint) return;
                lastDashboardFingerprint = fingerprint;

                const int maxLen = 44;
                const string separator = "  ------------------------------------------";

                var sb = dashboardBuilder;
                sb.Length = 0;
                AppendWrappedLine(sb, "  AMC PRO - ", string.Format("AUCTION MARKET CORE ({0} M{1})", instrumentRoot, VolumetricTimeframe), maxLen);
                sb.AppendLine(separator);
                
                // SECTION 1 : CONTEXTE MARCHÉ
                AppendWrappedLine(sb, "  Régime : ", currentDayType, maxLen);
                AppendWrappedLine(sb, "  POC    : ", string.Format("{0} | VAH: {1} | VAL: {2}",
                    Instrument.MasterInstrument.FormatPrice(pocPrice),
                    Instrument.MasterInstrument.FormatPrice(vahPrice),
                    Instrument.MasterInstrument.FormatPrice(valPrice)), maxLen);
                
                if (UseVwapFilter && currentVwapPrice != 0)
                    AppendWrappedLine(sb, "  VWAP   : ", Instrument.MasterInstrument.FormatPrice(currentVwapPrice), maxLen);

                string deltaStr = (currentCumulativeDelta > 0 ? "+" : "") + currentCumulativeDelta.ToString("N0");
                AppendWrappedLine(sb, "  Volume : ", string.Format("{0:N0} | Delta: {1}", sessionTotalVolume, deltaStr), maxLen);

                if (UseRegimeFilter && regimeAtr != null && regimeAtr.IsValidDataPoint(0))
                    AppendWrappedLine(sb, "  ATRReg : ", string.Format("{0:N1}t", regimeAtr[0] / TickSize), maxLen);

                sb.AppendLine(separator);

                // SECTION 2 : FLUX D'ORDRES (ORDER FLOW)
                if (!string.IsNullOrEmpty(allSignalsText))
                    AppendWrappedLine(sb, "  Multi  : ", CleanTextForDashboard(allSignalsText), maxLen);
                if (bidAskDataMissing)
                    AppendWrappedLine(sb, "  !DATA  : ", "Pas de donnees BidAsk - order flow inactif", maxLen);
                AppendWrappedLine(sb, "  Abs.   : ", CleanTextForDashboard(currentAbsorptionStatus), maxLen);
                AppendWrappedLine(sb, "  Iceb.  : ", CleanTextForDashboard(currentIcebergStatus), maxLen);
                AppendWrappedLine(sb, "  Imbal. : ", CleanTextForDashboard(currentImbalanceStatus), maxLen);
                if (EnableDeltaFlip)
                    AppendWrappedLine(sb, "  Flip   : ", CleanTextForDashboard(currentDeltaFlipStatus), maxLen);
                if (EnableCumDeltaDivergence)
                    AppendWrappedLine(sb, "  DivCD  : ", CleanTextForDashboard(currentCumDeltaDivStatus), maxLen);
                if (EnableFinishedAuction)
                    AppendWrappedLine(sb, "  Enchère: ", CleanTextForDashboard(currentFinishedAuctionStatus), maxLen);
                if (EnableExhaustion)
                    AppendWrappedLine(sb, "  Épuis. : ", CleanTextForDashboard(currentExhaustionStatus), maxLen);

                if (runtimeErrorCount > 0)
                    AppendWrappedLine(sb, "  ERREURS: ", string.Format("{0} ({1})", runtimeErrorCount, CleanTextForDashboard(lastRuntimeError)), maxLen);
                if (profileOutOfRangeCount > 0)
                    AppendWrappedLine(sb, "  Profil : ", string.Format("{0} barres hors bornes", profileOutOfRangeCount), maxLen);
                if (imbalanceZones.Count > 0)
                    AppendWrappedLine(sb, "  Zones  : ", string.Format("{0} stacked actives", imbalanceZones.Count), maxLen);

                sb.AppendLine(separator);

                // SECTION 3 : SCORE & SETUP CONFLUENCE
                AppendWrappedLine(sb, "  Interp : ", CleanTextForDashboard(currentInterpretation), maxLen);
                if (confluenceScore > 0)
                {
                    string confVal = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0}/{1} (p{2:0.0}) {3}", confluenceScore, maxConfluenceScore, confluenceWeighted,
                        CleanTextForDashboard(confluenceDetails));
                    AppendWrappedLine(sb, "  Conf.  : ", confVal, maxLen);
                }

                string sigBadge = currentSignal;
                if (sigBadge.Contains("BUY")) sigBadge = "🟢 " + sigBadge;
                else if (sigBadge.Contains("SELL")) sigBadge = "🔴 " + sigBadge;
                else sigBadge = "⚪ " + sigBadge;
                AppendWrappedLine(sb, "  Signal : ", CleanTextForDashboard(sigBadge), maxLen);

                if (currentSignal.StartsWith("Pas de trade") && !string.IsNullOrEmpty(lastTriggeredSignal) && lastTriggeredSignal != "Aucun")
                {
                    string lastVal = string.Format("{0} ({1:HH:mm:ss})", CleanTextForDashboard(lastTriggeredSignal), lastSignalTime);
                    AppendWrappedLine(sb, "  Dernier: ", lastVal, maxLen);
                }

                if (EnableHtfFilter)
                    AppendWrappedLine(sb, "  HTF    : ", CleanTextForDashboard(htfBiasText), maxLen);

                if (EnableRiskManagement && lastStopPrice != 0)
                {
                    string sizeStr = lastPositionSize > 0 ? string.Format("x{0}", lastPositionSize) : "(En attente)";
                    string planStr = string.Format("SL {0} TP {1} {2}",
                        Instrument.MasterInstrument.FormatPrice(lastStopPrice),
                        Instrument.MasterInstrument.FormatPrice(lastTarget1),
                        sizeStr);
                    AppendWrappedLine(sb, "  Plan   : ", planStr, maxLen);
                }

                string alertStr = lastAlertTime == DateTime.MinValue ? "Aucune" : lastAlertTime.ToString("HH:mm:ss");
                AppendWrappedLine(sb, "  Alerte : ", string.Format("{0} | Signaux: {1}", alertStr, signalsSentCount), maxLen);

                if (EnableTradeJournal && globalStats.Total > 0)
                    AppendWrappedLine(sb, "  Stats  : ", string.Format("{0}t {1:F0}%WR {2:F1}R",
                        globalStats.Total, globalStats.WinRate, globalStats.SumR), maxLen);

                if (EnableSniperEngine)
                {
                    sb.AppendLine(separator);
                    sb.Append(BuildSniperDashboardBlock(maxLen));
                }

                if (EnableClosedVolumeProfile && vpManager != null)
                {
                    sb.AppendLine(separator);
                    sb.Append(BuildVolumeProfileDashboardBlock(maxLen));
                }

                sb.AppendLine(separator);
                sb.Append(BuildMarketIntelligenceStatusLine(maxLen));

                string dashboardText = sb.ToString();
                if (dashboardText == lastDashboardText) return;
                lastDashboardText = dashboardText;

                Draw.TextFixed(this, "VP_Dashboard", dashboardText, TextPosition.TopLeft,
                              Brushes.White, dashboardFont, Brushes.Transparent, Brushes.DimGray, 90);
            }
            catch (Exception ex)
            {
                Print("VP_Dashboard Draw Error: " + ex.GetType().Name + " - " + ex.Message + "\n" + ex.StackTrace);
            }
        }

        private string BuildVolumeProfileDashboardBlock(int maxLen = 44)
        {
            if (!EnableClosedVolumeProfile || vpManager == null) return "";
            var sb = new StringBuilder(256);
            sb.AppendLine("  VOLUME PROFILE — RÉFÉRENCES CLÔTURÉES");

            if (vpManager.PrevDay != null && vpManager.PrevDay.Valid)
            {
                AppendWrappedLine(sb, "  JOUR PRÉ: ", string.Format(CultureInfo.InvariantCulture,
                    "VAH {0} | POC {1} | VAL {2}",
                    Instrument.MasterInstrument.FormatPrice(vpManager.PrevDay.Vah),
                    Instrument.MasterInstrument.FormatPrice(vpManager.PrevDay.Poc),
                    Instrument.MasterInstrument.FormatPrice(vpManager.PrevDay.Val)), maxLen);
            }
            else
            {
                AppendWrappedLine(sb, "  JOUR PRÉ: ", "En attente clôture session", maxLen);
            }

            if (vpManager.PrevWeek != null && vpManager.PrevWeek.Valid)
            {
                AppendWrappedLine(sb, "  SEM PRÉ : ", string.Format(CultureInfo.InvariantCulture,
                    "VAH {0} | POC {1} | VAL {2}",
                    Instrument.MasterInstrument.FormatPrice(vpManager.PrevWeek.Vah),
                    Instrument.MasterInstrument.FormatPrice(vpManager.PrevWeek.Poc),
                    Instrument.MasterInstrument.FormatPrice(vpManager.PrevWeek.Val)), maxLen);

                if (vpManager.PrevWeek.Nodes != null && vpManager.PrevWeek.Nodes.Count > 0)
                {
                    for (int i = 0; i < Math.Min(2, vpManager.PrevWeek.Nodes.Count); i++)
                    {
                        var n = vpManager.PrevWeek.Nodes[i];
                        string nodeLabel = string.Format("  S.{0,-4} : ", n.NodeType);
                        string nodeVal = string.Format(CultureInfo.InvariantCulture,
                            "{0}-{1} (Pic {2})",
                            Instrument.MasterInstrument.FormatPrice(n.ZoneLow),
                            Instrument.MasterInstrument.FormatPrice(n.ZoneHigh),
                            Instrument.MasterInstrument.FormatPrice(n.PeakPrice));
                        AppendWrappedLine(sb, nodeLabel, nodeVal, maxLen);
                    }
                }
            }

            if (vpManager.PrevMonth != null && vpManager.PrevMonth.Valid)
            {
                AppendWrappedLine(sb, "  MOIS PRÉ: ", string.Format(CultureInfo.InvariantCulture,
                    "VAH {0} | POC {1} | VAL {2}",
                    Instrument.MasterInstrument.FormatPrice(vpManager.PrevMonth.Vah),
                    Instrument.MasterInstrument.FormatPrice(vpManager.PrevMonth.Poc),
                    Instrument.MasterInstrument.FormatPrice(vpManager.PrevMonth.Val)), maxLen);
            }

            if (currentVpContext != null && currentVpContext.IsValid)
            {
                AppendWrappedLine(sb, "  VP LOC  : ", CleanTextForDashboard(currentVpContext.LocationSummary), maxLen);
                if (currentVpContext.ConfluenceCount >= 2 && !string.IsNullOrEmpty(currentVpContext.ConfluenceType))
                {
                    AppendWrappedLine(sb, "  VP CONF : ", CleanTextForDashboard(currentVpContext.ConfluenceType), maxLen);
                }
            }

            return sb.ToString();
        }

        // Empreinte 64 bits des valeurs reellement affichees : aucune allocation,
        // aucune mise en forme. Si elle est inchangee, le dashboard l'est aussi.
        private long ComputeDashboardFingerprint()
        {
            unchecked
            {
                long h = 1469598103934665603L;
                h = Mix(h, pocPrice); h = Mix(h, vahPrice); h = Mix(h, valPrice);
                h = Mix(h, sessionTotalVolume); h = Mix(h, currentCumulativeDelta);
                h = Mix(h, currentVwapPrice); h = Mix(h, lastStopPrice); h = Mix(h, lastTarget1);
                h = Mix(h, lastPositionSize); h = Mix(h, signalsSentCount);
                h = Mix(h, confluenceScore); h = Mix(h, maxConfluenceScore); h = Mix(h, confluenceWeighted);
                h = Mix(h, imbalanceZones.Count); h = Mix(h, globalStats.Total);
                h = Mix(h, globalStats.WinRate); h = Mix(h, globalStats.SumR);
                h = Mix(h, bidAskDataMissing ? 1 : 0);
                h = Mix(h, lastAlertTime.Ticks); h = Mix(h, lastSignalTime.Ticks);
                h = MixStr(h, currentDayType); h = MixStr(h, currentAbsorptionStatus);
                h = MixStr(h, currentIcebergStatus); h = MixStr(h, currentImbalanceStatus);
                h = MixStr(h, currentDeltaFlipStatus); h = MixStr(h, currentCumDeltaDivStatus);
                h = MixStr(h, currentFinishedAuctionStatus); h = MixStr(h, currentExhaustionStatus);
                h = MixStr(h, allSignalsText); h = MixStr(h, htfBiasText);
                h = MixStr(h, currentInterpretation); h = MixStr(h, confluenceDetails);
                h = MixStr(h, currentSignal); h = MixStr(h, lastTriggeredSignal);
                if (UseRegimeFilter && regimeAtr != null && regimeAtr.IsValidDataPoint(0)) h = Mix(h, regimeAtr[0]);
                if (EnableSniperEngine) h = Mix(h, SniperDashboardFingerprint());
                if (EnableClosedVolumeProfile && vpManager != null)
                {
                    if (vpManager.PrevDay != null) h = Mix(h, vpManager.PrevDay.Poc);
                    if (vpManager.PrevWeek != null) h = Mix(h, vpManager.PrevWeek.Poc);
                    if (vpManager.PrevMonth != null) h = Mix(h, vpManager.PrevMonth.Poc);
                    if (currentVpContext != null)
                    {
                        h = MixStr(h, currentVpContext.LocationSummary);
                        h = MixStr(h, currentVpContext.ConfluenceType);
                    }
                }
                h = Mix(h, MarketIntelligenceFingerprint());
                return h;
            }
        }

        private static long Mix(long h, double v)
        {
            unchecked { return (h ^ BitConverter.DoubleToInt64Bits(v)) * 1099511628211L; }
        }

        private static long Mix(long h, long v)
        {
            unchecked { return (h ^ v) * 1099511628211L; }
        }

        private static long MixStr(long h, string s)
        {
            unchecked { return (h ^ (s == null ? 0 : s.GetHashCode())) * 1099511628211L; }
        }

        private void AppendWrappedLine(StringBuilder sb, string prefix, string text, int maxLineLength = 44)
        {
            if (string.IsNullOrEmpty(text))
            {
                sb.AppendLine(prefix.TrimEnd());
                return;
            }

            if (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0)
                text = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');

            string fullLine = prefix + text;
            if (fullLine.Length <= maxLineLength)
            {
                sb.AppendLine(fullLine);
                return;
            }

            int indentSize = Math.Min(prefix.Length, 11);
            string indent = new string(' ', indentSize);
            string remainingText = text;
            bool isFirst = true;

            while (remainingText.Length > 0)
            {
                int currentPrefixLen = isFirst ? prefix.Length : indentSize;
                int availableSpace = maxLineLength - currentPrefixLen;
                if (availableSpace <= 8) availableSpace = Math.Max(15, maxLineLength - 4);

                if (remainingText.Length <= availableSpace)
                {
                    sb.AppendLine((isFirst ? prefix : indent) + remainingText);
                    break;
                }

                int splitIdx = -1;

                // Si le caractère exactement à availableSpace est un espace, couper ici
                if (availableSpace < remainingText.Length && remainingText[availableSpace] == ' ')
                {
                    splitIdx = availableSpace;
                }
                else
                {
                    int maxSearch = Math.Min(availableSpace - 1, remainingText.Length - 1);
                    for (int i = maxSearch; i > 0; i--)
                    {
                        char c = remainingText[i];
                        if (c == ' ')
                        {
                            splitIdx = i;
                            break;
                        }
                        if (c == '+' || c == ',' || c == ')' || c == ']' || c == '|' || c == ';')
                        {
                            splitIdx = i + 1;
                            break;
                        }
                    }
                }

                if (splitIdx <= 0)
                    splitIdx = availableSpace;

                string lineSegment = remainingText.Substring(0, splitIdx).TrimEnd();
                sb.AppendLine((isFirst ? prefix : indent) + lineSegment);

                remainingText = remainingText.Substring(splitIdx).TrimStart();
                isFirst = false;
            }
        }

        private string CleanTextForDashboard(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // Les libellés de statut se répètent tick après tick : on mémorise le résultat.
            string cached;
            if (cleanTextCache.TryGetValue(text, out cached)) return cached;
            if (cleanTextCache.Count > 512) cleanTextCache.Clear();

            // Itération par point de code (gère les paires de substitution pour emojis > U+FFFF)
            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                // Sauter les paires de substitution (emojis au-dessus de U+FFFF, ex. U+1F600)
                if (char.IsSurrogate(c))
                {
                    if (i + 1 < text.Length && char.IsSurrogatePair(c, text[i + 1]))
                        i++; // ignorer aussi le demi-substitut bas
                    continue;
                }

                int code = (int)c;

                // Sélecteurs de variation (U+FE00-FE0F) — modificateurs d'emoji
                if (code >= 0xFE00 && code <= 0xFE0F) continue;
                // ZWJ (U+200D), ZWNJ (U+200C), ZWSP (U+200B) — composeurs d'emoji
                if (code == 0x200D || code == 0x200C || code == 0x200B) continue;
                // BOM / ZWNBSP (U+FEFF)
                if (code == 0xFEFF) continue;

                // Filtrage de tous les blocs d'emojis et symboles pictographiques
                if (IsEmojiOrSymbolCodePoint(code)) continue;

                sb.Append(c);
            }

            // Nettoyage post-filtrage : espaces insécables → espace normal
            string result = sb.ToString().Replace(' ', ' ');

            // Replie les suites d'espaces en un seul
            var collapsed = new StringBuilder(result.Length);
            bool lastWasSpace = false;
            foreach (char ch in result)
            {
                if (ch == ' ')
                {
                    if (!lastWasSpace) collapsed.Append(' ');
                    lastWasSpace = true;
                }
                else
                {
                    collapsed.Append(ch);
                    lastWasSpace = false;
                }
            }
            string cleaned = collapsed.ToString().Trim();
            cleanTextCache[text] = cleaned;
            return cleaned;
        }

        private static bool IsEmojiOrSymbolCodePoint(int code)
        {
            return (code >= 0x2600 && code <= 0x26FF)   // Miscellaneous symbols (☀ ☂ ☎ ☑ etc.)
                || (code >= 0x2700 && code <= 0x27BF)   // Dingbats (✂ ✈ ✉ etc.)
                || (code >= 0x2B00 && code <= 0x2BFF)   // Misc symbols and arrows (⬅ ⬆ ⬇ etc.)
                || (code >= 0x1F000 && code <= 0x1F0FF) // Mahjong, playing cards
                || (code >= 0x1F100 && code <= 0x1F1FF) // Enclosed alphanumeric supplement + flags
                || (code >= 0x1F200 && code <= 0x1F2FF) // Enclosed ideographic supplement
                || (code >= 0x1F300 && code <= 0x1F9FF) // Emoticons, pictographs, transport, food, activities
                || (code >= 0x1FA00 && code <= 0x1FAFF) // Symbols and pictographs extended-A
                || (code >= 0x2190 && code <= 0x21FF)   // Arrows
                || (code >= 0x2300 && code <= 0x23FF)   // Miscellaneous technical
                || (code >= 0x25A0 && code <= 0x25FF)   // Geometric shapes (■ □ ▲ etc.)
                || (code >= 0x2B50 && code <= 0x2B55)   // Stars
                || (code >= 0x00A9 && code <= 0x00AE)   // Copyright / Registered
                || (code == 0x2122)                     // Trade mark sign
                || (code == 0x2139)                     // Information source
                || (code >= 0x2B05 && code <= 0x2B07);  // Left/right/up arrows
        }
        #endregion

        #region Level Lines Drawing
        private void DrawLevelLines()
        {
            try
            {
                // Ne rien redessiner tant que les niveaux n'ont pas bougé.
                if (pocPrice == lastDrawnPoc && vahPrice == lastDrawnVah && valPrice == lastDrawnVal)
                    return;
                lastDrawnPoc = pocPrice; lastDrawnVah = vahPrice; lastDrawnVal = valPrice;

                Brush pocBrush = Brushes.Orange;
                Brush vahValBrush = Brushes.White;

                Draw.HorizontalLine(this, "VP_POC_Line", pocPrice, pocBrush, DashStyleHelper.Solid, 2);
                Draw.HorizontalLine(this, "VP_VAH_Line", vahPrice, vahValBrush, DashStyleHelper.Dash, 1);
                Draw.HorizontalLine(this, "VP_VAL_Line", valPrice, vahValBrush, DashStyleHelper.Dash, 1);

                string pocText = string.Format("POC  {0}", Instrument.MasterInstrument.FormatPrice(pocPrice));
                string vahText = string.Format("VAH  {0}", Instrument.MasterInstrument.FormatPrice(vahPrice));
                string valText = string.Format("VAL  {0}", Instrument.MasterInstrument.FormatPrice(valPrice));

                NinjaTrader.Gui.Tools.SimpleFont labelFont = levelLabelFont
                    ?? (levelLabelFont = new NinjaTrader.Gui.Tools.SimpleFont("Consolas", 10) { Bold = true });

                int textBarOffset = -3;
                // Clamp offset to prevent exceeding drawing limits
                textBarOffset = Math.Max(Math.Min(textBarOffset, CurrentBars[0]), -CurrentBars[0]);

                Draw.Text(this, "VP_POC_Text", true, pocText, textBarOffset, pocPrice, 0, pocBrush, labelFont, System.Windows.TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                Draw.Text(this, "VP_VAH_Text", true, vahText, textBarOffset, vahPrice, 0, vahValBrush, labelFont, System.Windows.TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                Draw.Text(this, "VP_VAL_Text", true, valText, textBarOffset, valPrice, 0, vahValBrush, labelFont, System.Windows.TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
            }
            catch (Exception ex)
            {
                Print("VP_LevelLines Draw Error: " + ex.GetType().Name + " - " + ex.Message);
            }
        }
        #endregion
    }
}
