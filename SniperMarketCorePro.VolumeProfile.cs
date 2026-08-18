#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.BarsTypes;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Indicators.VolumeProfilePro;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    /// <summary>
    /// Intégration du module Volume Profile V2 (Historique, Déterministe & Persistant SQLite)
    /// dans l'indicateur SniperMarketCorePro.
    /// </summary>
    public partial class SniperMarketCorePro
    {
        #region Paramètres Volume Profile V2 (Closed References)

        [NinjaScriptProperty]
        [Display(Name = "Activer Volume Profile V2 (Closed)", Order = 1, GroupName = "15. Volume Profile V2 (Closed References)")]
        public bool EnableClosedVolumeProfile { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Activer Persistance SQLite", Order = 2, GroupName = "15. Volume Profile V2 (Closed References)")]
        public bool EnableSQLiteVolumeProfileHistory { get; set; }

        [NinjaScriptProperty]
        [Range(1, 20)]
        [Display(Name = "Tolérance Niveaux (Ticks)", Order = 3, GroupName = "15. Volume Profile V2 (Closed References)")]
        public int VolumeProfileLevelToleranceTicks { get; set; }

        [NinjaScriptProperty]
        [Range(1, 30)]
        [Display(Name = "Tolérance Nodes HVN/LVN (Ticks)", Order = 4, GroupName = "15. Volume Profile V2 (Closed References)")]
        public int VolumeProfileNodeToleranceTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Chemin Base SQLite (Vide = Auto Documents/NT8/db)", Order = 5, GroupName = "15. Volume Profile V2 (Closed References)")]
        public string VolumeProfileDbPath { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Activer Alertes Telegram VP", Order = 6, GroupName = "15. Volume Profile V2 (Closed References)")]
        public bool EnableVolumeProfileTelegramAlerts { get; set; }

        [NinjaScriptProperty]
        [Range(1, 5)]
        [Display(Name = "Confluence Min pour Alerte Telegram", Order = 7, GroupName = "15. Volume Profile V2 (Closed References)")]
        public int VolumeProfileMinConfluenceAlert { get; set; }

        [NinjaScriptProperty]
        [Range(1, 120)]
        [Display(Name = "Cooldown Alerte Niveau (Minutes)", Order = 8, GroupName = "15. Volume Profile V2 (Closed References)")]
        public int VolumeProfileAlertCooldownMinutes { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Alerter sur 1er Test de Niveau/Zone", Order = 9, GroupName = "15. Volume Profile V2 (Closed References)")]
        public bool VolumeProfileAlertOnFirstTouch { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Alerter sur Rejet Confirmé", Order = 10, GroupName = "15. Volume Profile V2 (Closed References)")]
        public bool VolumeProfileAlertOnRejection { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Alerter sur Entrée en LVN (Vide)", Order = 11, GroupName = "15. Volume Profile V2 (Closed References)")]
        public bool VolumeProfileAlertOnLvnEntry { get; set; }

        #endregion

        #region État Interne Volume Profile V2

        private VolumeProfileManager vpManager;
        private VolumeProfileContext currentVpContext;
        private string resolvedVpDbPath;
        private readonly Dictionary<string, DateTime> vpLastAlertTimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        private void VolumeProfileSetDefaults()
        {
            EnableClosedVolumeProfile = true;
            EnableSQLiteVolumeProfileHistory = true;
            VolumeProfileLevelToleranceTicks = 3;
            VolumeProfileNodeToleranceTicks = 4;
            VolumeProfileDbPath = "";
            EnableVolumeProfileTelegramAlerts = true;
            VolumeProfileMinConfluenceAlert = 2;
            VolumeProfileAlertCooldownMinutes = 15;
            VolumeProfileAlertOnFirstTouch = true;
            VolumeProfileAlertOnRejection = true;
            VolumeProfileAlertOnLvnEntry = true;
        }

        private void VolumeProfileDataLoaded()
        {
            if (!EnableClosedVolumeProfile) return;

            try
            {
                resolvedVpDbPath = ResolveVolumeProfileDbPath();
                vpLastAlertTimes.Clear();

                string sym = Instrument != null && Instrument.MasterInstrument != null ? Instrument.MasterInstrument.Name : "SYM";
                string exch = Instrument != null ? Instrument.Exchange.ToString() : "CME";
                string session = TradingHours != null ? TradingHours.Name : "RTH";

                vpManager = new VolumeProfileManager(
                    sym,
                    exch,
                    session,
                    TickSize,
                    ValueAreaPercent,
                    resolvedVpDbPath,
                    SafePrint);

                vpManager.Analyzer.LevelToleranceTicks = VolumeProfileLevelToleranceTicks;
                vpManager.Analyzer.NodeToleranceTicks = VolumeProfileNodeToleranceTicks;
                vpManager.Analyzer.ConfluenceToleranceTicks = VolumeProfileLevelToleranceTicks + 1;

                if (EnableSQLiteVolumeProfileHistory)
                {
                    vpManager.Initialize();
                }

                currentVpContext = new VolumeProfileContext();
            }
            catch (Exception ex)
            {
                SafePrint("VolumeProfile Init Erreur : " + ex.Message);
            }
        }

        private void VolumeProfileTerminated()
        {
            if (vpManager != null)
            {
                try
                {
                    vpManager.Dispose();
                }
                catch (Exception ex)
                {
                    SafePrint("VolumeProfile Terminated Erreur : " + ex.Message);
                }
                vpManager = null;
            }
            currentVpContext = null;
            vpLastAlertTimes.Clear();
        }

        private string ResolveVolumeProfileDbPath()
        {
            if (!string.IsNullOrEmpty(VolumeProfileDbPath))
                return VolumeProfileDbPath;

            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string ntDir = Path.Combine(docs, "NinjaTrader 8", "db");
                if (!Directory.Exists(ntDir))
                {
                    Directory.CreateDirectory(ntDir);
                }
                return Path.Combine(ntDir, "amc_volume_profile.db");
            }
            catch
            {
                return "amc_volume_profile.db";
            }
        }

        /// <summary>
        /// Traite l'ingestion volumétrique et construit le contexte Volume Profile pour la barre évaluée.
        /// </summary>
        private void VolumeProfileOnEvaluatedBar(VolumetricBarsType volType)
        {
            if (!EnableClosedVolumeProfile || vpManager == null || volType == null) return;

            try
            {
                if (evalBarIndex < 0 || evalBarIndex >= volType.Volumes.Length) return;

                VolumetricData vd = volType.Volumes[evalBarIndex];
                if (vd == null) return;

                int currentBar = CurrentBars[volumetricBarsIndex];
                int barsAgo = currentBar - evalBarIndex;
                if (barsAgo < 0 || barsAgo >= Lows[volumetricBarsIndex].Count) return;

                double barLow = Lows[volumetricBarsIndex][barsAgo];
                double barHigh = Highs[volumetricBarsIndex][barsAgo];
                double barClose = Closes[volumetricBarsIndex][barsAgo];
                double barOpen = Opens[volumetricBarsIndex][barsAgo];
                long barVol = vd.TotalVolume;
                double barDelta = vd.BarDelta;
                DateTime barTime = GetVolumetricTime();

                long lowTick = (long)Math.Round(barLow / TickSize);
                long highTick = (long)Math.Round(barHigh / TickSize);

                var tickList = new List<KeyValuePair<long, long>>((int)(highTick - lowTick + 1));
                for (long t = lowTick; t <= highTick; t++)
                {
                    double p = t * TickSize;
                    long v = vd.GetTotalVolumeForPrice(p);
                    if (v > 0)
                    {
                        tickList.Add(new KeyValuePair<long, long>(t, v));
                    }
                }

                // Ingestion dans le gestionnaire (transitions Day/Week/Month automatiques)
                vpManager.IngestVolumetricBar(
                    barTime.ToUniversalTime(),
                    barHigh, barLow, barClose, barOpen,
                    barVol, barDelta, tickList);

                // Extraction du contexte Volume Profile
                double currentAtr = regimeAtr != null && regimeAtr.IsValidDataPoint(0) ? regimeAtr[0] : (riskAtr != null && riskAtr.IsValidDataPoint(0) ? riskAtr[0] : TickSize * 4);
                currentVpContext = vpManager.GetContext(
                    snClose > 0 ? snClose : barClose,
                    barHigh, barLow, barClose, barDelta,
                    currentAtr, barTime.ToUniversalTime());

                // Évaluation & envoi des alertes Telegram sur niveaux Volume Profile (même canal que Market Intelligence)
                ProcessVolumeProfileTelegramAlerts(barClose, barHigh, barLow, barOpen, barDelta, barVol, barTime.ToUniversalTime(), currentVpContext);
            }
            catch (Exception ex)
            {
                if (EnableDebugMode)
                    SafePrint("VolumeProfileOnEvaluatedBar Erreur : " + ex.Message);
            }
        }

        #endregion

        #region Alertes Telegram Volume Profile (Même canal que Market Intelligence)

        private void ProcessVolumeProfileTelegramAlerts(
            double barClose,
            double barHigh,
            double barLow,
            double barOpen,
            double barDelta,
            long barVol,
            DateTime barTimeUtc,
            VolumeProfileContext ctx)
        {
            if (!EnableVolumeProfileTelegramAlerts || State != State.Realtime || ctx == null || !ctx.IsValid) return;

            string instName = Instrument != null && Instrument.MasterInstrument != null
                ? Instrument.MasterInstrument.Name
                : (Instrument != null ? Instrument.FullName : "FUTURES");

            // 1. Événement CONFLUENCE ou PREMIER TEST
            if (VolumeProfileAlertOnFirstTouch)
            {
                // A. Confluence majeure (ex: x2+)
                if (ctx.ConfluenceCount >= VolumeProfileMinConfluenceAlert && !string.IsNullOrEmpty(ctx.ConfluenceType))
                {
                    bool insideConf = barHigh >= ctx.ConfluenceZoneLow && barLow <= ctx.ConfluenceZoneHigh;
                    if (insideConf)
                    {
                        string confKey = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "CONF|{0:F2}_{1:F2}|{2}", ctx.ConfluenceZoneLow, ctx.ConfluenceZoneHigh, ctx.ConfluenceCount);
                        if (CanSendVolumeProfileAlert(confKey, barTimeUtc))
                        {
                            string msg = BuildVolumeProfileConfluenceTelegramAlert(
                                instName, ctx.ConfluenceType, ctx.ConfluenceCount,
                                ctx.ConfluenceZoneLow, ctx.ConfluenceZoneHigh,
                                barClose, barDelta, barVol, barTimeUtc);
                            SendTelegramMessage(msg, null, MiTelegramChannel);
                            RecordVolumeProfileAlert(confKey, barTimeUtc);
                            return; // Évite les doubles alertes sur la même barre
                        }
                    }
                }
                // B. Niveau institutionnel isolé (POC/VAH/VAL Jour/Sem/Mois)
                else if (ctx.DistanceToClosestReference <= VolumeProfileLevelToleranceTicks && !string.IsNullOrEmpty(ctx.ClosestReferenceName))
                {
                    string lvlKey = "LVL|" + ctx.ClosestReferenceName;
                    if (CanSendVolumeProfileAlert(lvlKey, barTimeUtc))
                    {
                        string msg = BuildVolumeProfileLevelTestTelegramAlert(
                            instName, ctx.ClosestReferenceName, ctx.ClosestReferencePrice,
                            barClose, barDelta, barVol, barTimeUtc);
                        SendTelegramMessage(msg, null, MiTelegramChannel);
                        RecordVolumeProfileAlert(lvlKey, barTimeUtc);
                        return;
                    }
                }
            }

            // 2. Événement REJET CONFIRMÉ
            if (VolumeProfileAlertOnRejection && !string.IsNullOrEmpty(ctx.ClosestReferenceName))
            {
                double refPrice = ctx.ClosestReferencePrice;
                double tolPrice = VolumeProfileLevelToleranceTicks * TickSize;

                bool isBullishRejection = (barLow <= refPrice && barClose > refPrice + tolPrice && barDelta > 0);
                bool isBearishRejection = (barHigh >= refPrice && barClose < refPrice - tolPrice && barDelta < 0);

                if (isBullishRejection || isBearishRejection)
                {
                    string rejKey = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "REJ|{0}|{1}", ctx.ClosestReferenceName, isBullishRejection ? "BULL" : "BEAR");
                    if (CanSendVolumeProfileAlert(rejKey, barTimeUtc))
                    {
                        string msg = BuildVolumeProfileRejectionTelegramAlert(
                            instName, ctx.ClosestReferenceName, refPrice,
                            isBullishRejection, barClose, barDelta, barVol, barTimeUtc);
                        SendTelegramMessage(msg, null, MiTelegramChannel);
                        RecordVolumeProfileAlert(rejKey, barTimeUtc);
                        return;
                    }
                }
            }

            // 3. Événement ENTRÉE DANS UN LVN (Low Volume Node)
            if (VolumeProfileAlertOnLvnEntry && ctx.ActiveNode != null && ctx.ActiveNode.NodeType == VolumeProfileNodeType.LVN)
            {
                if (ctx.ActiveNode.Contains(barClose))
                {
                    string lvnKey = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "LVN|{0:F2}_{1:F2}", ctx.ActiveNode.ZoneLow, ctx.ActiveNode.ZoneHigh);
                    if (CanSendVolumeProfileAlert(lvnKey, barTimeUtc))
                    {
                        string msg = BuildVolumeProfileLvnTelegramAlert(
                            instName, ctx.ActiveNode, barClose, barDelta, barVol, barTimeUtc);
                        SendTelegramMessage(msg, null, MiTelegramChannel);
                        RecordVolumeProfileAlert(lvnKey, barTimeUtc);
                    }
                }
            }
        }

        private bool CanSendVolumeProfileAlert(string alertKey, DateTime timeUtc)
        {
            if (string.IsNullOrEmpty(alertKey)) return false;
            DateTime lastTime;
            if (vpLastAlertTimes.TryGetValue(alertKey, out lastTime))
            {
                if ((timeUtc - lastTime).TotalMinutes < VolumeProfileAlertCooldownMinutes)
                    return false;
            }
            return true;
        }

        private void RecordVolumeProfileAlert(string alertKey, DateTime timeUtc)
        {
            if (string.IsNullOrEmpty(alertKey)) return;
            vpLastAlertTimes[alertKey] = timeUtc;
            if (vpLastAlertTimes.Count > 256)
            {
                var expired = new List<string>();
                foreach (var kv in vpLastAlertTimes)
                {
                    if ((timeUtc - kv.Value).TotalMinutes > VolumeProfileAlertCooldownMinutes * 2)
                        expired.Add(kv.Key);
                }
                foreach (var k in expired) vpLastAlertTimes.Remove(k);
            }
        }

        private string BuildVolumeProfileConfluenceTelegramAlert(
            string instName, string confType, int confCount,
            double zoneLow, double zoneHigh,
            double barClose, double barDelta, long barVol, DateTime timeUtc)
        {
            var sb = new System.Text.StringBuilder(600);
            sb.Append("🔔 <b>AMC PRO │ TEST CONFLUENCE INSTITUTIONNELLE</b>\n");
            sb.Append("🏢 <b>").Append(EscapeHtml(instName)).Append("</b> (M").Append(VolumetricTimeframe).Append(")\n");
            sb.Append("━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
            sb.Append("⚡ <b>Zone :</b> <code>").Append(EscapeHtml(confType)).Append("</code>\n");
            sb.Append("🎯 <b>Bornes Confluence :</b> <code>")
              .Append(Instrument.MasterInstrument.FormatPrice(zoneLow)).Append(" — ")
              .Append(Instrument.MasterInstrument.FormatPrice(zoneHigh)).Append("</code>\n");
            sb.Append("📍 <b>Prix actuel :</b> <code>").Append(Instrument.MasterInstrument.FormatPrice(barClose)).Append("</code>\n");
            sb.Append("📊 <b>Delta :</b> <code>").Append(barDelta > 0 ? "+" : "").Append(barDelta.ToString("N0", System.Globalization.CultureInfo.InvariantCulture))
              .Append("</code> │ <b>Volume :</b> <code>").Append(barVol.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)).Append("</code>\n");

            if (!string.IsNullOrEmpty(htfBiasText))
                sb.Append("🧭 <b>Régime HTF :</b> <code>").Append(EscapeHtml(htfBiasText)).Append("</code>\n");

            sb.Append("💡 <i>Surveiller l'absorption et la réaction du carnet d'ordres sur ce mur de liquidité.</i>\n");
            sb.Append("━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
            sb.Append("🕒 <i>").Append(timeUtc.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)).Append(" UTC</i>");
            return sb.ToString();
        }

        private string BuildVolumeProfileLevelTestTelegramAlert(
            string instName, string levelName, double levelPrice,
            double barClose, double barDelta, long barVol, DateTime timeUtc)
        {
            var sb = new System.Text.StringBuilder(600);
            sb.Append("📍 <b>AMC PRO │ TEST DE NIVEAU CLÉ</b>\n");
            sb.Append("🏢 <b>").Append(EscapeHtml(instName)).Append("</b> (M").Append(VolumetricTimeframe).Append(")\n");
            sb.Append("━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
            sb.Append("📌 <b>Niveau :</b> <code>").Append(EscapeHtml(levelName)).Append("</code>\n");
            sb.Append("🎯 <b>Prix Niveau :</b> <code>").Append(Instrument.MasterInstrument.FormatPrice(levelPrice)).Append("</code>\n");
            sb.Append("📍 <b>Prix Actuel :</b> <code>").Append(Instrument.MasterInstrument.FormatPrice(barClose)).Append("</code>\n");
            sb.Append("📊 <b>Delta :</b> <code>").Append(barDelta > 0 ? "+" : "").Append(barDelta.ToString("N0", System.Globalization.CultureInfo.InvariantCulture))
              .Append("</code> │ <b>Volume :</b> <code>").Append(barVol.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)).Append("</code>\n");

            if (!string.IsNullOrEmpty(htfBiasText))
                sb.Append("🧭 <b>Régime HTF :</b> <code>").Append(EscapeHtml(htfBiasText)).Append("</code>\n");

            sb.Append("💡 <i>Test de structure Volume Profile. Surveiller réaction/rejet.</i>\n");
            sb.Append("━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
            sb.Append("🕒 <i>").Append(timeUtc.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)).Append(" UTC</i>");
            return sb.ToString();
        }

        private string BuildVolumeProfileRejectionTelegramAlert(
            string instName, string levelName, double levelPrice,
            bool isBullishRejection, double barClose, double barDelta, long barVol, DateTime timeUtc)
        {
            var sb = new System.Text.StringBuilder(600);
            string icon = isBullishRejection ? "🟢" : "🔴";
            string dirLabel = isBullishRejection ? "REJET HAUSSIER (ACHETEURS EN FORCE)" : "REJET BAISSIER (VENDEURS EN FORCE)";

            sb.Append(icon).Append(" <b>AMC PRO │ REJET DE ZONE CONFIRMÉ</b>\n");
            sb.Append("🏢 <b>").Append(EscapeHtml(instName)).Append("</b> (M").Append(VolumetricTimeframe).Append(")\n");
            sb.Append("━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
            sb.Append("🛡️ <b>Niveau Rejeté :</b> <code>").Append(EscapeHtml(levelName)).Append(" (")
              .Append(Instrument.MasterInstrument.FormatPrice(levelPrice)).Append(")</code>\n");
            sb.Append("🎯 <b>Action :</b> <code>").Append(dirLabel).Append("</code>\n");
            sb.Append("📍 <b>Clôture :</b> <code>").Append(Instrument.MasterInstrument.FormatPrice(barClose)).Append("</code>\n");
            sb.Append("📊 <b>Delta Rejet :</b> <code>").Append(barDelta > 0 ? "+" : "").Append(barDelta.ToString("N0", System.Globalization.CultureInfo.InvariantCulture))
              .Append("</code> │ <b>Volume :</b> <code>").Append(barVol.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)).Append("</code>\n");

            double invPrice = isBullishRejection ? levelPrice - (3 * TickSize) : levelPrice + (3 * TickSize);
            sb.Append("🛑 <b>Invalidation Structurelle :</b> <code>").Append(Instrument.MasterInstrument.FormatPrice(invPrice)).Append("</code>\n");

            sb.Append("━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
            sb.Append("🕒 <i>").Append(timeUtc.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)).Append(" UTC</i>");
            return sb.ToString();
        }

        private string BuildVolumeProfileLvnTelegramAlert(
            string instName, VolumeProfileNode node,
            double barClose, double barDelta, long barVol, DateTime timeUtc)
        {
            var sb = new System.Text.StringBuilder(600);
            sb.Append("⚡ <b>AMC PRO │ ENTRÉE EN ZONE D'ACCÉLÉRATION (LVN)</b>\n");
            sb.Append("🏢 <b>").Append(EscapeHtml(instName)).Append("</b> (M").Append(VolumetricTimeframe).Append(")\n");
            sb.Append("━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
            sb.Append("🌪️ <b>Zone LVN :</b> <code>")
              .Append(Instrument.MasterInstrument.FormatPrice(node.ZoneLow)).Append(" — ")
              .Append(Instrument.MasterInstrument.FormatPrice(node.ZoneHigh)).Append("</code>\n");
            sb.Append("📍 <b>Pic Creux :</b> <code>").Append(Instrument.MasterInstrument.FormatPrice(node.PeakPrice)).Append("</code>\n");
            sb.Append("📍 <b>Prix Actuel :</b> <code>").Append(Instrument.MasterInstrument.FormatPrice(barClose)).Append("</code>\n");
            sb.Append("⚠️ <b>Avertissement :</b> <i>Faible liquidité historique. Traversée rapide ou rejet violent probable. Ne pas placer d'ordre passif au milieu du vide.</i>\n");
            sb.Append("━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
            sb.Append("🕒 <i>").Append(timeUtc.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)).Append(" UTC</i>");
            return sb.ToString();
        }

        #endregion
    }
}
