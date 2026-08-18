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

        #endregion

        #region État Interne Volume Profile V2

        private VolumeProfileManager vpManager;
        private VolumeProfileContext currentVpContext;
        private string resolvedVpDbPath;

        private void VolumeProfileSetDefaults()
        {
            EnableClosedVolumeProfile = true;
            EnableSQLiteVolumeProfileHistory = true;
            VolumeProfileLevelToleranceTicks = 3;
            VolumeProfileNodeToleranceTicks = 4;
            VolumeProfileDbPath = "";
        }

        private void VolumeProfileDataLoaded()
        {
            if (!EnableClosedVolumeProfile) return;

            try
            {
                resolvedVpDbPath = ResolveVolumeProfileDbPath();

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
            }
            catch (Exception ex)
            {
                if (EnableDebugMode)
                    SafePrint("VolumeProfileOnEvaluatedBar Erreur : " + ex.Message);
            }
        }

        #endregion
    }
}
