//+------------------------------------------------------------------+
//|                                        AMCPro_MT5_Receiver.mq5   |
//|                        Copyright 2026, AMC Pro Auto-Trading      |
//|                    Pont NinjaTrader 8 ➡️ MT5 (Socket TCP & JSON) |
//|                        Édition Prop Firm & Institutional V2.5    |
//+------------------------------------------------------------------+
#property copyright "Copyright 2026, AMC Pro Auto-Trading"
#property link      "https://github.com/volumeprofile/SniperMarketCorePro"
#property version   "2.50"
#property description "EA Récepteur MT5 Professionnel pour AMC Pro (SniperMarketCorePro NT8)"
#property description "Pont Hybride Ultra-Basse Latence : Socket TCP Localhost (<1ms) + Fallback Fichier JSON"
#property description "Gestion avancée du risque : Split TP1/TP2, Break-Even auto, Trailing Stop, Daily Max Loss Lockout & Circuit Breaker"

#include <Trade\Trade.mqh>

//+------------------------------------------------------------------+
//| ENUMERATIONS                                                     |
//+------------------------------------------------------------------+
enum ENUM_BRIDGE_MODE
  {
   BRIDGE_AUTO       = 1, // Hybride (Socket TCP < 1ms en priorité avec Fallback Fichier)
   BRIDGE_SOCKET_TCP = 2, // Socket TCP Localhost Exclusif (< 1ms)
   BRIDGE_FILE_JSON  = 3  // Fichier JSON Local Partagé (FILE_COMMON)
  };

enum ENUM_TP_TARGET
  {
   TP_TARGET_1     = 1, // TP1 Fixe Unique (Premier Objectif Structurel)
   TP_TARGET_2     = 2, // TP2 Fixe Unique (Second Objectif de Tendance)
   TP_TARGET_SPLIT = 3  // Split TP1/TP2 (Clôture partielle TP1 + Break-Even + TP2)
  };

enum ENUM_EXEC_MODE
  {
   EXEC_POINTS_OFFSET = 1, // Distance en points par rapport au prix MT5 courant (Recommandé CFD)
   EXEC_EXACT_PRICE   = 2  // Prix exacts transmis depuis NT8
  };

enum ENUM_LOT_MODE
  {
   LOT_RISK_PERCENT = 1, // % du Capital (Account Equity)
   LOT_FIXED        = 2  // Taille de Lot Fixe
  };

enum ENUM_POSITION_MODE
  {
   POS_MODE_ONE_PER_SYMBOL = 1, // 1 position max par symbole (Même sens: ignoré, Sens opposé: fermer + ouvrir)
   POS_MODE_ONE_PER_DIR    = 2, // 1 position max par direction (Même sens: ignoré, Sens opposé: autoriser hedge)
   POS_MODE_ALLOW_ALL      = 3  // Autoriser tous les signaux (Cumuler positions)
  };

//+------------------------------------------------------------------+
//| STRUCTURE DE GESTION DYNAMIQUE DES POSITIONS                     |
//+------------------------------------------------------------------+
struct SManagedPosition
  {
   ulong       ticket;
   long        sequence;
   string      symbol;
   bool        isBuy;
   double      openPrice;
   double      initialLot;
   double      tp1Price;
   double      tp2Price;
   double      slPrice;
   bool        tp1Hit;
   datetime    openTime;
  };

//+------------------------------------------------------------------+
//| PARAMETRES D'ENTREE (INPUTS)                                     |
//+------------------------------------------------------------------+
input group "=== 1. CONFIGURATION DU PONT NT8 (TCP & FICHIER) ==="
input ENUM_BRIDGE_MODE  InpBridgeMode            = BRIDGE_AUTO;            // Mode de communication Pont NT8
input string            InpTcpHost               = "127.0.0.1";            // Adresse IP Serveur NT8 (Localhost)
input int               InpTcpPort               = 18888;                  // Port TCP Serveur NT8
input string            InpBridgeFileName        = "amc_trade_signal.json"; // Nom du fichier signal JSON (Fallback)
input int               InpPollIntervalMs        = 100;                    // Fréquence de lecture (ms)
input int               InpMaxSignalAgeSec       = 120;                    // Âge maximum d'un signal (sec)

input group "=== 2. FILTRAGE ET VALIDATION DE SCORE ==="
input double            InpMinScore              = 35.0;                   // Score minimal exigé (/100) — aligné sur ScalpingPro
input bool              InpAllowGradeAPlus       = true;                   // Autoriser les signaux A+
input bool              InpAllowGradeA           = true;                   // Autoriser les signaux A
input bool              InpAllowGradeB           = true;                   // Autoriser les signaux B
input bool              InpAllowGradeC           = false;                  // Autoriser les signaux C

input group "=== 3. GESTION DU RISQUE & TAKE PROFIT ==="
input ENUM_POSITION_MODE InpPositionMode         = POS_MODE_ONE_PER_SYMBOL; // 1 position max par symbole (Reverse sur signal opposé)
input ENUM_TP_TARGET    InpTpTarget              = TP_TARGET_SPLIT;        // Mode TP (TP1, TP2 ou Split TP1/BE/TP2)
input ENUM_EXEC_MODE    InpExecutionMode         = EXEC_POINTS_OFFSET;     // Mode de calcul du SL/TP
input ENUM_LOT_MODE     InpLotMode               = LOT_RISK_PERCENT;       // Mode de dimensionnement des lots
input double            InpRiskPercent           = 1.0;                    // Risque % du Capital par trade
input double            InpFixedLot              = 0.10;                   // Lot Fixe si Mode Lot Fixe sélectionné
input double            InpMaxLot                = 10.0;                   // Lot Maximum autorisé
input double            InpMinLot                = 0.01;                   // Lot Minimum autorisé
input int               InpMaxSpreadPoints       = 50;                     // Spread maximum autorisé (points)
input ulong             InpSlippagePoints        = 30;                     // Glissement max (points)
input ulong             InpMagicNumber           = 888777;                 // Magic Number unique pour l'EA

input group "=== 3.1. SORTIES PARTIELLES, BREAK-EVEN & TRAILING ==="
input double            InpClosePercentTP1       = 50.0;                   // % du volume à fermer à TP1 (Mode Split)
input bool              InpEnableBreakEven       = true;                   // Déplacer le SL à Break-Even après TP1
input int               InpBreakEvenBufferPoints = 5;                      // Marge de sécurité au-delà de l'entrée (points)
input bool              InpEnableTrailing        = false;                  // Activer Trailing Stop dynamique après TP1
input int               InpTrailingDistancePoints= 50;                     // Distance de Trailing (points)
input int               InpTrailingStepPoints    = 10;                     // Pas d'incrément de Trailing (points)

input group "=== 3.2. SUITE RISK PROP FIRM & CIRCUIT BREAKER ==="
input bool              InpEnableDailyMaxLoss    = true;                   // Activer le Hard Lockout perte journalière
input double            InpDailyMaxLossPercent   = 2.5;                    // Perte journalière max (% de l'Equity de départ)
input double            InpDailyMaxLossCurrency  = 0.0;                    // Perte journalière max en devise (0.0 = % prioritaire)
input bool              InpEnableCircuitBreaker  = true;                   // Activer Circuit Breaker sur pertes consécutives
input int               InpMaxConsecutiveLosses  = 3;                      // Nombre de pertes d'affilée tolérées (0 = désactivé)
input int               InpCircuitBreakerPauseMin= 90;                     // Durée de la pause obligatoire (minutes)

input group "=== 4. MAPPING DES SYMBOLES (NT8 Futures ➡️ MT5 Broker) ==="
input bool              InpAutoChartSymbol       = false;                  // Prise d'ordre sur le symbole courant du graphique (Désactivé par défaut pour sécurité)
input string            InpSymbol_GC             = "XAUUSD";               // Symbole pour Or (GC/MGC)
input string            InpSymbol_NQ             = "USTECH";               // Symbole pour Nasdaq (NQ/MNQ)
input string            InpSymbol_ES             = "US500";                // Symbole pour S&P 500 (ES/MES)
input string            InpSymbol_CL             = "WTI";                  // Symbole pour Pétrole (CL/MCL)
input string            InpSymbol_6E             = "EURUSD";               // Symbole pour Euro Futures (6E)
input string            InpSymbol_6B             = "GBPUSD";               // Symbole pour GBP Futures (6B)
input string            InpSymbol_FDAX           = "GER40";                // Symbole pour DAX (FDAX)

input group "=== 5. JOURNALISATION ET LOGS CSV ==="
input bool              InpEnableExecutionLog    = true;                   // Activer journalisation CSV structurée (M3)
input string            InpLogFileName           = "amc_mt5_executions.csv"; // Nom du fichier journal CSV (FILE_COMMON)

//+------------------------------------------------------------------+
//| VARIABLES GLOBALES                                               |
//+------------------------------------------------------------------+
CTrade            ExtTrade;
long              ExtLastProcessedSequence  = -1;
string            ExtLastSignalInfo         = "Aucun signal reçu";
string            ExtLastExecStatus         = "En attente du premier signal...";
int               ExtTotalTradesExecuted    = 0;

SManagedPosition  ExtManagedPositions[];

// Variables Réseau Socket TCP (<1ms)
int               ExtTcpSocket              = INVALID_HANDLE;
datetime          ExtLastTcpConnectAttempt  = 0;
string            ExtTcpBuffer              = "";
bool              ExtTcpConnected           = false;

// Variables de gestion du risque journalier
datetime          ExtCurrentDay             = 0;
double            ExtDailyStartEquity       = 0.0;
bool              ExtDailyLockoutActive     = false;
datetime          ExtCircuitBreakerUntil    = 0;
int               ExtConsecutiveLossCount   = 0;
double            ExtDailyRealizedPl        = 0.0;
double            ExtDailyFloatingPl        = 0.0;
double            ExtDailyTotalPl           = 0.0;

//+------------------------------------------------------------------+
//| GESTION DU SOCKET TCP CLIENT (IPC ULTRA-BASSE LATENCE < 1MS)     |
//+------------------------------------------------------------------+
bool EnsureTcpConnected()
  {
   if(InpBridgeMode == BRIDGE_FILE_JSON) return false;

   if(ExtTcpSocket != INVALID_HANDLE && SocketIsConnected(ExtTcpSocket))
     {
      ExtTcpConnected = true;
      return true;
     }

   if(TimeCurrent() - ExtLastTcpConnectAttempt < 3)
      return false;

   ExtLastTcpConnectAttempt = TimeCurrent();

   if(ExtTcpSocket != INVALID_HANDLE)
     {
      SocketClose(ExtTcpSocket);
      ExtTcpSocket = INVALID_HANDLE;
     }

   ExtTcpSocket = SocketCreate();
   if(ExtTcpSocket == INVALID_HANDLE)
     {
      ExtTcpConnected = false;
      return false;
     }

   if(SocketConnect(ExtTcpSocket, InpTcpHost, InpTcpPort, 50))
     {
      ExtTcpConnected = true;
      PrintFormat("🟢 [PONT TCP] Connecté avec succès au serveur NinjaTrader 8 (%s:%d) - Latence < 1ms", InpTcpHost, InpTcpPort);
      return true;
     }
   else
     {
      SocketClose(ExtTcpSocket);
      ExtTcpSocket = INVALID_HANDLE;
      ExtTcpConnected = false;
      return false;
     }
  }

void CheckAndProcessTcpStream()
  {
   if(!EnsureTcpConnected()) return;

   uint readable = SocketIsReadable(ExtTcpSocket);
   if(readable > 0)
     {
      uchar data[];
      ArrayResize(data, (int)readable + 1);
      int bytesRead = SocketRead(ExtTcpSocket, data, (int)readable, 10);
      if(bytesRead > 0)
        {
         string chunk = CharArrayToString(data, 0, bytesRead, CP_UTF8);
         ExtTcpBuffer += chunk;

         int endIdx = StringFind(ExtTcpBuffer, "\n<END>\n");
         while(endIdx >= 0)
           {
            string jsonMsg = StringSubstr(ExtTcpBuffer, 0, endIdx);
            ExtTcpBuffer = StringSubstr(ExtTcpBuffer, endIdx + 7);

            StringTrimLeft(jsonMsg);
            StringTrimRight(jsonMsg);

            if(StringLen(jsonMsg) > 20)
              {
               long sequence = ExtractJsonLong(jsonMsg, "sequence");
               if(sequence > 0 && sequence > ExtLastProcessedSequence)
                 {
                  PrintFormat("⚡ [PONT TCP <1MS] Signal #%d reçu instantanément !", sequence);
                  ProcessSignalPayload(jsonMsg, sequence, true);
                 }
              }

            endIdx = StringFind(ExtTcpBuffer, "\n<END>\n");
           }
        }
      else if(bytesRead < 0)
        {
         PrintFormat("⚠️ [PONT TCP] Déconnexion du serveur NT8 (Code %d)", GetLastError());
         CloseTcpSocket();
        }
     }
  }

void SendAckToNt8(long seq, ulong ticket, const string sym, const string action, double price, double slippage, const string status)
  {
   if(ExtTcpSocket == INVALID_HANDLE || !SocketIsConnected(ExtTcpSocket)) return;

   string ackJson = StringFormat("{\"type\":\"ACK\",\"sequence\":%d,\"ticket\":%I64u,\"symbol\":\"%s\",\"action\":\"%s\",\"price\":%.5f,\"slippage_pts\":%.1f,\"status\":\"%s\"}\n",
                                 seq, ticket, sym, action, price, slippage, status);
   uchar data[];
   StringToCharArray(ackJson, data, 0, WHOLE_ARRAY, CP_UTF8);
   SocketSend(ExtTcpSocket, data, ArraySize(data) - 1);
  }

void CloseTcpSocket()
  {
   if(ExtTcpSocket != INVALID_HANDLE)
     {
      SocketClose(ExtTcpSocket);
      ExtTcpSocket = INVALID_HANDLE;
      ExtTcpConnected = false;
     }
  }

//+------------------------------------------------------------------+
//| FONCTIONS DE PARSING JSON SIMPLIFIE NATIVE                       |
//+------------------------------------------------------------------+
string ExtractJsonString(const string json, const string key)
  {
   string searchPattern = "\"" + key + "\":";
   int pos = StringFind(json, searchPattern);
   if(pos < 0) return "";

   pos += StringLen(searchPattern);
   while(pos < StringLen(json) &&
         (StringGetCharacter(json, pos) == ' ' || StringGetCharacter(json, pos) == '\t'))
      pos++;

   if(pos >= StringLen(json)) return "";

   // ZERO-TRUST JSON string scanner : ne s'arrête pas sur un guillemet échappé.
   if(StringGetCharacter(json, pos) == '"')
     {
      pos++;
      string out = "";
      bool escaped = false;
      for(int i = pos; i < StringLen(json); i++)
        {
         ushort ch = StringGetCharacter(json, i);
         if(escaped)
           {
            if(ch == '"' || ch == '\\' || ch == '/')
               out += ShortToString((short)ch);
            else if(ch == 'n') out += "\n";
            else if(ch == 'r') out += "\r";
            else if(ch == 't') out += "\t";
            else if(ch == 'b') out += "\b";
            else if(ch == 'f') out += "\f";
            else
               out += ShortToString((short)ch);
            escaped = false;
            continue;
           }
         if(ch == '\\')
           {
            escaped = true;
            continue;
           }
         if(ch == '"')
            return out;
         out += ShortToString((short)ch);
        }
      return "";
     }

   int endPos1 = StringFind(json, ",", pos);
   int endPos2 = StringFind(json, "}", pos);
   int endPos = endPos1;
   if(endPos < 0 || (endPos2 >= 0 && endPos2 < endPos)) endPos = endPos2;
   if(endPos > pos)
     {
      string val = StringSubstr(json, pos, endPos - pos);
      StringTrimLeft(val);
      StringTrimRight(val);
      return val;
     }
   return "";
  }

long ExtractJsonLong(const string json, const string key)
  {
   string strVal = ExtractJsonString(json, key);
   return (strVal != "") ? StringToInteger(strVal) : -1;
  }

double ExtractJsonDouble(const string json, const string key)
  {
   string strVal = ExtractJsonString(json, key);
   return (strVal != "") ? StringToDouble(strVal) : 0.0;
  }

bool ExtractJsonBool(const string json, const string key)
  {
   string strVal = ExtractJsonString(json, key);
   return (strVal == "true" || strVal == "1");
  }

//+------------------------------------------------------------------+
//| JOURNALISATION STRUCTUREE CSV (POST-MORTEM)                      |
//+------------------------------------------------------------------+
void LogExecutionToCsv(long sequence, const string timeStr, const string action,
                       const string symbol, double lot, double price, double sl,
                       double tp, double score, const string grade, const string sigName,
                       const string status, uint retcode, const string details)
  {
   if(!InpEnableExecutionLog) return;

   bool fileExists = FileIsExist(InpLogFileName, FILE_COMMON);
   int handle = FileOpen(InpLogFileName, FILE_READ|FILE_WRITE|FILE_TXT|FILE_COMMON|FILE_SHARE_WRITE);
   if(handle == INVALID_HANDLE)
     {
      PrintFormat("⚠️ [LOG CSV] Impossible d'ouvrir le fichier journal '%s'", InpLogFileName);
      return;
     }

   FileSeek(handle, 0, SEEK_END);

   if(!fileExists || FileSize(handle) == 0)
     {
      string header = "Timestamp_Local;Timestamp_Signal;Sequence;Action;Symbol;Lot;Price;SL;TP;Score;Grade;SignalName;Status;RetCode;Details\r\n";
      FileWriteString(handle, header);
     }

   string line = StringFormat("%s;%s;%d;%s;%s;%.2f;%.5f;%.5f;%.5f;%.1f;%s;\"%s\";%s;%d;\"%s\"\r\n",
                              TimeToString(TimeLocal(), TIME_DATE|TIME_SECONDS),
                              timeStr,
                              sequence,
                              action,
                              symbol,
                              lot,
                              price,
                              sl,
                              tp,
                              score,
                              grade,
                              sigName,
                              status,
                              retcode,
                              details);

   FileWriteString(handle, line);
   FileClose(handle);
  }

//+------------------------------------------------------------------+
//| EXPERT INITIALIZATION FUNCTION                                   |
//+------------------------------------------------------------------+
int OnInit()
  {
   ExtTrade.SetExpertMagicNumber(InpMagicNumber);
   ExtTrade.SetMarginMode();
   ExtTrade.SetTypeFillingBySymbol(_Symbol);
   ExtTrade.SetDeviationInPoints(InpSlippagePoints);

   if(!EventSetMillisecondTimer(InpPollIntervalMs))
     {
      Print("Erreur : Impossible d'initialiser le timer MQL5 pour le pont.");
      return INIT_FAILED;
     }

   datetime now = TimeCurrent();
   ExtCurrentDay = (datetime)(now - (now % 86400));
   ExtDailyStartEquity = AccountInfoDouble(ACCOUNT_EQUITY);
   if(ExtDailyStartEquity <= 0) ExtDailyStartEquity = AccountInfoDouble(ACCOUNT_BALANCE);

   ScanExistingPositions();

   // Tentative de connexion initiale TCP
   if(InpBridgeMode != BRIDGE_FILE_JSON)
      EnsureTcpConnected();

   CreateDashboard();
   UpdateDashboard("INITIALISE", "Connecté au pont - En attente de signaux NT8...");
   PrintFormat("EA AMC Pro Receiver prêt (V2.50 Hybrid TCP/File). Mode Pont: %s | Mode TP: %s",
               (InpBridgeMode == BRIDGE_AUTO ? "Auto (TCP/File)" : (InpBridgeMode == BRIDGE_SOCKET_TCP ? "TCP Socket <1ms" : "Fichier JSON")),
               (InpTpTarget == TP_TARGET_SPLIT ? "Split TP1/BE/TP2" : StringFormat("Fixe TP%d", (int)InpTpTarget)));
   return INIT_SUCCEEDED;
  }

//+------------------------------------------------------------------+
//| EXPERT DEINITIALIZATION FUNCTION                                 |
//+------------------------------------------------------------------+
void OnDeinit(const int reason)
  {
   EventKillTimer();
   CloseTcpSocket();
   ObjectsDeleteAll(0, "AMC_DB_");
   Comment("");
  }

//+------------------------------------------------------------------+
//| EXPERT TIMER FUNCTION                                            |
//+------------------------------------------------------------------+
void OnTimer()
  {
   CheckAndUpdateDailyRisk();
   ManageOpenPositions();

   // 1. Écoute du flux Socket TCP (<1ms)
   if(InpBridgeMode != BRIDGE_FILE_JSON)
      CheckAndProcessTcpStream();

   // 2. Écoute du Fichier JSON (Fallback ou mode exclusif)
   if(InpBridgeMode != BRIDGE_SOCKET_TCP)
      CheckAndProcessBridgeFile();
  }

//+------------------------------------------------------------------+
//| EXPERT TICK FUNCTION                                             |
//+------------------------------------------------------------------+
void OnTick()
  {
   CheckAndUpdateDailyRisk();
   ManageOpenPositions();

   if(InpBridgeMode != BRIDGE_FILE_JSON)
      CheckAndProcessTcpStream();

   UpdateDashboard("EN COURS", ExtLastExecStatus);
  }

//+------------------------------------------------------------------+
//| LECTURE ET TRAITEMENT DU FICHIER BRIDGE (FALLBACK)               |
//+------------------------------------------------------------------+
void CheckAndProcessBridgeFile()
  {
   string filesToCheck[6];
   int fileCount = 0;

   filesToCheck[fileCount++] = InpBridgeFileName;
   filesToCheck[fileCount++] = StringFormat("amc_trade_signal_%s.json", _Symbol);
   if(_Symbol == InpSymbol_ES) filesToCheck[fileCount++] = "amc_trade_signal_ES.json";
   else if(_Symbol == InpSymbol_NQ) filesToCheck[fileCount++] = "amc_trade_signal_NQ.json";
   else if(_Symbol == InpSymbol_GC) filesToCheck[fileCount++] = "amc_trade_signal_GC.json";
   else if(_Symbol == InpSymbol_CL) filesToCheck[fileCount++] = "amc_trade_signal_CL.json";

   for(int f = 0; f < fileCount; f++)
     {
      string fileName = filesToCheck[f];
      if(!FileIsExist(fileName, FILE_COMMON))
         continue;

      int handle = FileOpen(fileName, FILE_READ|FILE_TXT|FILE_COMMON|FILE_SHARE_READ);
      if(handle == INVALID_HANDLE)
         continue;

      string json = "";
      while(!FileIsEnding(handle))
        {
         json += FileReadString(handle);
        }
      FileClose(handle);

      if(StringLen(json) < 20)
         continue;

      long sequence = ExtractJsonLong(json, "sequence");
      if(sequence <= 0 || sequence <= ExtLastProcessedSequence)
         continue;

      ProcessSignalPayload(json, sequence, false);
      break;
     }
  }

//+------------------------------------------------------------------+
//| TRAITEMENT ET EXECUTION D'UN SIGNAL VALIDÉ                       |
//+------------------------------------------------------------------+
void ProcessSignalPayload(const string &json, long sequence, bool fromTcp = false)
  {
   string instrument   = ExtractJsonString(json, "instrument");
   string action       = ExtractJsonString(json, "action");
   bool   isBuy        = ExtractJsonBool(json, "is_buy");
   double entry        = ExtractJsonDouble(json, "entry");
   double sl           = ExtractJsonDouble(json, "sl");
   double tp1          = ExtractJsonDouble(json, "tp1");
   double tp2          = ExtractJsonDouble(json, "tp2");
   double score        = ExtractJsonDouble(json, "score");
   string grade        = ExtractJsonString(json, "grade");
   string sigName      = ExtractJsonString(json, "signal_name");
   string timestampStr = ExtractJsonString(json, "timestamp_utc");
   long   timestampEpoch = ExtractJsonLong(json, "timestamp_epoch");
   bool   riskValid     = ExtractJsonBool(json, "risk_valid");
   long   nt8PositionSize = ExtractJsonLong(json, "position_size");

   // Contrat financier Zero-Trust : les champs critiques doivent exister,
   // être finis et respecter la géométrie avant tout mapping/exécution.
   if(!riskValid || nt8PositionSize <= 0 || entry <= 0 || sl <= 0 || tp1 <= 0 || tp2 <= 0 ||
      !MathIsValidNumber(entry) || !MathIsValidNumber(sl) || !MathIsValidNumber(tp1) || !MathIsValidNumber(tp2))
     {
      ExtLastProcessedSequence = sequence;
      ExtLastExecStatus = "Rejeté : contrat financier invalide (risk_valid/position_size/NaN).";
      Print("❌ " + ExtLastExecStatus);
      LogExecutionToCsv(sequence, timestampStr, action, instrument, 0, entry, sl, tp1, score, grade, sigName, "REJECTED_INVALID_RISK_CONTRACT", 0, ExtLastExecStatus);
      return;
     }

   if((isBuy && !(sl < entry && entry < tp1 && tp1 <= tp2)) ||
      (!isBuy && !(tp2 <= tp1 && tp1 < entry && entry < sl)))
     {
      ExtLastProcessedSequence = sequence;
      ExtLastExecStatus = "Rejeté : géométrie source Entry/SL/TP invalide.";
      Print("❌ " + ExtLastExecStatus);
      LogExecutionToCsv(sequence, timestampStr, action, instrument, 0, entry, sl, tp1, score, grade, sigName, "REJECTED_INVALID_GEOMETRY", 0, ExtLastExecStatus);
      return;
     }

   if((isBuy && action != "BUY") || (!isBuy && action != "SELL"))
     {
      ExtLastProcessedSequence = sequence;
      ExtLastExecStatus = "Rejeté : action incohérente avec is_buy.";
      Print("❌ " + ExtLastExecStatus);
      LogExecutionToCsv(sequence, timestampStr, action, instrument, 0, entry, sl, tp1, score, grade, sigName, "REJECTED_ACTION_MISMATCH", 0, ExtLastExecStatus);
      return;
     }

   ExtLastSignalInfo = StringFormat("#%d [%s] | %s %s | Score: %.0f (%s) | %s",
                                    sequence, (fromTcp ? "TCP <1ms" : "FILE"), action, instrument, score, grade, sigName);

   PrintFormat("🔔 [AMC PRO SIGNAL #%d DECOUVERT VIA %s] %s %s | Score: %.1f | Grade: %s | SL: %.5f | TP1: %.5f | TP2: %.5f",
               sequence, (fromTcp ? "TCP SOCKET" : "FICHIER"), action, instrument, score, grade, sl, tp1, tp2);

   // -1. Protection Hard Lockout Perte Journalière
   if(ExtDailyLockoutActive)
     {
      ExtLastProcessedSequence = sequence;
      ExtLastExecStatus = "Rejeté : Hard Lockout Journalier Actif (Max Daily Loss atteinte)";
      Print("🚨 " + ExtLastExecStatus);
      LogExecutionToCsv(sequence, timestampStr, action, instrument, 0, entry, sl, tp1, score, grade, sigName, "REJECTED_DAILY_LOCKOUT", 0, ExtLastExecStatus);
      return;
     }

   // -0.5. Protection Circuit Breaker
   if(ExtCircuitBreakerUntil > TimeCurrent())
     {
      ExtLastProcessedSequence = sequence;
      int remainingMin = (int)((ExtCircuitBreakerUntil - TimeCurrent()) / 60) + 1;
      ExtLastExecStatus = StringFormat("Rejeté : Circuit Breaker Actif (Pause encore %d min)", remainingMin);
      Print("⏸️ " + ExtLastExecStatus);
      LogExecutionToCsv(sequence, timestampStr, action, instrument, 0, entry, sl, tp1, score, grade, sigName, "REJECTED_CIRCUIT_BREAKER", 0, ExtLastExecStatus);
      return;
     }

   // 0. Validation de l'âge du signal : epoch UTC explicite, sans ambiguïté fuseau.
   if(InpMaxSignalAgeSec > 0)
     {
      if(timestampEpoch <= 0)
        {
         ExtLastProcessedSequence = sequence;
         ExtLastExecStatus = "Rejeté : timestamp_epoch absent ou invalide.";
         Print("⚠️ " + ExtLastExecStatus);
         LogExecutionToCsv(sequence, timestampStr, action, instrument, 0, entry, sl, tp1, score, grade, sigName, "REJECTED_INVALID_TIMESTAMP", 0, ExtLastExecStatus);
         return;
        }
      long nowEpoch = (long)TimeGMT();
      long ageSec = nowEpoch - timestampEpoch;
      if(ageSec < 0) ageSec = -ageSec;
      if(ageSec > InpMaxSignalAgeSec)
        {
         ExtLastProcessedSequence = sequence;
         ExtLastExecStatus = StringFormat("Rejeté : Signal #%d trop ancien (%d s > %d s max)", sequence, ageSec, InpMaxSignalAgeSec);
         Print("⚠️ " + ExtLastExecStatus);
         LogExecutionToCsv(sequence, timestampStr, action, instrument, 0, entry, sl, tp1, score, grade, sigName, "REJECTED_EXPIRED", 0, ExtLastExecStatus);
         return;
        }
     }

   // 1. Filtrage par score et grade
   if(score < InpMinScore)
     {
      ExtLastProcessedSequence = sequence;
      ExtLastExecStatus = StringFormat("Rejeté : Score (%.0f) < Min (%.0f)", score, InpMinScore);
      Print("⚠️ " + ExtLastExecStatus);
      LogExecutionToCsv(sequence, timestampStr, action, instrument, 0, entry, sl, tp1, score, grade, sigName, "REJECTED_SCORE", 0, ExtLastExecStatus);
      return;
     }

   if((grade == "A+" && !InpAllowGradeAPlus) ||
      (grade == "A"  && !InpAllowGradeA)     ||
      (grade == "B"  && !InpAllowGradeB)     ||
      (grade == "C"  && !InpAllowGradeC))
     {
      ExtLastProcessedSequence = sequence;
      ExtLastExecStatus = StringFormat("Rejeté : Grade '%s' non autorisé dans les paramètres", grade);
      Print("⚠️ " + ExtLastExecStatus);
      LogExecutionToCsv(sequence, timestampStr, action, instrument, 0, entry, sl, tp1, score, grade, sigName, "REJECTED_GRADE", 0, ExtLastExecStatus);
      return;
     }

   // 2. Mapping du symbole MT5
   string mt5Symbol = MapInstrumentToMT5(instrument);
   if(mt5Symbol == "")
     {
      ExtLastProcessedSequence = sequence;
      ExtLastExecStatus = StringFormat("Erreur Mapping : Symbole '%s' non configuré", instrument);
      Print("❌ " + ExtLastExecStatus);
      LogExecutionToCsv(sequence, timestampStr, action, instrument, 0, entry, sl, tp1, score, grade, sigName, "REJECTED_MAPPING", 0, ExtLastExecStatus);
      return;
     }

   if(!SymbolSelect(mt5Symbol, true))
     {
      ExtLastExecStatus = StringFormat("Erreur temporaire : Impossible de sélectionner '%s' dans Market Watch", mt5Symbol);
      Print("⚠️ " + ExtLastExecStatus);
      return;
     }

   // 2.5. Filtrage des positions existantes
   if(!HandleExistingPositions(mt5Symbol, isBuy))
     {
      ExtLastProcessedSequence = sequence;
      LogExecutionToCsv(sequence, timestampStr, action, mt5Symbol, 0, entry, sl, tp1, score, grade, sigName, "REJECTED_POSITION_LIMIT", 0, ExtLastExecStatus);
      return;
     }

   // 3. Calcul des niveaux d'exécution SL et TP
   MqlTick tick;
   if(!SymbolInfoTick(mt5Symbol, tick))
     {
      ExtLastExecStatus = "Erreur temporaire SymbolInfoTick pour " + mt5Symbol;
      Print("⚠️ " + ExtLastExecStatus);
      return;
     }

   double ask = tick.ask;
   double bid = tick.bid;
   double currentPrice = isBuy ? ask : bid;
   int digits = (int)SymbolInfoInteger(mt5Symbol, SYMBOL_DIGITS);
   double point = SymbolInfoDouble(mt5Symbol, SYMBOL_POINT);
   if(point <= 0 || !MathIsValidNumber(point))
     {
      ExtLastProcessedSequence = sequence;
      ExtLastExecStatus = "Rejeté : SYMBOL_POINT invalide.";
      Print("❌ " + ExtLastExecStatus);
      LogExecutionToCsv(sequence, timestampStr, action, mt5Symbol, 0, currentPrice, sl, tp1, score, grade, sigName, "REJECTED_INVALID_POINT", 0, ExtLastExecStatus);
      return;
     }

   int currentSpreadPoints = (int)((ask - bid) / point);
   if(currentSpreadPoints > InpMaxSpreadPoints)
     {
      ExtLastExecStatus = StringFormat("Attente : Spread trop élevé (%d > %d pts)", currentSpreadPoints, InpMaxSpreadPoints);
      Print("⚠️ " + ExtLastExecStatus);
      return;
     }

   double finalSl  = 0.0;
   double finalTp  = 0.0;
   double finalTp1 = 0.0;
   double finalTp2 = 0.0;

   if(InpExecutionMode == EXEC_POINTS_OFFSET)
     {
      double slDist  = MathAbs(entry - sl);
      double tp1Dist = MathAbs(tp1 - entry);
      double tp2Dist = (tp2 > 0) ? MathAbs(tp2 - entry) : (tp1Dist * 2.0);

      if(isBuy)
        {
         finalSl  = NormalizeDouble(ask - slDist, digits);
         finalTp1 = NormalizeDouble(ask + tp1Dist, digits);
         finalTp2 = NormalizeDouble(ask + tp2Dist, digits);
        }
      else
        {
         finalSl  = NormalizeDouble(bid + slDist, digits);
         finalTp1 = NormalizeDouble(bid - tp1Dist, digits);
         finalTp2 = NormalizeDouble(bid - tp2Dist, digits);
        }
     }
   else // EXEC_EXACT_PRICE
     {
      finalSl  = NormalizeDouble(sl, digits);
      finalTp1 = NormalizeDouble(tp1, digits);
      finalTp2 = (tp2 > 0) ? NormalizeDouble(tp2, digits) : NormalizeDouble(tp1, digits);
     }

   if(InpTpTarget == TP_TARGET_1)
      finalTp = finalTp1;
   else if(InpTpTarget == TP_TARGET_2)
      finalTp = finalTp2;
   else // TP_TARGET_SPLIT
      finalTp = finalTp2;

   // ZERO-TRUST P0 : revalider la géométrie APRÈS conversion Futures -> CFD.
   if(!MathIsValidNumber(finalSl) || !MathIsValidNumber(finalTp1) || !MathIsValidNumber(finalTp2) ||
      finalSl <= 0 || finalTp1 <= 0 || finalTp2 <= 0 ||
      (isBuy && !(finalSl < currentPrice && currentPrice < finalTp1 && finalTp1 <= finalTp2)) ||
      (!isBuy && !(finalTp2 <= finalTp1 && finalTp1 < currentPrice && currentPrice < finalSl)))
     {
      ExtLastProcessedSequence = sequence;
      ExtLastExecStatus = "Rejeté : géométrie Entry/SL/TP invalide après conversion MT5.";
      Print("❌ " + ExtLastExecStatus);
      LogExecutionToCsv(sequence, timestampStr, action, mt5Symbol, 0, currentPrice, finalSl, finalTp, score, grade, sigName, "REJECTED_CONVERTED_GEOMETRY", 0, ExtLastExecStatus);
      return;
     }

   // Respect des contraintes broker : StopsLevel + FreezeLevel. Aucun ordre ne
   // doit partir si SL/TP est dans une zone interdite par le symbole.
   int stopsLevel = (int)SymbolInfoInteger(mt5Symbol, SYMBOL_TRADE_STOPS_LEVEL);
   int freezeLevel = (int)SymbolInfoInteger(mt5Symbol, SYMBOL_TRADE_FREEZE_LEVEL);
   double minBrokerDistance = MathMax(stopsLevel, freezeLevel) * point;
   if(minBrokerDistance > 0)
     {
      double slDistance = MathAbs(currentPrice - finalSl);
      double tp1Distance = MathAbs(finalTp1 - currentPrice);
      double tp2Distance = MathAbs(finalTp2 - currentPrice);
      if(slDistance < minBrokerDistance || tp1Distance < minBrokerDistance || tp2Distance < minBrokerDistance)
        {
         ExtLastProcessedSequence = sequence;
         ExtLastExecStatus = StringFormat("Rejeté : SL/TP trop proche du prix broker (min %.1f pts).", minBrokerDistance / point);
         Print("⚠️ " + ExtLastExecStatus);
         LogExecutionToCsv(sequence, timestampStr, action, mt5Symbol, 0, currentPrice, finalSl, finalTp, score, grade, sigName, "REJECTED_BROKER_DISTANCE", 0, ExtLastExecStatus);
         return;
        }
     }

   // 4. Calcul de la taille de position (Lot Size)
   double lotSize = CalculateLotSize(mt5Symbol, isBuy ? ask : bid, finalSl);
   if(lotSize <= 0)
     {
      ExtLastProcessedSequence = sequence;
      ExtLastExecStatus = "Erreur : Lot size calculé invalide (0)";
      Print("❌ " + ExtLastExecStatus);
      LogExecutionToCsv(sequence, timestampStr, action, mt5Symbol, 0, currentPrice, finalSl, finalTp, score, grade, sigName, "REJECTED_LOT_SIZE", 0, ExtLastExecStatus);
      return;
     }

   // 5. Prise d'ordre via CTrade avec retry
   string comment = StringFormat("AMC_%d_%s", sequence, (InpTpTarget == TP_TARGET_SPLIT ? "SPLIT" : StringFormat("TP%d", (int)InpTpTarget)));
   bool executed = ExecuteTradeWithRetry(isBuy, lotSize, mt5Symbol, ask, bid, finalSl, finalTp, comment);

   if(executed)
     {
      ExtLastProcessedSequence = sequence;
      ExtTotalTradesExecuted++;

      ulong posTicket = 0;
      if(PositionSelect(mt5Symbol))
         posTicket = PositionGetInteger(POSITION_TICKET);

      if(posTicket > 0)
         RegisterManagedPosition(posTicket, sequence, mt5Symbol, isBuy, (isBuy ? ask : bid), lotSize, finalSl, finalTp1, finalTp2);

      double slippagePoints = (point > 0) ? MathAbs(currentPrice - entry) / point : 0.0;

      // Envoi de l'acquittement (ACK) vers NinjaTrader 8
      SendAckToNt8(sequence, posTicket, mt5Symbol, action, currentPrice, slippagePoints, "EXECUTED");

      string tpDesc = (InpTpTarget == TP_TARGET_SPLIT)
                      ? StringFormat("TP1: %.5f (50%%+BE) | TP2: %.5f", finalTp1, finalTp2)
                      : StringFormat("TP: %.5f", finalTp);

      ExtLastExecStatus = StringFormat("EXÉCUTÉ #%d [%s] : %s %.2f lot(s) %s à %.5f [SL: %.5f | %s]",
                                       sequence, (fromTcp ? "TCP <1ms" : "FILE"), action, lotSize, mt5Symbol, currentPrice, finalSl, tpDesc);
      Print("🚀 " + ExtLastExecStatus);
      LogExecutionToCsv(sequence, timestampStr, action, mt5Symbol, lotSize, currentPrice, finalSl, finalTp, score, grade, sigName, "EXECUTED", ExtTrade.ResultRetcode(), ExtLastExecStatus);
     }
   else
     {
      uint retcode = ExtTrade.ResultRetcode();
      ExtLastProcessedSequence = sequence;
      ExtLastExecStatus = StringFormat("Échec définitif #%d après 3 tentatives (Code %d: %s)", sequence, retcode, ExtTrade.ResultRetcodeDescription());
      Print("❌ " + ExtLastExecStatus);
      LogExecutionToCsv(sequence, timestampStr, action, mt5Symbol, lotSize, currentPrice, finalSl, finalTp, score, grade, sigName, "FAILED_EXECUTION", retcode, ExtLastExecStatus);
     }
  }

//+------------------------------------------------------------------+
//| GESTION ET FILTRAGE DES POSITIONS EXISTANTES SUR L'INSTRUMENT    |
//+------------------------------------------------------------------+
bool HandleExistingPositions(const string symbol, bool isBuy)
  {
   if(InpPositionMode == POS_MODE_ALLOW_ALL)
      return true;

   for(int i = PositionsTotal() - 1; i >= 0; i--)
     {
      ulong ticket = PositionGetTicket(i);
      if(ticket <= 0) continue;
      if(PositionGetString(POSITION_SYMBOL) != symbol) continue;
      if(PositionGetInteger(POSITION_MAGIC) != (long)InpMagicNumber) continue;

      ENUM_POSITION_TYPE posType = (ENUM_POSITION_TYPE)PositionGetInteger(POSITION_TYPE);

      if(InpPositionMode == POS_MODE_ONE_PER_SYMBOL)
        {
         if((isBuy && posType == POSITION_TYPE_BUY) || (!isBuy && posType == POSITION_TYPE_SELL))
           {
            ExtLastExecStatus = StringFormat("Rejeté : Position %s déjà ouverte sur %s", (isBuy ? "BUY" : "SELL"), symbol);
            Print("⚠️ " + ExtLastExecStatus);
            return false;
           }
         else if((isBuy && posType == POSITION_TYPE_SELL) || (!isBuy && posType == POSITION_TYPE_BUY))
           {
            PrintFormat("🔄 Signal inverse (%s) reçu. Fermeture de la position opposée #%d (%s)...",
                        (isBuy ? "BUY" : "SELL"), ticket, (posType == POSITION_TYPE_BUY ? "BUY" : "SELL"));
            if(!ExtTrade.PositionClose(ticket))
              {
               PrintFormat("❌ Échec fermeture position opposée #%d: %s", ticket, ExtTrade.ResultRetcodeDescription());
               ExtLastExecStatus = StringFormat("Rejeté : Échec de clôture de la position opposée #%d", ticket);
               return false;
              }
           }
        }
      else if(InpPositionMode == POS_MODE_ONE_PER_DIR)
        {
         if((isBuy && posType == POSITION_TYPE_BUY) || (!isBuy && posType == POSITION_TYPE_SELL))
           {
            ExtLastExecStatus = StringFormat("Rejeté : Position %s déjà ouverte sur %s", (isBuy ? "BUY" : "SELL"), symbol);
            Print("⚠️ " + ExtLastExecStatus);
            return false;
           }
        }
     }
   return true;
  }

//+------------------------------------------------------------------+
//| EXECUTION AVEC RETRY (REQUOTE / TIMEOUT)                         |
//+------------------------------------------------------------------+
bool ExecuteTradeWithRetry(bool isBuy, double lotSize, const string symbol,
                           double ask, double bid, double sl, double tp, const string comment)
  {
   const int MAX_RETRIES = 3;
   for(int attempt = 1; attempt <= MAX_RETRIES; attempt++)
     {
      bool success = false;
      if(isBuy)
         success = ExtTrade.Buy(lotSize, symbol, ask, sl, tp, comment);
      else
         success = ExtTrade.Sell(lotSize, symbol, bid, sl, tp, comment);

      uint retcode = ExtTrade.ResultRetcode();

      if(success && (retcode == TRADE_RETCODE_DONE || retcode == TRADE_RETCODE_PLACED || retcode == TRADE_RETCODE_DONE_PARTIAL))
         return true;

      bool isRetryable = (retcode == TRADE_RETCODE_REQUOTE ||
                          retcode == TRADE_RETCODE_PRICE_OFF ||
                          retcode == TRADE_RETCODE_TIMEOUT ||
                          retcode == TRADE_RETCODE_PRICE_CHANGED ||
                          retcode == TRADE_RETCODE_CONNECTION);

      if(!isRetryable || attempt == MAX_RETRIES)
        {
         PrintFormat("❌ OrderSend échec définitif (tentative %d/%d, code %d: %s)",
                     attempt, MAX_RETRIES, retcode, ExtTrade.ResultRetcodeDescription());
         return false;
        }

      PrintFormat("⚠️ OrderSend requote/timeout (tentative %d/%d, code %d), retry dans %d ms...",
                  attempt, MAX_RETRIES, retcode, 500 * attempt);
      Sleep(500 * attempt);

      MqlTick tick;
      if(SymbolInfoTick(symbol, tick))
        {
         ask = tick.ask;
         bid = tick.bid;
        }
     }
   return false;
  }

//+------------------------------------------------------------------+
//| GESTIONNAIRE DE POSITIONS ACTIVES (SPLIT TP1 / BE / TRAILING)   |
//+------------------------------------------------------------------+
void RegisterManagedPosition(ulong ticket, long sequence, const string symbol, bool isBuy,
                             double openPrice, double lot, double sl, double tp1, double tp2)
  {
   int size = ArraySize(ExtManagedPositions);
   ArrayResize(ExtManagedPositions, size + 1);
   ExtManagedPositions[size].ticket     = ticket;
   ExtManagedPositions[size].sequence   = sequence;
   ExtManagedPositions[size].symbol     = symbol;
   ExtManagedPositions[size].isBuy      = isBuy;
   ExtManagedPositions[size].openPrice  = openPrice;
   ExtManagedPositions[size].initialLot = lot;
   ExtManagedPositions[size].slPrice    = sl;
   ExtManagedPositions[size].tp1Price   = tp1;
   ExtManagedPositions[size].tp2Price   = tp2;
   ExtManagedPositions[size].tp1Hit     = false;
   ExtManagedPositions[size].openTime   = TimeCurrent();
  }

void CleanupManagedPositions()
  {
   int total = ArraySize(ExtManagedPositions);
   for(int i = total - 1; i >= 0; i--)
     {
      if(!PositionSelectByTicket(ExtManagedPositions[i].ticket))
        {
         for(int j = i; j < total - 1; j++)
           {
            ExtManagedPositions[j] = ExtManagedPositions[j + 1];
           }
         total--;
         ArrayResize(ExtManagedPositions, total);
        }
     }
  }

void ManageOpenPositions()
  {
   CleanupManagedPositions();
   int total = ArraySize(ExtManagedPositions);
   if(total == 0) return;

   for(int i = 0; i < total; i++)
     {
      ulong ticket = ExtManagedPositions[i].ticket;
      if(!PositionSelectByTicket(ticket)) continue;

      string sym           = ExtManagedPositions[i].symbol;
      bool isBuy           = ExtManagedPositions[i].isBuy;
      double openPrice     = ExtManagedPositions[i].openPrice;
      double tp1Price      = ExtManagedPositions[i].tp1Price;
      double currentSl     = PositionGetDouble(POSITION_SL);
      double currentTp     = PositionGetDouble(POSITION_TP);
      double currentVolume = PositionGetDouble(POSITION_VOLUME);

      MqlTick tick;
      if(!SymbolInfoTick(sym, tick)) continue;

      int digits   = (int)SymbolInfoInteger(sym, SYMBOL_DIGITS);
      double point = SymbolInfoDouble(sym, SYMBOL_POINT);
      if(point <= 0) point = 0.00001;

      // 1. Détection de franchissement de TP1 en mode SPLIT
      if(InpTpTarget == TP_TARGET_SPLIT && !ExtManagedPositions[i].tp1Hit && tp1Price > 0)
        {
         bool tp1Reached = isBuy ? (tick.bid >= tp1Price) : (tick.ask <= tp1Price);
         if(tp1Reached)
           {
            double closeLot = NormalizeLot(sym, ExtManagedPositions[i].initialLot * (InpClosePercentTP1 / 100.0));
            double minLot   = SymbolInfoDouble(sym, SYMBOL_VOLUME_MIN);
            double remainingLot = currentVolume - closeLot;

            if(closeLot >= minLot && remainingLot >= minLot && closeLot < currentVolume)
              {
               if(ExtTrade.PositionClosePartial(ticket, closeLot))
                 {
                  PrintFormat("🎯 [PARTIAL TP1] Ticket #%d : Clôture de %.2f lots à TP1 (%.5f) - Reste: %.2f lots",
                              ticket, closeLot, tp1Price, remainingLot);
                  LogExecutionToCsv(ExtManagedPositions[i].sequence, TimeToString(TimeLocal()), (isBuy ? "BUY" : "SELL"),
                                    sym, closeLot, (isBuy ? tick.bid : tick.ask), currentSl, tp1Price, 0, "", "SPLIT_TP1", "PARTIAL_CLOSED",
                                    ExtTrade.ResultRetcode(), "Clôture partielle TP1 réussie");
                 }
               else
                 {
                  PrintFormat("⚠️ [PARTIAL TP1] Échec clôture partielle ticket #%d: %s", ticket, ExtTrade.ResultRetcodeDescription());
                 }
              }

            // Déplacement du Stop Loss au Break-Even
            if(InpEnableBreakEven)
              {
               double bePrice = isBuy ? NormalizeDouble(openPrice + InpBreakEvenBufferPoints * point, digits)
                                      : NormalizeDouble(openPrice - InpBreakEvenBufferPoints * point, digits);

               bool shouldModify = isBuy ? (bePrice > currentSl) : (bePrice < currentSl || currentSl <= 0);
               if(shouldModify)
                 {
                  if(ExtTrade.PositionModify(ticket, bePrice, currentTp))
                    {
                     PrintFormat("🛡️ [BREAK-EVEN] Ticket #%d : SL déplacé à BE (%.5f) [Buffer: %d pts]",
                                 ticket, bePrice, InpBreakEvenBufferPoints);
                     LogExecutionToCsv(ExtManagedPositions[i].sequence, TimeToString(TimeLocal()), (isBuy ? "BUY" : "SELL"),
                                       sym, currentVolume, (isBuy ? tick.bid : tick.ask), bePrice, currentTp, 0, "", "BREAK_EVEN", "SL_MODIFIED",
                                       ExtTrade.ResultRetcode(), "Stop Loss sécurisé à Break-Even");
                    }
                  else
                    {
                     PrintFormat("⚠️ [BREAK-EVEN] Échec modification SL ticket #%d: %s", ticket, ExtTrade.ResultRetcodeDescription());
                    }
                 }
              }

            ExtManagedPositions[i].tp1Hit = true;
           }
        }

      // 2. Trailing Stop dynamique
      if(InpEnableTrailing && (ExtManagedPositions[i].tp1Hit || InpTpTarget != TP_TARGET_SPLIT))
        {
         double trailingDist = InpTrailingDistancePoints * point;
         double trailingStep = InpTrailingStepPoints * point;

         if(isBuy)
           {
            double newSl = NormalizeDouble(tick.bid - trailingDist, digits);
            if(newSl > currentSl + trailingStep && newSl > openPrice)
              {
               if(ExtTrade.PositionModify(ticket, newSl, currentTp))
                 {
                  PrintFormat("📈 [TRAILING STOP] Ticket #%d (BUY) : SL ajusté à %.5f (Prix: %.5f)", ticket, newSl, tick.bid);
                 }
              }
           }
         else
           {
            double newSl = NormalizeDouble(tick.ask + trailingDist, digits);
            if((newSl < currentSl - trailingStep || currentSl <= 0) && newSl < openPrice)
              {
               if(ExtTrade.PositionModify(ticket, newSl, currentTp))
                 {
                  PrintFormat("📉 [TRAILING STOP] Ticket #%d (SELL) : SL ajusté à %.5f (Prix: %.5f)", ticket, newSl, tick.ask);
                 }
              }
           }
        }
     }
  }

//+------------------------------------------------------------------+
//| CONTROLE DU RISQUE PROP FIRM & CIRCUIT BREAKER                   |
//+------------------------------------------------------------------+
void CheckAndUpdateDailyRisk()
  {
   datetime now = TimeCurrent();
   datetime dayStart = (datetime)(now - (now % 86400));

   if(ExtCurrentDay != dayStart)
     {
      ExtCurrentDay = dayStart;
      ExtDailyStartEquity = AccountInfoDouble(ACCOUNT_EQUITY);
      if(ExtDailyStartEquity <= 0) ExtDailyStartEquity = AccountInfoDouble(ACCOUNT_BALANCE);
      ExtDailyLockoutActive = false;
      ExtCircuitBreakerUntil = 0;
      PrintFormat("🌅 [DAILY RESET] Nouvelle journée de trading. Equity de départ: %.2f %s",
                  ExtDailyStartEquity, AccountInfoString(ACCOUNT_CURRENCY));
     }

   // 1. Calcul du P&L réalisé aujourd'hui (depuis dayStart)
   ExtDailyRealizedPl = 0.0;
   int consecutiveLosses = 0;

   if(HistorySelect(dayStart, now))
     {
      int dealsTotal = HistoryDealsTotal();
      bool countingConsecutive = true;

      for(int i = dealsTotal - 1; i >= 0; i--)
        {
         ulong dealTicket = HistoryDealGetTicket(i);
         if(dealTicket <= 0) continue;
         if(HistoryDealGetInteger(dealTicket, DEAL_MAGIC) != (long)InpMagicNumber) continue;

         ENUM_DEAL_ENTRY entryType = (ENUM_DEAL_ENTRY)HistoryDealGetInteger(dealTicket, DEAL_ENTRY);
         if(entryType == DEAL_ENTRY_OUT || entryType == DEAL_ENTRY_INOUT)
           {
            double profit = HistoryDealGetDouble(dealTicket, DEAL_PROFIT)
                          + HistoryDealGetDouble(dealTicket, DEAL_SWAP)
                          + HistoryDealGetDouble(dealTicket, DEAL_COMMISSION);

            ExtDailyRealizedPl += profit;

            if(countingConsecutive)
              {
               if(profit < -0.001)
                  consecutiveLosses++;
               else if(profit > 0.001)
                  countingConsecutive = false;
              }
           }
        }
     }

   ExtConsecutiveLossCount = consecutiveLosses;

   // 2. Calcul du P&L flottant actuel
   ExtDailyFloatingPl = 0.0;
   for(int i = PositionsTotal() - 1; i >= 0; i--)
     {
      ulong ticket = PositionGetTicket(i);
      if(ticket <= 0) continue;
      if(PositionGetInteger(POSITION_MAGIC) != (long)InpMagicNumber) continue;
      ExtDailyFloatingPl += PositionGetDouble(POSITION_PROFIT) + PositionGetDouble(POSITION_SWAP);
     }

   ExtDailyTotalPl = ExtDailyRealizedPl + ExtDailyFloatingPl;

   // 3. Vérification du Hard Lockout Perte Journalière
   if(InpEnableDailyMaxLoss)
     {
      double maxAllowedLossCurrency = 0.0;
      if(InpDailyMaxLossCurrency > 0.0)
         maxAllowedLossCurrency = InpDailyMaxLossCurrency;
      else if(ExtDailyStartEquity > 0.0 && InpDailyMaxLossPercent > 0.0)
         maxAllowedLossCurrency = ExtDailyStartEquity * (InpDailyMaxLossPercent / 100.0);

      if(maxAllowedLossCurrency > 0.0 && ExtDailyTotalPl <= -maxAllowedLossCurrency)
        {
         if(!ExtDailyLockoutActive)
           {
            ExtDailyLockoutActive = true;
            PrintFormat("🚨 [HARD LOCKOUT] Perte journalière maximale atteinte ! Total P/L: %.2f / Max autorisée: -%.2f",
                        ExtDailyTotalPl, maxAllowedLossCurrency);
            CloseAllPositions("DAILY_MAX_LOSS_REACHED");
            LogExecutionToCsv(0, TimeToString(TimeLocal()), "LOCKOUT", "ALL", 0, 0, 0, 0, 0, "", "HARD_LOCKOUT", "TRIGGERED", 0,
                              StringFormat("P/L Jour: %.2f | Seuil: -%.2f", ExtDailyTotalPl, maxAllowedLossCurrency));
           }
        }
     }

   // 4. Vérification du Circuit Breaker (Pertes consécutives)
   if(InpEnableCircuitBreaker && InpMaxConsecutiveLosses > 0)
     {
      if(ExtConsecutiveLossCount >= InpMaxConsecutiveLosses)
        {
         if(ExtCircuitBreakerUntil < now)
           {
            ExtCircuitBreakerUntil = now + InpCircuitBreakerPauseMin * 60;
            PrintFormat("⏸️ [CIRCUIT BREAKER] %d pertes consécutives détectées. Suspension des nouveaux trades jusqu'à %s.",
                        ExtConsecutiveLossCount, TimeToString(ExtCircuitBreakerUntil, TIME_MINUTES|TIME_SECONDS));
           }
        }
     }
  }

void CloseAllPositions(const string reason)
  {
   PrintFormat("🛑 Fermeture d'urgence de toutes les positions (Raison: %s)...", reason);
   const int MAX_RETRIES = 3;
   for(int attempt = 1; attempt <= MAX_RETRIES; attempt++)
     {
      int remaining = 0;
      for(int i = PositionsTotal() - 1; i >= 0; i--)
        {
         ulong ticket = PositionGetTicket(i);
         if(ticket <= 0) continue;
         if(PositionGetInteger(POSITION_MAGIC) != (long)InpMagicNumber) continue;

         remaining++;
         if(!ExtTrade.PositionClose(ticket))
            PrintFormat("❌ [Tentative %d/%d] Échec fermeture position #%d: %s", attempt, MAX_RETRIES, ticket, ExtTrade.ResultRetcodeDescription());
         else
            PrintFormat("✅ Position #%d fermée avec succès", ticket);
        }

      if(remaining == 0)
         break;

      if(attempt < MAX_RETRIES)
         Sleep(100);
     }
  }

//+------------------------------------------------------------------+
//| SCAN ET RESTAURATION DES POSITIONS EXISTANTES AU DEMARRAGE       |
//+------------------------------------------------------------------+
void ScanExistingPositions()
  {
   int count = 0;
   for(int i = PositionsTotal() - 1; i >= 0; i--)
     {
      ulong ticket = PositionGetTicket(i);
      if(ticket <= 0) continue;
      if(PositionGetInteger(POSITION_MAGIC) != (long)InpMagicNumber) continue;

      count++;
      string sym = PositionGetString(POSITION_SYMBOL);
      ENUM_POSITION_TYPE posType = (ENUM_POSITION_TYPE)PositionGetInteger(POSITION_TYPE);
      bool isBuy = (posType == POSITION_TYPE_BUY);
      double volume = PositionGetDouble(POSITION_VOLUME);
      double openPrice = PositionGetDouble(POSITION_PRICE_OPEN);
      double slPrice = PositionGetDouble(POSITION_SL);
      double tpPrice = PositionGetDouble(POSITION_TP);
      double profit = PositionGetDouble(POSITION_PROFIT);

      PrintFormat("📋 [RECOVERY] Position existante #%d : %s %s %.2f lots @ %.5f | SL: %.5f | TP: %.5f | P/L: %.2f",
                  ticket, (isBuy ? "BUY" : "SELL"), sym,
                  volume, openPrice, slPrice, tpPrice, profit);

      RegisterManagedPosition(ticket, 0, sym, isBuy, openPrice, volume, slPrice, tpPrice, tpPrice);
     }

   if(count > 0)
      PrintFormat("📋 [RECOVERY] %d position(s) existante(s) restaurée(s) dans le gestionnaire actif.", count);
   else
      Print("📋 [RECOVERY] Aucune position existante avec ce Magic Number.");
  }

//+------------------------------------------------------------------+
//| MAPPING EXACT DES SYMBOLES (ANTI FAUX POSITIFS)                  |
//+------------------------------------------------------------------+
string MapInstrumentToMT5(const string instrument)
  {
   if(InpAutoChartSymbol)
      return _Symbol;

   string instUpper = instrument;
   StringToUpper(instUpper);
   StringTrimLeft(instUpper);
   StringTrimRight(instUpper);

   if(instUpper == "GC" || instUpper == "MGC" || instUpper == "GOLD")   return InpSymbol_GC;
   if(instUpper == "NQ" || instUpper == "MNQ")                         return InpSymbol_NQ;
   if(instUpper == "ES" || instUpper == "MES")                         return InpSymbol_ES;
   if(instUpper == "CL" || instUpper == "MCL")                         return InpSymbol_CL;
   if(instUpper == "6E")                                               return InpSymbol_6E;
   if(instUpper == "6B")                                               return InpSymbol_6B;
   if(instUpper == "FDAX")                                             return InpSymbol_FDAX;

   if(StringFind(instUpper, "GC ") == 0 || StringFind(instUpper, "MGC ") == 0)   return InpSymbol_GC;
   if(StringFind(instUpper, "NQ ") == 0 || StringFind(instUpper, "MNQ ") == 0)   return InpSymbol_NQ;
   if(StringFind(instUpper, "ES ") == 0 || StringFind(instUpper, "MES ") == 0)   return InpSymbol_ES;
   if(StringFind(instUpper, "CL ") == 0 || StringFind(instUpper, "MCL ") == 0)   return InpSymbol_CL;
   if(StringFind(instUpper, "6E ") == 0)                                         return InpSymbol_6E;
   if(StringFind(instUpper, "6B ") == 0)                                         return InpSymbol_6B;
   if(StringFind(instUpper, "FDAX ") == 0)                                       return InpSymbol_FDAX;

   PrintFormat("❌ Mapping introuvable pour l'instrument '%s' -> Signal rejeté par sécurité.", instrument);
   return "";
  }

//+------------------------------------------------------------------+
//| CALCUL DYNAMIQUE DE LA TAILLE DU LOT (MONEY MANAGEMENT)          |
//+------------------------------------------------------------------+
double CalculateLotSize(const string symbol, double entryPrice, double slPrice)
  {
   if(!MathIsValidNumber(entryPrice) || !MathIsValidNumber(slPrice) || entryPrice <= 0 || slPrice <= 0)
      return 0;

   if(InpLotMode == LOT_FIXED)
     {
      if(InpFixedLot <= 0 || !MathIsValidNumber(InpFixedLot)) return 0;
      return NormalizeLot(symbol, InpFixedLot);
     }

   double equity = AccountInfoDouble(ACCOUNT_EQUITY);
   if(!MathIsValidNumber(equity) || equity <= 0) return 0;
   if(!MathIsValidNumber(InpRiskPercent) || InpRiskPercent <= 0) return 0;

   double riskMoney = equity * (InpRiskPercent / 100.0);
   double slDistancePoints = MathAbs(entryPrice - slPrice);
   if(!MathIsValidNumber(riskMoney) || riskMoney <= 0 || !MathIsValidNumber(slDistancePoints) || slDistancePoints <= 0)
      return 0;

   double point = SymbolInfoDouble(symbol, SYMBOL_POINT);
   double tickValue = SymbolInfoDouble(symbol, SYMBOL_TRADE_TICK_VALUE);
   double tickSize = SymbolInfoDouble(symbol, SYMBOL_TRADE_TICK_SIZE);

   if(point <= 0 || tickValue <= 0 || tickSize <= 0 || !MathIsValidNumber(point) ||
      !MathIsValidNumber(tickValue) || !MathIsValidNumber(tickSize))
     {
      PrintFormat("⚠️ Données marché invalides (point=%.5f tickValue=%.5f tickSize=%.5f slDist=%.5f) — trade rejeté",
                  point, tickValue, tickSize, slDistancePoints);
      return 0;
     }

   double lossPerLot = (slDistancePoints / tickSize) * tickValue;
   if(!MathIsValidNumber(lossPerLot) || lossPerLot <= 0) return 0;

   double rawLot = riskMoney / lossPerLot;
   if(!MathIsValidNumber(rawLot) || rawLot <= 0) return 0;
   return NormalizeLot(symbol, rawLot);
  }

double NormalizeLot(const string symbol, double lot)
  {
   double minLot  = SymbolInfoDouble(symbol, SYMBOL_VOLUME_MIN);
   double maxLot  = SymbolInfoDouble(symbol, SYMBOL_VOLUME_MAX);
   double lotStep = SymbolInfoDouble(symbol, SYMBOL_VOLUME_STEP);

   // ZERO-TRUST : jamais de fallback sur les métadonnées broker.
   if(!MathIsValidNumber(lot) || lot <= 0 || !MathIsValidNumber(minLot) ||
      !MathIsValidNumber(maxLot) || !MathIsValidNumber(lotStep) ||
      minLot <= 0 || maxLot < minLot || lotStep <= 0 || InpMaxLot <= 0)
      return 0;

   // Arrondir vers le bas protège le budget de risque.
   lot = MathFloor(lot / lotStep) * lotStep;
   if(lot <= 0 || lot < minLot) return 0; // surtout ne jamais remonter au min broker.
   if(lot > maxLot) lot = maxLot;
   if(lot > InpMaxLot) lot = InpMaxLot;
   if(lot < minLot) return 0;

   return NormalizeDouble(lot, 8);
  }

//+------------------------------------------------------------------+
//| GESTION DU DASHBOARD VISUEL SUR LE GRAPHIQUE MT5                 |
//+------------------------------------------------------------------+
void CreateDashboard()
  {
   int x = 15;
   int y = 25;
   int width = 430;
   int height = 215;

   CreatePanelObj("AMC_DB_BG", x, y, width, height, C'18,22,30', C'40,50,68');
   CreateTextObj("AMC_DB_TITLE", "🎯 AMC PRO - RECEIVER EA (HYBRID TCP/FILE)", x + 12, y + 10, "Arial", 10, clrGold, true);
   
   string modeStr = (InpTpTarget == TP_TARGET_SPLIT) ? "SPLIT (TP1 50% + BE / TP2)" : StringFormat("FIXE (TP%d)", (int)InpTpTarget);
   string lotStr  = (InpLotMode == LOT_RISK_PERCENT) ? StringFormat("%.1f%% Risque", InpRiskPercent) : StringFormat("%.2f Fixe", InpFixedLot);
   CreateTextObj("AMC_DB_MODE", StringFormat("Mode TP : %s | Lot : %s", modeStr, lotStr), x + 12, y + 32, "Arial", 8, clrLightGray, false);

   CreateTextObj("AMC_DB_L1", "Statut du Pont : Connecté", x + 12, y + 55, "Arial", 8, clrLime, true);
   CreateTextObj("AMC_DB_L2", "Risk Guard     : Protection ACTIVE (OK)", x + 12, y + 78, "Arial", 8, clrLime, true);
   CreateTextObj("AMC_DB_L3", "P&L Journalier : 0.00 USD (0.00%) | Pertes conséc: 0", x + 12, y + 101, "Arial", 8, clrLightGray, false);
   CreateTextObj("AMC_DB_L4", "Dernier Signal : " + ExtLastSignalInfo, x + 12, y + 124, "Arial", 8, clrWhite, false);
   CreateTextObj("AMC_DB_L5", "Exécution      : " + ExtLastExecStatus, x + 12, y + 147, "Arial", 8, clrCyan, false);
   CreateTextObj("AMC_DB_L6", StringFormat("Positions Managées: 0 | Total Exécutés: %d", ExtTotalTradesExecuted), x + 12, y + 175, "Arial", 8, clrYellow, true);
  }

void UpdateDashboard(const string statusState, const string execStatus)
  {
   string bridgeStatus = "Statut Pont    : ";
   color bridgeColor = clrLime;
   if(ExtTcpConnected)
     {
      bridgeStatus += StringFormat("🟢 Socket TCP 127.0.0.1:%d (<1ms)", InpTcpPort);
      bridgeColor = clrLime;
     }
   else if(InpBridgeMode == BRIDGE_SOCKET_TCP)
     {
      bridgeStatus += "🔴 TCP Déconnecté (En attente NT8...)";
      bridgeColor = clrTomato;
     }
   else
     {
      bridgeStatus += "🟡 Fichier JSON (Polling 100ms)";
      bridgeColor = clrYellow;
     }

   ObjectSetString(0, "AMC_DB_L1", OBJPROP_TEXT, bridgeStatus);
   ObjectSetInteger(0, "AMC_DB_L1", OBJPROP_COLOR, bridgeColor);

   string riskStatus = "Risk Guard     : Protection ACTIVE (OK)";
   color riskColor = clrLime;
   if(ExtDailyLockoutActive)
     {
      riskStatus = "Risk Guard     : 🚨 HARD LOCKOUT (Max Loss Atteinte)";
      riskColor = clrTomato;
     }
   else if(ExtCircuitBreakerUntil > TimeCurrent())
     {
      int remMin = (int)((ExtCircuitBreakerUntil - TimeCurrent()) / 60) + 1;
      riskStatus = StringFormat("Risk Guard     : ⏸️ CIRCUIT BREAKER (%d min restantes)", remMin);
      riskColor = clrOrange;
     }
   ObjectSetString(0, "AMC_DB_L2", OBJPROP_TEXT, riskStatus);
   ObjectSetInteger(0, "AMC_DB_L2", OBJPROP_COLOR, riskColor);

   double plPercent = (ExtDailyStartEquity > 0) ? (ExtDailyTotalPl / ExtDailyStartEquity) * 100.0 : 0.0;
   string plText = StringFormat("P&L Journalier : %+.2f %s (%+.2f%%) | Pertes conséc: %d",
                                ExtDailyTotalPl, AccountInfoString(ACCOUNT_CURRENCY), plPercent, ExtConsecutiveLossCount);
   color plColor = (ExtDailyTotalPl >= 0) ? clrLimeGreen : clrTomato;
   ObjectSetString(0, "AMC_DB_L3", OBJPROP_TEXT, plText);
   ObjectSetInteger(0, "AMC_DB_L3", OBJPROP_COLOR, plColor);

   ObjectSetString(0, "AMC_DB_L4", OBJPROP_TEXT, "Dernier Signal : " + ExtLastSignalInfo);
   ObjectSetString(0, "AMC_DB_L5", OBJPROP_TEXT, "Exécution      : " + execStatus);
   
   int activeManaged = ArraySize(ExtManagedPositions);
   ObjectSetString(0, "AMC_DB_L6", OBJPROP_TEXT, StringFormat("Positions Managées: %d | Total Exécutés: %d",
                                                              activeManaged, ExtTotalTradesExecuted));
   ChartRedraw(0);
  }

void CreatePanelObj(string name, int x, int y, int w, int h, color bgColor, color bdColor)
  {
   ObjectDelete(0, name);
   ObjectCreate(0, name, OBJ_RECTANGLE_LABEL, 0, 0, 0);
   ObjectSetInteger(0, name, OBJPROP_XDISTANCE, x);
   ObjectSetInteger(0, name, OBJPROP_YDISTANCE, y);
   ObjectSetInteger(0, name, OBJPROP_XSIZE, w);
   ObjectSetInteger(0, name, OBJPROP_YSIZE, h);
   ObjectSetInteger(0, name, OBJPROP_BGCOLOR, bgColor);
   ObjectSetInteger(0, name, OBJPROP_BORDER_COLOR, bdColor);
   ObjectSetInteger(0, name, OBJPROP_BORDER_TYPE, BORDER_FLAT);
   ObjectSetInteger(0, name, OBJPROP_CORNER, CORNER_LEFT_UPPER);
   ObjectSetInteger(0, name, OBJPROP_BACK, false);
   ObjectSetInteger(0, name, OBJPROP_SELECTABLE, false);
  }

void CreateTextObj(string name, string text, int x, int y, string font, int fontSize, color textColor, bool isBold)
  {
   ObjectDelete(0, name);
   ObjectCreate(0, name, OBJ_LABEL, 0, 0, 0);
   ObjectSetInteger(0, name, OBJPROP_XDISTANCE, x);
   ObjectSetInteger(0, name, OBJPROP_YDISTANCE, y);
   ObjectSetString(0, name, OBJPROP_TEXT, text);
   ObjectSetString(0, name, OBJPROP_FONT, font);
   ObjectSetInteger(0, name, OBJPROP_FONTSIZE, fontSize);
   ObjectSetInteger(0, name, OBJPROP_COLOR, textColor);
   ObjectSetInteger(0, name, OBJPROP_CORNER, CORNER_LEFT_UPPER);
   ObjectSetInteger(0, name, OBJPROP_SELECTABLE, false);
  }
//+------------------------------------------------------------------+
