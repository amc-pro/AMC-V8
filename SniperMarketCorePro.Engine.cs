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
    public partial class SniperMarketCorePro
    {
        #region Gestion du risque
        // Calcule entrée / stop / cibles / taille de position pour le signal courant.
        private bool CalculateRiskParameters(bool isBuy, double entry, out double stopPrice, out double target1, out double target2, out double riskTicks, out double riskReward, out int positionSize, out bool riskGuardRejected)
        {
            stopPrice = 0; target1 = 0; target2 = 0;
            riskTicks = 0; riskReward = 0; positionSize = 0;
            riskGuardRejected = false;

            if (entry <= 0 || double.IsNaN(entry) || double.IsInfinity(entry)) return false;
            if (MinStopTicks <= 0 || MaxStopTicks < MinStopTicks || MaxContracts <= 0 ||
                TargetR1 <= 0 || TargetR2 < TargetR1 || MinRiskReward <= 0 ||
                ExecutionCostTicks < 0 || StopBufferTicks < 0)
            {
                riskGuardRejected = true;
                if (EnableDebugMode) Print("AMC RISK REJECT: paramètres de risque invalides.");
                return false;
            }

            // ZERO-TRUST P0: aucune donnée financière invalide ne doit être
            // remplacée par une valeur par défaut. Un fallback de TickSize à 1.0
            // pouvait transformer une configuration corrompue en trade valide.
            double tick = TickSize;
            if (double.IsNaN(tick) || double.IsInfinity(tick) || tick <= 0)
            {
                riskGuardRejected = true;
                if (EnableDebugMode) Print("AMC RISK REJECT: TickSize invalide.");
                return false;
            }

            if (volumetricBarsIndex < 0 || volumetricBarsIndex >= CurrentBars.Length ||
                CurrentBars[volumetricBarsIndex] < 0)
            {
                riskGuardRejected = true;
                if (EnableDebugMode) Print("AMC RISK REJECT: série volumétrique indisponible.");
                return false;
            }

            int atrOffset = Math.Min(evalOffset, Math.Max(0, CurrentBars[volumetricBarsIndex]));
            double atr = (riskAtr != null && riskAtr.IsValidDataPoint(atrOffset)) ? riskAtr[atrOffset] : 0;
            double atrDistance = atr > 0 ? atr * StopAtrMultiple : 0;

            // Distance structurelle : dernier niveau cle oppose au sens du trade.
            double structDistance = 0;
            if (isBuy)
            {
                double refLevel = 0;
                if (valPrice > 0 && valPrice < entry) refLevel = valPrice;
                if (vahPrice > 0 && vahPrice < entry && vahPrice > refLevel) refLevel = vahPrice;
                if (pocPrice > 0 && pocPrice < entry && pocPrice > refLevel) refLevel = pocPrice;
                if (refLevel > 0) structDistance = entry - refLevel;
            }
            else
            {
                double refLevel = double.MaxValue;
                if (vahPrice > 0 && vahPrice > entry) refLevel = vahPrice;
                if (valPrice > 0 && valPrice > entry && valPrice < refLevel) refLevel = valPrice;
                if (pocPrice > 0 && pocPrice > entry && pocPrice < refLevel) refLevel = pocPrice;
                if (refLevel != double.MaxValue) structDistance = refLevel - entry;
            }

            double risk = Math.Max(atrDistance, structDistance);
            if (risk <= 0) risk = 4 * tick;
            risk += StopBufferTicks * tick;

            // Arrondi au tick.
            risk = Math.Max(tick, Math.Round(risk / tick) * tick);

            // Un stop de moins de MinStopTicks est du bruit (sortie quasi certaine) ;
            // un stop de plus de MaxStopTicks detruit le R:R et la taille de position.
            double riskTicksRaw = risk / tick;
            if (riskTicksRaw < MinStopTicks || riskTicksRaw > MaxStopTicks)
            {
                riskGuardRejected = true;
                if (EnableDebugMode)
                    Print(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "VP RISK GUARD : stop de {0:F1} ticks hors bornes [{1};{2}] -> signal invalide.",
                        riskTicksRaw, MinStopTicks, MaxStopTicks));
                return false;
            }

            // Cap en pips : le stop ne doit pas depasser MaxStopPips de l'entree.
            if (MaxStopPips > 0 && PipSize > 0)
            {
                double maxRiskPips = MaxStopPips * PipSize;
                if (risk > maxRiskPips)
                {
                    if (EnableDebugMode)
                        Print(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "VP RISK CAP PIPS : stop de {0:F1} pips ramene a {1} pips max.",
                            risk / PipSize, MaxStopPips));
                    risk = Math.Round(maxRiskPips / tick) * tick;
                }
            }

            // Le cap en pips peut réduire le stop sous MinStopTicks : revalider
            // après TOUTE transformation de la distance de risque.
            if (double.IsNaN(risk) || double.IsInfinity(risk) || risk <= 0)
            {
                riskGuardRejected = true;
                return false;
            }
            double finalRiskTicks = risk / tick;
            if (finalRiskTicks < MinStopTicks || finalRiskTicks > MaxStopTicks)
            {
                riskGuardRejected = true;
                if (EnableDebugMode)
                    Print(string.Format(CultureInfo.InvariantCulture,
                        "AMC RISK REJECT: stop final {0:F1} ticks hors bornes [{1};{2}] après cap.",
                        finalRiskTicks, MinStopTicks, MaxStopTicks));
                return false;
            }

            stopPrice = isBuy ? entry - risk : entry + risk;

            // (bord oppose de la Value Area, HVN suivant, sinon POC). Le R:R est
            // calcule contre ce niveau : (cible - entree) / risque. Auparavant la
            // cible etait derivee du risque (R:R = TargetR1 par construction),
            // ce qui rendait le filtre MinRiskReward tautologique.
            double structTarget = FindStructuralTarget(isBuy, entry, risk);
            double minTarget = isBuy ? entry + risk * TargetR1 : entry - risk * TargetR1;

            // POINT 7 : la cible ne doit plus etre "structure OU volatilite" mais la
            // COMBINAISON des deux. FindStructuralTarget exige deja >= 1R, mais avec
            // TargetR1 = 1.5 un niveau structurel a 1.05R produisait un R:R annonce
            // inferieur a l'objectif de volatilite : on retient donc le plus ELOIGNE
            // des deux (niveau structurel, minimum ATR = risque x TargetR1), le
            // risque etant lui-meme max(ATR, structure). Un niveau structurel plus
            // lointain reste privilegie : c'est la ou la liquidite se trouve.
            if (structTarget > 0)
                target1 = isBuy ? Math.Max(structTarget, minTarget) : Math.Min(structTarget, minTarget);
            else
                target1 = minTarget;

            // Arrondi au tick pour un niveau exploitable a l'ordre.
            target1 = Math.Round(target1 / tick) * tick;

            // Cible 2 : extension au-dela de la cible 1 (ratio TargetR2/TargetR1).
            double t1Distance = Math.Abs(target1 - entry);
            double ext = TargetR1 > 0 ? Math.Max(1.0, TargetR2 / TargetR1) : 1.5;
            target2 = isBuy ? entry + t1Distance * ext : entry - t1Distance * ext;

            // Cout d'execution (spread + slippage) : il degrade le R:R reel.
            double cost = ExecutionCostTicks * tick;
            double netReward = Math.Max(0, t1Distance - cost);
            double netRisk = risk + cost;

            riskTicks = netRisk / tick;
            riskReward = netRisk > 0 ? netReward / netRisk : 0;

            // Validation finale du R:R NET après coûts d'exécution.
            // Le seuil doit porter sur la valeur réellement tradable, pas sur un
            // R:R théorique calculé avant spread/slippage.
            if (double.IsNaN(riskReward) || double.IsInfinity(riskReward) || riskReward < MinRiskReward)
            {
                riskGuardRejected = true;
                if (EnableDebugMode)
                    Print(string.Format(CultureInfo.InvariantCulture,
                        "AMC RISK REJECT: R:R net {0:0.00} < minimum {1:0.00}.", riskReward, MinRiskReward));
                return false;
            }

            if (double.IsNaN(stopPrice) || double.IsInfinity(stopPrice) ||
                double.IsNaN(target1) || double.IsInfinity(target1) ||
                double.IsNaN(target2) || double.IsInfinity(target2) ||
                (isBuy && !(stopPrice < entry && entry < target1 && target1 <= target2)) ||
                (!isBuy && !(target2 <= target1 && target1 < entry && entry < stopPrice)))
            {
                riskGuardRejected = true;
                return false;
            }

            positionSize = ComputePositionSize(netRisk);
            // contrat -> le signal est explicitement invalide (et plus seulement
            // filtre en aval).
            if (positionSize <= 0)
            {
                riskGuardRejected = true;
                return false;
            }
            return true;
        }

        // Calcule entrée / stop / cibles / taille de position pour le signal courant.
        // Le stop combine la structure (POC/VAH/VAL) et la volatilité (ATR) en retenant le plus large
        // avec un buffer de sécurité pour éviter le bruit.
        // forceCompute = true permet au mode shadow de recalculer les niveaux sans altérer EnableRiskManagement ni écraser last*.
        private bool ComputeRiskLevels(bool isBuy, double entry, bool forceCompute = false)
        {
            if (!forceCompute)
            {
                lastEntryPrice = entry;
                lastStopPrice = 0; lastTarget1 = 0; lastTarget2 = 0;
                lastRiskTicks = 0; lastRiskReward = 0; lastPositionSize = 0;
                lastRiskGuardRejected = false;
            }

            if ((!EnableRiskManagement && !forceCompute) || entry <= 0) return false;

            double stop, t1, t2, rTicks, rr;
            int posSize;
            bool guardRejected;

            bool ok = CalculateRiskParameters(isBuy, entry, out stop, out t1, out t2, out rTicks, out rr, out posSize, out guardRejected);

            if (!forceCompute)
            {
                lastStopPrice = stop;
                lastTarget1 = t1;
                lastTarget2 = t2;
                lastRiskTicks = rTicks;
                lastRiskReward = rr;
                lastPositionSize = posSize;
                lastRiskGuardRejected = guardRejected;
            }

            return ok;
        }

        // Cible structurelle : premier niveau de reference dans le sens du trade.
        // Priorite : HVN suivant (si noeuds actifs) -> bord oppose de la VA -> POC.
        // Renvoie 0 si aucun niveau exploitable au-dela d'un minimum de 1R.
        private double FindStructuralTarget(bool isBuy, double entry, double risk)
        {
            double best = 0;
            double minDist = risk; // au moins 1R de marge, sinon niveau ignore

            // POINT 4 : plus de closure Action<double> allouee a chaque appel ;
            // la logique passe par une methode statique avec "ref best".

            // 1) HVN le plus proche dans le sens du trade (zone d'aimantation).
            if (EnableNodeSetups && hvnVolumeThreshold > 0 && profileCount > 0)
            {
                // recherche au niveau du prix d'entree au lieu de scanner tout le profil.
                long entryTick = tickSize > 0 ? (long)Math.Round(entry / tickSize) : 0;
                int startIdx = LowerBoundTick(profileTicks, profileCount, entryTick);
                if (isBuy)
                {
                    for (int i = startIdx; i < profileCount; i++)
                    {
                        if (profileVols[i] < hvnVolumeThreshold) continue;
                        double lvl = profileTicks[i] * tickSize;
                        if (lvl <= entry) continue;
                        ConsiderTargetLevel(lvl, isBuy, entry, minDist, ref best);
                    }
                }
                else
                {
                    for (int i = Math.Min(startIdx, profileCount - 1); i >= 0; i--)
                    {
                        if (profileVols[i] < hvnVolumeThreshold) continue;
                        double lvl = profileTicks[i] * tickSize;
                        if (lvl >= entry) continue;
                        ConsiderTargetLevel(lvl, isBuy, entry, minDist, ref best);
                    }
                }
            }

            // 2) Bord oppose de la Value Area (seulement si la VA est complete).
            if (!valueAreaIncomplete)
                ConsiderTargetLevel(isBuy ? vahPrice : valPrice, isBuy, entry, minDist, ref best);

            // 3) POC comme cible de repli (retour a la valeur).
            ConsiderTargetLevel(pocPrice, isBuy, entry, minDist, ref best);

            return best;
        }

        // Premier index dont le tick est >= value (recherche binaire sur tableau trie).
        private static int LowerBoundTick(long[] ticks, int count, long value)
        {
            int lo = 0, hi = count;
            while (lo < hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                if (ticks[mid] < value) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        // Retient le niveau le plus PROCHE parmi ceux situes au moins a minDist
        // dans le sens du trade. Statique et sans capture : zero allocation.
        private static void ConsiderTargetLevel(double level, bool isBuy, double entry, double minDist, ref double best)
        {
            if (level <= 0) return;
            double dist = isBuy ? level - entry : entry - level;
            if (dist < minDist) return;
            if (best == 0 || dist < (isBuy ? best - entry : entry - best))
                best = level;
        }

        private int ComputePositionSize(double riskDistance)
        {
            // ZERO-TRUST P0: toute donnée de sizing invalide = REJECT.
            // Aucun fallback vers 1 contrat n'est autorisé.
            if (double.IsNaN(riskDistance) || double.IsInfinity(riskDistance) || riskDistance <= 0)
                return 0;
            if (double.IsNaN(RiskPerTradeCurrency) || double.IsInfinity(RiskPerTradeCurrency) || RiskPerTradeCurrency <= 0)
                return 0;
            if (double.IsNaN(TickSize) || double.IsInfinity(TickSize) || TickSize <= 0)
                return 0;
            if (Instrument == null || Instrument.MasterInstrument == null)
                return 0;

            double pointValue = Instrument.MasterInstrument.PointValue;
            if (double.IsNaN(pointValue) || double.IsInfinity(pointValue) || pointValue <= 0)
                return 0;

            double tickValue = pointValue * TickSize;
            if (double.IsNaN(tickValue) || double.IsInfinity(tickValue) || tickValue <= 0)
                return 0;

            double riskPerContract = (riskDistance / TickSize) * tickValue;
            if (double.IsNaN(riskPerContract) || double.IsInfinity(riskPerContract) || riskPerContract <= 0)
                return 0;

            double rawQty = Math.Floor(RiskPerTradeCurrency / riskPerContract);
            if (double.IsNaN(rawQty) || double.IsInfinity(rawQty) || rawQty < 1)
                return 0;

            int qty = rawQty > int.MaxValue ? int.MaxValue : (int)rawQty;
            if (MaxContracts <= 0) return 0;
            if (qty > MaxContracts) qty = MaxContracts;
            return qty >= 1 ? qty : 0;
        }
        #endregion

        #region Journal & Statistiques
        private string ResolveJournalPath()
        {
            try
            {
                // fichier obligatoire. Ferme le vecteur de path traversal via un
                // preset/workspace partage.
                if (!string.IsNullOrWhiteSpace(JournalFilePath))
                {
                    string full = Path.GetFullPath(JournalFilePath.Trim());
                    string fileName = Path.GetFileName(full);
                    if (full.StartsWith(@"\\", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(fileName))
                        throw new ArgumentException("Chemin de journal invalide (UNC ou nom de fichier absent).");
                    if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                        throw new ArgumentException("Nom de fichier de journal invalide.");
                    return full;
                }
                string dir = NinjaTrader.Core.Globals.UserDataDir;
                return Path.Combine(dir, "AuctionMarketCorePro_journal.csv");
            }
            catch (Exception ex)
            {
                Print("VP_Journal: chemin invalide, journal desactive (" + ex.Message + ").");
                return null;
            }
        }

        // avec le contexte fige du signal reellement diffuse (tous les filtres,
        // le cooldown et l'envoi ont deja reussi). Les statistiques du journal
        // decrivent donc exactement la population alertee.
        private void RegisterAlertedSignal(bool isBuy, double entry, int barIdx,
            string signal, int confluence, double stop, double target1, double target2)
        {
            if (!EnableTradeJournal) return;
            if (JournalLiveOnly && State != State.Realtime) return;
            if (entry <= 0 || stop <= 0 || target1 <= 0) return;

            string family = GetSignalFamily(signal);
            // Un seul suivi ouvert par famille : evite de compter dix fois le meme setup.
            // Le suivi "shadow" est une population distincte : il ne bloque pas un
            // suivi reel et reciproquement.
            for (int i = 0; i < openSignals.Count; i++)
                if (openSignals[i].Family == family && !openSignals[i].Shadow) return;

            openSignals.Add(new TrackedSignal
            {
                Time = GetVolumetricTime(),
                Signal = signal,
                Family = family,
                IsBuy = isBuy,
                Entry = entry,
                Stop = stop,
                Target1 = target1,
                Target2 = target2,
                BarIndex = barIdx,
                Confluence = confluence,
                ConfluencePercent = CurrentConfluencePercent(),
                Shadow = false,
                Reason = "ALERTE",
                SignalId = ++featureSignalCounter,
                Features = CaptureFeatures(isBuy, entry, stop, target1)
            });
        }

        // POINT 4 : motif de rejet, compact et parsable (plusieurs motifs possibles).
        private static string BuildFilterReason(bool confluenceOk, bool regimeOk,
            bool signalExpired, bool htfOk, bool riskOk, bool quotaOk, bool vaOk, bool newsOk = true)
        {
            StringBuilder sb = new StringBuilder(48);
            if (!confluenceOk) sb.Append("CONFLUENCE+");
            if (!regimeOk) sb.Append("REGIME+");
            if (signalExpired) sb.Append("EXPIRE+");
            if (!htfOk) sb.Append("HTF+");
            if (!riskOk) sb.Append("RR+");
            if (!quotaOk) sb.Append("QUOTA+");
            if (!vaOk) sb.Append("VA_INCOMPLETE+");
            if (!newsOk) sb.Append("NEWS_BLACKOUT+");
            if (sb.Length == 0) return "NON_NOUVEAU";
            return sb.ToString(0, sb.Length - 1);
        }

        private double CurrentConfluencePercent()
        {
            if (maxConfluenceWeighted <= 0) return 0;
            double pct = 100.0 * confluenceWeighted / maxConfluenceWeighted;
            return pct > 100.0 ? 100.0 : pct;
        }

        // POINT 4 : suivi d'un signal directionnel NON diffuse. Meme mesure d'issue
        // (stop / cible / timeout) que pour un signal alerte, donc directement
        // comparable : c'est ce qui permet de mesurer si un filtre cree ou detruit
        // de la performance, et de calibrer les ponderations sur une population
        // complete plutot que sur les seuls signaux retenus.
        private void RegisterShadowSignal(int barIdx, string reason)
        {
            if (!EnableTradeJournal || !JournalShadowMode) return;
            if (JournalLiveOnly && State != State.Realtime) return;
            if (string.IsNullOrEmpty(currentSignal)) return;

            // Direction : celle du signal courant, sinon le camp dominant lorsque le
            // signal a ete transforme en "Pas de trade (Conflit order flow)".
            bool isBuy;
            if (currentSignal.Contains("BUY")) isBuy = true;
            else if (currentSignal.Contains("SELL")) isBuy = false;
            else if (buySideWeight > 0 || sellSideWeight > 0) isBuy = buySideWeight >= sellSideWeight;
            else return;   // aucun signal directionnel a tracer

            string signal = currentSignal;
            if (signal.StartsWith("Pas de trade") && signalCandidates.Count > 0)
            {
                // On trace le meilleur candidat du camp retenu, plus informatif que
                // le libelle generique de rejet.
                double best = -1;
                for (int i = 0; i < signalCandidates.Count; i++)
                {
                    SignalCandidate c = signalCandidates[i];
                    if (c.IsBuy != isBuy || c.Weight <= best) continue;
                    best = c.Weight;
                    signal = c.Signal;
                }
                if (best < 0) return;
            }

            string family = GetSignalFamily(signal);
            for (int i = 0; i < openSignals.Count; i++)
                if (openSignals[i].Family == family && openSignals[i].Shadow) return;

            double entry = Closes[volumetricBarsIndex][evalOffset];
            if (entry <= 0) return;

            double shadowStop = lastStopPrice;
            double shadowT1 = lastTarget1;
            double shadowT2 = lastTarget2;

            if (shadowStop <= 0 || shadowT1 <= 0 || lastEntryPrice != entry)
            {
                double s, t1, t2, rt, rr;
                int ps;
                bool gr;
                if (CalculateRiskParameters(isBuy, entry, out s, out t1, out t2, out rt, out rr, out ps, out gr))
                {
                    shadowStop = s;
                    shadowT1 = t1;
                    shadowT2 = t2;
                }
            }

            if (shadowStop <= 0 || shadowT1 <= 0) return;

            openSignals.Add(new TrackedSignal
            {
                Time = GetVolumetricTime(),
                Signal = signal,
                Family = family,
                IsBuy = isBuy,
                Entry = entry,
                Stop = shadowStop,
                Target1 = shadowT1,
                Target2 = shadowT2,
                BarIndex = barIdx,
                Confluence = confluenceScore,
                ConfluencePercent = CurrentConfluencePercent(),
                Shadow = true,
                Reason = reason,
                // meme vecteur que la population alertee, sinon les deux ne sont pas
                // comparables et le filtrage ne peut pas etre evalue.
                SignalId = ++featureSignalCounter,
                Features = CaptureFeatures(isBuy, entry, shadowStop, shadowT1)
            });
        }

        // Evalue les signaux ouverts contre la barre qui vient de cloturer.
        // Conservateur : si stop ET cible sont touches dans la meme barre, on
        // compte la perte (hypothese defavorable).
        private void UpdateTradeJournal(int barIdx, double high, double low)
        {
            if (!EnableTradeJournal || openSignals.Count == 0) return;

            for (int i = openSignals.Count - 1; i >= 0; i--)
            {
                TrackedSignal t = openSignals[i];
                if (barIdx <= t.BarIndex) continue;

                double riskDist = Math.Abs(t.Entry - t.Stop);

                // Tant que le trailing n'est pas arme, le comportement est celui
                // d'origine (sortie a T1). Une fois arme, la position vise T2 avec
                // un stop suiveur de TrailWidthT2R x R sous le plus haut atteint.
                if (UseTrailingStop && riskDist > 0)
                {
                    double favorable = t.IsBuy ? high - t.Entry : t.Entry - low;
                    double t1Dist = Math.Abs(t.Target1 - t.Entry);
                    t.BestPrice = t.BestPrice == 0
                        ? (t.IsBuy ? high : low)
                        : (t.IsBuy ? Math.Max(t.BestPrice, high) : Math.Min(t.BestPrice, low));

                    if (!t.TrailActive && t1Dist > 0
                        && favorable >= t1Dist * (TrailingStartPercent / 100.0))
                    {
                        t.TrailActive = true;
                        // Amorce : jamais moins protecteur que le stop initial.
                        double seed = t.IsBuy
                            ? t.BestPrice - TrailWidthT2R * riskDist
                            : t.BestPrice + TrailWidthT2R * riskDist;
                        t.TrailStop = t.IsBuy ? Math.Max(seed, t.Stop) : Math.Min(seed, t.Stop);
                    }

                    if (t.TrailActive)
                    {
                        double candidate = t.IsBuy
                            ? t.BestPrice - TrailWidthT2R * riskDist
                            : t.BestPrice + TrailWidthT2R * riskDist;
                        // Le trailing ne recule jamais.
                        t.TrailStop = t.IsBuy ? Math.Max(t.TrailStop, candidate) : Math.Min(t.TrailStop, candidate);
                        // Passage a breakeven des que T1 est touche.
                        if (!t.Target1Hit && (t.IsBuy ? high >= t.Target1 : low <= t.Target1))
                        {
                            t.Target1Hit = true;
                            t.TrailStop = t.IsBuy ? Math.Max(t.TrailStop, t.Entry) : Math.Min(t.TrailStop, t.Entry);
                        }
                    }
                }

                bool stopHit = t.IsBuy ? low <= t.Stop : high >= t.Stop;
                bool targetHit = t.IsBuy ? high >= t.Target1 : low <= t.Target1;
                bool trailHit = t.TrailActive && (t.IsBuy ? low <= t.TrailStop : high >= t.TrailStop);
                bool target2Hit = t.Target2 > 0 && (t.IsBuy ? high >= t.Target2 : low <= t.Target2);

                string outcome = null;
                double rMultiple = 0;

                // Runner : tant que le trailing est arme et n'est pas touche, la
                // position reste ouverte au-dela de T1 pour viser T2.
                if (t.TrailActive && !stopHit)
                {
                    if (target2Hit)
                    {
                        outcome = "WIN_T2";
                        rMultiple = riskDist > 0 ? Math.Abs(t.Target2 - t.Entry) / riskDist : 0;
                    }
                    else if (trailHit)
                    {
                        outcome = t.Target1Hit ? "TRAIL_WIN" : "TRAIL_EXIT";
                        rMultiple = riskDist > 0
                            ? ((t.IsBuy ? t.TrailStop - t.Entry : t.Entry - t.TrailStop) / riskDist)
                            : 0;
                    }
                    else if (barIdx - t.BarIndex >= JournalMaxBarsInTrade)
                    {
                        outcome = "TIMEOUT";
                        double exitT = Closes[volumetricBarsIndex][evalOffset];
                        rMultiple = riskDist > 0 ? ((t.IsBuy ? exitT - t.Entry : t.Entry - exitT) / riskDist) : 0;
                    }

                    if (outcome == null) continue;
                    RecordOutcome(t, outcome, rMultiple);
                    openSignals.RemoveAt(i);
                    continue;
                }

                if (stopHit) { outcome = "LOSS"; rMultiple = -1.0; }
                // reellement enregistree, plus sur la constante TargetR1.
                else if (targetHit)
                {
                    outcome = "WIN";
                    double riskWin = Math.Abs(t.Entry - t.Stop);
                    rMultiple = riskWin > 0 ? Math.Abs(t.Target1 - t.Entry) / riskWin : 0;
                }
                else if (barIdx - t.BarIndex >= JournalMaxBarsInTrade)
                {
                    outcome = "TIMEOUT";
                    double exit = Closes[volumetricBarsIndex][evalOffset];
                    double risk = Math.Abs(t.Entry - t.Stop);
                    rMultiple = risk > 0 ? ((t.IsBuy ? exit - t.Entry : t.Entry - exit) / risk) : 0;
                }

                if (outcome == null) continue;

                RecordOutcome(t, outcome, rMultiple);
                openSignals.RemoveAt(i);
            }
        }

        // openSignals a chaque ouverture de session : les trades encore ouverts
        // disparaissaient sans etre comptes. Or un trade encore ouvert en fin de
        // session est, statistiquement, un trade qui n'a PAS atteint sa cible :
        // les supprimer gonflait mecaniquement le win rate affiche.
        // On les cloture donc au dernier close connu, avec l'issue SESSION_END,
        // et leur R reel (positif ou negatif) entre dans les statistiques.
        private void FlushOpenSignalsAtSessionEnd(double exitPrice)
        {
            if (openSignals.Count == 0) return;
            if (exitPrice <= 0)
            {
                // Sans prix de sortie exploitable, on ne peut pas mesurer l'issue :
                // on trace en TIMEOUT a 0R plutot que de faire disparaitre le trade.
                for (int i = openSignals.Count - 1; i >= 0; i--)
                {
                    RecordOutcome(openSignals[i], "SESSION_END", 0.0);
                    openSignals.RemoveAt(i);
                }
                return;
            }

            for (int i = openSignals.Count - 1; i >= 0; i--)
            {
                TrackedSignal t = openSignals[i];
                double risk = Math.Abs(t.Entry - t.Stop);
                double rMultiple = risk > 0
                    ? ((t.IsBuy ? exitPrice - t.Entry : t.Entry - exitPrice) / risk)
                    : 0.0;
                RecordOutcome(t, "SESSION_END", rMultiple);
                openSignals.RemoveAt(i);
            }
        }

        private void RecordOutcome(TrackedSignal t, string outcome, double rMultiple)
        {
            // POINT 4 : un suivi "shadow" est ecrit dans le CSV mais N'ALIMENTE PAS
            // les statistiques affichees (dashboard / alerte Telegram), qui doivent
            // continuer a decrire exactement la population diffusee.
            if (!t.Shadow)
            {
                FamilyStats fs;
                if (!statsByFamily.TryGetValue(t.Family, out fs))
                {
                    fs = new FamilyStats();
                    statsByFamily[t.Family] = fs;
                }

                // sur trailing) doivent etre classees, sinon toute sortie geree par
                // trailing tombait dans "Timeouts" et faussait le win rate affiche.
                if (outcome == "WIN" || outcome == "WIN_T2" || outcome == "TRAIL_WIN")
                { fs.Wins++; globalStats.Wins++; }
                else if (outcome == "LOSS") { fs.Losses++; globalStats.Losses++; }
                else if (outcome == "TRAIL_EXIT")
                {
                    if (rMultiple >= 0) { fs.Wins++; globalStats.Wins++; }
                    else { fs.Losses++; globalStats.Losses++; }
                }
                else { fs.Timeouts++; globalStats.Timeouts++; }

                fs.SumR += rMultiple;
                globalStats.SumR += rMultiple;
            }

            WriteJournalLine(t, outcome, rMultiple);
            // alertee comme shadow. C'est ce fichier qui alimentera l'apprentissage.
            WriteFeatureJournalLine(t, outcome, rMultiple);
            // alertes ; on ne persiste donc que lorsqu'elles ont reellement change.
            if (!t.Shadow) SavePersistedStats();
        }

        // (evite la corruption si deux signaux sont enregistres rapidement).
        private void WriteJournalLine(TrackedSignal t, string outcome, double rMultiple)
        {
            if (string.IsNullOrEmpty(journalPathResolved)) return;
            try
            {
                // POINT 4 : deux colonnes supplementaires (Mode, Motif) pour
                // distinguer la population alertee de la population filtree.
                string line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0:yyyy-MM-dd HH:mm:ss};{1};{2};{3};{4};{5};{6};{7};{8};{9};{10};{11:F2};{12};{13};{14:F1}\n",
                    t.Time, instrumentRoot, t.Signal, t.Family, t.IsBuy ? "BUY" : "SELL",
                    Instrument.MasterInstrument.FormatPrice(t.Entry),
                    Instrument.MasterInstrument.FormatPrice(t.Stop),
                    Instrument.MasterInstrument.FormatPrice(t.Target1),
                    Instrument.MasterInstrument.FormatPrice(t.Target2),
                    t.Confluence, outcome, rMultiple,
                    t.Shadow ? "SHADOW" : "ALERTE",
                    string.IsNullOrEmpty(t.Reason) ? "-" : t.Reason,
                    t.ConfluencePercent);

                // verrou sur le thread de donnees. La ligne part dans la file du
                // thread ecrivain, qui detient un StreamWriter bufferise persistant.
                journalHeaderWritten = true;
                if (journalWriter != null)
                    journalWriter.Enqueue(journalPathResolved,
                        "Date;Instrument;Signal;Famille;Sens;Entree;Stop;Cible1;Cible2;Confluence;Resultat;R;Mode;Motif;ConfluencePct\n",
                        line);
            }
            catch (Exception ex)
            {
                if (EnableDebugMode) Print("VP_Journal: " + ex.Message);
            }
        }

        private string GetStatsText(string family)
        {
            FamilyStats fs;
            if (statsByFamily.TryGetValue(family, out fs) && fs.Total > 0)
            {
                return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0} : {1} trades, {2:F0}% WR, {3:F2}R cumul | Global : {4} trades, {5:F0}% WR, {6:F2}R",
                    family, fs.Total, fs.WinRate, fs.SumR,
                    globalStats.Total, globalStats.WinRate, globalStats.SumR);
            }

            if (globalStats.Total > 0)
                return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Historique {0} insuffisant | Global : {1} trades, {2:F0}% WR, {3:F2}R",
                    family, globalStats.Total, globalStats.WinRate, globalStats.SumR);

            return "Aucun historique (echantillon en cours de constitution)";
        }
        #endregion

        #region Volume Profile Methods
        private void ResetSessionTrackers()
        {
            sessionHigh = double.MinValue;
            sessionLow = double.MaxValue;
            ClearProfileAggregate();
            currentCumulativeDelta = 0;
            sessionTotalVolume = 0;
            currentVwapPrice = 0;
            signalsSentCount = 0;
            sessionAlertsCount = 0;
            // trade non compte. L'appelant (ouverture de session) appelle
            // FlushOpenSignalsAtSessionEnd juste avant ; les autres appelants
            // (DataLoaded) partent d'un etat vierge legitime.
            openSignals.Clear();
            confluenceScore = 0;
            confluenceDetails = "";
            currentAbsorptionStatus = "Néant";
            isBullishAbsorptionActive = false;
            isBearishAbsorptionActive = false;
            currentAbsorptionVolume = 0;
            lastAbsorptionBarIndex = -1;
            isAbsorptionStrong = false;
            absorptionQualityFactor = 1.0;
            activeBreakoutSignal = "NONE";
            lastTriggeredSignal = "Aucun";
            lastSignalTime = DateTime.MinValue;
            lastAlertedSignal = "";
            signalTriggerBarIndex = -1;
            lastBreakoutBarIndex = -1;
            currentInterpretation = "Équilibre";
            currentSignal = "Pas de trade";
            amcCoreValidatedSignal = "";
            amcCoreSignalDirectional = false;

            breakoutPhase = BreakoutPhase.None;
            breakoutIsUp = false;
            breakoutLevel = 0;
            breakoutStartBarIdx = -1;
            breakoutLifecycleBarIdx = -1;
            acceptanceBarCount = 0;
            trapSignalThisBar = false;
            medianNodeVolume = 0;
            lvnVolumeThreshold = 0;
            hvnVolumeThreshold = 0;

            icebergHistory.Clear();
            currentIcebergSnapshot = null;
            isIcebergBullish = false;
            isIcebergBearish = false;
            isIcebergNeutral = false;
            icebergPrice = 0;
            icebergTotalAggression = 0;
            icebergNetDelta = 0;
            currentIcebergStatus = "Néant";
            lastIcebergBarIndex = -1;

            isImbalanceBullish = false;
            isImbalanceBearish = false;
            imbalancePrice = 0;
            imbalanceConsecutiveCount = 0;
            currentImbalanceStatus = "Néant";
            lastImbalanceBarIndex = -1;

            imbalanceZones.Clear();
            lastZoneRegisteredBarIdx = -1;

            fvgEngineZones.Clear();
            lastFvgRegisteredBarIdx = -1;
            lastHtfFvgRegisteredBar = -1;

            absDeltaHistoryEth.Clear();
            barRangeHistoryEth.Clear();
            barVolumeHistoryEth.Clear();
            currentBucketIsRth = true;

            signedDeltaHistory.Clear();
            cumDeltaHistory.Clear();
            barHighHistory.Clear();
            barLowHistory.Clear();
            runningCumDelta = 0;
            deltaFlipMagnitudeThreshold = 0;
            isDeltaFlipBullish = false;
            isDeltaFlipBearish = false;
            deltaFlipStrength = 0;
            currentDeltaFlipStatus = "Néant";
            isCumDeltaDivBullish = false;
            isCumDeltaDivBearish = false;
            cumDeltaDivStrength = 0;
            currentCumDeltaDivStatus = "Néant";

            isFinishedAuctionBuy = false;
            isFinishedAuctionSell = false;
            currentFinishedAuctionStatus = "Néant";
            lastFinishedAuctionBarIndex = -1;
            hasUnfinishedHigh = false;
            unfinishedHighPrice = 0;
            unfinishedHighBar = -1;
            hasUnfinishedLow = false;
            unfinishedLowPrice = 0;
            unfinishedLowBar = -1;
            barVolumeHistory.Clear();
            exhaustionDeltaThreshold = 0;
            exhaustionVolumeThreshold = 0;
            isExhaustionBuy = false;
            isExhaustionSell = false;
            exhaustionStrength = 0;
            currentExhaustionStatus = "Néant";

            ibHigh = double.MinValue;
            ibLow = double.MaxValue;
            isIbComplete = false;
            ibExtensionRatio = 0.0;
            isIbUpExtension = false;
            isIbDownExtension = false;
            currentDayType = "Non déterminé";
            dayTypeScore = 5;

            cachedAvgVolBarIdx = -1;
            cachedAvgVolume = 0;
        }

        private void ClearProfileAggregate()
        {
            foreach (var kv in includedBars) barProfilePool.Push(kv.Value);
            includedBars.Clear();
            // Compaction : évite que le tableau dense ne grossisse indéfiniment
            // sur les sessions longues à forte dérive de prix.
            if (aggVols.Length > 16384)
            {
                aggVols = new long[0];
                aggBaseTick = 0;
            }
            else if (aggMinTick <= aggMaxTick && aggVols.Length > 0)
            {
                Array.Clear(aggVols, 0, aggVols.Length);
            }
            aggMinTick = long.MaxValue;
            aggMaxTick = long.MinValue;
            aggNonZeroCount = 0;
            sessionTotalVolume = 0;
            currentCumulativeDelta = 0;
            profileCount = 0;
            profileDirty = true;
            extremesDirty = true;
            profileComputeBarIdx = -1;
        }

        // Redimensionne/recentre le tableau dense pour couvrir [lowTick, highTick].
        private void EnsureAggRange(long lowTick, long highTick)
        {
            if (aggVols.Length > 0 && lowTick >= aggBaseTick && highTick < aggBaseTick + aggVols.Length)
                return;

            long newBase = aggVols.Length == 0 ? lowTick - 256 : Math.Min(aggBaseTick, lowTick - 256);
            long newEnd = aggVols.Length == 0 ? highTick + 256 : Math.Max(aggBaseTick + aggVols.Length - 1, highTick + 256);
            int newLen = (int)(newEnd - newBase + 1);

            long[] next = new long[newLen];
            if (aggVols.Length > 0 && aggMinTick <= aggMaxTick)
            {
                int srcStart = (int)(aggMinTick - aggBaseTick);
                int dstStart = (int)(aggMinTick - newBase);
                int count = (int)(aggMaxTick - aggMinTick + 1);
                Array.Copy(aggVols, srcStart, next, dstStart, count);
            }
            aggVols = next;
            aggBaseTick = newBase;
        }

        // NOUVEAU (M2) : NIVEAUX DES SESSIONS PRECEDENTES + NAKED POC
        // C'est le meilleur rapport effort/edge de tout le fichier. Un POC
        // calcule sur une fenetre glissante est un objet STATISTIQUE : personne
        // sur le marche ne le regarde. Un POC de session precedente, lui, est un
        // objet de MARCHE : il figure sur tous les ecrans (Sierra, ATAS,
        // Quantower, Bookmap). Un "Naked POC" (POC d'une session anterieure
        // jamais retouche depuis) est l'un des edges intraday les mieux
        // documentes : le marche y revient chercher la liquidite non servie.
        private sealed class SessionLevels
        {
            public double Poc;
            public double Vah;
            public double Val;
            public double High;
            public double Low;
            public bool PocNaked;   // le POC n'a jamais ete retouche depuis sa session
        }

        // La capacite fixe assure elle-meme l'eviction du plus ancien niveau.
        private const int MaxSessionHistory = 10;
        private readonly RingBuffer<SessionLevels> sessionHistory = new RingBuffer<SessionLevels>(MaxSessionHistory);

        // Archive les niveaux de la session qui vient de se terminer.
        private void ArchiveSessionLevels()
        {
            if (pocPrice <= 0 || profileCount == 0) return;

            sessionHistory.Add(new SessionLevels
            {
                Poc = pocPrice,
                Vah = vahPrice,
                Val = valPrice,
                High = sessionHigh == double.MinValue ? pocPrice : sessionHigh,
                Low = sessionLow == double.MaxValue ? pocPrice : sessionLow,
                PocNaked = true
            });
        }

        // Un POC est "denude" tant que le prix ne l'a pas retouche. Appele une
        // fois par barre close avec le range de la barre.
        private void UpdateNakedPocs(double barHigh, double barLow)
        {
            for (int i = 0; i < sessionHistory.Count; i++)
            {
                SessionLevels s = sessionHistory[i];
                if (!s.PocNaked) continue;
                if (barLow <= s.Poc && barHigh >= s.Poc) s.PocNaked = false;
            }
        }

        // Distance au Naked POC le plus proche (cible structurelle naturelle).
        private double NearestNakedPoc(double price)
        {
            double best = 0;
            double bestDist = double.MaxValue;
            for (int i = 0; i < sessionHistory.Count; i++)
            {
                SessionLevels s = sessionHistory[i];
                if (!s.PocNaked) continue;
                double d = Math.Abs(price - s.Poc);
                if (d < bestDist) { bestDist = d; best = s.Poc; }
            }
            return best;
        }

        // niveau clef ?". Absorption, iceberg, imbalance et finished auction
        // testaient chacun leur propre copie du meme test sur POC/VAH/VAL, ce qui
        // faisait passer pour trois "confirmations independantes" trois filtres
        // conditionnes au MEME objet. On centralise, et on elargit aux niveaux de
        // session precedente qui, eux, sont reellement partages par le marche.
        private bool IsNearKeyLevel(double price, double tol, out bool isSharedLevel)
        {
            isSharedLevel = false;
            if (tol <= 0) tol = tickSize;

            double refPoc = frozenPocPrice != 0 ? frozenPocPrice : pocPrice;
            double refVah = frozenVahPrice != 0 ? frozenVahPrice : vahPrice;
            double refVal = frozenValPrice != 0 ? frozenValPrice : valPrice;

            bool near = (refPoc != 0 && Math.Abs(price - refPoc) <= tol)
                     || (refVah != 0 && Math.Abs(price - refVah) <= tol)
                     || (refVal != 0 && Math.Abs(price - refVal) <= tol);

            for (int i = 0; i < sessionHistory.Count; i++)
            {
                SessionLevels s = sessionHistory[i];
                if (Math.Abs(price - s.Poc) <= tol
                    || Math.Abs(price - s.Vah) <= tol
                    || Math.Abs(price - s.Val) <= tol
                    || Math.Abs(price - s.High) <= tol
                    || Math.Abs(price - s.Low) <= tol)
                {
                    // Niveau reellement partage par les autres participants :
                    // il merite une ponderation superieure a un niveau prive.
                    isSharedLevel = true;
                    near = true;
                    break;
                }
            }
            return near;
        }

        private bool IsNearPriorSessionLevel(double price, double tol, out bool isNakedPoc)
        {
            isNakedPoc = false;
            if (tol <= 0) tol = tickSize;
            for (int i = 0; i < sessionHistory.Count; i++)
            {
                SessionLevels s = sessionHistory[i];
                if (s.PocNaked && Math.Abs(price - s.Poc) <= tol)
                {
                    isNakedPoc = true;
                    return true;
                }
                if (Math.Abs(price - s.Poc) <= tol
                    || Math.Abs(price - s.Vah) <= tol
                    || Math.Abs(price - s.Val) <= tol
                    || Math.Abs(price - s.High) <= tol
                    || Math.Abs(price - s.Low) <= tol)
                {
                    return true;
                }
            }
            return false;
        }



        // JAMAIS au retrait d'une barre. Après quelques centaines de barres,
        // y compris des zones désormais à volume strictement 0. Comme
        // IsPriceInsideProfile ne teste que l'appartenance aux bornes, ces zones
        // vidées étaient déclarées "dans le profil" : IsLowVolumeNode y renvoyait
        // true avec une proéminence infinie, produisant un REJET LVN
        // contre-tendance à poids élevé sur CHAQUE extension de range en tendance.
        // On suit désormais le nombre de ticks à volume non nul et on resserre
        // réellement les bornes à chaque retrait.
        private void ApplyBarProfile(BarProfile bp, int sign)
        {
            if (bp.Vols == null || bp.Total == 0)
            {
                sessionTotalVolume += sign * bp.Total;
                currentCumulativeDelta += sign * bp.Delta;
                return;
            }

            long lowTick = bp.BaseTick;
            long highTick = bp.BaseTick + bp.Vols.Length - 1;
            EnsureAggRange(lowTick, highTick);

            int offset = (int)(lowTick - aggBaseTick);
            long[] agg = aggVols;
            long[] src = bp.Vols;

            for (int i = 0; i < src.Length; i++)
            {
                if (src[i] == 0) continue;
                int k = offset + i;
                long prev = agg[k];
                long next = prev + sign * src[i];
                if (next < 0) next = 0;               // garde contre la dérive d'arrondi
                agg[k] = next;

                if (prev == 0 && next > 0) aggNonZeroCount++;
                else if (prev > 0 && next == 0) aggNonZeroCount--;
            }

            if (sign > 0)
            {
                if (lowTick < aggMinTick) aggMinTick = lowTick;
                if (highTick > aggMaxTick) aggMaxTick = highTick;
            }
            else
            {
                ShrinkAggBounds();
            }

            sessionTotalVolume += sign * bp.Total;
            currentCumulativeDelta += sign * bp.Delta;
            profileDirty = true;
        }

        // Resserre les bornes sur la première/dernière colonne réellement non nulle.
        private void ShrinkAggBounds()
        {
            if (aggNonZeroCount <= 0 || aggVols.Length == 0)
            {
                aggMinTick = long.MaxValue;
                aggMaxTick = long.MinValue;
                return;
            }
            if (aggMinTick > aggMaxTick) return;

            int lo = (int)(aggMinTick - aggBaseTick);
            int hi = (int)(aggMaxTick - aggBaseTick);
            if (lo < 0) lo = 0;
            if (hi >= aggVols.Length) hi = aggVols.Length - 1;

            while (lo <= hi && aggVols[lo] == 0) lo++;
            while (hi >= lo && aggVols[hi] == 0) hi--;

            if (lo > hi)
            {
                aggMinTick = long.MaxValue;
                aggMaxTick = long.MinValue;
                return;
            }
            aggMinTick = aggBaseTick + lo;
            aggMaxTick = aggBaseTick + hi;
        }


        // Construit (ou reconstruit) la contribution d'une seule barre.
        private BarProfile BuildBarProfile(BarProfile reuse, VolumetricData barData, double barHigh, double barLow)
        {
            BarProfile bp = reuse ?? (barProfilePool.Count > 0 ? barProfilePool.Pop() : new BarProfile());

            long lowTick = (long)Math.Round(barLow / tickSize);
            long highTick = (long)Math.Round(barHigh / tickSize);
            int len = (int)(highTick - lowTick + 1);
            if (len < 1) len = 1;

            if (bp.Vols == null || bp.Vols.Length != len) bp.Vols = new long[len];
            else Array.Clear(bp.Vols, 0, len);

            long total = 0;
            for (int i = 0; i < len; i++)
            {
                long v = barData.GetTotalVolumeForPrice((lowTick + i) * tickSize);
                if (v <= 0) continue;
                bp.Vols[i] = v;
                total += v;
            }

            bp.BaseTick = lowTick;
            bp.Total = total;
            bp.Delta = barData.BarDelta;
            bp.RawVolume = barData.TotalVolume;
            bp.High = barHigh;
            bp.Low = barLow;
            return bp;
        }

        // Version incrémentale : au lieu de rebalayer LookbackBars barres à chaque tick,
        // on n'ajoute que les barres entrantes, on retire les sortantes, et on rafraîchit
        // uniquement la barre en cours quand son volume a changé.
        private void CalculateRollingVolumeProfile(int barIdx, VolumetricBarsType barsType)
        {
            // Memorise la barre de reference : sert au tie-break DETERMINISTE de la
            // Value Area et au gel du recalcul POC/VA/noeuds hors changement de barre.
            currentProfileBarIdx = barIdx;

            // de la session courante (et non a la barre 0 du chart, ce qui rescannait
            int sessionStart = Math.Max(0, Math.Min(sessionStartBarIndex, barIdx));
            int endIdx = UseSessionProfile ? sessionStart : Math.Max(0, barIdx - LookbackBars + 1);

            // 1) Retirer les barres sorties de la fenêtre.
            if (includedBars.Count > 0)
            {
                barsToDrop.Clear();
                foreach (var kv in includedBars)
                    if (kv.Key < endIdx || kv.Key > barIdx) barsToDrop.Add(kv.Key);

                for (int i = 0; i < barsToDrop.Count; i++)
                {
                    int key = barsToDrop[i];
                    BarProfile bp = includedBars[key];
                    ApplyBarProfile(bp, -1);
                    includedBars.Remove(key);
                    barProfilePool.Push(bp);
                    extremesDirty = true;
                }
            }


            // 2) Ajouter les barres entrantes + rafraîchir la barre courante.
            var highs = Highs[volumetricBarsIndex];
            var lows = Lows[volumetricBarsIndex];
            int volumesLength = barsType.Volumes.Length;
            // barIdx passe en parametre (qui peut valoir CurrentBars - 1).
            int currentAbsBar = CurrentBars[volumetricBarsIndex];

            for (int i = endIdx; i <= barIdx; i++)
            {
                if (i < 0 || i >= volumesLength) continue;
                int offset = currentAbsBar - i;
                if (offset < 0 || offset >= highs.Count)
                {
                    // POC/VAH/VAL sans aucune trace. Le compteur est expose au
                    // dashboard : la degradation devient detectable en production.
                    profileOutOfRangeCount++;
                    if (EnableDebugMode && profileOutOfRangeCount <= 20)
                        Print("VP_Profile: barre " + i + " hors bornes (offset " + offset
                            + ", currentAbsBar " + currentAbsBar + ", barIdx " + barIdx + ").");
                    continue;
                }

                VolumetricData barData = barsType.Volumes[i];
                if (barData == null) continue;

                double barHigh = highs[offset];
                double barLow = lows[offset];

                BarProfile existing;
                bool known = includedBars.TryGetValue(i, out existing);

                if (known)
                {
                    // Seule la barre courante peut encore évoluer : les barres closes
                    // gardent leur contribution telle quelle (coût nul).
                    if (i != barIdx) continue;
                    // les extremes au tick pres. L'ancienne egalite stricte sur double
                    // declenchant une reconstruction complete a chaque tick.
                    if (existing.RawVolume == (long)barData.TotalVolume
                        && SamePriceLevel(existing.High, barHigh)
                        && SamePriceLevel(existing.Low, barLow)) continue;

                    double prevHigh = existing.High;
                    double prevLow = existing.Low;

                    ApplyBarProfile(existing, -1);
                    BuildBarProfile(existing, barData, barHigh, barLow);
                    ApplyBarProfile(existing, 1);

                    // Une simple progression du range de la barre courante n'exige
                    // aucun rebalayage de la fenetre (le cas dominant intrabar) ;
                    // seule une CONTRACTION du range d'une barre qui portait un
                    // extreme impose un recalcul complet.
                    if (existing.High >= prevHigh && existing.Low <= prevLow)
                        TouchExtremes(existing.High, existing.Low);
                    else
                        extremesDirty = true;
                }
                else
                {
                    BarProfile bp = BuildBarProfile(null, barData, barHigh, barLow);
                    includedBars[i] = bp;
                    ApplyBarProfile(bp, 1);
                    // Ajout de barre : les extremes ne peuvent que s'elargir.
                    TouchExtremes(bp.High, bp.Low);
                }
            }

            // subsiste que pour les retraits de barre et les contractions de range,
            // evenements rares — plus jamais a chaque tick de volume.
            if (extremesDirty)
            {
                double hi = double.MinValue, lo = double.MaxValue;
                foreach (var kv in includedBars)
                {
                    if (kv.Value.High > hi) hi = kv.Value.High;
                    if (kv.Value.Low < lo) lo = kv.Value.Low;
                }
                sessionHigh = hi;
                sessionLow = lo;
                extremesDirty = false;
            }

            CalculateVolumeProfile();
        }


        private void TouchExtremes(double high, double low)
        {
            if (high > sessionHigh) sessionHigh = high;
            if (low < sessionLow) sessionLow = low;
        }

        // egalite stricte sur double).
        private bool SamePriceLevel(double a, double b)
        {
            double tol = tickSize > 0 ? tickSize * 0.25 : 1e-9;
            return Math.Abs(a - b) < tol;
        }

        private void CalculateVolumeProfile()
        {
            if (aggMinTick > aggMaxTick) return;

            // par barre (evaluation en barre close). Les recalculer a chaque tick
            // coutait une compaction O(span) + une boucle VA + un Array.Sort O(n log n)
            // plusieurs dizaines de fois par seconde pour un resultat identique.
            if (!forceProfileRecompute && profileCount > 0
                && profileComputeBarIdx == currentProfileBarIdx) return;
            profileComputeBarIdx = currentProfileBarIdx;
            forceProfileRecompute = false;


            // Compaction linéaire du tableau dense en paires triées (aucun tri, aucune
            // allocation en régime permanent) : O(range) au lieu de O(n log n) par tick.
            if (profileDirty)
            {

                int span = (int)(aggMaxTick - aggMinTick + 1);
                if (profileTicks.Length < span)
                {
                    profileTicks = new long[span];
                    profileVols = new long[span];
                }

                int n = 0;
                int start = (int)(aggMinTick - aggBaseTick);
                for (int i = 0; i < span; i++)
                {
                    long v = aggVols[start + i];
                    if (v <= 0) continue;
                    profileTicks[n] = aggMinTick + i;
                    profileVols[n] = v;
                    n++;
                }
                profileCount = n;
                profileDirty = false;
            }

            // Pas de profil exploitable : la Value Area est consideree incomplete.
            if (profileCount == 0) { valueAreaIncomplete = true; return; }

            int pocIdx = 0;
            long maxVol = -1;
            for (int i = 0; i < profileCount; i++)
            {
                if (profileVols[i] > maxVol)
                {
                    maxVol = profileVols[i];
                    pocIdx = i;
                }
            }

            pocPrice = profileTicks[pocIdx] * tickSize;

            long valueAreaTarget = (long)Math.Round(sessionTotalVolume * (ValueAreaPercent / 100.0));
            long areaVolume = maxVol;
            int vahIdx = pocIdx;
            int valIdx = pocIdx;

            // Sans garde-fou, la Value Area pouvait sauter un gap et s'etendre
            // jusqu'a un cluster distant. On limite l'extension aux prix contigus.
            // parametre instrument-dependant cache (3 ticks = 0,75 pt sur ES mais
            // 0,03 $ sur CL et 15 $ sur BTC). Pendant tout le warmup
            // (adaptiveAvgBarRange == 0) la Value Area etait donc declaree
            // tronquee en permanence et TOUS les setups de bord etaient bloques
            // range moyen, avec un repli sur une fraction du range du profil
            // lui-meme tant que la calibration n'est pas disponible.
            double gapRef = adaptiveAvgBarRange > 0
                ? adaptiveAvgBarRange * 0.15
                : (aggMaxTick - aggMinTick) * (tickSize > 0 ? tickSize : 1.0) * 0.02;
            int maxGapTicks = Math.Max(3, (int)Math.Round(gapRef / (tickSize > 0 ? tickSize : 1.0)));


            // Market Profile standard, repris par Sierra, ATAS, Quantower).
            // L'extension tick par tick produisait des VAH/VAL decales de
            // plusieurs ticks par rapport aux niveaux que les autres
            // participants observent - or tout l'edge d'un niveau de profil
            // des deux rangees au-dessus a la somme des deux rangees en
            // dessous, et on absorbe le cote gagnant en entier.
            // Tie-break DETERMINISTE : l'ancien vaTieBreakToggle etait un etat
            // global persistant entre barres, ce qui rendait la Value Area
            // NON REPRODUCTIBLE entre backtest, replay et temps reel (le meme
            // parite differente, donc une VA differente). La parite est
            // maintenant derivee de l'index de barre et du tick du POC : meme
            // esperance de biais nulle, mais reproductibilite totale.
            vaTieBreakToggle = (((currentProfileBarIdx + (int)(profileTicks[pocIdx] & 0x7FFFFFFF)) & 1) == 0);
            int tieStep = 0;

            while (areaVolume < valueAreaTarget)
            {
                bool canGoUp = (vahIdx + 1) < profileCount
                               && (profileTicks[vahIdx + 1] - profileTicks[vahIdx]) <= maxGapTicks;
                bool canGoDown = (valIdx - 1) >= 0
                               && (profileTicks[valIdx] - profileTicks[valIdx - 1]) <= maxGapTicks;

                if (!canGoUp && !canGoDown)
                    break;

                // Somme de la paire disponible de chaque cote (1 rangee si la
                // seconde est absente ou separee par un gap).
                int upCount = 0;
                long volAbove = 0;
                if (canGoUp)
                {
                    upCount = 1;
                    volAbove = profileVols[vahIdx + 1];
                    if ((vahIdx + 2) < profileCount
                        && (profileTicks[vahIdx + 2] - profileTicks[vahIdx + 1]) <= maxGapTicks)
                    { upCount = 2; volAbove += profileVols[vahIdx + 2]; }
                }

                int downCount = 0;
                long volBelow = 0;
                if (canGoDown)
                {
                    downCount = 1;
                    volBelow = profileVols[valIdx - 1];
                    if ((valIdx - 2) >= 0
                        && (profileTicks[valIdx - 1] - profileTicks[valIdx - 2]) <= maxGapTicks)
                    { downCount = 2; volBelow += profileVols[valIdx - 2]; }
                }

                bool preferUp;
                if (volAbove != volBelow) preferUp = volAbove > volBelow;
                else
                {
                    // Alternance deterministe : depend uniquement de la barre,
                    // du POC et du rang de l'egalite dans la boucle.
                    preferUp = vaTieBreakToggle ^ ((tieStep & 1) == 1);
                    tieStep++;
                }

                if (!canGoDown || (canGoUp && preferUp))
                {
                    for (int s = 0; s < upCount; s++) { vahIdx++; areaVolume += profileVols[vahIdx]; }
                }
                else if (canGoDown)
                {
                    for (int s = 0; s < downCount; s++) { valIdx--; areaVolume += profileVols[valIdx]; }
                }
            }


            // pour que les setups qui dependent des bords (VAH/VAL, breakout) ne
            // soient pas alertes sur des niveaux non representatifs.
            valueAreaIncomplete = areaVolume < valueAreaTarget;

            // Traiter une VA remplie a 69 % comme une VA remplie a 20 % faisait
            // perdre toute l'information. valueAreaCompleteness pondere les
            // setups de bord au lieu de les bloquer en tout ou rien.
            valueAreaCompleteness = valueAreaTarget > 0
                ? Clamp((double)areaVolume / valueAreaTarget, 0.0, 1.0)
                : 0.0;

            vahPrice = profileTicks[vahIdx] * tickSize;
            valPrice = profileTicks[valIdx] * tickSize;

            // range de barre moyen n'est pas une zone de valeur, c'est du bruit :
            // en faible volatilite, VAH-VAL pouvait tomber sous 4 ticks et les
            // rejets/breakouts de bord se declenchaient sur des oscillations de
            // tick. Les setups de bord sont neutralises dans ce cas.
            double vaWidth = vahPrice - valPrice;
            valueAreaTooNarrow = adaptiveAvgBarRange > 0 && vaWidth < adaptiveAvgBarRange;
            if (valueAreaTooNarrow) valueAreaIncomplete = true;

            ComputeVolumeNodes();

        }

        // Nœuds de volume : LVN (zones fuies par le marché) et HVN (zones de
        // consolidation). Calculés à partir du profil compacté déjà trié.
        private void ComputeVolumeNodes()
        {
            medianNodeVolume = 0;
            lvnVolumeThreshold = 0;
            hvnVolumeThreshold = 0;
            if (!EnableNodeSetups || profileCount == 0) return;

            // session (quelques dizaines de ticks echanges), la mediane n'a
            // aucune signification statistique et tout le profil se retrouve
            // classe LVN. Les noeuds restent desactives tant que le profil
            // n'est pas suffisamment construit.
            if (profileCount < MinProfileTicksForNodes) return;

            if (nodeScratch.Length < profileCount) nodeScratch = new long[profileCount];
            Array.Copy(profileVols, nodeScratch, profileCount);
            Array.Sort(nodeScratch, 0, profileCount);

            // volume nul. La mediane etait donc calculee uniquement sur les prix
            // echanges, ce qui la gonflait artificiellement : les seuils LVN/HVN
            // devenaient trop hauts et les vrais gaps (volume 0) etaient ignores.
            // les ticks vides comptant pour 0.
            int span = (int)(profileTicks[profileCount - 1] - profileTicks[0]) + 1;
            if (span < profileCount) span = profileCount;
            int emptyTicks = span - profileCount;
            int medianRank = span / 2;

            long medianFull = medianRank < emptyTicks ? 0 : nodeScratch[medianRank - emptyTicks];

            // Les seuils LVN/HVN devenaient incomparables d'une barre a l'autre,
            // et donc ininterpretables. Quand plus de la moitie de la plage est
            // vide, le profil n'est pas exploitable : on desactive les noeuds
            // plutot que de changer d'estimateur.
            medianNodeVolume = medianFull;
            if (medianNodeVolume <= 0) return;

            lvnVolumeThreshold = (long)(medianNodeVolume * (LvnThresholdPercent / 100.0));
            hvnVolumeThreshold = (long)(medianNodeVolume * (HvnThresholdPercent / 100.0));
        }


        private long VolumeAtPrice(double price)
        {
            if (aggVols.Length == 0 || tickSize <= 0) return 0;
            long tick = (long)Math.Round(price / tickSize);
            if (tick < aggMinTick || tick > aggMaxTick) return 0;
            int idx = (int)(tick - aggBaseTick);
            if (idx < 0 || idx >= aggVols.Length) return 0;
            return aggVols[idx];
        }

        // de zero, il a un volume INCONNU. VolumeAtPrice renvoie 0 hors bornes (ce qui
        // est correct pour un affichage), donc VolumeMinAround renvoyait 0 et tout prix
        // hors profil etait classe LVN. En tendance, chaque nouveau plus-bas declenchait
        // ainsi un "REJET LVN (BUY)" contre-tendance a poids ~8.75. Ces deux gardes
        // permettent de distinguer "volume faible" de "hors profil".
        private bool IsPriceInsideProfile(double price)
        {
            if (aggVols.Length == 0 || tickSize <= 0) return false;
            if (aggMinTick > aggMaxTick) return false;
            long tick = (long)Math.Round(price / tickSize);
            return tick >= aggMinTick && tick <= aggMaxTick;
        }

        // La fenetre de tolerance entiere doit etre couverte par le profil : sinon le
        // minimum local n'est pas mesurable et le noeud ne doit pas etre qualifie.
        private bool IsWindowInsideProfile(double price, int ticks)
        {
            if (tickSize <= 0) return false;
            int t = ticks < 0 ? 0 : ticks;
            return IsPriceInsideProfile(price - t * tickSize)
                && IsPriceInsideProfile(price + t * tickSize);
        }

        private long VolumeMinAround(double price, int ticks)
        {
            long min = long.MaxValue;
            for (int i = -ticks; i <= ticks; i++)
            {
                long v = VolumeAtPrice(price + i * tickSize);
                if (v < min) min = v;
            }
            return min == long.MaxValue ? 0 : min;
        }

        private long VolumeMaxAround(double price, int ticks)
        {
            long max = 0;
            for (int i = -ticks; i <= ticks; i++)
            {
                long v = VolumeAtPrice(price + i * tickSize);
                if (v > max) max = v;
            }
            return max;
        }

        // Trois conditions cumulatives, la qualification par seuil seule etant
        // insuffisante :
        //   1) la fenetre de tolerance est ENTIEREMENT couverte par le profil
        //      (hors bornes le volume est inconnu, pas faible) ;
        //   2) le minimum local passe sous le seuil LVN ;
        //   3) PROEMINENCE : le creux doit etre un vrai creux, c'est-a-dire
        //      nettement plus bas que le volume environnant. Sans cette garde,
        //      toute la queue d'un profil (ou tout un profil peu construit en
        //      debut de session) est classee LVN et genere des rejets
        //      contre-tendance a poids eleve.
        // La proeminence est mesuree sur une fenetre elargie (3x la tolerance,
        // au moins 6 ticks) : le maximum voisin doit valoir au moins
        // LvnProminenceRatio fois le minimum local.
        private const double LvnProminenceRatio = 2.5;
        // les noeuds (un profil de 12 ticks n'a pas de mediane exploitable).
        private const int MinProfileTicksForNodes = 30;

        // noeuds de forte activite. Isole, tout bord de profil satisfait la
        // condition de proeminence : la queue d'un profil est structurellement
        // decroissante, donc "proeminente" par construction. Le concept d'auction
        // theory est un CREUX ENTRE DEUX MODES (zone traversee vite entre deux
        // zones d'acceptation), pas une extremite de distribution.
        // On exige donc un maximum >= seuil HVN au-dessus ET en dessous du creux.
        private bool IsLowVolumeNode(double price, int ticks)
        {
            if (profileCount < MinProfileTicksForNodes) return false;
            if (!IsWindowInsideProfile(price, ticks)) return false;
            if (lvnVolumeThreshold <= 0 || hvnVolumeThreshold <= 0) return false;

            long localMin = VolumeMinAround(price, ticks);
            if (localMin > lvnVolumeThreshold) return false;

            int wide = Math.Max(6, ticks * 3);
            // La fenetre elargie peut deborder du profil : on la retrecit jusqu'a
            // ce qu'elle soit mesurable, sinon la proeminence n'a pas de sens.
            while (wide > ticks && !IsWindowInsideProfile(price, wide)) wide--;
            if (wide <= ticks) return false;

            // Encadrement : il faut un noeud de forte activite AU-DESSUS *et*
            // EN DESSOUS. C'est ce test qui elimine les faux LVN de bord.
            long maxAbove = 0, maxBelow = 0;
            for (int i = ticks + 1; i <= wide; i++)
            {
                long va = VolumeAtPrice(price + i * tickSize);
                long vb = VolumeAtPrice(price - i * tickSize);
                if (va > maxAbove) maxAbove = va;
                if (vb > maxBelow) maxBelow = vb;
            }
            if (maxAbove < hvnVolumeThreshold || maxBelow < hvnVolumeThreshold) return false;

            // Proeminence mesuree sur le cote le PLUS FAIBLE de l'encadrement :
            // l'ancien wideMax prenait le max global, ce qui rendait la condition
            // triviale des qu'un seul cote etait charge.
            double basis = Math.Max(1.0, localMin);
            return Math.Min(maxAbove, maxBelow) >= basis * LvnProminenceRatio;
        }


        // Machine à états du breakout :
        //   None -> Broken -> Accepted -> Retest  (setups 1 et 2)
        //   Broken -> Failed Auction              (setup 6)
        private void EvaluateBreakoutLifecycle(int barIdx, double openPrice, double highPrice,
            double lowPrice, double closePrice, bool newVahBreakout, bool newValBreakout, double volFactor)
        {
            if (!EnableAcceptanceSetups) return;

            double tol = RetestToleranceTicks * tickSize;

            // Nouvelle cassure : on (re)arme la machine. La barre de cassure est
            // déjà signalée par la section A, on ne double pas le signal ici.
            if (newVahBreakout || newValBreakout)
            {
                breakoutPhase = BreakoutPhase.Broken;
                breakoutIsUp = newVahBreakout;
                breakoutLevel = newVahBreakout ? vahPrice : valPrice;
                breakoutStartBarIdx = barIdx;
                acceptanceBarCount = 0;
                return;
            }

            if (breakoutPhase == BreakoutPhase.None) return;
            if (barIdx == breakoutLifecycleBarIdx) return;   // une évaluation par barre
            breakoutLifecycleBarIdx = barIdx;

            int age = barIdx - breakoutStartBarIdx;
            bool beyond = breakoutIsUp ? closePrice > breakoutLevel : closePrice < breakoutLevel;
            bool backInside = breakoutIsUp
                ? closePrice < (breakoutLevel - tol)
                : closePrice > (breakoutLevel + tol);
            bool deltaAgainst = breakoutIsUp ? currentBarDelta < 0 : currentBarDelta > 0;
            bool absorbAgainst = breakoutIsUp ? isBearishAbsorptionActive : isBullishAbsorptionActive;

            if (EnableFailedAuction && backInside
                && breakoutPhase == BreakoutPhase.Broken
                && age <= FailedAuctionMaxBars
                && (deltaAgainst || absorbAgainst))
            {
                double w = 4.0 * volFactor * (absorbAgainst ? 1.5 : 1.0);
                if (breakoutIsUp)
                    AddCandidate("FAILED AUCTION VAH (SELL très fort)",
                        "Enchère ratée : piège haussier, retour dans la Value Area", false, w, true);
                else
                    AddCandidate("FAILED AUCTION VAL (BUY très fort)",
                        "Enchère ratée : piège baissier, retour dans la Value Area", true, w, true);

                trapSignalThisBar = true;
                breakoutPhase = BreakoutPhase.None;
                return;
            }

            if (backInside)
            {
                breakoutPhase = BreakoutPhase.None;
                return;
            }

            switch (breakoutPhase)
            {
                case BreakoutPhase.Broken:
                    if (beyond) acceptanceBarCount++;
                    if (acceptanceBarCount >= AcceptanceBars)
                    {
                        breakoutPhase = BreakoutPhase.Accepted;
                        if (breakoutIsUp)
                            AddCandidate("ACCEPTANCE VAH (BUY fort)",
                                "Acceptance : le prix tient au-dessus de la VAH", true, 4.0 * volFactor, true);
                        else
                            AddCandidate("ACCEPTANCE VAL (SELL fort)",
                                "Acceptance : le prix tient sous la VAL", false, 4.0 * volFactor, true);
                    }
                    break;

                case BreakoutPhase.Accepted:
                {
                    bool touchedLevel = breakoutIsUp
                        ? lowPrice <= (breakoutLevel + tol)
                        : highPrice >= (breakoutLevel - tol);
                    bool rejected = breakoutIsUp ? closePrice > breakoutLevel : closePrice < breakoutLevel;
                    bool deltaOk = !RequireDeltaConfirmation
                        || (breakoutIsUp ? currentBarDelta > 0 : currentBarDelta < 0);
                    bool flowOk = breakoutIsUp
                        ? (isImbalanceBullish || isBullishAbsorptionActive)
                        : (isImbalanceBearish || isBearishAbsorptionActive);

                    double retestBarRange = highPrice - lowPrice;
                    double wickRatio = retestBarRange > 0
                        ? (breakoutIsUp ? (closePrice - lowPrice) / retestBarRange : (highPrice - closePrice) / retestBarRange)
                        : 0;
                    bool wickOk = wickRatio >= 0.40;

                    if (touchedLevel && rejected && deltaOk && wickOk)
                    {
                        double w = 6.0 * volFactor * (flowOk ? 1.3 : 1.0);
                        if (breakoutIsUp)
                            AddCandidate("RETEST VAH TENU (BUY très fort)",
                                "Breakout + Acceptance + Retest VAH rejeté", true, w, true);
                        else
                            AddCandidate("RETEST VAL TENU (SELL très fort)",
                                "Breakout + Acceptance + Retest VAL rejeté", false, w, true);
                        breakoutPhase = BreakoutPhase.Retest;
                    }
                    else if (age > RetestMaxBars)
                    {
                        breakoutPhase = BreakoutPhase.None;
                    }
                    break;
                }

                case BreakoutPhase.Retest:
                    if (age > (RetestMaxBars * 2)) breakoutPhase = BreakoutPhase.None;
                    break;
            }
        }

        private static double Clamp(double v, double lo, double hi)
        {
            if (double.IsNaN(v)) return lo;
            return v < lo ? lo : (v > hi ? hi : v);
        }

        // POINT 1 : chaque candidat est rattache a une FAMILLE de preuve. La
        // classification est centralisee ici (une seule source de verite) pour ne
        // pas avoir a modifier les ~25 sites d'appel. L'ordre des tests compte :
        // les libelles composites ("RETEST STACKED IMBALANCE", "FAILED AUCTION")
        // doivent etre captures avant les mots-cles plus generiques.
        private static int FamilyOf(string signal)
        {
            if (string.IsNullOrEmpty(signal)) return FamilyOther;

            // Epuisement (sortie) : teste avant "AUCTION" generique.
            if (signal.IndexOf("EXHAUSTION", StringComparison.Ordinal) >= 0) return FamilyExhaustion;
            if (signal.IndexOf("FINISHED AUCTION", StringComparison.Ordinal) >= 0) return FamilyExhaustion;

            // Structure du profil : niveaux, ruptures, retests de zones memorisees.
            if (signal.IndexOf("RETEST", StringComparison.Ordinal) >= 0) return FamilyStructure;
            if (signal.IndexOf("BREAKOUT", StringComparison.Ordinal) >= 0) return FamilyStructure;
            if (signal.IndexOf("FAILED AUCTION", StringComparison.Ordinal) >= 0) return FamilyStructure;
            if (signal.IndexOf("ACCEPTANCE", StringComparison.Ordinal) >= 0) return FamilyStructure;
            if (signal.IndexOf("REJET", StringComparison.Ordinal) >= 0) return FamilyStructure;

            // Flux : toutes ces preuves derivent du meme delta -> une seule famille.
            if (signal.IndexOf("ABSORPTION", StringComparison.Ordinal) >= 0) return FamilyFlow;
            if (signal.IndexOf("ICEBERG", StringComparison.Ordinal) >= 0) return FamilyFlow;
            if (signal.IndexOf("IMBALANCE", StringComparison.Ordinal) >= 0) return FamilyFlow;
            if (signal.IndexOf("DELTA FLIP", StringComparison.Ordinal) >= 0) return FamilyFlow;
            if (signal.IndexOf("DIVERGENCE", StringComparison.Ordinal) >= 0) return FamilyFlow;

            return FamilyOther;   // migration de la value area, divergences diverses
        }

        private void AddCandidate(string signal, string interpretation, bool isBuy, double weight, bool triggered)
        {
            signalCandidates.Add(new SignalCandidate
            {
                Signal = signal,
                Interpretation = interpretation,
                IsBuy = isBuy,
                Weight = weight,
                Triggered = triggered,
                Family = FamilyOf(signal)
            });
        }

        private static int CompareCandidateDesc(SignalCandidate a, SignalCandidate b)
        {
            return b.Weight.CompareTo(a.Weight);
        }

        // On ne code pas 9h30-16h00 en dur : la fenetre est parametrable et
        // exprimee dans le fuseau du graphique (celui des Times[]), donc
        // coherente quel que soit le Trading Hours template applique. La
        // fenetre peut franchir minuit (ex. instruments asiatiques) : le test
        // gere ce cas. Si la calibration par bucket est desactivee, tout est
        private bool IsRthBar(DateTime barTime)
        {
            if (!EnableSessionBucketCalibration) return true;

            int hhmm = barTime.Hour * 100 + barTime.Minute;
            int start = RthStartHHMM;
            int end = RthEndHHMM;
            if (start == end) return true;                 // fenetre degeneree
            if (start < end) return hhmm >= start && hhmm < end;
            return hhmm >= start || hhmm < end;            // fenetre a cheval sur minuit
        }

        private RingBuffer<long> ActiveAbsDeltaHistory
        { get { return currentBucketIsRth ? absDeltaHistory : absDeltaHistoryEth; } }

        private RingBuffer<double> ActiveBarRangeHistory
        { get { return currentBucketIsRth ? barRangeHistory : barRangeHistoryEth; } }

        private RingBuffer<long> ActiveBarVolumeHistory
        { get { return currentBucketIsRth ? barVolumeHistory : barVolumeHistoryEth; } }

        // Bucket de repli pendant la montee en charge : tant que le regime
        // courant n'a pas 30 observations, mieux vaut un seuil calibre sur
        // l'autre regime qu'un seuil nul (aucun signal) ou fige.
        private RingBuffer<long> FallbackAbsDeltaHistory
        { get { return currentBucketIsRth ? absDeltaHistoryEth : absDeltaHistory; } }

        private RingBuffer<long> FallbackBarVolumeHistory
        { get { return currentBucketIsRth ? barVolumeHistoryEth : barVolumeHistory; } }

        // (quantile glissant de |delta| et range moyen), au lieu de constantes fixes.
        private void UpdateAdaptiveCalibration(int barIdx, VolumetricData currentBar, double highPrice, double lowPrice)
        {
            if (barIdx == lastCalibrationBarIdx) return;
            lastCalibrationBarIdx = barIdx;

            // Le regime est fige AVANT toute ecriture : la barre alimente et lit
            // le meme bucket.
            currentBucketIsRth = IsRthBar(GetVolumetricTime());

            // distribution qui sert a la juger. Auparavant son |delta|, son
            // range et son volume etaient ajoutes AVANT le calcul du quantile,
            // puis compares a ce meme quantile : une barre exceptionnelle
            // relevait elle-meme le seuil qu'elle devait franchir (biais
            // d'auto-inclusion, qui attenue justement les evenements que l'on
            // cherche a detecter). Les seuils sont donc calcules d'abord sur
            // qu'ensuite, pour servir aux barres suivantes.
            double range = highPrice - lowPrice;

            RingBuffer<double> rangeSrc = ActiveBarRangeHistory;
            if (rangeSrc.Count < 20 && ActiveBarRangeHistory != barRangeHistory) rangeSrc = barRangeHistory;
            if (rangeSrc.Count == 0) rangeSrc = ActiveBarRangeHistory;
            if (rangeSrc.Count > 0)
            {
                double sum = 0;
                for (int i = 0; i < rangeSrc.Count; i++) sum += rangeSrc[i];
                adaptiveAvgBarRange = sum / rangeSrc.Count;
            }

            // Le quantile n'est recalcule que toutes les 10 barres (cout negligeable).
            calibrationRefreshCounter++;
            RingBuffer<long> deltaSrc = ActiveAbsDeltaHistory;
            if (deltaSrc.Count < 30) deltaSrc = FallbackAbsDeltaHistory;
            if (deltaSrc.Count >= 30 && (calibrationRefreshCounter % 10 == 1 || adaptiveDeltaThreshold == 0))
            {
                if (percentileScratch.Length < deltaSrc.Count)
                    percentileScratch = new long[Math.Max(deltaSrc.Count, AdaptiveCalibrationBars)];

                int n = deltaSrc.Count;
                deltaSrc.CopyTo(percentileScratch, 0);
                Array.Sort(percentileScratch, 0, n);

                // quand la calibration auto est active (sinon valeurs GUI conservees).
                double pAbs = AutoCalibrationV3 ? SniperProfilerAbsorptionPercentile() : AbsorptionDeltaPercentile;
                double pFlip = AutoCalibrationV3 ? SniperDeltaFlipPercentile() : DeltaFlipMinPercentile;
                double pExh = AutoCalibrationV3 ? SniperExhaustionPercentile() : ExhaustionPercentile;

                int idx = (int)Math.Floor((pAbs / 100.0) * (n - 1));
                if (idx < 0) idx = 0;
                if (idx > n - 1) idx = n - 1;
                adaptiveDeltaThreshold = (int)Math.Max(10, percentileScratch[idx]);

                int flipIdx = (int)Math.Floor((pFlip / 100.0) * (n - 1));
                if (flipIdx < 0) flipIdx = 0;
                if (flipIdx > n - 1) flipIdx = n - 1;
                deltaFlipMagnitudeThreshold = Math.Max(1, percentileScratch[flipIdx]);

                int exhIdx = (int)Math.Floor((pExh / 100.0) * (n - 1));
                if (exhIdx < 0) exhIdx = 0;
                if (exhIdx > n - 1) exhIdx = n - 1;
                exhaustionDeltaThreshold = Math.Max(1, percentileScratch[exhIdx]);

                RingBuffer<long> volSrc = ActiveBarVolumeHistory;
                if (volSrc.Count < 30) volSrc = FallbackBarVolumeHistory;
                int vn = volSrc.Count;
                if (vn >= 30)
                {
                    if (percentileScratch.Length < vn)
                        percentileScratch = new long[Math.Max(vn, AdaptiveCalibrationBars)];
                    volSrc.CopyTo(percentileScratch, 0);
                    Array.Sort(percentileScratch, 0, vn);

                    int vIdx = (int)Math.Floor((pExh / 100.0) * (vn - 1));
                    if (vIdx < 0) vIdx = 0;
                    if (vIdx > vn - 1) vIdx = vn - 1;
                    exhaustionVolumeThreshold = Math.Max(1, percentileScratch[vIdx]);
                }
            }

            if (range > 0) ActiveBarRangeHistory.Add(range);

            if (currentBar != null)
            {
                long absDelta = Math.Abs(currentBar.BarDelta);
                ActiveAbsDeltaHistory.Add(absDelta);

                // Ceux-ci incluent volontairement la barre courante : ils servent a
                // decrire la sequence, pas a fixer un seuil qui la juge.
                runningCumDelta += currentBar.BarDelta;
                signedDeltaHistory.Add(currentBar.BarDelta);
                cumDeltaHistory.Add(runningCumDelta);
                barHighHistory.Add(highPrice);
                barLowHistory.Add(lowPrice);
                ActiveBarVolumeHistory.Add(currentBar.TotalVolume);

                bidAskProbeBars++;
                if (absDelta != 0 || currentBar.TotalVolume == 0) bidAskNonZeroBars++;
                // (fenetre glissante de 50 barres) au lieu d'un diagnostic unique en
                // debut de serie. Une perte de flux BidAsk en cours de session est
                // donc detectee, et le retour du flux leve l'alerte.
                if (bidAskProbeBars >= BidAskProbeBars)
                {
                    bool missingNow = bidAskNonZeroBars == 0;
                    if (missingNow != bidAskDataMissing)
                    {
                        bidAskDataMissing = missingNow;
                        if (missingNow)
                            Print("VP_ALERTE: aucun delta non nul sur " + BidAskProbeBars + " barres. La serie "
                                + "volumetrique ne semble pas disposer de donnees BidAsk : absorption, iceberg "
                                + "et imbalance sont inactifs.");
                        else
                            Print("VP_INFO: donnees BidAsk de nouveau disponibles, order flow reactive.");
                    }
                    bidAskWarningSent = true;
                    bidAskProbeBars = 0;
                    bidAskNonZeroBars = 0;
                }
            }
        }


        // Bascule complete du flux agressif sur N barres : la moitie gauche est
        // nettement negative, la moitie droite nettement positive (ou l'inverse).
        private void EvaluateDeltaFlip()
        {
            isDeltaFlipBullish = false;
            isDeltaFlipBearish = false;
            deltaFlipStrength = 0;
            currentDeltaFlipStatus = "Néant";

            if (!EnableDeltaFlip) return;
            // En mode playback, ignorer bidAskDataMissing pour permettre les signaux
            if (bidAskDataMissing && State != State.Historical) return;

            int half = DeltaFlipLookback;
            int need = half * 2;
            if (signedDeltaHistory.Count < need) return;

            int start = signedDeltaHistory.Count - need;
            long before = 0, after = 0;
            int negBefore = 0, posBefore = 0, negAfter = 0, posAfter = 0;

            for (int i = 0; i < half; i++)
            {
                long d = signedDeltaHistory[start + i];
                before += d;
                if (d < 0) negBefore++; else if (d > 0) posBefore++;
            }
            for (int i = half; i < need; i++)
            {
                long d = signedDeltaHistory[start + i];
                after += d;
                if (d < 0) negAfter++; else if (d > 0) posAfter++;
            }

            long mag = Math.Max(1, deltaFlipMagnitudeThreshold);
            // Tolerance d'une seule barre a contre-sens du cote "avant" ; le cote
            // "apres" doit etre homogene (c'est lui qui porte le signal).
            int maxNoise = half >= 3 ? 1 : 0;

            bool bullish = posAfter == half
                           && negBefore >= half - maxNoise
                           && before <= -mag
                           && after >= mag;

            bool bearish = negAfter == half
                           && posBefore >= half - maxNoise
                           && before >= mag
                           && after <= -mag;

            if (!bullish && !bearish) return;

            long swing = Math.Abs(after - before);
            deltaFlipStrength = Clamp((double)swing / (2.0 * mag), 0.5, 3.0);

            if (bullish)
            {
                isDeltaFlipBullish = true;
                currentDeltaFlipStatus = string.Format("FLIP HAUSSIER ({0:N0} -> +{1:N0})", before, after);
            }
            else
            {
                isDeltaFlipBearish = true;
                currentDeltaFlipStatus = string.Format("FLIP BAISSIER (+{0:N0} -> {1:N0})", before, after);
            }
        }

        // Prix Higher High + Cumulative Delta Lower High  -> SELL institutionnel.
        // Prix Lower Low  + Cumulative Delta Higher Low   -> BUY institutionnel.
        // Les swings sont detectes par fractale de force CumDeltaSwingStrength.
        private void EvaluateCumDeltaDivergence()
        {
            isCumDeltaDivBullish = false;
            isCumDeltaDivBearish = false;
            cumDeltaDivStrength = 0;
            currentCumDeltaDivStatus = "Néant";

            if (!EnableCumDeltaDivergence) return;
            // En mode playback, ignorer bidAskDataMissing pour permettre les signaux
            if (bidAskDataMissing && State != State.Historical) return;

            int k = CumDeltaSwingStrength;
            int n = cumDeltaHistory.Count;
            if (n < (k * 2) + 3) return;

            int windowStart = Math.Max(k, n - CumDeltaDivergenceLookback);
            int lastValid = n - 1 - k;   // un swing a besoin de k barres a sa droite
            if (lastValid <= windowStart) return;

            int hiA = -1, hiB = -1, loA = -1, loB = -1;

            for (int i = lastValid; i >= windowStart; i--)
            {
                bool isHigh = true, isLow = true;
                double h = barHighHistory[i];
                double l = barLowHistory[i];

                for (int j = 1; j <= k; j++)
                {
                    if (barHighHistory[i - j] >= h || barHighHistory[i + j] >= h) isHigh = false;
                    if (barLowHistory[i - j] <= l || barLowHistory[i + j] <= l) isLow = false;
                    if (!isHigh && !isLow) break;
                }

                if (isHigh)
                {
                    if (hiB < 0) hiB = i;
                    else if (hiA < 0) hiA = i;
                }
                if (isLow)
                {
                    if (loB < 0) loB = i;
                    else if (loA < 0) loA = i;
                }

                if (hiA >= 0 && loA >= 0) break;
            }

            double tick = TickSize;

            if (hiA >= 0 && hiB > hiA)
            {
                double priceGap = barHighHistory[hiB] - barHighHistory[hiA];
                double cumGap = cumDeltaHistory[hiA] - cumDeltaHistory[hiB];
                // meme fenetre, pas le delta d'UNE barre.
                double refCum = CumDeltaSwingScale(hiA, hiB);
                double minGap = refCum * (CumDeltaMinDivergencePercent / 100.0);
                if (priceGap >= tick && refCum > 0 && cumGap >= minGap)
                {
                    isCumDeltaDivBearish = true;
                    cumDeltaDivStrength = Clamp(cumGap / refCum, 0.0, 3.0);
                    currentCumDeltaDivStatus = string.Format(
                        "DIV BAISSIÈRE : prix HH {0} / CumDelta {1:N0} -> {2:N0}",
                        Instrument.MasterInstrument.FormatPrice(barHighHistory[hiB]),
                        cumDeltaHistory[hiA], cumDeltaHistory[hiB]);
                }
            }

            if (!isCumDeltaDivBearish && loA >= 0 && loB > loA)
            {
                double priceGap = barLowHistory[loA] - barLowHistory[loB];
                double cumGap = cumDeltaHistory[loB] - cumDeltaHistory[loA];
                double refCum = CumDeltaSwingScale(loA, loB);
                double minGap = refCum * (CumDeltaMinDivergencePercent / 100.0);
                if (priceGap >= tick && refCum > 0 && cumGap >= minGap)
                {
                    isCumDeltaDivBullish = true;
                    cumDeltaDivStrength = Clamp(cumGap / refCum, 0.0, 3.0);
                    currentCumDeltaDivStatus = string.Format(
                        "DIV HAUSSIÈRE : prix LL {0} / CumDelta {1:N0} -> {2:N0}",
                        Instrument.MasterInstrument.FormatPrice(barLowHistory[loB]),
                        cumDeltaHistory[loA], cumDeltaHistory[loB]);
                }
            }
        }

        // Le code comparait un ecart cumule sur N barres a un seuil derive du
        // delta d'UNE barre (EffectiveAbsorptionDeltaThreshold). Comme un cumul
        // croit typiquement en sqrt(N) x sigma_barre, le rapport etait d'un
        // facteur 10 a 100 : la condition etait vraie en permanence et la force
        // saturait a 3.0 (poids 9.0 sur un maximum de 9.0).
        // On mesure donc la dispersion REELLE des increments de cumul sur la
        // fenetre du swing, puis on la met a l'echelle en marche aleatoire :
        //     scale = sigma(increments) x sqrt(nb de barres du swing)
        // Un ecart devient significatif lorsqu'il depasse ce bruit attendu, ce
        // qui rend CumDeltaMinDivergencePercent interpretable (100 % = 1 ecart
        // type de marche aleatoire) et desature la ponderation.
        private double CumDeltaSwingScale(int idxA, int idxB)
        {
            int lo = Math.Min(idxA, idxB);
            int hi = Math.Max(idxA, idxB);
            int span = hi - lo;
            if (span < 1) return 0;

            // Fenetre d'estimation : le swing lui-meme, elargi si trop court pour
            // que l'ecart type soit stable (minimum 10 increments).
            int estStart = Math.Max(1, hi - Math.Max(span, 10));
            int m = hi - estStart;
            if (m < 2) return 0;

            double mean = 0;
            for (int i = estStart; i <= hi; i++)
                mean += cumDeltaHistory[i] - cumDeltaHistory[i - 1];
            mean /= m + 1;

            double var = 0;
            for (int i = estStart; i <= hi; i++)
            {
                double d = (cumDeltaHistory[i] - cumDeltaHistory[i - 1]) - mean;
                var += d * d;
            }
            var /= m;                       // estimateur non biaise
            double sigma = Math.Sqrt(var);
            if (sigma <= 0) return 0;

            return sigma * Math.Sqrt(span);
        }

        // Au close, l'enchere est "terminee" quand l'extreme de la barre n'a
        // presque plus d'agression du cote qui l'a produit :
        //   Ask(high) <= seuil  -> epuisement ACHETEUR  -> SELL
        //   Bid(low)  <= seuil  -> epuisement VENDEUR   -> BUY
        // Filtre obligatoire de proximite POC / VAH / VAL (meme logique que
        // ImbalanceOnlyAtKeyLevels) : hors niveau clef le bruit est important sur
        // les barres a faible volume.
        // POINT 3 : le seuil "volume max a l'extreme" en constante (2 contrats)
        // rendait le module quasi inerte sur ES/NQ, ou l'extreme d'une barre porte
        // couramment des dizaines de contrats, et bruyant sur les instruments peu
        // liquides. On le rapporte au volume moyen PAR TICK de l'instrument :
        //   seuil = max(seuil utilisateur, % x volume moyen de barre / range moyen en ticks)
        // Le seuil utilisateur reste le plancher : aucun reglage existant ne devient
        // plus permissif qu'avant.
        private long EffectiveFinishedAuctionMaxVolume(int barIdx)
        {
            long floorVol = FinishedAuctionMaxVolume;
            if (!UseAdaptiveFinishedAuction) return floorVol;

            // On force le calcul (cache par barre) : l'ordre d'appel des modules ne
            // garantit pas que la moyenne ait deja ete demandee sur cette barre.
            long avgVol = GetAverageVolume(barIdx,
                BarsArray[volumetricBarsIndex].BarsType as VolumetricBarsType);
            if (avgVol <= 0) return floorVol;

            RingBuffer<double> src = ActiveBarRangeHistory;
            if (src.Count < 20) src = barRangeHistory;   // repli pendant la montee en charge
            int n = src.Count;
            if (n < 20) return floorVol;

            double sumRange = 0;
            for (int i = 0; i < n; i++) sumRange += src[i];
            double avgRange = sumRange / n;
            if (avgRange <= 0 || tickSize <= 0) return floorVol;

            double avgRangeTicks = Math.Max(1.0, avgRange / tickSize);
            double volPerTick = avgVol / avgRangeTicks;
            long adaptive = (long)Math.Round(volPerTick * (FinishedAuctionVolumePercent / 100.0));

            return Math.Max(floorVol, adaptive);
        }

        private void EvaluateFinishedAuction(VolumetricData currentBar, int barIdx,
                                             double highPrice, double lowPrice,
                                             double closePrice, double openPrice)
        {
            isFinishedAuctionBuy = false;
            isFinishedAuctionSell = false;
            currentFinishedAuctionStatus = "Néant";

            if (!EnableFinishedAuction || currentBar == null || bidAskDataMissing) return;
            if (currentBar.TotalVolume <= 0) return;
            if (EvaluateOnBarClose && lastFinishedAuctionBarIndex == barIdx) return;

            // inside bar) a structurellement un volume faible à ses extrêmes par
            // construction, ce qui déclenche de fausses Finished Auctions.
            double faBarRange = highPrice - lowPrice;
            if (adaptiveAvgBarRange > 0 && faBarRange < adaptiveAvgBarRange * 0.5) return;

            long askAtHigh = currentBar.GetAskVolumeForPrice(highPrice);
            long bidAtLow = currentBar.GetBidVolumeForPrice(lowPrice);

            // POINT 3 : seuil adaptatif a la liquidite de l'instrument.
            long faMaxVolume = EffectiveFinishedAuctionMaxVolume(barIdx);

            bool sellExhaustedBuyers = askAtHigh <= faMaxVolume;
            bool buyExhaustedSellers = bidAtLow <= faMaxVolume;

            // Nettoyage des Unfinished Auctions consommées par le prix ou expirées (>25 barres)
            if (hasUnfinishedHigh && (highPrice >= unfinishedHighPrice || barIdx - unfinishedHighBar > 25))
                hasUnfinishedHigh = false;
            if (hasUnfinishedLow && (lowPrice <= unfinishedLowPrice || barIdx - unfinishedLowBar > 25))
                hasUnfinishedLow = false;

            if (!sellExhaustedBuyers && !buyExhaustedSellers)
            {
                // Si pas d'épuisement, vérifier si une enchère agressive inachevée a eu lieu à l'extrême (Poor High / Low)
                if (askAtHigh >= faMaxVolume * 2 && closePrice >= highPrice - faBarRange * 0.35)
                {
                    hasUnfinishedHigh = true;
                    unfinishedHighPrice = highPrice;
                    unfinishedHighBar = barIdx;
                }
                if (bidAtLow >= faMaxVolume * 2 && closePrice <= lowPrice + faBarRange * 0.35)
                {
                    hasUnfinishedLow = true;
                    unfinishedLowPrice = lowPrice;
                    unfinishedLowBar = barIdx;
                }
                return;
            }

            // Une barre ne peut pas etre epuisee des deux cotes de maniere
            // exploitable : on garde le cote oppose a la cloture (rejet le plus net).
            if (sellExhaustedBuyers && buyExhaustedSellers)
            {
                double mid = (highPrice + lowPrice) / 2.0;
                if (closePrice >= mid) sellExhaustedBuyers = false;
                else buyExhaustedSellers = false;
            }

            double refPrice = sellExhaustedBuyers ? highPrice : lowPrice;

            if (FinishedAuctionOnlyAtKeyLevels)
            {
                bool shared;
                if (!IsNearKeyLevel(refPrice, FinishedAuctionKeyLevelTicks * TickSize, out shared))
                    return;
            }


            if (sellExhaustedBuyers)
            {
                isFinishedAuctionSell = true;
                hasUnfinishedHigh = false; // Vrai épuisement validé
                currentFinishedAuctionStatus = string.Format(
                    "FINISHED AUCTION HAUT {0} (Ask:{1})",
                    Instrument.MasterInstrument.FormatPrice(highPrice), askAtHigh);
            }
            else
            {
                isFinishedAuctionBuy = true;
                hasUnfinishedLow = false; // Vrai épuisement validé
                currentFinishedAuctionStatus = string.Format(
                    "FINISHED AUCTION BAS {0} (Bid:{1})",
                    Instrument.MasterInstrument.FormatPrice(lowPrice), bidAtLow);
            }

            lastFinishedAuctionBarIndex = barIdx;
        }

        // Le plus sujet aux faux positifs : declenche uniquement si
        //   |delta| >= P(ExhaustionPercentile)  ET  volume >= P(ExhaustionPercentile)
        //   ET aucun nouvel extreme sur les ExhaustionFailBars dernieres barres
        //   dans le sens de l'effort.
        // Traite comme signal de sortie / contre-tendance faible (non declencheur).
        private void EvaluateExhaustion(double highPrice, double lowPrice,
                                        double closePrice, double openPrice)
        {
            isExhaustionBuy = false;
            isExhaustionSell = false;
            exhaustionStrength = 0;
            currentExhaustionStatus = "Néant";

            if (!EnableExhaustion || bidAskDataMissing) return;
            if (exhaustionDeltaThreshold <= 0 || exhaustionVolumeThreshold <= 0) return;

            int need = ExhaustionFailBars + 1;
            int n = signedDeltaHistory.Count;
            if (n < need || barHighHistory.Count < need || barLowHistory.Count < need) return;

            long delta = signedDeltaHistory[n - 1];
            RingBuffer<long> volHist = ActiveBarVolumeHistory;
            long volume = volHist != null && volHist.Count > 0 ? volHist[volHist.Count - 1] : 0;

            if (Math.Abs(delta) < exhaustionDeltaThreshold) return;
            if (volume < exhaustionVolumeThreshold) return;

            // Echec de nouvel extreme : la barre courante ne depasse pas le plus
            // haut (resp. plus bas) des ExhaustionFailBars barres precedentes.
            double priorHigh = double.MinValue;
            double priorLow = double.MaxValue;
            for (int i = n - 1 - ExhaustionFailBars; i <= n - 2; i++)
            {
                if (i < 0) continue;
                if (barHighHistory[i] > priorHigh) priorHigh = barHighHistory[i];
                if (barLowHistory[i] < priorLow) priorLow = barLowHistory[i];
            }

            double tick = TickSize;
            double magnitude = Clamp((double)Math.Abs(delta) / Math.Max(1, exhaustionDeltaThreshold), 0.8, 2.5);

            if (delta > 0 && highPrice <= priorHigh + tick / 2.0)
            {
                // Effort acheteur maximal sans nouveau plus haut -> jambe haussiere epuisee.
                isExhaustionSell = true;
                exhaustionStrength = magnitude;
                currentExhaustionStatus = string.Format(
                    "HAUSSIÈRE épuisée (Δ+{0:N0}, Vol {1:N0}, pas de nouveau haut sur {2}b)",
                    delta, volume, ExhaustionFailBars);
            }
            else if (delta < 0 && lowPrice >= priorLow - tick / 2.0)
            {
                isExhaustionBuy = true;
                exhaustionStrength = magnitude;
                currentExhaustionStatus = string.Format(
                    "BAISSIÈRE épuisée (Δ{0:N0}, Vol {1:N0}, pas de nouveau bas sur {2}b)",
                    delta, volume, ExhaustionFailBars);
            }
        }

        // niveau actif pendant ImbalanceZoneMemoryBars barres et genere un signal
        // lorsqu'elle est retestee puis defendue.
        private void RegisterImbalanceZone(double bottom, double top, bool isBull, int levels, int barIdx, long referenceBarVolume)
        {
            if (ImbalanceZoneMemoryBars <= 0 || levels < ImbalanceZoneMinLevels) return;
            if (lastZoneRegisteredBarIdx == barIdx) return;
            lastZoneRegisteredBarIdx = barIdx;

            imbalanceZones.Add(new ImbalanceZone
            {
                Bottom = bottom,
                Top = top,
                IsBull = isBull,
                Levels = levels,
                BarIndex = barIdx,
                Retested = false,
                ReferenceBarVolume = referenceBarVolume
            });

            // le RingBuffer ne permet pas. On conserve donc List<T> mais l'eviction
            // du plus ancien se fait par lot (RemoveRange), pas element par element.
            if (imbalanceZones.Count > 64) imbalanceZones.RemoveRange(0, imbalanceZones.Count - 64);
        }

        private void PurgeImbalanceZones(int barIdx)
        {
            for (int i = imbalanceZones.Count - 1; i >= 0; i--)
            {
                if (barIdx - imbalanceZones[i].BarIndex > ImbalanceZoneMemoryBars
                    || imbalanceZones[i].Retested)
                    imbalanceZones.RemoveAt(i);
            }
        }

        private void EvaluateImbalanceZoneRetests(int barIdx, double highPrice, double lowPrice, double closePrice)
        {
            if (!EnableImbalanceDetection || ImbalanceZoneMemoryBars <= 0) return;

            PurgeImbalanceZones(barIdx);
            double tol = ImbalanceZoneRetestTicks * TickSize;

            for (int i = 0; i < imbalanceZones.Count; i++)
            {
                ImbalanceZone z = imbalanceZones[i];
                if (z.BarIndex >= barIdx) continue;   // la barre d'origine ne compte pas

                double w = Clamp(z.Levels / 3.0, 0.7, 2.5);

                if (z.IsBull
                    && lowPrice <= z.Top + tol && lowPrice >= z.Bottom - tol
                    && closePrice > z.Top
                    && (!RequireDeltaConfirmation || currentBarDelta > 0))
                {
                    z.Retested = true;
                    AddCandidate(string.Format("RETEST STACKED IMBALANCE (BUY) [{0} niv]", z.Levels),
                                 "Zone imbalance acheteuse défendue", true, 2.5 * w, true);
                }
                else if (!z.IsBull
                    && highPrice >= z.Bottom - tol && highPrice <= z.Top + tol
                    && closePrice < z.Bottom
                    && (!RequireDeltaConfirmation || currentBarDelta < 0))
                {
                    z.Retested = true;
                    AddCandidate(string.Format("RETEST STACKED IMBALANCE (SELL) [{0} niv]", z.Levels),
                                 "Zone imbalance vendeuse défendue", false, 2.5 * w, true);
                }
            }
        }

        private void EvaluateFvgZoneRetests(int barIdx, double highPrice, double lowPrice, double closePrice, double openPrice)
        {
            if (!EnableFvgRetestTrigger || FvgZoneMemoryBars <= 0) return;

            // 1. Enregistrement des nouveaux Fair Value Gaps LTF (série volumétrique)
            if (volumetricBarsIndex >= 0 && volumetricBarsIndex < BarsArray.Length
                && CurrentBars[volumetricBarsIndex] >= evalOffset + 2
                && barIdx != lastFvgRegisteredBarIdx)
            {
                double l0 = Lows[volumetricBarsIndex][evalOffset];
                double h0 = Highs[volumetricBarsIndex][evalOffset];
                double l2 = Lows[volumetricBarsIndex][evalOffset + 2];
                double h2 = Highs[volumetricBarsIndex][evalOffset + 2];

                if (l0 > h2) // Bullish FVG
                {
                    fvgEngineZones.Add(new FvgEngineZone
                    {
                        Bottom = h2,
                        Top = l0,
                        IsBull = true,
                        BarIndex = barIdx,
                        Retested = false,
                        RetestCount = 0,
                        Invalidated = false,
                        IsHtf = false
                    });
                    lastFvgRegisteredBarIdx = barIdx;
                }
                else if (h0 < l2) // Bearish FVG
                {
                    fvgEngineZones.Add(new FvgEngineZone
                    {
                        Bottom = h0,
                        Top = l2,
                        IsBull = false,
                        BarIndex = barIdx,
                        Retested = false,
                        RetestCount = 0,
                        Invalidated = false,
                        IsHtf = false
                    });
                    lastFvgRegisteredBarIdx = barIdx;
                }
            }

            // 1b. Enregistrement des Fair Value Gaps HTF (M5 / M15 / M60) si disponible
            // NOTE INDEXATION : on utilise [1] et [3] (au lieu de [0] et [2]) pour ne considérer
            // que des bougies HTF entièrement clôturées. Bar[0] est potentiellement en cours de
            // formation. Le FVG est donc : bar[3]=bougie ancienne, bar[2]=gap, bar[1]=bougie récente.
            if (EnableHtfFilter && htfBarsIndex > 0 && htfBarsIndex < BarsArray.Length
                && CurrentBars[htfBarsIndex] >= 3)
            {
                int htfBar = CurrentBars[htfBarsIndex];
                if (htfBar != lastHtfFvgRegisteredBar)
                {
                    double htfL0 = Lows[htfBarsIndex][1];
                    double htfH0 = Highs[htfBarsIndex][1];
                    double htfL2 = Lows[htfBarsIndex][3];
                    double htfH2 = Highs[htfBarsIndex][3];

                    if (htfL0 > htfH2) // HTF Bullish FVG
                    {
                        fvgEngineZones.Add(new FvgEngineZone
                        {
                            Bottom = htfH2,
                            Top = htfL0,
                            IsBull = true,
                            BarIndex = barIdx,
                            Retested = false,
                            RetestCount = 0,
                            Invalidated = false,
                            IsHtf = true
                        });
                        lastHtfFvgRegisteredBar = htfBar;
                    }
                    else if (htfH0 < htfL2) // HTF Bearish FVG
                    {
                        fvgEngineZones.Add(new FvgEngineZone
                        {
                            Bottom = htfH0,
                            Top = htfL2,
                            IsBull = false,
                            BarIndex = barIdx,
                            Retested = false,
                            RetestCount = 0,
                            Invalidated = false,
                            IsHtf = true
                        });
                        lastHtfFvgRegisteredBar = htfBar;
                    }
                }
            }

            // 2. Purge des zones expirées, invalidées ou consommées (AVANT troncature
            // pour ne pas éjecter des zones valides récentes au profit de zones mortes)
            for (int i = fvgEngineZones.Count - 1; i >= 0; i--)
            {
                if (barIdx - fvgEngineZones[i].BarIndex > FvgZoneMemoryBars 
                    || fvgEngineZones[i].Invalidated 
                    || fvgEngineZones[i].Retested)
                {
                    fvgEngineZones.RemoveAt(i);
                }
            }

            if (fvgEngineZones.Count > 64)
                fvgEngineZones.RemoveRange(0, fvgEngineZones.Count - 64);

            // 3. Évaluation du retest des zones FVG actives (avec Consequent Encroachment à 50%)
            // Garde anti-doublon : un seul signal FVG par direction et par barre
            double fvgTol = FvgZoneRetestTicks * TickSize;
            bool fvgBuyEmitted = false;
            bool fvgSellEmitted = false;
            for (int i = 0; i < fvgEngineZones.Count; i++)
            {
                FvgEngineZone fz = fvgEngineZones[i];
                if (fz.BarIndex >= barIdx || fz.Invalidated || fz.Retested) continue;

                double midCe = (fz.Top + fz.Bottom) / 2.0;

                if (fz.IsBull)
                {
                    // Invalidation si clôture nette sous le bas du FVG
                    if (closePrice < fz.Bottom - fvgTol)
                    {
                        fz.Invalidated = true;
                        continue;
                    }

                    // Test de la zone (pénétration dans le gap sans rupture)
                    bool touchedZone = lowPrice <= fz.Top + fvgTol && lowPrice >= fz.Bottom - fvgTol;
                    // Défense valide : soit clôture au-dessus du 50% (C.E.) avec barre verte (Close > Open), soit rejet net au-dessus du Top
                    bool defended = (closePrice >= midCe && closePrice > openPrice) || closePrice > fz.Top;

                    if (touchedZone && defended && !fvgBuyEmitted && (!RequireDeltaConfirmation || currentBarDelta > 0))
                    {
                        fz.RetestCount++;
                        if (fz.RetestCount >= Math.Max(1, MaxFvgRetests)) fz.Retested = true;

                        string label = fz.IsHtf ? "RETEST FVG HTF (BUY)" : "RETEST FVG (BUY)";
                        string desc = fz.IsHtf ? "Fair Value Gap HTF acheteur défendu (50% C.E.)" : "Fair Value Gap acheteur défendu (50% C.E.)";
                        double weight = fz.IsHtf ? 3.0 : 2.5;
                        AddCandidate(label, desc, true, weight, true);
                        fvgBuyEmitted = true;
                    }
                }
                else
                {
                    // Invalidation si clôture nette au-dessus du haut du FVG
                    if (closePrice > fz.Top + fvgTol)
                    {
                        fz.Invalidated = true;
                        continue;
                    }

                    // Test de la zone (pénétration dans le gap sans rupture)
                    bool touchedZone = highPrice >= fz.Bottom - fvgTol && highPrice <= fz.Top + fvgTol;
                    // Défense valide : soit clôture sous le 50% (C.E.) avec barre rouge (Close < Open), soit rejet net sous le Bottom
                    bool defended = (closePrice <= midCe && closePrice < openPrice) || closePrice < fz.Bottom;

                    if (touchedZone && defended && !fvgSellEmitted && (!RequireDeltaConfirmation || currentBarDelta < 0))
                    {
                        fz.RetestCount++;
                        if (fz.RetestCount >= Math.Max(1, MaxFvgRetests)) fz.Retested = true;

                        string label = fz.IsHtf ? "RETEST FVG HTF (SELL)" : "RETEST FVG (SELL)";
                        string desc = fz.IsHtf ? "Fair Value Gap HTF vendeur défendu (50% C.E.)" : "Fair Value Gap vendeur défendu (50% C.E.)";
                        double weight = fz.IsHtf ? 3.0 : 2.5;
                        AddCandidate(label, desc, false, weight, true);
                        fvgSellEmitted = true;
                    }
                }
            }
        }

        private int EffectiveAbsorptionDeltaThreshold()
        {
            // (bucket horaire courant, encadre plancher/plafond). Repli automatique
            if (SniperV3Ready()) return SniperV3DeltaThreshold();

            if (!UseAdaptiveAbsorptionThreshold || adaptiveDeltaThreshold <= 0)
                return AbsorptionDeltaThreshold;
            return adaptiveDeltaThreshold;
        }

        private int EffectiveAbsorptionTickVolumeThreshold()
        {
            if (!UseAdaptiveAbsorptionThreshold || adaptiveDeltaThreshold <= 0)
                return AbsorptionTickVolumeThreshold;
            // Le seuil par tick suit la meme echelle que le seuil de delta.
            double ratio = (double)adaptiveDeltaThreshold / Math.Max(1, AbsorptionDeltaThreshold);
            return (int)Math.Max(5, Math.Round(AbsorptionTickVolumeThreshold * Clamp(ratio, 0.25, 4.0)));
        }

        private double EffectiveMovementThreshold()
        {
            double baseThreshold = MovementThresholdTicks * TickSize;
            if (!UseAdaptiveMovementThreshold || adaptiveAvgBarRange <= 0)
                return baseThreshold;
            return Math.Max(baseThreshold, MovementAtrFactor * adaptiveAvgBarRange);
        }

        private void EvaluateVolumeProfileSignal()
        {
            double tick = TickSize;
            double threshold = EffectiveMovementThreshold();

            bool pocUp = prevBarPocPrice != 0 && (pocPrice - prevBarPocPrice) >= threshold;
            bool pocDown = prevBarPocPrice != 0 && (prevBarPocPrice - pocPrice) >= threshold;
            bool pocStable = prevBarPocPrice != 0 && Math.Abs(pocPrice - prevBarPocPrice) < threshold;

            bool vahUp = prevBarVahPrice != 0 && (vahPrice - prevBarVahPrice) >= threshold;
            bool valUp = prevBarValPrice != 0 && (valPrice - prevBarValPrice) >= threshold;

            bool vahDown = prevBarVahPrice != 0 && (prevBarVahPrice - vahPrice) >= threshold;
            bool valDown = prevBarValPrice != 0 && (prevBarValPrice - valPrice) >= threshold;

            int barIdx = CurrentBars[volumetricBarsIndex] - evalOffset;
            if (barIdx < 0) return;
            evalBarIndex = barIdx;

            double closePrice = Closes[volumetricBarsIndex][evalOffset];
            double openPrice = Opens[volumetricBarsIndex][evalOffset];
            double highPrice = Highs[volumetricBarsIndex][evalOffset];
            double lowPrice = Lows[volumetricBarsIndex][evalOffset];

            // Suivi des signaux ouverts (stop / cible) sur la barre cloturee.
            UpdateTradeJournal(barIdx, highPrice, lowPrice);

            // pour eviter un IndexOutOfRange au tout debut de session.
            bool hasPrevClose = barIdx > 0 && (evalOffset + 1) < Closes[volumetricBarsIndex].Count;
            bool priceUp = hasPrevClose && closePrice > Closes[volumetricBarsIndex][evalOffset + 1];
            bool priceDown = hasPrevClose && closePrice < Closes[volumetricBarsIndex][evalOffset + 1];

            currentBarDelta = 0;
            long currentBarVolume = 0;
            VolumetricBarsType barsType = BarsArray[volumetricBarsIndex].BarsType as VolumetricBarsType;
            VolumetricData currentBar = null;
            if (barsType != null && barIdx >= 0 && barIdx < barsType.Volumes.Length)
            {
                currentBar = barsType.Volumes[barIdx];
                if (currentBar != null)
                {
                    currentBarDelta = currentBar.BarDelta;
                    currentBarVolume = currentBar.TotalVolume;
                }
            }

            UpdateTrendFilters();

            // Calibration statistique (seuils adaptatifs) avant toute detection.
            UpdateAdaptiveCalibration(barIdx, currentBar, highPrice, lowPrice);

            UpdateInitialBalance(barIdx, highPrice, lowPrice);

            EvaluateAbsorption(currentBar, highPrice, lowPrice, closePrice, openPrice);
            EvaluateIceberg(currentBar, barIdx, highPrice, lowPrice, barsType);
            EvaluateImbalance(currentBar, barIdx, highPrice, lowPrice);
            EvaluateDeltaFlip();
            EvaluateCumDeltaDivergence();
            EvaluateFinishedAuction(currentBar, barIdx, highPrice, lowPrice, closePrice, openPrice);
            EvaluateExhaustion(highPrice, lowPrice, closePrice, openPrice);

            // Volume moyen glissant — calculé une seule fois par barre (cache).
            long avgVolume = GetAverageVolume(barIdx, barsType);

            // par une COLLECTE de tous les signaux declenches sur la barre, chacun
            // pondere par son intensite reelle. Le signal principal est celui dont
            // la preuve est la plus forte, et la liste complete est diffusee : une
            // absorption simultanee d'un breakout n'est plus invisible.
            string rawSignal = "Pas de trade";
            string rawInterpretation = "Équilibre";
            bool triggeredThisBar = false;

            signalCandidates.Clear();
            triggeredSignalsThisBar.Clear();
            buySideWeight = 0;
            sellSideWeight = 0;
            allSignalsText = "";
            trapSignalThisBar = false;

            double barRange = highPrice - lowPrice;
            double volFactor = avgVolume > 0 ? Clamp((double)currentBarVolume / avgVolume, 0.5, 2.5) : 1.0;
            double absRef = Math.Max(1.0, EffectiveAbsorptionDeltaThreshold());

            bool hasBreakoutVolume = (avgVolume == 0 || currentBarVolume >= (long)(avgVolume * 2.0));
            bool isVahBreakout = EnableBreakoutSignals && openPrice <= vahPrice && closePrice > vahPrice;
            bool isValBreakout = EnableBreakoutSignals && openPrice >= valPrice && closePrice < valPrice;

            bool newVahBreakout = isVahBreakout && hasBreakoutVolume
                && (!RequireDeltaConfirmation || currentBarDelta > 0) && lastBreakoutBarIndex != barIdx;
            bool newValBreakout = isValBreakout && hasBreakoutVolume
                && (!RequireDeltaConfirmation || currentBarDelta < 0) && lastBreakoutBarIndex != barIdx;

            if (newVahBreakout)
            {
                activeBreakoutSignal = "BUY";
                lastBreakoutBarIndex = barIdx;
            }
            else if (newValBreakout)
            {
                activeBreakoutSignal = "SELL";
                lastBreakoutBarIndex = barIdx;
            }
            else
            {
                if (activeBreakoutSignal == "BUY" && closePrice <= vahPrice) activeBreakoutSignal = "NONE";
                if (activeBreakoutSignal == "SELL" && closePrice >= valPrice) activeBreakoutSignal = "NONE";
            }

            if (newVahBreakout)
                AddCandidate("BREAKOUT VAH (BUY)", "Breakout VAH", true, 3.0 * volFactor, true);
            else if (activeBreakoutSignal == "BUY" && closePrice > vahPrice)
                AddCandidate("BREAKOUT VAH (BUY)", "Breakout VAH (En cours)", true, 1.5, false);

            if (newValBreakout)
                AddCandidate("BREAKOUT VAL (SELL)", "Breakout VAL", false, 3.0 * volFactor, true);
            else if (activeBreakoutSignal == "SELL" && closePrice < valPrice)
                AddCandidate("BREAKOUT VAL (SELL)", "Breakout VAL (En cours)", false, 1.5, false);

            double absFactor = Clamp(currentAbsorptionVolume / absRef, 0.5, 3.0) * absorptionQualityFactor;
            if (isBullishAbsorptionActive)
                AddCandidate("ABSORPTION ACHETEUR (BUY)", "Absorption Acheteuse", true, 3.0 * absFactor, true);
            if (isBearishAbsorptionActive)
                AddCandidate("ABSORPTION VENDEUR (SELL)", "Absorption Vendeuse", false, 3.0 * absFactor, true);

            double iceRef = SniperV3Ready()
                ? Math.Max(1.0, (double)EffectiveAbsorptionDeltaThreshold() * Math.Max(1, IcebergLookbackBars) / 2.0)
                : IcebergMinAggression;
            double iceFactor = iceRef > 0
                ? Clamp((double)icebergTotalAggression / iceRef, 0.5, 3.0)
                : 1.0;
            if (isIcebergBullish)
                AddCandidate("ICEBERG ACHETEUR (BUY)", "Iceberg Acheteur", true, 2.5 * iceFactor, true);
            if (isIcebergBearish)
                AddCandidate("ICEBERG VENDEUR (SELL)", "Iceberg Vendeur", false, 2.5 * iceFactor, true);

            // candidat autonome d'entrée (poids 0, non déclencheur). Elle sert uniquement
            // à enregistrer la zone (memorisation). Seul le RETEST de zone défendue
            // déclenche un signal d'entrée.
            if (isImbalanceBullish)
                AddCandidate("IMBALANCE ACHETEUR (zone)", "Imbalance Acheteuse (FVG)", true, 0.0, false);
            if (isImbalanceBearish)
                AddCandidate("IMBALANCE VENDEUR (zone)", "Imbalance Vendeuse (FVG)", false, 0.0, false);

            EvaluateImbalanceZoneRetests(barIdx, highPrice, lowPrice, closePrice);
            EvaluateFvgZoneRetests(barIdx, highPrice, lowPrice, closePrice, openPrice);

            // Delta Flip : réactivé comme déclencheur autonome avec poids 2.5 pour capter les V-bottoms / V-tops
            if (isDeltaFlipBullish)
                AddCandidate("DELTA FLIP (BUY)", "Bascule du flux agressif", true, 2.5, true);
            if (isDeltaFlipBearish)
                AddCandidate("DELTA FLIP (SELL)", "Bascule du flux agressif", false, 2.5, true);

            if (isCumDeltaDivBullish)
                AddCandidate("DIVERGENCE CUM. DELTA (BUY)", "Divergence Cumulative Delta", true, 3.0 * cumDeltaDivStrength, true);
            if (isCumDeltaDivBearish)
                AddCandidate("DIVERGENCE CUM. DELTA (SELL)", "Divergence Cumulative Delta", false, 3.0 * cumDeltaDivStrength, true);

            if (isFinishedAuctionBuy)
                AddCandidate("FINISHED AUCTION BAS (BUY)", "Épuisement vendeur au plus bas", true, 2.8 * volFactor, true);
            if (isFinishedAuctionSell)
                AddCandidate("FINISHED AUCTION HAUT (SELL)", "Épuisement acheteur au plus haut", false, 2.8 * volFactor, true);

            // POINT 2 : l'exhaustion portait encore un poids (1.2 x force) qui
            // alimentait buySideWeight/sellSideWeight, donc la selection du signal
            // principal et le filtre de conflit : un signal de SORTIE pesait sur une
            // informatif, visible dans l'alerte et le dashboard, sans effet directionnel.
            if (isExhaustionBuy)
                AddCandidate("EXHAUSTION BAISSIÈRE (sortie short, info)",
                             "Épuisement de la jambe baissière", true, 0.0, false);
            if (isExhaustionSell)
                AddCandidate("EXHAUSTION HAUSSIÈRE (sortie long, info)",
                             "Épuisement de la jambe haussière", false, 0.0, false);

            if (EnableRejectionSignals && lowPrice <= valPrice && closePrice > valPrice
                && (!RequireDeltaConfirmation || currentBarDelta > 0))
            {
                double wick = barRange > 0 ? Clamp((closePrice - lowPrice) / barRange, 0.3, 1.0) : 0.5;
                AddCandidate("REJET VAL (BUY)", "Support VAL", true, 2.0 * (0.5 + wick), true);
            }
            if (EnableRejectionSignals && highPrice >= vahPrice && closePrice < vahPrice
                && (!RequireDeltaConfirmation || currentBarDelta < 0))
            {
                double wick = barRange > 0 ? Clamp((highPrice - closePrice) / barRange, 0.3, 1.0) : 0.5;
                AddCandidate("REJET VAH (SELL)", "Résistance VAH", false, 2.0 * (0.5 + wick), true);
            }

            // indicateur RETARDÉ (il décrit ce qui vient de se passer, pas ce qui
            // va se passer). FP estimé 70-80% en tant que signal d'entrée. Un
            // déplacement complet est une information de CONTEXTE (régime trend),
            // pas un signal d'entrée.
            // if (pocUp && vahUp && valUp)
            //     AddCandidate("BUY très fort", "Valeur monte", true, 1.8, true);
            // if (pocDown && vahDown && valDown)
            //     AddCandidate("SELL très fort", "Valeur baisse", false, 1.8, true);

            // Confluence obligatoire renforcée : absorption ou inversion de delta
            // au POC multiplie le poids (le POC est un aimant, le rejet net compte).
            if (EnableRejectionSignals && lowPrice <= pocPrice && closePrice > pocPrice && openPrice > pocPrice
                && (!RequireDeltaConfirmation || currentBarDelta > 0))
            {
                double w = 1.5 * (isBullishAbsorptionActive ? 2.0 : 1.0) * (currentBarDelta > 0 ? 1.2 : 1.0);
                AddCandidate("REJET POC (BUY)", "Support POC", true, w, true);
            }
            if (EnableRejectionSignals && highPrice >= pocPrice && closePrice < pocPrice && openPrice < pocPrice
                && (!RequireDeltaConfirmation || currentBarDelta < 0))
            {
                double w = 1.5 * (isBearishAbsorptionActive ? 2.0 : 1.0) * (currentBarDelta < 0 ? 1.2 : 1.0);
                AddCandidate("REJET POC (SELL)", "Résistance POC", false, w, true);
            }

            // Le POC monte parce que le volume se concentre plus haut (début de
            // tendance haussière) tandis que le prix corrige : fonctionnement normal.
            // FP estimé 80-90%. Signal quasi-permanent en première heure de session.
            // if (priceDown && pocUp)
            //     AddCandidate("BUY potentiel", "Divergence", true, 1.0, true);
            // if (priceUp && pocDown)
            //     AddCandidate("SELL potentiel", "Divergence", false, 1.0, true);

            EvaluateBreakoutLifecycle(barIdx, openPrice, highPrice, lowPrice, closePrice,
                                      newVahBreakout, newValBreakout, volFactor);

            if (EnableNodeSetups && medianNodeVolume > 0)
            {
                double nodeTol = NodeToleranceTicks * tickSize;
                // des bornes du profil n'est pas un LVN, c'est une extension de range :
                // le volume y est inconnu, pas faible.
                bool lowInLvn = IsLowVolumeNode(lowPrice, NodeToleranceTicks);
                bool highInLvn = IsLowVolumeNode(highPrice, NodeToleranceTicks);

                if (lowInLvn && closePrice > lowPrice + nodeTol
                    && (!RequireDeltaConfirmation || currentBarDelta > 0))
                {
                    double w = 3.5 * volFactor * (isBullishAbsorptionActive ? 1.6 : 1.0);
                    AddCandidate("REJET LVN (BUY)", "Rejet Low Volume Node", true, w, true);
                }
                if (highInLvn && closePrice < highPrice - nodeTol
                    && (!RequireDeltaConfirmation || currentBarDelta < 0))
                {
                    double w = 3.5 * volFactor * (isBearishAbsorptionActive ? 1.6 : 1.0);
                    AddCandidate("REJET LVN (SELL)", "Rejet Low Volume Node", false, w, true);
                }

                bool openInHvn = VolumeMaxAround(openPrice, NodeToleranceTicks) >= hvnVolumeThreshold;
                bool closeOutHvn = VolumeAtPrice(closePrice) < hvnVolumeThreshold;
                bool strongVol = avgVolume == 0 || currentBarVolume >= (long)(avgVolume * 1.5);
                if (openInHvn && closeOutHvn && strongVol)
                {
                    if (closePrice > openPrice && currentBarDelta > 0
                        && (!EnableImbalanceDetection || isImbalanceBullish))
                        AddCandidate("BREAKOUT HVN (BUY)", "Sortie de consolidation HVN", true, 4.0 * volFactor, true);
                    else if (closePrice < openPrice && currentBarDelta < 0
                        && (!EnableImbalanceDetection || isImbalanceBearish))
                        AddCandidate("BREAKOUT HVN (SELL)", "Sortie de consolidation HVN", false, 4.0 * volFactor, true);
                }
            }

            if (signalCandidates.Count > 0)
            {
                // POINT 1 : les poids de camp etaient une SOMME A PLAT de tous les
                // candidats. Or absorption, iceberg, delta flip, divergence cumulative
                // et imbalance derivent tous du MEME delta : un seul phenomene pouvait
                // accumuler ~13 points et emporter la selection du signal principal
                // poids MAXIMAL de chaque FAMILLE independante, puis on somme les
                // familles (meme principe que ComputeConfluence).
                for (int f = 0; f < FamilyCount; f++)
                {
                    bestByFamilyBuy[f] = 0;
                    bestByFamilySell[f] = 0;
                }

                for (int i = 0; i < signalCandidates.Count; i++)
                {
                    SignalCandidate c = signalCandidates[i];
                    if (c.Weight <= 0) continue;   // candidat informatif (ex. exhaustion)
                    int f = c.Family;
                    if (f < 0 || f >= FamilyCount) f = FamilyOther;
                    if (c.IsBuy)
                    {
                        if (c.Weight > bestByFamilyBuy[f]) bestByFamilyBuy[f] = c.Weight;
                    }
                    else
                    {
                        if (c.Weight > bestByFamilySell[f]) bestByFamilySell[f] = c.Weight;
                    }
                }

                for (int f = 0; f < FamilyCount; f++)
                {
                    buySideWeight += bestByFamilyBuy[f];
                    sellSideWeight += bestByFamilySell[f];
                }

                // Le signal principal appartient au camp le mieux confirme, et non
                // au premier detecteur d'une liste de priorite arbitraire.
                // mode de selection.
                int chosen = -1;
                if (UseWeightedMultiSignal)
                {
                    bool buyDominant = buySideWeight >= sellSideWeight;
                    double bestWeight = -1;
                    for (int i = 0; i < signalCandidates.Count; i++)
                    {
                        SignalCandidate c = signalCandidates[i];
                        if (c.IsBuy != buyDominant) continue;
                        if (c.Weight > bestWeight) { bestWeight = c.Weight; chosen = i; }
                    }
                }
                else
                {
                    // reellement declenche.
                    for (int i = 0; i < signalCandidates.Count; i++)
                    {
                        if (signalCandidates[i].Triggered) { chosen = i; break; }
                    }
                }
                if (chosen < 0) chosen = 0;

                SignalCandidate primary = signalCandidates[chosen];
                rawSignal = primary.Signal;
                rawInterpretation = primary.Interpretation;
                if (primary.Triggered)
                {
                    triggeredThisBar = true;
                    lastTriggeredSignal = rawSignal;
                    lastSignalTime = GetVolumetricTime();
                }

                // Liste complete, triee par poids decroissant : toutes les confluences
                // restent visibles dans l'alerte et le dashboard.
                signalCandidates.Sort(CompareCandidateDesc);
                for (int i = 0; i < signalCandidates.Count; i++)
                {
                    triggeredSignalsThisBar.Add(string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "{0} [{1:0.0}]", signalCandidates[i].Signal, signalCandidates[i].Weight));
                }
                allSignalsText = string.Join(" | ", triggeredSignalsThisBar);

                // Conflit directionnel : les deux camps sont confirmes de maniere
                // comparable -> pas de trade (remplace l'ancien arbitrage binaire).
                double minorSide = Math.Min(buySideWeight, sellSideWeight);
                double majorSide = Math.Max(buySideWeight, sellSideWeight);
                if (minorSide > 0 && majorSide > 0
                    && (minorSide / majorSide) >= (DirectionalConflictPercent / 100.0)
                    && !trapSignalThisBar)   // un Failed Auction est par nature bi-directionnel
                {
                    rawSignal = "Pas de trade (Conflit order flow)";
                    rawInterpretation = "Conflit order flow";
                    triggeredThisBar = false;
                }
            }
            else if (pocUp)
            {
                rawInterpretation = "Accumulation";
                rawSignal = "Attendre";
                lastTriggeredSignal = rawSignal;
                lastSignalTime = GetVolumetricTime();
                triggeredThisBar = true;
            }
            else if (pocDown)
            {
                rawInterpretation = "Distribution";
                rawSignal = "Attendre";
                lastTriggeredSignal = rawSignal;
                lastSignalTime = GetVolumetricTime();
                triggeredThisBar = true;
            }

            // Iceberg detecte mais sans preuve de rejet : affiche a titre informatif,
            if (isIcebergNeutral)
            {
                triggeredSignalsThisBar.Add("ICEBERG NON DIRECTIONNEL [info]");
                allSignalsText = string.Join(" | ", triggeredSignalsThisBar);
            }

            currentInterpretation = rawInterpretation;
            currentSignal = rawSignal;

            // Synchronisation AMC Core -> Sniper: stocker le signal validé pour éviter rejet artificiel
            if (triggeredThisBar && !currentSignal.StartsWith("Pas de trade"))
            {
                signalTriggerBarIndex = barIdx;
                amcCoreValidatedSignal = rawSignal;
                amcCoreSignalDirectional = rawSignal.Contains("BUY") || rawSignal.Contains("SELL");
            }
            else
            {
                amcCoreValidatedSignal = "";
                amcCoreSignalDirectional = false;
            }

            // Calcul du score de confluence normalisé
            bool isDirectionalBuy = rawSignal.Contains("BUY");
            bool isDirectionalSell = rawSignal.Contains("SELL");

            maxConfluenceScore = GetMaxConfluenceScore();
            confluenceScore = 0;
            confluenceWeighted = 0;
            confluenceDetails = "";

            // ponderation multi-signaux (buySideWeight / sellSideWeight).

            if (isDirectionalBuy || isDirectionalSell)
            {
                confluenceScore = ComputeConfluence(isDirectionalBuy, closePrice, rawInterpretation, confListReusable);
                confluenceDetails = confListReusable.Count > 0 ? string.Join("+", confListReusable) : "";
            }

            // La VWAP n'est plus un filtre directionnel : elle sert uniquement de
            // niveau de confluence (voir ComputeConfluence). Aucun signal n'est

            // ProcessTelegramAlerts (callback d'envoi). Ici, les filtres de
            // confluence, de R:R, de budget de risque et de quota n'etaient pas
            // encore appliques : le journal mesurait donc une population de
            // signaux differente de celle reellement alertee.

            // Reset apres application des filtres, afin que les signaux transformes
            // en "Pas de trade (Filtre ...)" reinitialisent aussi la deduplication.
            if (currentSignal.StartsWith("Pas de trade"))
                lastAlertedSignal = "";
        }

        // Score de confluence calculé indépendamment du signal retenu, afin de
        // pouvoir arbitrer entre deux directions concurrentes.
        private int GetMaxConfluenceScore()
        {
            // actifs (l'ancien plafond fixe de 4 rendait MinConfluencePercentToAlert
            // inatteignable quand absorption/iceberg etaient desactives).
            int max = 1;                                     // structure / interpretation
            max++;                                           // delta directionnel
            if (EnableAbsorptionDetection || EnableIcebergDetection) max++; // order flow passif
            if (EnableImbalanceDetection) max++;             // imbalance
            if (EnableDeltaFlip || EnableCumDeltaDivergence) max++;
            if (EnableFinishedAuction) max++;
            if (UseVwapFilter && currentVwapPrice != 0) max++;           // VWAP (confluence)
            return max;
        }

        private double ibHigh = double.MinValue;
        private double ibLow = double.MaxValue;
        private bool isIbComplete = false;
        private double ibExtensionRatio = 0.0;
        private bool isIbUpExtension = false;
        private bool isIbDownExtension = false;
        private string currentDayType = "Non déterminé";
        private int dayTypeScore = 5;

        [Range(15, 240)]
        [Display(Name = "Période Initial Balance (min)", Order = 13, GroupName = "Volume Profile")]
        public int IbPeriodMinutes { get; set; }

        private void UpdateInitialBalance(int barIdx, double highPrice, double lowPrice)
        {
            if (barIdx < sessionStartBarIndex) return;

            DateTime currentBarTime = GetVolumetricTime();
            int offset = CurrentBars[volumetricBarsIndex] - sessionStartBarIndex;
            DateTime sessionStart = (sessionStartBarIndex >= 0 && offset >= 0 && offset < Times[volumetricBarsIndex].Count)
                ? Times[volumetricBarsIndex][offset]
                : currentBarTime;

            double minutesElapsed = (currentBarTime - sessionStart).TotalMinutes;

            if (minutesElapsed <= IbPeriodMinutes)
            {
                if (highPrice > ibHigh) ibHigh = highPrice;
                if (lowPrice < ibLow) ibLow = lowPrice;
                isIbComplete = false;
                isIbUpExtension = false;
                isIbDownExtension = false;
                currentDayType = "IB en cours";
                dayTypeScore = 5;
            }
            else
            {
                isIbComplete = true;
                double ibRange = ibHigh > ibLow ? (ibHigh - ibLow) : 0;
                double upExt = (sessionHigh != double.MinValue && ibHigh != double.MinValue && sessionHigh > ibHigh) ? (sessionHigh - ibHigh) : 0.0;
                double downExt = (sessionLow != double.MaxValue && ibLow != double.MaxValue && sessionLow < ibLow) ? (ibLow - sessionLow) : 0.0;
                double ibExtension = Math.Max(upExt, downExt);
                ibExtensionRatio = ibRange > 0 ? (ibExtension / ibRange) : 0.0;

                isIbUpExtension = upExt > downExt && upExt > 0;
                isIbDownExtension = downExt > upExt && downExt > 0;

                if (ibExtensionRatio >= 1.5)
                {
                    currentDayType = isIbUpExtension ? "Trend Day Bullish" : (isIbDownExtension ? "Trend Day Bearish" : "Trend Day");
                    dayTypeScore = 10;
                }
                else if (ibExtensionRatio >= 1.0)
                {
                    currentDayType = isIbUpExtension ? "Normal Variation Bullish" : (isIbDownExtension ? "Normal Variation Bearish" : "Normal Variation");
                    dayTypeScore = 7;
                }
                else if (ibExtensionRatio >= 0.5)
                {
                    currentDayType = "Normal Day";
                    dayTypeScore = 5;
                }
                else
                {
                    currentDayType = "Range Day";
                    dayTypeScore = 3;
                }
            }
        }

        private int ComputeConfluence(bool isBuy, double closePrice, string interpretation, List<string> details)
        {
            if (details != null)
            {
                details.Clear();
                details.Add(interpretation);
            }

            double tick = TickSize;
            double highPrice = Highs[volumetricBarsIndex][evalOffset];
            double lowPrice = Lows[volumetricBarsIndex][evalOffset];
            double barRange = highPrice - lowPrice;

            // L1 : Contexte de marché & Initial Balance avec vérification directionnelle
            bool isTrendType = currentDayType.StartsWith("Trend Day");
            bool isVarType = currentDayType.StartsWith("Normal Variation");
            bool trendAligned = (isBuy && isIbUpExtension) || (!isBuy && isIbDownExtension);
            bool trendOpposed = (isBuy && isIbDownExtension) || (!isBuy && isIbUpExtension);

            double l1DayType;
            double l1IbExt;

            if (isTrendType)
            {
                l1DayType = trendAligned ? 10.0 : (trendOpposed ? 1.0 : 5.0);
                l1IbExt = trendAligned ? Clamp(ibExtensionRatio * 5.0, 0.0, 10.0) : 0.0;
            }
            else if (isVarType)
            {
                l1DayType = trendAligned ? 7.0 : (trendOpposed ? 3.0 : 5.0);
                l1IbExt = trendAligned ? Clamp(ibExtensionRatio * 4.0, 0.0, 8.0) : 0.0;
            }
            else
            {
                l1DayType = dayTypeScore; // 3..5 (Normal Day / Range Day)
                l1IbExt = Clamp(ibExtensionRatio * 3.0, 0.0, 5.0);
            }

            double l1Inventory = IsPriceInsideProfile(closePrice) ? 5.0 : 2.0; // 2..5
            double l1Htf = IsHtfAligned(isBuy) ? 5.0 : 0.0; // 0..5
            double l1Total = l1DayType + l1IbExt + l1Inventory + l1Htf;

            if (details != null)
                details.Add(string.Format("L1:{0:F0}/30({1})", l1Total, currentDayType));

            bool isShared;
            bool nearKey = IsNearKeyLevel(closePrice, 5 * tick, out isShared);
            double l2Key = isShared ? 8.0 : (nearKey ? 5.0 : 0.0);

            bool isNaked;
            bool nearPrior = IsNearPriorSessionLevel(closePrice, 5 * tick, out isNaked);
            double l2Prior = isNaked ? 8.0 : (nearPrior ? 6.0 : 0.0);

            bool vwapAligned = UseVwapFilter && currentVwapPrice != 0 && (isBuy ? closePrice > currentVwapPrice : closePrice < currentVwapPrice);
            double l2Vwap = vwapAligned ? 4.0 : 0.0;

            bool nearBorder = (vahPrice > 0 && Math.Abs(closePrice - vahPrice) <= 5 * tick) || (valPrice > 0 && Math.Abs(closePrice - valPrice) <= 5 * tick);
            double l2Border = nearBorder ? 5.0 : 2.0;

            bool isLvn = IsLowVolumeNode(closePrice, NodeToleranceTicks);
            bool isHvn = hvnVolumeThreshold > 0 && VolumeAtPrice(closePrice) >= hvnVolumeThreshold;
            double l2Node = (isLvn || isHvn) ? 5.0 : 0.0;

            double l2Total = l2Key + l2Prior + l2Vwap + l2Border + l2Node;

            if (details != null && l2Total > 0)
                details.Add(string.Format("L2:{0:F0}/30", l2Total));

            bool absorption = isBuy ? isBullishAbsorptionActive : isBearishAbsorptionActive;
            double l3Abs = absorption ? (isAbsorptionStrong ? 10.0 : 4.0) : 0.0;

            bool iceberg = isBuy ? isIcebergBullish : isIcebergBearish;
            double l3Ice = iceberg ? Clamp((lastIcebergBarIndex == evalBarIndex ? 8.0 : 4.0), 0.0, 8.0) : 0.0;

            bool imbalanceRetest = isBuy ? (isImbalanceBullish || isBullishAbsorptionActive) : (isImbalanceBearish || isBearishAbsorptionActive);
            double l3Imb = imbalanceRetest ? 6.0 : 0.0;

            bool div = isBuy ? isCumDeltaDivBullish : isCumDeltaDivBearish;
            double l3Div = div ? Clamp(cumDeltaDivStrength * 3.0, 0.0, 6.0) : 0.0;

            double l3Total = l3Abs + l3Ice + l3Imb + l3Div;

            if (details != null && l3Total > 0)
                details.Add(string.Format("L3:{0:F0}/30", l3Total));

            bool fa = isBuy ? isFinishedAuctionBuy : isFinishedAuctionSell;
            double l4Fa = fa ? 4.0 : 0.0;

            double wickRatio = barRange > 0 ? (isBuy ? (closePrice - lowPrice) / barRange : (highPrice - closePrice) / barRange) : 0;
            double l4Wick = wickRatio >= 0.40 ? 3.0 : 0.0;

            bool deltaConf = Math.Abs(currentBarDelta) >= EffectiveAbsorptionDeltaThreshold();
            double l4Delta = deltaConf ? 3.0 : 0.0;

            double l4Total = l4Fa + l4Wick + l4Delta;

            if (details != null && l4Total > 0)
                details.Add(string.Format("L4:{0:F0}/10", l4Total));

            double totalScore = l1Total + l2Total + l3Total + l4Total;

            // Application des portes d'arrêt en mode Sniper (pénalisation si gating non satisfait)
            // Pénalisation bornée sur le palier le plus faible pour éviter un écrasement multiplicatif non-linéaire
            if (TradingPreset == SniperMarketPreset.Sniper)
            {
                double penaltyMult = 1.0;
                if (l1Total < 10.0) penaltyMult = Math.Min(penaltyMult, 0.7); // Gate 1: Contexte faible
                if ((l1Total + l2Total) < 25.0) penaltyMult = Math.Min(penaltyMult, 0.7); // Gate 2: Localisation faible
                if ((l1Total + l2Total + l3Total) < 45.0) penaltyMult = Math.Min(penaltyMult, 0.8); // Gate 3: Microstructure faible
                totalScore *= penaltyMult;
            }

            totalScore = Clamp(totalScore, 0.0, 100.0);

            if (details != null)
            {
                confluenceWeighted = totalScore;
                maxConfluenceWeighted = 100.0;
            }

            // L'ancien code renvoyait (int)Math.Round(totalScore / 25.0), donc une
            // valeur bornee a 0..4, alors que GetMaxConfluenceScore() peut valoir
            // jusqu'a 7. Le garde-fou structurel du declencheur
            //     pctCount = 100 * confluenceScore / maxConfluenceScore
            //     pctCount >= MinConfluencePercentToAlert * 0.5
            // comparait donc deux echelles differentes : selon les detecteurs
            // actifs, il etait soit inoperant, soit impossible a satisfaire. Ce
            // n'etait plus une garde, c'etait du bruit.
            // avec EXACTEMENT les memes conditions que GetMaxConfluenceScore().
            // Les deux valeurs vivent enfin sur la meme echelle :
            //   - confluenceWeighted / 100  = INTENSITE de la preuve (inchangee)
            int contributors = 0;

            // 1. structure / interpretation (toujours comptabilise dans le max)
            if (nearKey || nearPrior || nearBorder || isLvn || isHvn) contributors++;

            // 2. delta directionnel (toujours comptabilise dans le max)
            if (deltaConf) contributors++;

            // 3. order flow passif
            if (EnableAbsorptionDetection || EnableIcebergDetection)
                if (absorption || iceberg) contributors++;

            // 4. imbalance
            if (EnableImbalanceDetection)
                if (imbalanceRetest) contributors++;

            if (EnableDeltaFlip || EnableCumDeltaDivergence)
                if (div || (isBuy ? isDeltaFlipBullish : isDeltaFlipBearish)) contributors++;

            if (EnableFinishedAuction)
                if (fa) contributors++;

            // 7. VWAP
            if (UseVwapFilter && currentVwapPrice != 0)
                if (vwapAligned) contributors++;

            // Filet de securite : le compte ne peut jamais depasser le plafond
            // publie, meme si les deux methodes venaient a diverger.
            int cap = GetMaxConfluenceScore();
            if (contributors > cap) contributors = cap;

            return contributors;
        }

        // Cached rolling average volume — computed once per bar index.
        private long GetAverageVolume(int barIdx, VolumetricBarsType barsType)
        {
            if (barIdx == cachedAvgVolBarIdx)
                return cachedAvgVolume;

            cachedAvgVolBarIdx = barIdx;
            cachedAvgVolume = 0;

            if (barsType == null || barIdx < 1)
                return 0;

            long volSum = 0;
            int countBars = Math.Min(LookbackBars, barIdx);
            for (int i = 0; i < countBars; i++)
            {
                int vi = barIdx - 1 - i;
                if (vi >= 0 && vi < barsType.Volumes.Length && barsType.Volumes[vi] != null)
                    volSum += barsType.Volumes[vi].TotalVolume;
            }
            if (countBars > 0) cachedAvgVolume = volSum / countBars;
            return cachedAvgVolume;
        }

        // autour d'une extremite (l'agression se concentre rarement au prix exact).
        // dir = -1 : on descend depuis le high ; dir = +1 : on monte depuis le low.
        private void ProbeAggressionWindow(VolumetricData bar, double startPrice, double boundPrice,
                                           int dir, bool wantBidVolume,
                                           out long aggression, out long total)
        {
            aggression = 0;
            total = 0;
            if (bar == null) return;

            double tick = TickSize;
            if (tick <= 0) return;

            // Minimum 1 tick : scanner uniquement le prix exact de l'extremite
            // manque quasi systematiquement l'agression reelle.
            int probe = Math.Max(1, AbsorptionProbeTicks);
            int sym = Math.Max(0, AbsorptionSymmetricTicks);
            long startTick = (long)Math.Round(startPrice / tick);
            long boundTick = (long)Math.Round(boundPrice / tick);

            // Fenetre asymetrique : `probe` ticks vers l'interieur de la barre,
            // `sym` ticks vers l'exterieur (au-dela du high / sous le low).
            for (int i = -sym; i <= probe; i++)
            {
                long t = startTick + (dir * i);
                if (i >= 0)
                {
                    if (dir < 0 && t < boundTick) break;
                    if (dir > 0 && t > boundTick) break;
                }

                double p = t * tick;
                aggression += wantBidVolume ? bar.GetBidVolumeForPrice(p) : bar.GetAskVolumeForPrice(p);
                total += bar.GetTotalVolumeForPrice(p);
            }
        }

        private void EvaluateAbsorption(VolumetricData currentBar, double highPrice, double lowPrice, double closePrice, double openPrice)
        {
            int barIdx = evalBarIndex >= 0 ? evalBarIndex : CurrentBars[volumetricBarsIndex];

            // En evaluation au tick, on laisse le signal se reevaluer / se renforcer.
            if (EvaluateOnBarClose && lastAbsorptionBarIndex == barIdx
                && (isBullishAbsorptionActive || isBearishAbsorptionActive))
                return;

            isBullishAbsorptionActive = false;
            isBearishAbsorptionActive = false;
            currentAbsorptionVolume = 0;
            isAbsorptionStrong = false;
            absorptionQualityFactor = 1.0;
            currentAbsorptionStatus = "Néant";

            if (!EnableAbsorptionDetection || currentBar == null) return;

            double barRange = highPrice - lowPrice;
            if (barRange <= 0) return;

            if (currentBar.TotalVolume < MinBarVolumeForAbsorption) return;

            long barDelta = currentBar.BarDelta;

            int deltaThreshold = EffectiveAbsorptionDeltaThreshold();
            int tickVolThreshold = EffectiveAbsorptionTickVolumeThreshold();

            // Sémantique NinjaTrader Volumetric (BidAsk) :
            //   Bid volume = volume transigé au Bid = agression VENDUE (market sell)
            //   Ask volume = volume transigé au Ask = agression ACHETÉE (market buy)
            // L'absorption = forte agression absorbée par des ordres limites passifs, avec recul du prix.
            // qui agressent (volume a l'Ask) et qui se font absorber par des vendeurs
            // passifs -> signature BAISSIERE. Au LOW, ce sont les VENDEURS qui agressent
            // (volume au Bid), absorbes par des acheteurs passifs -> signature HAUSSIERE.
            // L'ancienne version lisait le Bid au high et l'Ask au low : elle mesurait
            // du MOMENTUM, pas de l'absorption, et contredisait la branche
            // divergence delta/prix ci-dessous (qui etait, elle, correcte). Avec
            // AbsorptionRequireStrongSignal = true les deux branches devenaient donc
            // quasi mutuellement exclusives.
            long askVolAtHigh, totalAtHigh, bidVolAtLow, totalAtLow;
            ProbeAggressionWindow(currentBar, highPrice, lowPrice, -1, false, out askVolAtHigh, out totalAtHigh);
            ProbeAggressionWindow(currentBar, lowPrice, highPrice, +1, true, out bidVolAtLow, out totalAtLow);

            // un flux normal massif avec une veritable absorption).
            double minRatio = Clamp(AbsorptionMinAggressionPercent / 100.0, 0.0, 1.0);
            double bearRatio = totalAtHigh > 0 ? (double)askVolAtHigh / totalAtHigh : 0.0;
            double bullRatio = totalAtLow > 0 ? (double)bidVolAtLow / totalAtLow : 0.0;

            // 1. Détection absorption par divergence Delta/Prix
            //    Vendeurs agressent fortement (delta très négatif) mais le prix ne baisse pas → absorption acheteuse
            bool candleBullishAbs = (barDelta <= -deltaThreshold &&
                                     closePrice >= lowPrice + (barRange * 0.6) &&
                                     (!AbsorptionRequireCloseVsOpen || closePrice >= openPrice));

            //    Acheteurs agressent fortement (delta très positif) mais le prix ne monte pas → absorption vendeuse
            bool candleBearishAbs = (barDelta >= deltaThreshold &&
                                    closePrice <= highPrice - (barRange * 0.6) &&
                                    (!AbsorptionRequireCloseVsOpen || closePrice <= openPrice));

            // 2. Détection absorption par forte agression aux extrémités avec recul du prix
            //    Au sommet : forte agression ACHETEUSE (ask) absorbée + recul → absorption vendeuse (bearish)
            //    Au creux  : forte agression VENDEUSE (bid) absorbée + recul → absorption acheteuse (bullish)
            bool tickBearishAbs = (askVolAtHigh >= tickVolThreshold &&
                                   bearRatio >= minRatio &&
                                   closePrice <= highPrice - (barRange * 0.3));

            bool tickBullishAbs = (bidVolAtLow >= tickVolThreshold &&
                                  bullRatio >= minRatio &&
                                  closePrice >= lowPrice + (barRange * 0.3));

            //   fort  = les deux signatures (divergence delta/prix ET agression tick)
            //   faible = une seule signature
            bool strongBullishAbs = candleBullishAbs && tickBullishAbs;
            bool strongBearishAbs = candleBearishAbs && tickBearishAbs;

            bool rawBullishAbs = AbsorptionRequireStrongSignal
                ? strongBullishAbs : (candleBullishAbs || tickBullishAbs);
            bool rawBearishAbs = AbsorptionRequireStrongSignal
                ? strongBearishAbs : (candleBearishAbs || tickBearishAbs);

            // 3. Filtrage par niveaux clés (figés au début de barre pour stabilité)
            if (AbsorptionOnlyAtKeyLevels && (rawBullishAbs || rawBearishAbs))
            {
                double tol = AbsorptionKeyLevelTicks * TickSize;
                bool sh;
                bool nearLow = IsNearKeyLevel(lowPrice, tol, out sh)
                            || IsNearKeyLevel(closePrice, tol, out sh);
                bool nearHigh = IsNearKeyLevel(highPrice, tol, out sh)
                             || IsNearKeyLevel(closePrice, tol, out sh);

                if (rawBullishAbs && !nearLow) rawBullishAbs = false;
                if (rawBearishAbs && !nearHigh) rawBearishAbs = false;
            }


            // 4. Résolution de conflit réellement neutre.
            //    deux cotes ; quand |barDelta| dominait, les deux forces etaient egales
            //    et le haussier gagnait systematiquement (biais cache).
            //    On compare d'abord la signature forte, puis l'agression tick, puis le
            //    signe du delta comme ultime departage.
            if (rawBullishAbs && rawBearishAbs)
            {
                if (strongBullishAbs != strongBearishAbs)
                {
                    rawBullishAbs = strongBullishAbs;
                    rawBearishAbs = strongBearishAbs;
                }
                else if (askVolAtHigh != bidVolAtLow)
                {
                    if (askVolAtHigh > bidVolAtLow) rawBullishAbs = false;
                    else rawBearishAbs = false;
                }
                else if (barDelta != 0)
                {
                    // delta positif = agression acheteuse dominante absorbee -> bearish
                    if (barDelta > 0) rawBullishAbs = false;
                    else rawBearishAbs = false;
                }
                else
                {
                    rawBullishAbs = false;
                    rawBearishAbs = false;
                }
            }

            if (!rawBullishAbs && !rawBearishAbs) return;

            bool isBull = rawBullishAbs;
            isAbsorptionStrong = isBull ? strongBullishAbs : strongBearishAbs;

            // Volume absorbe : on retient la signature la plus significative.
            long tickVol = isBull ? bidVolAtLow : askVolAtHigh;
            bool tickSide = isBull ? tickBullishAbs : tickBearishAbs;
            currentAbsorptionVolume = tickSide
                ? Math.Max(tickVol, Math.Abs(barDelta))
                : Math.Abs(barDelta);

            absorptionQualityFactor = isAbsorptionStrong ? 1.25 : 0.65;
            if (AbsorptionUseTrendContext && EnableHtfFilter && htfEma != null && htfBias != 0)
            {
                // Une absorption alignee avec le biais HTF est plus fiable qu'une
                // absorption a contre-tendance (souvent simple pause).
                bool aligned = isBull ? htfBias > 0 : htfBias < 0;
                absorptionQualityFactor *= aligned ? 1.15 : 0.75;
            }
            absorptionQualityFactor = Clamp(absorptionQualityFactor, 0.4, 1.6);

            string quality = isAbsorptionStrong ? "FORT" : "faible";
            if (isBull)
            {
                isBullishAbsorptionActive = true;
                currentAbsorptionStatus = string.Format("ACHETEUR {0} ({1:N0}v, {2:P0} agr.)",
                    quality, currentAbsorptionVolume, bullRatio);
            }
            else
            {
                isBearishAbsorptionActive = true;
                currentAbsorptionStatus = string.Format("VENDEUR {0} ({1:N0}v, {2:P0} agr.)",
                    quality, currentAbsorptionVolume, bearRatio);
            }
            lastAbsorptionBarIndex = barIdx;
        }

        private void EvaluateIceberg(VolumetricData currentBar, int barIdx, double highPrice, double lowPrice, VolumetricBarsType barsType)
        {
            if (!EnableIcebergDetection || currentBar == null) return;

            // mode barre fermee ; au tick, l'iceberg doit pouvoir etre re-evalue/invalide.
            if (EvaluateOnBarClose && lastIcebergBarIndex == barIdx
                && (isIcebergBullish || isIcebergBearish))
                return;

            double tick = TickSize;
            double closePrice = Closes[volumetricBarsIndex][evalOffset];

            // Purge des snapshots hors fenêtre
            while (icebergHistory.Count > 0 &&
                   (icebergHistory.Count > IcebergLookbackBars || (barIdx - icebergHistory.First.Value.BarIndex) >= IcebergLookbackBars))
            {
                icebergHistory.RemoveFirst();
            }

            // Création/mise à jour du snapshot courant
            if (icebergHistory.Last == null || icebergHistory.Last.Value.BarIndex != barIdx)
            {
                DateTime barTime = (volumetricBarsIndex < Times.Length && Times[volumetricBarsIndex].Count > 0)
                    ? GetVolumetricTime()
                    : DateTime.MinValue;

                currentIcebergSnapshot = new IcebergBarSnapshot
                {
                    BarIndex = barIdx,
                    Time = barTime
                };
                icebergHistory.AddLast(currentIcebergSnapshot);
            }
            else
            {
                currentIcebergSnapshot = icebergHistory.Last.Value;
            }

            currentIcebergSnapshot.Close = closePrice;
            currentIcebergSnapshot.High = highPrice;
            currentIcebergSnapshot.Low = lowPrice;
            currentIcebergSnapshot.BarDelta = currentBar.BarDelta;
            currentIcebergSnapshot.TotalVolume = currentBar.TotalVolume;

            isIcebergBullish = false;
            isIcebergBearish = false;
            isIcebergNeutral = false;
            icebergPrice = 0;
            icebergTotalAggression = 0;
            icebergNetDelta = 0;
            currentIcebergStatus = "Néant";

            // Fenêtre complète requise
            if (icebergHistory.Count < IcebergLookbackBars) return;

            // NOTE : avec evalOffset, la barre analysee est deja cloturee ;
            // il n'y a aucune barre partielle a exclure de la fenetre.

            long totalAbsDelta = 0;
            long cumulDelta = 0;
            long totalWindowVolume = 0;
            double windowHigh = double.MinValue;
            double windowLow = double.MaxValue;
            double highestClose = double.MinValue;
            double lowestClose = double.MaxValue;
            double sumBarRange = 0;
            int countedBars = 0;
            long lastSnapDelta = 0;
            long prevSnapDelta = 0;

            foreach (var snap in icebergHistory)
            {
                totalAbsDelta += Math.Abs(snap.BarDelta);
                cumulDelta += snap.BarDelta;
                totalWindowVolume += snap.TotalVolume;
                sumBarRange += (snap.High - snap.Low);

                if (snap.High > windowHigh) windowHigh = snap.High;
                if (snap.Low < windowLow) windowLow = snap.Low;

                if (snap.Close > highestClose) highestClose = snap.Close;
                if (snap.Close < lowestClose) lowestClose = snap.Close;

                prevSnapDelta = lastSnapDelta;
                lastSnapDelta = snap.BarDelta;
                countedBars++;
            }

            double maxCloseDisplacement = highestClose - lowestClose;
            int maxCloseDisplacementTicks = (int)Math.Round(maxCloseDisplacement / tick);

            double priceRange = windowHigh - windowLow;
            int priceRangeTicks = (int)Math.Round(priceRange / tick);

            double avgBarRange = countedBars > 0 ? sumBarRange / countedBars : 0;
            double atrRatio = avgBarRange > 0 ? (priceRange / avgBarRange) : 0;

            double aggressionIntensityRatio = totalWindowVolume > 0 ? (double)totalAbsDelta / totalWindowVolume : 0;
            double minAggressionIntensityRatio = IcebergMinAggressionRatioPercent / 100.0;

            // ROBUSTE (MAD) de l'agression, insensible aux fat tails, combine au ratio
            // de dominance. Un plancher absolu evite les declenchements en marche mort.
            long icebergAggressionFloor = IcebergMinAggression;
            bool hasHighAggression;
            if (SniperV3Ready())
            {
                double zAggr = SniperZMadDelta();
                icebergAggressionFloor = (long)Math.Max(1, (long)SniperV3DeltaThreshold() * Math.Max(1, IcebergLookbackBars) / 2);
                hasHighAggression = zAggr >= SniperIcebergZMin()
                                    && totalAbsDelta >= icebergAggressionFloor
                                    && aggressionIntensityRatio >= minAggressionIntensityRatio;
            }
            else
            {
                hasHighAggression = totalAbsDelta >= IcebergMinAggression && aggressionIntensityRatio >= minAggressionIntensityRatio;
            }
            bool hasLowDisplacement = maxCloseDisplacementTicks <= IcebergMaxDisplacementTicks;
            bool hasTightRange = UseAtrRangeFilter && avgBarRange > 0
                ? (atrRatio <= IcebergMaxAtrRatio)
                : (priceRangeTicks <= IcebergMaxRangeTicks);

            if (!hasHighAggression || !hasLowDisplacement || !hasTightRange) return;

            // Pic de volume local (POC de l'iceberg)
            double peakVolumePrice = (windowHigh + windowLow) / 2.0;
            long maxPriceVolume = -1;

            if (barsType != null)
            {
                var priceVolMap = icebergPriceVolMap;
                priceVolMap.Clear();
                foreach (var snap in icebergHistory)
                {
                        if (snap.BarIndex >= 0 && snap.BarIndex < barsType.Volumes.Length)
                    {
                        VolumetricData bData = barsType.Volumes[snap.BarIndex];
                        if (bData != null)
                        {
                            long lowT = (long)Math.Round(snap.Low / tick);
                            long highT = (long)Math.Round(snap.High / tick);
                            for (long t = lowT; t <= highT; t++)
                            {
                                double p = t * tick;
                                long v = bData.GetTotalVolumeForPrice(p);
                                if (v > 0)
                                {
                                    long curV = 0;
                                    priceVolMap.TryGetValue(t, out curV);
                                    curV += v;
                                    priceVolMap[t] = curV;

                                    if (curV > maxPriceVolume)
                                    {
                                        maxPriceVolume = curV;
                                        peakVolumePrice = p;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Filtrage par niveaux clés (figés)
            if (IcebergOnlyAtKeyLevels)
            {
                bool shared;
                if (!IsNearKeyLevel(peakVolumePrice, IcebergKeyLevelTicks * tick, out shared))
                    return;
            }


            // Dominance directionnelle
            double dominanceRatio = totalAbsDelta > 0 ? (double)Math.Abs(cumulDelta) / totalAbsDelta : 0;
            double minDominanceRatio = IcebergMinDominancePercent / 100.0;

            if (dominanceRatio < minDominanceRatio)
            {
                isIcebergBullish = false;
                isIcebergBearish = false;
                currentIcebergStatus = "Néant";
                return;
            }

            // Scoring composite institutionnel
            long avgVol = GetAverageVolume(barIdx, barsType);
            double rangeTicks = adaptiveAvgBarRange > 0 && tick > 0 ? adaptiveAvgBarRange / tick : 1.0;
            double expectedTickVol = avgVol > 0 ? (avgVol / Math.Max(1.0, rangeTicks)) : 50.0;
            double expectedWindowAggr = expectedTickVol * IcebergLookbackBars * 2.0;
            double targetAggr = Math.Max(SniperV3Ready() ? icebergAggressionFloor : IcebergMinAggression, expectedWindowAggr);

            double scoreAggr = Math.Min(40.0, 40.0 * ((double)totalAbsDelta / targetAggr));

            double dispFactor = IcebergMaxDisplacementTicks > 0
                ? Math.Max(0.0, 1.0 - ((double)maxCloseDisplacementTicks / IcebergMaxDisplacementTicks))
                : 1.0;
            double rangeFactor = UseAtrRangeFilter && avgBarRange > 0
                ? Math.Max(0.0, 1.0 - (atrRatio / IcebergMaxAtrRatio))
                : Math.Max(0.0, 1.0 - ((double)priceRangeTicks / IcebergMaxRangeTicks));
            double scoreCompression = (15.0 * dispFactor) + (10.0 * rangeFactor);

            double intensityTarget = (IcebergMinAggressionRatioPercent / 100.0) * 3.0;
            double scoreVolume = intensityTarget > 0
                ? Math.Min(20.0, 20.0 * (aggressionIntensityRatio / intensityTarget))
                : 20.0;

            // Score de répétition corrigé : proximité du CLOSE au pic, non inclusion de range (tolérance 1.5 ticks)
            int repeatHits = 0;
            double tickTolerance = tick * 1.5;
            foreach (var snap in icebergHistory)
            {
                if (Math.Abs(snap.Close - peakVolumePrice) <= tickTolerance)
                    repeatHits++;
            }
            int effectiveCount = icebergHistory.Count;
            double repeatRatio = effectiveCount > 0 ? (double)repeatHits / effectiveCount : 0;
            double scoreRepeat = 15.0 * repeatRatio;

            int totalIcebergScore = (int)Math.Round(scoreAggr + scoreCompression + scoreVolume + scoreRepeat);

            if (totalIcebergScore < IcebergMinScore)
            {
                isIcebergBullish = false;
                isIcebergBearish = false;
                currentIcebergStatus = "Néant";
                return;
            }

            icebergPrice = peakVolumePrice;
            icebergTotalAggression = totalAbsDelta;
            icebergNetDelta = cumulDelta;
            lastIcebergBarIndex = barIdx;

            // absorption acheteuse => bullish". C'est une hypothese non prouvee :
            // en tendance, un delta net vendeur dans un range compresse est le plus
            // souvent une pression vendeuse qui n'a pas ENCORE casse, ce qui
            // produisait des signaux de contre-tendance systematiquement perdants.
            // effectivement DEFENDU :
            //   1) le pic de volume se situe du bon cote de la fenetre (support/resistance)
            //   2) le prix s'en est ecarte d'au moins X % du range (preuve de rejet)
            //   3) l'agression opposee s'essouffle (dernier delta moins hostile)
            // Sinon l'iceberg reste informatif et non directionnel.
            bool bullCandidate = cumulDelta < 0;   // agression vendeuse nette absorbee
            bool bearCandidate = cumulDelta > 0;   // agression acheteuse nette absorbee

            bool rejectionProven = !IcebergRequireRejection;
            if (IcebergRequireRejection && priceRange > 0)
            {
                double minRejection = (IcebergMinRejectionPercent / 100.0) * priceRange;

                if (bullCandidate)
                {
                    bool peakAtSupport = peakVolumePrice <= (windowLow + priceRange * 0.3);
                    bool priceHeldAbove = (closePrice - peakVolumePrice) >= minRejection;
                    bool sellingFading = lastSnapDelta > prevSnapDelta;
                    rejectionProven = peakAtSupport && priceHeldAbove && sellingFading;
                }
                else if (bearCandidate)
                {
                    bool peakAtResistance = peakVolumePrice >= (windowHigh - priceRange * 0.3);
                    bool priceHeldBelow = (peakVolumePrice - closePrice) >= minRejection;
                    bool buyingFading = lastSnapDelta < prevSnapDelta;
                    rejectionProven = peakAtResistance && priceHeldBelow && buyingFading;
                }
            }

            if ((bullCandidate || bearCandidate) && !rejectionProven)
            {
                // Iceberg reel mais non confirme : aucune direction n'est publiee.
                isIcebergBullish = false;
                isIcebergBearish = false;
                isIcebergNeutral = true;
                currentIcebergStatus = string.Format(
                    "NON DIRECTIONNEL @{0} [{1}/100] (rejet non confirme, D:{2:N0})",
                    Instrument.MasterInstrument.FormatPrice(icebergPrice),
                    totalIcebergScore, cumulDelta);
                return;
            }

            if (bullCandidate)
            {
                isIcebergBullish = true;
                currentIcebergStatus = string.Format("ACHETEUR @{0} [{1}/100] (Agr:{2:N0}, D:{3:N0}, Int:{4:P0}, Dom:{5:P0}, Rng:{6}t)",
                    Instrument.MasterInstrument.FormatPrice(icebergPrice),
                    totalIcebergScore, icebergTotalAggression, icebergNetDelta, aggressionIntensityRatio, dominanceRatio, priceRangeTicks);
            }
            else if (bearCandidate)
            {
                isIcebergBearish = true;
                currentIcebergStatus = string.Format("VENDEUR @{0} [{1}/100] (Agr:{2:N0}, D:{3:N0}, Int:{4:P0}, Dom:{5:P0}, Rng:{6}t)",
                    Instrument.MasterInstrument.FormatPrice(icebergPrice),
                    totalIcebergScore, icebergTotalAggression, icebergNetDelta, aggressionIntensityRatio, dominanceRatio, priceRangeTicks);
            }
            else
            {
                // cumulDelta == 0 : dominance insuffisante pour déterminer la direction
                isIcebergBullish = false;
                isIcebergBearish = false;
                currentIcebergStatus = "Néant";
            }
        }

        // Mode diagonal (defaut, standard Bookmap / Jigsaw / MZpack) :
        //   imbalance acheteuse  -> Ask(p)  vs Bid(p - 1 tick)
        //   imbalance vendeuse   -> Bid(p)  vs Ask(p + 1 tick)
        // Mode horizontal (legacy) : Ask(p) vs Bid(p) au meme prix.
        // Un niveau non qualifiant rompt la contiguite ; la meilleure pile est
        // conservee et memorisee comme zone active (support/resistance).
        private void EvaluateImbalance(VolumetricData currentBar, int barIdx, double highPrice, double lowPrice)
        {
            isImbalanceBullish = false;
            isImbalanceBearish = false;
            imbalancePrice = 0;
            imbalanceConsecutiveCount = 0;
            currentImbalanceStatus = "Néant";

            if (!EnableImbalanceDetection || currentBar == null) return;
            if (EvaluateOnBarClose && lastImbalanceBarIndex == barIdx) return;

            double tick = TickSize;
            double ratioThreshold = ImbalanceRatioPercent / 100.0;

            long lowT = (long)Math.Round(lowPrice / tick);
            long highT = (long)Math.Round(highPrice / tick);

            int bullStreak = 0, bearStreak = 0;
            double bullBottom = 0, bearBottom = 0;
            long bullVolume = 0, bearVolume = 0;

            int bestBullStreak = 0, bestBearStreak = 0;
            double bestBullBottom = 0, bestBullTop = 0;
            double bestBearBottom = 0, bestBearTop = 0;
            long bestBullVolume = 0, bestBearVolume = 0;

            for (long t = lowT; t <= highT; t++)
            {
                double p = t * tick;
                long askV = currentBar.GetAskVolumeForPrice(p);
                long bidV = currentBar.GetBidVolumeForPrice(p);

                long bullRef = ImbalanceDiagonalMode
                    ? currentBar.GetBidVolumeForPrice(p - tick)
                    : bidV;
                bool bullQualifies = askV >= ImbalanceMinLevelVolume
                                     && bullRef > 0
                                     && (double)askV / bullRef >= ratioThreshold;

                if (bullQualifies)
                {
                    bullStreak++;
                    if (bullStreak == 1) { bullBottom = p; bullVolume = askV; }
                    else bullVolume += askV;

                    if (bullStreak > bestBullStreak
                        || (bullStreak == bestBullStreak && bullVolume > bestBullVolume))
                    {
                        bestBullStreak = bullStreak;
                        bestBullBottom = bullBottom;
                        bestBullTop = p;
                        bestBullVolume = bullVolume;
                    }
                }
                else
                {
                    bullStreak = 0; bullBottom = 0; bullVolume = 0;
                }

                long bearRef = ImbalanceDiagonalMode
                    ? currentBar.GetAskVolumeForPrice(p + tick)
                    : askV;
                bool bearQualifies = bidV >= ImbalanceMinLevelVolume
                                     && bearRef > 0
                                     && (double)bidV / bearRef >= ratioThreshold;

                if (bearQualifies)
                {
                    bearStreak++;
                    if (bearStreak == 1) { bearBottom = p; bearVolume = bidV; }
                    else bearVolume += bidV;

                    if (bearStreak > bestBearStreak
                        || (bearStreak == bestBearStreak && bearVolume > bestBearVolume))
                    {
                        bestBearStreak = bearStreak;
                        bestBearBottom = bearBottom;
                        bestBearTop = p;
                        bestBearVolume = bearVolume;
                    }
                }
                else
                {
                    bearStreak = 0; bearBottom = 0; bearVolume = 0;
                }
            }

            bool bullValid = bestBullStreak >= ImbalanceConsecutiveLevels && bestBullBottom != 0;
            bool bearValid = bestBearStreak >= ImbalanceConsecutiveLevels && bestBearBottom != 0;

            bool selectedIsBull;
            double zoneBottom, zoneTop;
            long dominantVolume, referenceVolume;

            if (bullValid && (!bearValid || bestBullStreak > bestBearStreak
                || (bestBullStreak == bestBearStreak && bestBullVolume >= bestBearVolume)))
            {
                selectedIsBull = true;
                zoneBottom = bestBullBottom;
                zoneTop = bestBullTop;
                imbalanceConsecutiveCount = bestBullStreak;
                // Le prix de reference d'une pile acheteuse est son sommet :
                // c'est la que l'agression a laisse le desequilibre le plus recent.
                imbalancePrice = bestBullTop;
                dominantVolume = bestBullVolume;
                referenceVolume = ImbalanceDiagonalMode
                    ? currentBar.GetBidVolumeForPrice(bestBullTop - tick)
                    : currentBar.GetBidVolumeForPrice(bestBullTop);
            }
            else if (bearValid)
            {
                selectedIsBull = false;
                zoneBottom = bestBearBottom;
                zoneTop = bestBearTop;
                imbalanceConsecutiveCount = bestBearStreak;
                imbalancePrice = bestBearBottom;
                dominantVolume = bestBearVolume;
                referenceVolume = ImbalanceDiagonalMode
                    ? currentBar.GetAskVolumeForPrice(bestBearBottom + tick)
                    : currentBar.GetAskVolumeForPrice(bestBearBottom);
            }
            else
            {
                return;
            }

            if (ImbalanceOnlyAtKeyLevels)
            {
                bool shared;
                if (!IsNearKeyLevel(imbalancePrice, ImbalanceKeyLevelTicks * tick, out shared))
                {
                    imbalancePrice = 0;
                    imbalanceConsecutiveCount = 0;
                    return;
                }
            }


            string mode = ImbalanceDiagonalMode ? "diag" : "horiz";
            // de N niveaux, referenceVolume est un seul niveau : sans division par le
            // nombre de niveaux, le ratio affiche etait N fois trop eleve).
            double ratio = (referenceVolume > 0 && imbalanceConsecutiveCount > 0)
                ? ((double)dominantVolume / imbalanceConsecutiveCount) / referenceVolume
                : 0;

            if (selectedIsBull)
            {
                isImbalanceBullish = true;
                currentImbalanceStatus = string.Format(
                    "ACHETEUR {0}-{1} [{2} niv {3}] (Vol:{4:N0}, Ratio:{5:P0})",
                    Instrument.MasterInstrument.FormatPrice(zoneBottom),
                    Instrument.MasterInstrument.FormatPrice(zoneTop),
                    imbalanceConsecutiveCount, mode, dominantVolume, ratio);
            }
            else
            {
                isImbalanceBearish = true;
                currentImbalanceStatus = string.Format(
                    "VENDEUR {0}-{1} [{2} niv {3}] (Vol:{4:N0}, Ratio:{5:P0})",
                    Instrument.MasterInstrument.FormatPrice(zoneBottom),
                    Instrument.MasterInstrument.FormatPrice(zoneTop),
                    imbalanceConsecutiveCount, mode, dominantVolume, ratio);
            }

            // Memorisation de la zone : une pile de 3+ niveaux reste un niveau
            // operationnel sur les barres suivantes (retest / defense).
            long refBarVol = currentBar != null ? currentBar.TotalVolume : 0;
            RegisterImbalanceZone(zoneBottom, zoneTop, selectedIsBull, imbalanceConsecutiveCount, barIdx, refBarVol);

            lastImbalanceBarIndex = barIdx;
        }

        private static string ResolveInstrumentRoot(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string u = name.ToUpper().Trim();

            string[] knownRoots = { "MNQ", "MES", "MGC", "MYM", "M2K", "NQ", "ES", "GC", "YM", "RTY", "CL", "SI", "HG", "ZB", "ZN", "ZC", "ZS", "ZW", "6E", "6J", "6B" };
            foreach (string root in knownRoots)
            {
                if (u.StartsWith(root))
                    return root;
            }

            return u.Length >= 2 ? u.Substring(0, 2) : u;
        }
        #endregion
    }
}
