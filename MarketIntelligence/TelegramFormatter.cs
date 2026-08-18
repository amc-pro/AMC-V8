#region Using declarations
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence
{
    /// <summary>
    /// Formateur de messages Telegram. Produit un rendu propre et structure.
    /// avec echappement systematique du contenu variable.
    /// </summary>
    public sealed class TelegramFormatter
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public const int MaxExtraLines = 4;

        private static string Esc(string v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            return v.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private static string FormatDistance(double ticks)
        {
            if (ticks < 0) return "n/d";
            return Math.Round(ticks).ToString("0", Inv) + " ticks";
        }

        private static string Zone(string label)
        {
            return string.IsNullOrEmpty(label) ? "?" : Esc(label);
        }

        public string FormatReport(MarketSnapshot s)
        {
            if (s == null) return null;

            var sb = new StringBuilder(1000);
            sb.AppendLine("🏛️ <b>MARKET INTELLIGENCE │ RAPPORT H4</b>");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("📊 <b>Instrument :</b> <b>" + Esc(s.Instrument) + "</b>");
            sb.AppendLine("🕒 <b>Horodatage :</b> <code>" + s.Time.ToString("yyyy-MM-dd HH:mm", Inv) + " (" + Zone(s.TimeZoneLabel) + ")</code>");
            sb.AppendLine("");
            sb.AppendLine("🎯 <b>BIAIS DIRECTIONNEL :</b> " + MiText.BiasEmoji(s.Bias) + " <b>" + MiText.Bias(s.Bias) + "</b>");
            sb.AppendLine("⚡ <b>Indice de Confiance :</b> <code>" + s.Confidence + "/100</code>");
            if (!string.IsNullOrEmpty(s.BiasReason))
                sb.AppendLine("📝 <i>" + Esc(s.BiasReason) + "</i>");
            sb.AppendLine("");
            sb.AppendLine("📈 <b>MATRICE DE TENDANCE</b> (Alignement " + s.AlignmentPercent + "%)");
            sb.AppendLine("▫️ <b>H4  :</b> " + MiText.Trend(s.TrendH4));
            sb.AppendLine("▫️ <b>H1  :</b> " + MiText.Trend(s.TrendH1));
            sb.AppendLine("▫️ <b>M15 :</b> " + MiText.Trend(s.TrendM15));
            sb.AppendLine("▫️ <b>M5  :</b> " + MiText.Trend(s.TrendM5));
            sb.AppendLine("");
            sb.AppendLine("🏗️ <b>STRUCTURE DE MARCHÉ (SMC)</b>");
            sb.AppendLine("▫️ <b>BOS H4   :</b> <code>" + MiText.Structure(s.LastBosH4) + "</code><i>" + MiText.Age(s.BarsSinceBosH4, "H4") + "</i>");
            sb.AppendLine("▫️ <b>CHOCH H4 :</b> <code>" + MiText.Structure(s.LastChochH4) + "</code><i>" + MiText.Age(s.BarsSinceChochH4, "H4") + "</i>");
            sb.AppendLine("▫️ <b>BOS H1   :</b> <code>" + MiText.Structure(s.LastBos) + "</code><i>" + MiText.Age(s.BarsSinceBos, "H1") + "</i>");
            sb.AppendLine("▫️ <b>CHOCH H1 :</b> <code>" + MiText.Structure(s.LastChoch) + "</code><i>" + MiText.Age(s.BarsSinceChoch, "H1") + "</i>");
            sb.AppendLine("");
            sb.AppendLine("🎯 <b>LIQUIDITÉS & ORDER BLOCKS</b>");
            sb.AppendLine("▫️ <b>Cible Principale :</b> <code>" + MiText.Target(s.Target) + "</code>");
            sb.AppendLine("▫️ <b>Distance Buy-Side :</b> <code>" + FormatDistance(s.BuySideDistanceTicks) + "</code>");
            sb.AppendLine("▫️ <b>Distance Sell-Side :</b> <code>" + FormatDistance(s.SellSideDistanceTicks) + "</code>");
            sb.AppendLine("▫️ <b>Order Block (H1)  :</b> <code>" + MiText.OrderBlock(s.OrderBlockKind, s.OrderBlockState) + "</code><i>" + MiText.Age(s.BarsSinceOrderBlock, "H1") + "</i>");

            if (s.ExtraLines != null && s.ExtraLines.Count > 0)
            {
                sb.AppendLine("");
                sb.AppendLine("🌊 <b>FLUX & DONNÉES ADDITIONNELLES</b>");
                int shown = Math.Min(MaxExtraLines, s.ExtraLines.Count);
                for (int i = 0; i < shown; i++) sb.AppendLine("▫️ <i>" + Esc(s.ExtraLines[i]) + "</i>");
                if (s.ExtraLines.Count > shown)
                    sb.AppendLine("<i>(+" + (s.ExtraLines.Count - shown) + " ligne(s) masquée(s))</i>");
            }

            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("💡 <i>Qualité contexte (Trend 30 / Structure 20 / Liq 15 / OB 15 / Vol 10 / Mom 10)</i>");

            return sb.ToString();
        }

        public string FormatUpdate(MarketSnapshot previous, MarketSnapshot current, MiAnalysisResult analysis)
        {
            if (current == null || analysis == null || analysis.Changes.Count == 0) return null;

            var sb = new StringBuilder(800);
            sb.AppendLine("🚨 <b>MARKET INTELLIGENCE │ CHANGEMENT MAJEUR</b>");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("📊 <b>Instrument :</b> <b>" + Esc(current.Instrument) + "</b>");

            // Type d'evenement (on prend le plus important)
            MiChange mainChange = null;
            foreach (var c in analysis.Changes)
            {
                if (c.Level == MiEventLevel.Critical) { mainChange = c; break; }
            }
            if (mainChange == null)
            {
                foreach (var c in analysis.Changes)
                {
                    if (c.Level == MiEventLevel.Important) { mainChange = c; break; }
                }
            }
            if (mainChange == null) mainChange = analysis.Changes[0];

            if (!string.IsNullOrEmpty(mainChange.From) && mainChange.From != mainChange.To)
                sb.AppendLine("⚡ <b>Événement Clé :</b> <code>" + Esc(mainChange.Label) + " (" + Esc(mainChange.From) + " ➔ " + Esc(mainChange.To) + ")</code>");
            else
                sb.AppendLine("⚡ <b>Événement Clé :</b> <code>" + Esc(mainChange.Label) + "</code>");

            sb.AppendLine("");
            sb.AppendLine("🎯 <b>Nouveau Biais :</b> " + MiText.BiasEmoji(current.Bias) + " <b>" + MiText.Bias(current.Bias) + "</b>");
            if (previous != null)
                sb.AppendLine("📊 <b>Confiance :</b> <code>" + previous.Confidence + " ➔ " + current.Confidence + "/100</code> │ <b>Impact :</b> <code>" + analysis.TotalScore + "/100</code>");
            else
                sb.AppendLine("📊 <b>Confiance :</b> <code>" + current.Confidence + "/100</code> │ <b>Impact :</b> <code>" + analysis.TotalScore + "/100</code>");

            if (!string.IsNullOrEmpty(current.BiasReason))
                sb.AppendLine("📝 <i>" + Esc(current.BiasReason) + "</i>");

            sb.AppendLine("");
            sb.AppendLine("🔍 <b>Facteurs Déclencheurs :</b>");
            foreach (var c in analysis.Changes)
            {
                if (!string.IsNullOrEmpty(c.From) && c.From != c.To)
                    sb.AppendLine("  ▫️ <b>" + Esc(c.Label) + " :</b> <code>" + Esc(c.From) + " ➔ " + Esc(c.To) + "</code>");
                else
                    sb.AppendLine("  ▫️ <b>" + Esc(c.Label) + "</b>");
            }

            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("🕒 <i>" + current.Time.ToString("yyyy-MM-dd HH:mm:ss", Inv) + " (" + Zone(current.TimeZoneLabel) + ")</i>");

            return sb.ToString();
        }
    }
}
