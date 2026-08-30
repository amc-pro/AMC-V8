#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.BarsTypes;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Indicators.VolumeProfilePro;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public partial class AuctionMarketCore
    {
        #region Paramètres Swing Dédiés

        [Display(Name = "Activer Moteur Swing", Order = 1, GroupName = "Swing 01. Moteur")]
        public bool EnableSwingEngine { get; set; }

        [Range(10, 100)]
        [Display(Name = "Seuil Alerte Swing (Min Score)", Order = 2, GroupName = "Swing 01. Moteur")]
        public double SwingMinScoreToAlert { get; set; }

        [Range(10, 100)]
        [Display(Name = "Seuil Tier Moyen (Silver)", Order = 3, GroupName = "Swing 01. Moteur")]
        public double SwingTierSilverScore { get; set; }

        [Range(10, 100)]
        [Display(Name = "Seuil Tier Fort (Gold)", Order = 4, GroupName = "Swing 01. Moteur")]
        public double SwingTierGoldScore { get; set; }

        [Range(10, 100)]
        [Display(Name = "Seuil Tier Très Fort", Order = 5, GroupName = "Swing 01. Moteur")]
        public double SwingTierTresFortScore { get; set; }

        [Display(Name = "Autoriser Maintien Overnight", Order = 6, GroupName = "Swing 02. Risque")]
        public bool SwingAllowOvernightHold { get; set; }

        [Range(1, 10)]
        [Display(Name = "Max Positions Swing Simultanées", Order = 7, GroupName = "Swing 02. Risque")]
        public int SwingMaxActiveTrades { get; set; }

        [Display(Name = "Fichier Journal Swing (Vide = Auto shadow/swing_trades.csv)", Order = 8, GroupName = "Swing 03. Journal")]
        public string SwingJournalFilePath { get; set; }

        [Display(Name = "Activer Alertes Telegram Swing", Order = 9, GroupName = "Swing 03. Alertes")]
        public bool EnableSwingTelegramAlerts { get; set; }

        #endregion

        #region État Interne Swing

        [Browsable(false)]
        [XmlIgnore]
        public bool IsSwing
        {
            get { return TradingPreset == SniperMarketPreset.Swing; }
        }

        private ISwingScorer swingScorer;
        private ISwingRiskManager swingRiskManager;
        private readonly List<SwingSignal> activeSwingSignals = new List<SwingSignal>();
        private readonly List<TrackedSwingTrade> openSwingTrades = new List<TrackedSwingTrade>();
        private readonly List<TrackedSwingTrade> closedSwingTrades = new List<TrackedSwingTrade>();
        private string resolvedSwingJournalPath;
        private bool swingJournalHeaderWritten;
        private int swingLastEvaluatedBar = -1;

        #endregion

        #region Initialisation & Defaults Swing

        private void ApplySwingDefaults()
        {
            EnableSwingEngine = true;
            SwingMinScoreToAlert = 50.0;
            SwingTierSilverScore = 50.0;
            SwingTierGoldScore = 70.0;
            SwingTierTresFortScore = 85.0;
            SwingAllowOvernightHold = true;
            SwingMaxActiveTrades = 2;
            SwingJournalFilePath = string.Empty;
            EnableSwingTelegramAlerts = true;
        }

        /// <summary>
        /// Applique la configuration Swing institutionnelle par défaut.
        /// </summary>
        private void ApplySwingPreset()
        {
            EvaluateOnBarClose = true;
            UseSessionProfile = true;
            EnableClosedVolumeProfile = true;
            EnableSQLiteVolumeProfileHistory = true;
            EnableMarketIntelligence = true;

            // Paramètres de Risque Swing Macro
            MinRiskReward = 1.5;
            TargetR1 = 1.5;
            TargetR2 = 3.0;
            StopAtrMultiple = 2.0;
            StopBufferTicks = 4;
            RiskPerTradeCurrency = 250;
            MaxContracts = 4;
            ExecutionCostTicks = 1;

            // Anti-Lookahead & Filtrage HTF
            EnableHtfFilter = true;
            HtfMinutes = 240; // 4 Heures
            HtfEmaPeriod = 50;
            HtfStrictMode = false;
            HtfSoftMode = true;

            // Moteur News Swing
            NewsBlackoutMinutes = 15;
            NewsWindowPenalty = 20;
            NewsHardBlock = false;

            ApplySwingDefaults();
        }

        private void InitSwingEngine()
        {
            if (swingScorer == null)
                swingScorer = new SwingScorer();

            if (swingRiskManager == null)
                swingRiskManager = new SwingRiskManager();

            activeSwingSignals.Clear();
            openSwingTrades.Clear();
            closedSwingTrades.Clear();
            resolvedSwingJournalPath = ResolveSwingJournalPath();
            swingJournalHeaderWritten = false;
            swingLastEvaluatedBar = -1;
        }

        private void SwingTerminated()
        {
            // Clôture des trades swing en fin de session / déchargement
            for (int i = openSwingTrades.Count - 1; i >= 0; i--)
            {
                TrackedSwingTrade t = openSwingTrades[i];
                t.CloseTrade(snClose > 0 ? snClose : 0.0, DateTime.UtcNow, "SESSION_TERMINATED", TickSize, ResolvePointValue());
                LogSwingTrade(t);
            }
            openSwingTrades.Clear();
            activeSwingSignals.Clear();
            closedSwingTrades.Clear();
        }

        private string ResolveSwingJournalPath()
        {
            if (!string.IsNullOrEmpty(SwingJournalFilePath))
                return SwingJournalFilePath;

            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string shadowDir = Path.Combine(docs, "NinjaTrader 8", "shadow");
                if (!Directory.Exists(shadowDir))
                {
                    Directory.CreateDirectory(shadowDir);
                }
                return Path.Combine(shadowDir, "swing_trades.csv");
            }
            catch
            {
                return "swing_trades.csv";
            }
        }

        private double ResolvePointValue()
        {
            try
            {
                if (Instrument != null && Instrument.MasterInstrument != null)
                    return Instrument.MasterInstrument.PointValue;
            }
            catch { }
            return 50.0;
        }

        #endregion

        #region Moteur d'Évaluation Swing sur Barre Clôturée

        /// <summary>
        /// Point d'entrée Swing exécuté sur chaque barre clôturée (anti-lookahead strict).
        /// </summary>
        private void SwingOnEvaluatedBar()
        {
            if (!IsSwing || !EnableSwingEngine) return;

            try
            {
                if (evalBarIndex < 0 || evalBarIndex == swingLastEvaluatedBar) return;
                swingLastEvaluatedBar = evalBarIndex;

                if (CurrentBars[volumetricBarsIndex] < 5) return;

                // 1. Mise à jour des trades ouverts (vérification des Stops et Take Profits)
                UpdateOpenSwingTrades();

                // 2. Construction du contexte de marché Swing immuable
                SwingContext ctxLong = BuildSwingContext(true);
                SwingContext ctxShort = BuildSwingContext(false);

                // 3. Détection et évaluation des signaux sur les 5 familles institutionnelles
                EvaluateSwingDirection(ctxLong, SwingDirection.Long);
                EvaluateSwingDirection(ctxShort, SwingDirection.Short);
            }
            catch (Exception ex)
            {
                RegisterRuntimeError("SwingOnEvaluatedBar", ex);
                if (EnableDebugMode)
                    Print("VP_Swing Error: " + ex.Message);
            }
        }

        private SwingContext BuildSwingContext(bool isBuy)
        {
            var ctx = new SwingContext
            {
                Symbol = Instrument != null && Instrument.MasterInstrument != null ? Instrument.MasterInstrument.Name : "SYM",
                BarIndex = evalBarIndex,
                TimeUtc = GetVolumetricTime().ToUniversalTime(),
                Open = snOpen,
                High = snHigh,
                Low = snLow,
                Close = snClose,
                Volume = snVolume,
                TickSize = TickSize,
                PointValue = ResolvePointValue(),
                AtrCurrent = riskAtr != null && riskAtr.IsValidDataPoint(0) ? riskAtr[0] : TickSize * 10,
                AtrDaily = regimeAtr != null && regimeAtr.IsValidDataPoint(0) ? regimeAtr[0] : TickSize * 40,
                RiskPerTradeCurrency = RiskPerTradeCurrency,
                HtfTrendDirection = htfEma != null && htfEma.IsValidDataPoint(0) ? (snClose > htfEma[0] ? 1 : -1) : 0,
                HtfEma = htfEma != null && htfEma.IsValidDataPoint(0) ? htfEma[0] : 0.0,
                RegimeHtf = isBuy ? SwingMarketRegime.TrendUp : SwingMarketRegime.TrendDown,
                IsOvernightHoldAllowed = SwingAllowOvernightHold,
                InNewsWindow = false,
                NewsSeverity = 0,
                GapPercent = 0.0
            };

            // Ingestion des données Volume Profile V2 Clôturées
            if (currentVpContext != null)
            {
                if (currentVpContext.ActiveDailyProfile != null && currentVpContext.ActiveDailyProfile.Valid)
                {
                    ctx.DailyPoc = currentVpContext.ActiveDailyProfile.Poc;
                    ctx.DailyVah = currentVpContext.ActiveDailyProfile.Vah;
                    ctx.DailyVal = currentVpContext.ActiveDailyProfile.Val;
                    ctx.ClosedVwap = currentVpContext.ActiveDailyProfile.Vwap;
                    ctx.Sd1Upper = currentVpContext.ActiveDailyProfile.VwapSd1Upper;
                    ctx.Sd1Lower = currentVpContext.ActiveDailyProfile.VwapSd1Lower;
                    ctx.Sd2Upper = currentVpContext.ActiveDailyProfile.VwapSd2Upper;
                    ctx.Sd2Lower = currentVpContext.ActiveDailyProfile.VwapSd2Lower;
                    ctx.Sd3Upper = currentVpContext.ActiveDailyProfile.VwapSd3Upper;
                    ctx.Sd3Lower = currentVpContext.ActiveDailyProfile.VwapSd3Lower;
                }

                if (currentVpContext.ActiveWeeklyProfile != null && currentVpContext.ActiveWeeklyProfile.Valid)
                {
                    ctx.WeeklyPoc = currentVpContext.ActiveWeeklyProfile.Poc;
                    ctx.WeeklyVah = currentVpContext.ActiveWeeklyProfile.Vah;
                    ctx.WeeklyVal = currentVpContext.ActiveWeeklyProfile.Val;
                }

                if (currentVpContext.ActiveMonthlyProfile != null && currentVpContext.ActiveMonthlyProfile.Valid)
                {
                    ctx.MonthlyPoc = currentVpContext.ActiveMonthlyProfile.Poc;
                    ctx.MonthlyVah = currentVpContext.ActiveMonthlyProfile.Vah;
                    ctx.MonthlyVal = currentVpContext.ActiveMonthlyProfile.Val;
                }

                ctx.NearDailyPoc = currentVpContext.NearDailyPoc;
                ctx.NearDailyVah = currentVpContext.NearDailyVah;
                ctx.NearDailyVal = currentVpContext.NearDailyVal;
                ctx.NearWeeklyPoc = currentVpContext.NearWeeklyPoc;
                ctx.NearWeeklyVah = currentVpContext.NearWeeklyVah;
                ctx.NearWeeklyVal = currentVpContext.NearWeeklyVal;
                ctx.InsideHvn = currentVpContext.InsideHvn;
                ctx.InsideLvn = currentVpContext.InsideLvn;
            }

            // Order Flow & Microstructure
            ctx.BarDelta = snDelta;
            ctx.CumulativeDelta = snCumDelta;
            ctx.HasDeltaDivergence = HasCumDeltaDivergence(isBuy);
            ctx.HasAbsorptionEvidence = HasRecentAbsorption(isBuy);

            // Structure SMC (FVG, BOS)
            ctx.InFairValueGap = IsInActiveFvg(snClose, isBuy);
            ctx.HasBos = HasRecentBos(isBuy);
            ctx.HasChoch = HasRecentChoch(isBuy);

            return ctx;
        }

        private void EvaluateSwingDirection(SwingContext ctx, SwingDirection dir)
        {
            if (ctx == null || dir == SwingDirection.None) return;

            // Filtre Anti-Stacking : Pas plus d'une position Swing active dans la même direction
            if (HasOpenTradeInDirection(dir)) return;
            if (openSwingTrades.Count >= SwingMaxActiveTrades) return;

            var setupTypes = new[]
            {
                SwingSetupType.RejectExtreme,
                SwingSetupType.ValueReentry,
                SwingSetupType.BreakoutRetest,
                SwingSetupType.MacroReversal,
                SwingSetupType.HtfContinuation
            };

            foreach (var setup in setupTypes)
            {
                string rejectionReason;
                if (!swingScorer.ValidatePreconditions(ctx, setup, dir, out rejectionReason))
                    continue;

                SwingWeightedScore score = swingScorer.ComputeScore(ctx, setup, dir);
                SwingTier tier = swingScorer.ResolveTier(score.Total, SwingTierSilverScore, SwingTierGoldScore, SwingTierTresFortScore);

                if (tier == SwingTier.Aucun || score.Total < SwingMinScoreToAlert)
                    continue;

                // Construction et dimensionnement du signal
                SwingSignal signal = BuildAndSizeSignal(ctx, setup, dir, score, tier);
                if (signal != null && signal.Status == SwingSignalStatus.Validated)
                {
                    ExecuteSwingSignal(signal);
                    break; // Un seul setup prioritaire par barre
                }
            }
        }

        private SwingSignal BuildAndSizeSignal(SwingContext ctx, SwingSetupType setup, SwingDirection dir, SwingWeightedScore score, SwingTier tier)
        {
            double entry = ctx.Close;
            bool isLong = dir == SwingDirection.Long;

            // Calcul du niveau structurel de référence
            double structuralLevel = isLong ? ctx.Low - (StopBufferTicks * TickSize) : ctx.High + (StopBufferTicks * TickSize);
            if (setup == SwingSetupType.RejectExtreme && ctx.Sd2Lower > 0 && isLong)
                structuralLevel = Math.Min(structuralLevel, ctx.Sd2Lower - (StopBufferTicks * TickSize));
            else if (setup == SwingSetupType.RejectExtreme && ctx.Sd2Upper > 0 && !isLong)
                structuralLevel = Math.Max(structuralLevel, ctx.Sd2Upper + (StopBufferTicks * TickSize));

            // Calcul du Stop hybride (ATR + Structurel borné par Min/MaxStopTicks)
            double stop = swingRiskManager.CalculateHybridStop(
                entry, dir, structuralLevel, ctx.AtrCurrent, StopAtrMultiple, TickSize, MinStopTicks, MaxStopTicks);

            double stopDistTicks = Math.Abs(entry - stop) / TickSize;
            if (stopDistTicks < MinStopTicks || stopDistTicks > MaxStopTicks)
                return null;

            // Calcul des objectifs Take Profit
            double opposingLevel = isLong ? (ctx.DailyVah > entry ? ctx.DailyVah : (ctx.Sd2Upper > entry ? ctx.Sd2Upper : 0.0))
                                          : (ctx.DailyVal < entry ? ctx.DailyVal : (ctx.Sd2Lower < entry ? ctx.Sd2Lower : 0.0));

            double tp1, tp2;
            swingRiskManager.CalculateTargets(entry, stop, dir, TargetR1, TargetR2, opposingLevel, out tp1, out tp2);

            double tp1DistTicks = Math.Abs(entry - tp1) / TickSize;
            double tp2DistTicks = Math.Abs(entry - tp2) / TickSize;
            double rr1 = stopDistTicks > 0 ? tp1DistTicks / stopDistTicks : 0.0;
            double rr2 = stopDistTicks > 0 ? tp2DistTicks / stopDistTicks : 0.0;

            if (rr1 < MinRiskReward) return null;

            // Dimensionnement exact de la position selon la valeur du tick
            double tickVal = ctx.PointValue * TickSize;
            int contracts = swingRiskManager.CalculatePositionSize(
                ctx.RiskPerTradeCurrency, stopDistTicks, tickVal, ExecutionCostTicks, MaxContracts);

            if (contracts <= 0) return null;

            var signal = new SwingSignal
            {
                Symbol = ctx.Symbol,
                GeneratedTimeUtc = ctx.TimeUtc,
                Direction = dir,
                SetupType = setup,
                Tier = tier,
                Status = SwingSignalStatus.Validated,
                Score = score,
                EntryPrice = entry,
                InitialStopPrice = stop,
                StructuralStopPrice = structuralLevel,
                AtrStopPrice = isLong ? entry - (ctx.AtrCurrent * StopAtrMultiple) : entry + (ctx.AtrCurrent * StopAtrMultiple),
                Target1Price = tp1,
                Target2Price = tp2,
                StopDistanceTicks = stopDistTicks,
                Target1DistanceTicks = tp1DistTicks,
                Target2DistanceTicks = tp2DistTicks,
                RiskRewardRatio1 = rr1,
                RiskRewardRatio2 = rr2,
                PositionSizeContracts = contracts,
                EstimatedRiskCurrency = (stopDistTicks + ExecutionCostTicks) * tickVal * contracts,
                ExecutionNotes = string.Format(CultureInfo.InvariantCulture, "{0} | {1} | Score={2:F1}", setup, tier, score.Total)
            };

            return signal;
        }

        private void ExecuteSwingSignal(SwingSignal sig)
        {
            if (sig == null) return;

            var trade = new TrackedSwingTrade(sig, TickSize, ResolvePointValue());
            openSwingTrades.Add(trade);
            activeSwingSignals.Add(sig);

            // Log d'entrée dans le journal Shadow
            LogSwingTrade(trade);

            // Notification Telegram si activée
            if (EnableSwingTelegramAlerts && (sig.Tier == SwingTier.Fort || sig.Tier == SwingTier.TresFort))
            {
                string msg = string.Format(CultureInfo.InvariantCulture,
                    "🚨 <b>SWING {0} {1}</b>\n" +
                    "Instrument: <code>{2}</code> | Tier: <b>{3}</b>\n" +
                    "Setup: <b>{4}</b> | Score: <b>{5:F1}/100</b>\n" +
                    "Entrée: <code>{6:F2}</code>\n" +
                    "Stop: <code>{7:F2}</code> ({8:F0} ticks)\n" +
                    "TP1: <code>{9:F2}</code> ({10:F1}R) | TP2: <code>{11:F2}</code> ({12:F1}R)\n" +
                    "Taille: <b>{13} contrat(s)</b> | Risque: <b>${14:F2}</b>",
                    sig.Direction == SwingDirection.Long ? "ACHAT (LONG)" : "VENTE (SHORT)",
                    sig.Symbol, sig.Symbol, sig.Tier, sig.SetupType, sig.Score.Total,
                    sig.EntryPrice, sig.InitialStopPrice, sig.StopDistanceTicks,
                    sig.Target1Price, sig.RiskRewardRatio1, sig.Target2Price, sig.RiskRewardRatio2,
                    sig.PositionSizeContracts, sig.EstimatedRiskCurrency);

                QueueTelegramMessage(msg);
            }
        }

        #endregion

        #region Suivi des Trades Shadow Swing & Idempotence

        private void UpdateOpenSwingTrades()
        {
            if (openSwingTrades.Count == 0) return;

            DateTime nowUtc = GetVolumetricTime().ToUniversalTime();
            double high = snHigh;
            double low = snLow;
            double close = snClose;

            for (int i = openSwingTrades.Count - 1; i >= 0; i--)
            {
                TrackedSwingTrade t = openSwingTrades[i];
                if (t.Closed)
                {
                    openSwingTrades.RemoveAt(i);
                    continue;
                }

                t.BarsElapsed++;

                // Vérification du Stop Loss
                if (t.IsLong && low <= t.CurrentStopPrice)
                {
                    t.CloseTrade(t.CurrentStopPrice, nowUtc, "STOP_LOSS", TickSize, ResolvePointValue());
                    LogSwingTrade(t);
                    closedSwingTrades.Add(t);
                    openSwingTrades.RemoveAt(i);
                    continue;
                }
                else if (!t.IsLong && high >= t.CurrentStopPrice)
                {
                    t.CloseTrade(t.CurrentStopPrice, nowUtc, "STOP_LOSS", TickSize, ResolvePointValue());
                    LogSwingTrade(t);
                    closedSwingTrades.Add(t);
                    openSwingTrades.RemoveAt(i);
                    continue;
                }

                // Vérification de TP1 (Sortie partielle + passage à Break-Even)
                if (!t.Tp1Hit)
                {
                    if ((t.IsLong && high >= t.Target1Price) || (!t.IsLong && low <= t.Target1Price))
                    {
                        t.Tp1Hit = true;
                        // Déplacement du stop à Break-Even (+ 1 tick de sécurité)
                        t.CurrentStopPrice = t.IsLong ? t.EntryPrice + TickSize : t.EntryPrice - TickSize;
                        t.ExecutionNotes += " [TP1_HIT -> BE]";
                    }
                }

                // Vérification de TP2 (Sortie finale complète)
                if (t.Tp1Hit)
                {
                    if ((t.IsLong && high >= t.Target2Price) || (!t.IsLong && low <= t.Target2Price))
                    {
                        t.CloseTrade(t.Target2Price, nowUtc, "TAKE_PROFIT_2", TickSize, ResolvePointValue());
                        LogSwingTrade(t);
                        closedSwingTrades.Add(t);
                        openSwingTrades.RemoveAt(i);
                        continue;
                    }
                }
            }
        }

        private bool HasOpenTradeInDirection(SwingDirection dir)
        {
            bool isLong = dir == SwingDirection.Long;
            for (int i = 0; i < openSwingTrades.Count; i++)
            {
                if (openSwingTrades[i].IsLong == isLong && !openSwingTrades[i].Closed)
                    return true;
            }
            return false;
        }

        private void LogSwingTrade(TrackedSwingTrade t)
        {
            if (t == null) return;

            try
            {
                if (string.IsNullOrEmpty(resolvedSwingJournalPath))
                    resolvedSwingJournalPath = ResolveSwingJournalPath();

                lock (this)
                {
                    if (!swingJournalHeaderWritten && !File.Exists(resolvedSwingJournalPath))
                    {
                        File.WriteAllText(resolvedSwingJournalPath,
                            "TradeId,SignalId,Symbol,Direction,SetupType,Tier,Status,EntryTimeUtc,ExitTimeUtc,EntryPrice,ExitPrice,StopPrice,TP1,TP2,Contracts,RealizedR,RealizedUSD,ExitReason,Notes\n",
                            System.Text.Encoding.UTF8);
                        swingJournalHeaderWritten = true;
                    }

                    string line = string.Format(CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4},{5},{6},{7:yyyy-MM-dd HH:mm:ss},{8},{9:F2},{10:F2},{11:F2},{12:F2},{13:F2},{14},{15:F2},{16:F2},{17},\"{18}\"\n",
                        t.TradeId, t.Signal.Id, t.Signal.Symbol, t.Signal.Direction, t.Signal.SetupType, t.Signal.Tier,
                        t.Closed ? "CLOSED" : "OPEN",
                        t.EntryTimeUtc,
                        t.Closed ? t.ExitTimeUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "ACTIVE",
                        t.EntryPrice, t.Closed ? t.ExitPrice : 0.0, t.CurrentStopPrice, t.Target1Price, t.Target2Price,
                        t.PositionSizeContracts, t.RealizedR, t.RealizedPnlCurrency, t.ExitReason, t.ExecutionNotes);

                    File.AppendAllText(resolvedSwingJournalPath, line, System.Text.Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                RegisterRuntimeError("LogSwingTrade", ex);
            }
        }

        #endregion

        #region Helpers & Vérifications Microstructure

        private bool HasCumDeltaDivergence(bool isBuy)
        {
            if (isBuy)
                return snDelta > 0 && snLow <= prevBarValPrice && snClose > snOpen;
            return snDelta < 0 && snHigh >= prevBarVahPrice && snClose < snOpen;
        }

        private bool HasRecentAbsorption(bool isBuy)
        {
            return isBuy ? snDelta > 100 : snDelta < -100;
        }

        private bool IsInActiveFvg(double price, bool isBuy)
        {
            // Vérification FVG active sur barre clôturée
            return isBuy ? (snLow <= prevBarPocPrice && snClose > prevBarPocPrice)
                         : (snHigh >= prevBarPocPrice && snClose < prevBarPocPrice);
        }

        private bool HasRecentBos(bool isBuy)
        {
            return isBuy ? snClose > snOpen && snHigh > prevBarVahPrice
                         : snClose < snOpen && snLow < prevBarValPrice;
        }

        private bool HasRecentChoch(bool isBuy)
        {
            return isBuy ? snClose > prevBarPocPrice && snOpen < prevBarPocPrice
                         : snClose < prevBarPocPrice && snOpen > prevBarPocPrice;
        }

        #endregion
    }
}
