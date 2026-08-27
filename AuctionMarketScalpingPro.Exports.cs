#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using NinjaTrader.NinjaScript.Indicators.VolumeProfilePro;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    /// <summary>
    /// Exposition en lecture seule de l'état du dernier signal émis par le moteur Sniper
    /// pour la consommation par une stratégie NinjaTrader ou le pont MT5 (Fichier & Socket TCP).
    /// Les signaux sont publiés dans EmitAlert() (après validation des portes et filtres).
    /// </summary>
    public partial class AuctionMarketScalpingPro
    {
        #region SNIPER - Section 12 : exports publics (V7.8)

        private string exportSignal = "";
        private double exportScore;
        private double exportScoreRaw;
        private bool exportIsBuy;
        private bool exportIsSell;
        private double exportStop;
        private double exportTarget1;
        private double exportTarget2;
        private double exportRr;
        private string exportGrade = "";
        private double exportEntry;
        private double exportEntryAtEmission;
        private DateTime exportSignalTime = DateTime.MinValue;
        private int exportSignalBar = -1;
        private long exportSequence;
        private static long exportSequenceCounter;
        private int exportPendingCount;
        private double exportN1, exportN2, exportN3, exportN4, exportPenalty;
        private bool exportHtfAligned;

        #region Propriétés du Pont MT5 (Méthode 1 Fichier & Méthode 2 Socket TCP)
        [Display(Name = "Activer Pont MT5 (Fichier JSON)", Order = 1, GroupName = "14. Pont MT5 Auto-Trading")]
        public bool EnableFileBridge { get; set; }

        [Display(Name = "Activer Pont MT5 (Socket TCP <1ms)", Order = 2, GroupName = "14. Pont MT5 Auto-Trading")]
        public bool EnableTcpBridge { get; set; }

        [Display(Name = "Port TCP Serveur Localhost", Order = 3, GroupName = "14. Pont MT5 Auto-Trading")]
        public int TcpBridgePort { get; set; }

        [Display(Name = "Répertoire Cible MT5 (Vide = Autodetect Common Files)", Order = 4, GroupName = "14. Pont MT5 Auto-Trading")]
        public string FileBridgeDirectory { get; set; }

        [Display(Name = "Nom du Fichier Signal", Order = 5, GroupName = "14. Pont MT5 Auto-Trading")]
        public string FileBridgeFileName { get; set; }
        #endregion

        // Pont TCP partagé au niveau du processus NinjaTrader. Chaque instance
        // s'enregistre auprès du serveur mais aucune instance ne peut arrêter le
        // serveur utilisé par les autres instruments.
        private static AmcTcpBridgeServer tcpBridgeServer;
        private static readonly object tcpBridgeSync = new object();
        private static readonly List<AuctionMarketScalpingPro> tcpBridgeConsumers = new List<AuctionMarketScalpingPro>();
        private static int tcpBridgePortInUse = -1;

        /// <summary>Libelle du signal principal (ex: "NPOC_ABSORPTION_REVERSAL"). Vide tant qu'aucun signal n'a ete emis.</summary>
        [Browsable(false)]
        [XmlIgnore]
        public string CurrentSignal { get { return exportSignal; } }

        /// <summary>Grade du signal (A+, A, B, C).</summary>
        [Browsable(false)]
        [XmlIgnore]
        public string SignalGrade { get { return exportGrade; } }

        /// <summary>Score final du signal (/100).</summary>
        [Browsable(false)]
        [XmlIgnore]
        public double CurrentScore { get { return exportScore; } }

        /// <summary>Score brut (avant gates) — base du grade et du journal shadow.</summary>
        [Browsable(false)]
        [XmlIgnore]
        public double ScoreRaw { get { return exportScoreRaw; } }

        /// <summary>Vrai si le dernier signal emis est acheteur.</summary>
        [Browsable(false)]
        [XmlIgnore]
        public bool IsBuySignal { get { return exportIsBuy; } }

        /// <summary>Vrai si le dernier signal emis est vendeur.</summary>
        [Browsable(false)]
        [XmlIgnore]
        public bool IsSellSignal { get { return exportIsSell; } }

        /// <summary>Prix de stop calcule (structurel + ATR).</summary>
        [Browsable(false)]
        [XmlIgnore]
        public double LastStopPrice { get { return exportStop; } }

        /// <summary>Premiere cible (structurelle).</summary>
        [Browsable(false)]
        [XmlIgnore]
        public double LastTarget1 { get { return exportTarget1; } }

        /// <summary>Seconde cible.</summary>
        [Browsable(false)]
        [XmlIgnore]
        public double LastTarget2 { get { return exportTarget2; } }

        /// <summary>Taille de position recommandee par le moteur de risque de l'AMC Pro.</summary>
        [Browsable(false)]
        [XmlIgnore]
        public int LastPositionSize { get { return lastPositionSize; } }

        /// <summary>Grade du signal (A+, A, B, C).</summary>
        [Browsable(false)]
        [XmlIgnore]
        public string Grade { get { return exportGrade ?? ""; } }

        /// <summary>Nombre de candidats encore en attente dans le buffer best-of-window.</summary>
        [Browsable(false)]
        [XmlIgnore]
        public int PendingCandidateCount { get { return exportPendingCount; } }

        /// <summary>
        /// Sequenceur d'emission : s'incremente de +1 a CHAQUE nouveau signal.
        /// C'est la cle unique d'identification d'un signal pour SniperValidationStrategy.
        /// </summary>
        [Browsable(false)]
        [XmlIgnore]
        public long SignalSequence { get { return exportSequence; } }

        /// <summary>Ratio Risk/Reward du dernier signal emis.</summary>
        [Browsable(false)]
        [XmlIgnore]
        public double LastRiskReward { get { return exportRr; } }

        /// <summary>Prix d'entree ideal du setup.</summary>
        [Browsable(false)]
        [XmlIgnore]
        public double TargetEntryPrice { get { return exportEntry; } }

        /// <summary>Prix au moment exact de l'emission (inclut la derive intrabar).</summary>
        [Browsable(false)]
        [XmlIgnore]
        public double EntryAtEmission { get { return exportEntryAtEmission; } }

        /// <summary>Horodatage du dernier signal emis.</summary>
        [Browsable(false)]
        [XmlIgnore]
        public DateTime LastSignalTime { get { return exportSignalTime; } }

        /// <summary>Index de la barre a laquelle le dernier signal a ete emet.</summary>
        [Browsable(false)]
        [XmlIgnore]
        public int LastSignalBar { get { return exportSignalBar; } }

        [Browsable(false)] [XmlIgnore] public double ExportN1 { get { return exportN1; } }
        [Browsable(false)] [XmlIgnore] public double ExportN2 { get { return exportN2; } }
        [Browsable(false)] [XmlIgnore] public double ExportN3 { get { return exportN3; } }
        [Browsable(false)] [XmlIgnore] public double ExportN4 { get { return exportN4; } }
        [Browsable(false)] [XmlIgnore] public double ExportPenalty { get { return exportPenalty; } }
        [Browsable(false)] [XmlIgnore] public bool ExportHtfAligned { get { return exportHtfAligned; } }
        [Browsable(false)] [XmlIgnore] public VolumeProfileContext LastVolumeProfileContext { get { return currentVpContext; } }

        /// <summary>Statut interne du moteur ("ok", "pret", message d'erreur...).</summary>
        [Browsable(false)]
        [XmlIgnore]
        public string EngineStatus { get { return sniperLastStatus ?? ""; } }

        /// <summary>Reinitialisation des exports. Appele depuis State.SetDefaults.</summary>
        private void SniperResetExports()
        {
            EnableFileBridge = true;
            EnableTcpBridge = true;
            TcpBridgePort = 18888;
            FileBridgeDirectory = "";
            FileBridgeFileName = "amc_trade_signal.json";

            exportSignal = "";
            exportGrade = "";
            exportScore = 0;
            exportScoreRaw = 0;
            exportIsBuy = false;
            exportIsSell = false;
            exportStop = 0;
            exportTarget1 = 0;
            exportTarget2 = 0;
            exportRr = 0;
            exportEntry = 0;
            exportEntryAtEmission = 0;
            exportSignalTime = DateTime.MinValue;
            exportSignalBar = -1;
            exportSequence = 0;
            exportPendingCount = 0;
            exportN1 = exportN2 = exportN3 = exportN4 = exportPenalty = 0;
            exportHtfAligned = false;
        }

        /// <summary>Initialisation du serveur TCP pour le pont MT5 ultra-rapide.</summary>
        private void InitTcpBridge()
        {
            if (!EnableTcpBridge) return;
            int requestedPort = TcpBridgePort > 0 ? TcpBridgePort : 18888;
            lock (tcpBridgeSync)
            {
                if (tcpBridgePortInUse > 0 && tcpBridgePortInUse != requestedPort)
                {
                    SafePrint(string.Format(CultureInfo.InvariantCulture,
                        "AMC TCP REJECT: port {0} demandé alors que le pont partagé utilise déjà {1}.",
                        requestedPort, tcpBridgePortInUse));
                    return;
                }

                if (!tcpBridgeConsumers.Contains(this))
                    tcpBridgeConsumers.Add(this);

                if (tcpBridgeServer == null)
                {
                    tcpBridgeServer = new AmcTcpBridgeServer(
                        msg => SafePrintStatic(msg),
                        ackMsg => DispatchMt5Ack(ackMsg));
                    if (tcpBridgeServer.Start(requestedPort))
                        tcpBridgePortInUse = requestedPort;
                    else
                    {
                        tcpBridgeServer.Dispose();
                        tcpBridgeServer = null;
                        tcpBridgeConsumers.Remove(this);
                    }
                }
            }
        }

        private static void SafePrintStatic(string message)
        {
            try { System.Diagnostics.Debug.WriteLine(message); } catch { }
        }

        private static void DispatchMt5Ack(string ackJson)
        {
            AuctionMarketScalpingPro[] consumers;
            lock (tcpBridgeSync) consumers = tcpBridgeConsumers.ToArray();
            for (int i = 0; i < consumers.Length; i++)
            {
                try { consumers[i].ProcessMt5Ack(ackJson); } catch { }
            }
        }

        /// <summary>Arrêt propre du pont partagé : l'instance se désabonne uniquement.</summary>
        private void StopTcpBridge()
        {
            lock (tcpBridgeSync)
            {
                tcpBridgeConsumers.Remove(this);
                if (tcpBridgeConsumers.Count == 0 && tcpBridgeServer != null)
                {
                    tcpBridgeServer.Stop();
                    tcpBridgeServer = null;
                    tcpBridgePortInUse = -1;
                }
            }
        }

        /// <summary>Traitement des messages d'acquittement (ACK) reçus depuis MT5.</summary>
        private void ProcessMt5Ack(string ackJson)
        {
            if (string.IsNullOrEmpty(ackJson)) return;
            try
            {
                SafePrint("📥 [MT5 ACK REÇU] " + ackJson.Trim());
            }
            catch (Exception ex)
            {
                SafePrint("Erreur lecture ACK MT5 : " + ex.Message);
            }
        }

        /// <summary>Rafraichissement par barre des exports non lies a une emission.</summary>
        private void SniperSyncExports()
        {
            exportPendingCount = pendingCandidates.Count;
        }

        /// <summary>Publication de l'etat d'un candidat emis. Appele depuis EmitAlert().</summary>
        private void SniperPublishExports(Candidate c)
        {
            if (c == null) return;

            exportSignal = c.Name ?? "";
            exportGrade = c.Grade;
            exportScore = c.Score;
            exportScoreRaw = c.ScoreRaw;
            exportIsBuy = c.IsBuy;
            exportIsSell = !c.IsBuy;
            exportStop = c.Stop;
            exportTarget1 = c.Target1;
            exportTarget2 = c.Target2;
            exportRr = c.Rr;
            exportEntry = c.Entry;
            exportEntryAtEmission = c.EntryAtEmission > 0 ? c.EntryAtEmission : c.Entry;
            exportSignalTime = c.Time;
            exportSignalBar = c.BarIdx;
            exportN1 = c.N1;
            exportN2 = c.N2;
            exportN3 = c.N3;
            exportN4 = c.N4;
            exportPenalty = c.Penalty;
            exportHtfAligned = c.HtfAligned;
            exportPendingCount = pendingCandidates.Count;

            // Séquence globale au processus : évite les collisions entre NQ/MNQ/MGC/MCL/etc.
            exportSequence = Interlocked.Increment(ref exportSequenceCounter);

            ExportSignalToBridge(c);
        }

        /// <summary>
        /// Exportation double canal : Fichier JSON atomique + Diffusion TCP Socket (<1ms).
        /// </summary>
        private void ExportSignalToBridge(Candidate c)
        {
            if (c == null) return;

            // ZERO-TRUST P0: un signal sans sizing valide n'est jamais transformé
            // en taille 1 par le contrat JSON.
            if (lastPositionSize <= 0 || double.IsNaN(c.Entry) || double.IsInfinity(c.Entry) || double.IsNaN(c.Stop) || double.IsInfinity(c.Stop) ||
                double.IsNaN(c.Target1) || double.IsInfinity(c.Target1) || double.IsNaN(c.Target2) || double.IsInfinity(c.Target2) || c.Entry <= 0 ||
                c.Stop <= 0 || c.Target1 <= 0 || c.Target2 <= 0)
            {
                SafePrint("AMC EXPORT REJECT: contrat financier invalide ou position_size <= 0.");
                return;
            }

            bool geometryOk = c.IsBuy
                ? (c.Stop < c.Entry && c.Entry < c.Target1 && c.Target1 <= c.Target2)
                : (c.Target2 <= c.Target1 && c.Target1 < c.Entry && c.Entry < c.Stop);
            if (!geometryOk)
            {
                SafePrint("AMC EXPORT REJECT: géométrie Entry/SL/TP invalide.");
                return;
            }

            string rawSymbol = Instrument != null ? Instrument.MasterInstrument.Name : "UNKNOWN";
            DateTime signalUtc = c.Time.ToUniversalTime();
            long timestampEpoch = new DateTimeOffset(signalUtc).ToUnixTimeSeconds();

            StringBuilder sb = new StringBuilder(768);
            sb.AppendLine("{");
            sb.AppendLine("  \"protocol_version\": 2,");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"sequence\": {0},", exportSequence));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"timestamp_utc\": \"{0:yyyy-MM-ddTHH:mm:ss.fffZ}\",", signalUtc));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"timestamp_epoch\": {0},", timestampEpoch));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"instrument\": \"{0}\",", EscapeJsonString(rawSymbol)));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"signal_name\": \"{0}\",", EscapeJsonString(c.Name ?? "")));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"action\": \"{0}\",", c.IsBuy ? "BUY" : "SELL"));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"is_buy\": {0},", c.IsBuy ? "true" : "false"));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"entry\": {0:F5},", c.EntryAtEmission > 0 ? c.EntryAtEmission : c.Entry));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"sl\": {0:F5},", c.Stop));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"tp1\": {0:F5},", c.Target1));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"tp2\": {0:F5},", c.Target2));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"score\": {0:F1},", c.Score));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"grade\": \"{0}\",", EscapeJsonString(c.Grade ?? "")));
            sb.AppendLine("  \"risk_valid\": true,");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"position_size\": {0},", lastPositionSize));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"rr\": {0:F2},", c.Rr));

            // V7.9 Volume Profile Context
            if (c.VolumeProfile != null && c.VolumeProfile.IsValid)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"vp_location\": \"{0}\",", EscapeJsonString(c.VolumeProfile.LocationSummary)));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"vp_closest_ref\": \"{0}\",", EscapeJsonString(c.VolumeProfile.ClosestReferenceName)));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"vp_closest_dist_ticks\": {0:F1},", c.VolumeProfile.DistanceToClosestReference));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"vp_confluence_count\": {0},", c.VolumeProfile.ConfluenceCount));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"vp_confluence_type\": \"{0}\"", EscapeJsonString(c.VolumeProfile.ConfluenceType)));
            }
            else
            {
                sb.AppendLine("  \"vp_confluence_count\": 0");
            }
            sb.AppendLine("}");

            string json = sb.ToString();

            // Canal 1 : Diffusion Socket TCP (<1ms)
            if (EnableTcpBridge && tcpBridgeServer != null)
            {
                tcpBridgeServer.Broadcast(json);
            }

            // Canal 2 : Export Fichier Atomique (Méthode 1 / Fallback)
            if (EnableFileBridge)
            {
                try
                {
                    string dirPath = FileBridgeDirectory;
                    if (string.IsNullOrWhiteSpace(dirPath))
                    {
                        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        string metaQuotesCommon = Path.Combine(appData, "MetaQuotes", "Terminal", "Common", "Files");
                        string metaQuotesBase = Path.Combine(appData, "MetaQuotes");

                        if (Directory.Exists(metaQuotesBase) || Directory.Exists(metaQuotesCommon))
                            dirPath = metaQuotesCommon;
                        else
                            dirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AMC_Signals");
                    }

                    if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

                    string fileName;
                    if (string.IsNullOrWhiteSpace(FileBridgeFileName) || FileBridgeFileName == "amc_trade_signal.json")
                        fileName = string.Format("amc_trade_signal_{0}.json", rawSymbol);
                    else
                        fileName = FileBridgeFileName.Replace("{SYMBOL}", rawSymbol);

                    string filePath = Path.Combine(dirPath, fileName);

                    string tmpPath = filePath + ".tmp";
                    File.WriteAllText(tmpPath, json, Encoding.UTF8);
                    if (File.Exists(filePath))
                        File.Replace(tmpPath, filePath, null);
                    else
                        File.Move(tmpPath, filePath);

                    SafePrint("MT5 Pont (Fichier JSON) : Signal #" + exportSequence + " (" + (c.IsBuy ? "BUY" : "SELL") + " " + rawSymbol + ") exporté vers " + filePath);
                }
                catch (Exception ex)
                {
                    SafePrint("MT5 Pont Erreur Export Fichier : " + ex.Message);
                }
            }
        }

        private static string EscapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            StringBuilder esc = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                switch (ch)
                {
                    case '"':  esc.Append("\\\""); break;
                    case '\\': esc.Append("\\\\"); break;
                    case '\n': esc.Append("\\n"); break;
                    case '\r': esc.Append("\\r"); break;
                    case '\t': esc.Append("\\t"); break;
                    default:
                        if (ch < ' ') esc.AppendFormat("\\u{0:X4}", (int)ch);
                        else esc.Append(ch);
                        break;
                }
            }
            return esc.ToString();
        }

        #endregion

        #region SERVEUR TCP ASYNCHRONE (IPC PONT MT5 < 1MS)

        /// <summary>
        /// Serveur TCP asynchrone non-bloquant pour diffusion en temps réel (<1ms)
        /// vers MetaTrader 5 sans goulot d'étranglement disque.
        /// </summary>
        internal sealed class AmcTcpBridgeServer : IDisposable
        {
            private TcpListener listener;
            private CancellationTokenSource cts;
            private readonly List<TcpClient> clients = new List<TcpClient>();
            private readonly object syncLock = new object();
            private readonly Action<string> onLog;
            private readonly Action<string> onAckReceived;
            private bool isRunning;

            public AmcTcpBridgeServer(Action<string> logCallback, Action<string> ackCallback = null)
            {
                onLog = logCallback;
                onAckReceived = ackCallback;
            }

            public bool Start(int port)
            {
                if (isRunning) return true;
                try
                {
                    cts = new CancellationTokenSource();
                    listener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
                    listener.Start();
                    isRunning = true;
                    if (onLog != null) onLog(string.Format(CultureInfo.InvariantCulture, "🚀 Serveur TCP Pont MT5 actif sur 127.0.0.1:{0} (Latence < 1ms)", port));
                    Task.Run(() => AcceptClientsLoop(cts.Token));
                    return true;
                }
                catch (Exception ex)
                {
                    isRunning = false;
                    try { if (cts != null) { cts.Dispose(); cts = null; } } catch { }
                    try { if (listener != null) { listener.Stop(); listener = null; } } catch { }
                    if (onLog != null) onLog("Erreur démarrage Serveur TCP : " + ex.Message);
                    return false;
                }
            }

            private async Task AcceptClientsLoop(CancellationToken token)
            {
                while (!token.IsCancellationRequested && isRunning)
                {
                    try
                    {
                        TcpClient client = await listener.AcceptTcpClientAsync();
                        lock (syncLock) { clients.Add(client); }
                        if (onLog != null) onLog("🟢 Client MT5 EA connecté au Pont TCP !");
                        Task.Run(() => HandleClientAsync(client, token));
                    }
                    catch
                    {
                        if (token.IsCancellationRequested || !isRunning) break;
                    }
                }
            }

            private async Task HandleClientAsync(TcpClient client, CancellationToken token)
            {
                byte[] buffer = new byte[2048];
                try
                {
                    using (NetworkStream stream = client.GetStream())
                    {
                        while (!token.IsCancellationRequested && client.Connected)
                        {
                            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                            if (bytesRead <= 0) break;
                            string msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            if (onAckReceived != null) onAckReceived(msg);
                        }
                    }
                }
                catch
                {
                }
                finally
                {
                    lock (syncLock) { clients.Remove(client); }
                    try { client.Close(); } catch { }
                    if (onLog != null) onLog("🔴 Client MT5 déconnecté du Pont TCP.");
                }
            }

            public void Broadcast(string json)
            {
                if (!isRunning || string.IsNullOrEmpty(json)) return;

                byte[] data = Encoding.UTF8.GetBytes(json + "\n<END>\n");
                TcpClient[] activeClients;
                lock (syncLock)
                {
                    activeClients = clients.ToArray();
                }

                List<TcpClient> deadClients = null;
                for (int i = 0; i < activeClients.Length; i++)
                {
                    TcpClient client = activeClients[i];
                    if (client != null && client.Connected)
                    {
                        try
                        {
                            NetworkStream stream = client.GetStream();
                            if (stream != null && stream.CanWrite)
                            {
                                if (stream.CanTimeout) stream.WriteTimeout = 500;
                                stream.Write(data, 0, data.Length);
                                stream.Flush();
                            }
                        }
                        catch
                        {
                            try { client.Close(); } catch { }
                            if (deadClients == null) deadClients = new List<TcpClient>();
                            deadClients.Add(client);
                        }
                    }
                    else
                    {
                        if (deadClients == null) deadClients = new List<TcpClient>();
                        deadClients.Add(client);
                    }
                }

                if (deadClients != null)
                {
                    lock (syncLock)
                    {
                        for (int i = 0; i < deadClients.Count; i++)
                        {
                            clients.Remove(deadClients[i]);
                        }
                    }
                }
            }

            public void Stop()
            {
                if (!isRunning) return;
                isRunning = false;
                try
                {
                    if (cts != null) { cts.Cancel(); cts.Dispose(); cts = null; }
                    lock (syncLock)
                    {
                        for (int i = 0; i < clients.Count; i++)
                        {
                            try { clients[i].Close(); } catch { }
                        }
                        clients.Clear();
                    }
                    if (listener != null) { listener.Stop(); listener = null; }
                    if (onLog != null) onLog("Serveur TCP Pont MT5 arrêté.");
                }
                catch (Exception ex)
                {
                    if (onLog != null) onLog("Erreur arrêt Serveur TCP : " + ex.Message);
                }
            }

            public void Dispose()
            {
                Stop();
            }
        }

        #endregion
    }
}
