#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Xml.Serialization;
using NinjaTrader.NinjaScript;
using SMI = NinjaTrader.NinjaScript.Indicators.SniperMarketIntelligence;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    /// <summary>Niveaux d'alerte du preset Scalping Pro.</summary>
    public enum ScalpingProTier
    {
        /// <summary>Score insuffisant : aucune alerte.</summary>
        Aucun,
        /// <summary>Setup valide, confluence minimale.</summary>
        Moyen,
        /// <summary>Setup solide.</summary>
        Fort,
        /// <summary>Setup premium : toutes les briques alignees.</summary>
        TresFort
    }

    /// <summary>Familles de candidats de trading (Preuves).</summary>
    public enum CandidateFamily
    {
        Reversal,
        Continuation,
        Breakout,
        Location,
        Microstructure
    }

    /// <summary>Setups composites (Reversal, Continuation, Breakout).</summary>
    public enum SetupType
    {
        Reversal,
        Continuation,
        Breakout
    }

    /// <summary>
    /// Preset ScalpingPro : profil destiné au trading réel (5 à 10 setups institutionnels par session).
    /// Pipeline : Contexte -> Market Structure -> Liquidity -> Order Block -> Footprint -> Volume -> Momentum -> Risk -> Alert.
    /// </summary>
    public partial class AuctionMarketCore
    {
        #region SCALPING PRO - Contrats (interfaces, DI)

        /// <summary>Photographie du contexte de marche transmise aux collaborateurs
        /// du preset Scalping Pro. Structure immuable : elle decouple les modules
        /// (structure SMC, footprint, scoring) de l'indicateur lui-meme, ce qui les
        /// rend testables hors NinjaTrader (principe D de SOLID).</summary>
        internal sealed class ScalpingProContext
        {
            public int BarIndex;
            public DateTime Time;
            public bool IsBuy;
            public double Open, High, Low, Close;
            public double Atr;
            public double TickSize;

            /// <summary>Scores bruts de l'entonnoir existant (N1..N4), reutilises
            /// tels quels : le preset ne recalcule pas ce que le moteur sait deja.</summary>
            public double N1, N2, N3, N4, Penalty;

            public bool HasImbalance;
            public int ImbalanceLevels;
            public bool HasAbsorption;
            public double AbsorptionZSum;
            public bool DeltaCoherent;
            public double ZDelta;
            public bool HasExhaustion;
            public double VolumeRank;      // 0..100
            public bool HtfAligned;

            // NOUVEAU STEP 2 : Profiling Initial Balance (IB) & Session
            public bool IsIbComplete;
            public double IbHigh, IbLow, IbRange;
            public double IbExtensionRatio;
            public string DayType;
            public bool IsIbUpExtension, IsIbDownExtension;

            // NOUVEAU STEP 2 : Order Flow CME - Finished Auction & Unfinished Business
            public bool IsFinishedAuction;
            public bool HasUnfinishedMagnet;
            public double UnfinishedMagnetPrice;

            // NOUVEAU : Extrêmes VWAP HTF Clôturés (SD ±2 / ±3 Mois & Semaine)
            public bool IsNearClosedVwapSdExtreme;
            public string ClosedVwapSdExtremeName;
            public double VwapSigmaDistance;

            // Contexte Market Intelligence, fourni sans recalcul.
            public SMI.MiBias MiBias;
            public int MiConfidence;
            public int MiPenalty;

            // Classification V7.8 & Modificateurs
            public CandidateFamily CandidateFamily;
            public SetupType SetupType;
            public string CandidateName;
            public double HtfModifier;
            public double M5Modifier;
        }

        /// <summary>Evalue la confluence Smart Money Concepts autour de la barre
        /// evaluee (BOS, CHOCH, Order Block, Liquidity Sweep, FVG, Inversion Breakers, Mitigation).</summary>
        internal interface ISmcConfluenceEvaluator
        {
            /// <summary>Alimente le suivi de structure avec la barre evaluee.</summary>
            void OnBar(int barIndex, double open, double high, double low, double close, double atr);
            /// <summary>Reinitialise l'etat (nouvelle session, F5, changement d'instrument).</summary>
            void Reset();
            /// <summary>Retourne la confluence SMC pour le sens demande.</summary>
            SmcConfluence Evaluate(bool isBuy, int barIndex);
        }

        /// <summary>Valide (ou rejette) un setup sur preuve de footprint.</summary>
        internal interface IFootprintValidator
        {
            FootprintEvidence Validate(ScalpingProContext ctx);
        }

        /// <summary>Calcule le score pondere final 0..100.</summary>
        internal interface IWeightedScoreModel
        {
            WeightedScore Compute(ScalpingProContext ctx, SmcConfluence smc, FootprintEvidence fp);
        }

        /// <summary>Traduit un score en niveau d'alerte (Moyen / Fort / Tres Fort).</summary>
        internal interface IAlertTierResolver
        {
            NinjaTrader.NinjaScript.Indicators.ScalpingProTier Resolve(double score);
        }

        #endregion

        #region SCALPING PRO - Modeles de resultat

        /// <summary>Detail de la confluence SMC : chaque element porte son propre
        /// poids, ce qui rend le bareme auditable ligne a ligne dans le journal.</summary>
        internal sealed class SmcConfluence
        {
            public bool Bos;
            public bool Choch;
            public bool OrderBlock;
            public bool LiquiditySweep;
            public bool FairValueGap;
            public bool InversionFvg;   // NOUVEAU STEP 2 : FVG Breaker / Inversion
            public bool Mitigation;
            public double Points;      // somme ponderee
            public double MaxPoints;   // total atteignable (normalisation)
            public string Detail = "";

            /// <summary>Confluence normalisee 0..1.</summary>
            public double Normalized
            {
                get { return MaxPoints > 0 ? Clamp(Points / MaxPoints, 0, 1) : 0; }
            }
        }

        /// <summary>Preuve de footprint. En Scalping Pro le footprint est OBLIGATOIRE :
        /// aucune preuve = setup rejete, quel que soit le reste du score.</summary>
        internal sealed class FootprintEvidence
        {
            public bool Imbalance;
            public bool Absorption;
            public bool DeltaCoherent;
            public bool Exhaustion;
            public bool FinishedAuction;   // NOUVEAU STEP 2 : Vrai rejet à zéro contrat
            public bool UnfinishedMagnet;  // NOUVEAU STEP 2 : Aimant Poor High / Poor Low
            public double Strength;    // 0..1
            public string Detail = "";

            /// <summary>
            /// Evidence score 0..1. Une seule micro-preuve faible ne suffit plus à
            /// valider le footprint obligatoire : il faut au moins 0.30 d'évidence,
            /// ce qui correspond à une preuve forte ou à plusieurs preuves moyennes.
            /// </summary>
            public double EvidenceScore { get; set; }

            public bool IsValid
            {
                get { return EvidenceScore >= 0.30; }
            }

            public string Status
            {
                get
                {
                    if (EvidenceScore <= 0) return "ABSENT";
                    if (EvidenceScore < 0.30) return "WEAK";
                    return "STRONG";
                }
            }
        }

        /// <summary>Decomposition du score pondere (somme = 100 maximum).</summary>
        internal sealed class WeightedScore
        {
            public double Structure;   // 30
            public double Footprint;   // 30
            public double Volume;      // 15
            public double Momentum;    // 15
            public double Context;     // 10
            public double Penalty;     // modulateurs du moteur (negatif ou bonus IB/HTF)
            public double Total;       // 0..100
            public string Detail = "";
        }

        #endregion

        #region SCALPING PRO - Suivi de structure de marche (SMC)

        /// <summary>Suivi incremental de la structure de marche : pivots, BOS, CHOCH,
        /// Order Blocks, balayages de liquidite, Fair Value Gaps (creation, mitigation, inversion breaker).
        /// Implementation volontairement O(1) par barre (ring buffer borne) : le
        /// moteur tourne en temps reel sur des barres volumetriques.</summary>
        internal sealed class SmcStructureTracker : ISmcConfluenceEvaluator
        {
            private const int Capacity = 256;
            private const int PivotStrength = 2;
            private const int MaxFvgZones = 16;

            private struct FvgZone
            {
                public bool IsBull;
                public double Top;
                public double Bottom;
                public int BarIndex;
                public bool Mitigated;
                public bool Inverted; // true si traverse a la cloture (devient Breaker S/R oppose)
            }

            private readonly double[] o = new double[Capacity];
            private readonly double[] h = new double[Capacity];
            private readonly double[] l = new double[Capacity];
            private readonly double[] c = new double[Capacity];
            private readonly int[] idx = new int[Capacity];
            private int count;
            private int head = -1;
            private int lastBar = int.MinValue;

            private double swingHigh, swingLow;
            private int swingHighBar = -1, swingLowBar = -1;
            private int trend;                       // +1 haussier, -1 baissier, 0 indetermine

            private int bosBullBar = -1, bosBearBar = -1;
            private int chochBullBar = -1, chochBearBar = -1;
            private int sweepBullBar = -1, sweepBearBar = -1;
            private int fvgBullBar = -1, fvgBearBar = -1;
            private int inversionBullBar = -1, inversionBearBar = -1; // FVG Inversion Breakers
            private int obBullBar = -1, obBearBar = -1;
            private double obBullTop, obBullBottom, obBearTop, obBearBottom;
            private int mitigBullBar = -1, mitigBearBar = -1;
            private int lastBullBreakBar = int.MinValue, lastBearBreakBar = int.MinValue;

            // Zones FVG dynamiques (mémoire bornée)
            private readonly FvgZone[] fvgZones = new FvgZone[MaxFvgZones];
            private int fvgCount = 0;

            private SmcConfluence cacheBull, cacheBear;
            private int cacheBullBar = int.MinValue, cacheBearBar = int.MinValue;

            private readonly System.Text.StringBuilder evalSb = new System.Text.StringBuilder(64);

            private readonly SmcWeights w;
            private readonly int maxAgeBars;

            public SmcStructureTracker(SmcWeights weights, int maxAgeBars)
            {
                this.w = weights ?? new SmcWeights();
                this.maxAgeBars = Math.Max(3, maxAgeBars);
            }

            public void Reset()
            {
                count = 0; head = -1; lastBar = int.MinValue;
                swingHigh = 0; swingLow = 0; swingHighBar = -1; swingLowBar = -1; trend = 0;
                bosBullBar = bosBearBar = chochBullBar = chochBearBar = -1;
                sweepBullBar = sweepBearBar = fvgBullBar = fvgBearBar = -1;
                inversionBullBar = inversionBearBar = -1;
                obBullBar = obBearBar = mitigBullBar = mitigBearBar = -1;
                obBullTop = obBullBottom = obBearTop = obBearBottom = 0;
                lastBullBreakBar = lastBearBreakBar = int.MinValue;
                fvgCount = 0;
                cacheBull = cacheBear = null;
                cacheBullBar = cacheBearBar = int.MinValue;
            }

            private double H(int back) { return h[(head - back + Capacity * 2) % Capacity]; }
            private double L(int back) { return l[(head - back + Capacity * 2) % Capacity]; }
            private double O(int back) { return o[(head - back + Capacity * 2) % Capacity]; }
            private double C(int back) { return c[(head - back + Capacity * 2) % Capacity]; }
            private int I(int back) { return idx[(head - back + Capacity * 2) % Capacity]; }

            public void OnBar(int barIndex, double open, double high, double low, double close, double atr)
            {
                if (barIndex == lastBar) return;      // idempotent : une seule passe par barre
                lastBar = barIndex;
                cacheBull = cacheBear = null;

                head = (head + 1) % Capacity;
                o[head] = open; h[head] = high; l[head] = low; c[head] = close; idx[head] = barIndex;
                if (count < Capacity) count++;
                if (count < PivotStrength * 2 + 2) return;

                DetectPivots();
                DetectStructureBreaks(barIndex, close, high, low);
                DetectSweeps(barIndex, close, high, low, atr);
                DetectAndManageFvg(barIndex, high, low, close);
                DetectMitigation(barIndex, high, low);
            }

            private void DetectPivots()
            {
                int p = PivotStrength;
                double ph = H(p), pl = L(p);
                bool isHigh = true, isLow = true;
                for (int k = 1; k <= p; k++)
                {
                    if (H(p - k) >= ph || H(p + k) >= ph) isHigh = false;
                    if (L(p - k) <= pl || L(p + k) <= pl) isLow = false;
                }
                if (isHigh && I(p) > lastBullBreakBar) { swingHigh = ph; swingHighBar = I(p); }
                if (isLow && I(p) > lastBearBreakBar) { swingLow = pl; swingLowBar = I(p); }
            }

            private void DetectStructureBreaks(int barIndex, double close, double high, double low)
            {
                if (swingHighBar >= 0 && swingHigh > 0 && close > swingHigh)
                {
                    if (trend >= 0) bosBullBar = barIndex; else chochBullBar = barIndex;
                    CaptureOrderBlock(true, barIndex);
                    trend = 1;
                    lastBullBreakBar = barIndex;
                    swingHigh = high;
                }
                else if (swingLowBar >= 0 && swingLow > 0 && close < swingLow)
                {
                    if (trend <= 0) bosBearBar = barIndex; else chochBearBar = barIndex;
                    CaptureOrderBlock(false, barIndex);
                    trend = -1;
                    lastBearBreakBar = barIndex;
                    swingLow = low;
                }
            }

            private void CaptureOrderBlock(bool bullish, int barIndex)
            {
                for (int back = 1; back <= Math.Min(10, count - 1); back++)
                {
                    bool opposite = bullish ? C(back) < O(back) : C(back) > O(back);
                    if (!opposite) continue;
                    if (bullish) { obBullBar = barIndex; obBullTop = H(back); obBullBottom = L(back); }
                    else { obBearBar = barIndex; obBearTop = H(back); obBearBottom = L(back); }
                    return;
                }
            }

            private void DetectSweeps(int barIndex, double close, double high, double low, double atr)
            {
                double minPierce = atr > 0 ? atr * 0.05 : 0;
                if (swingLow > 0 && low < swingLow - minPierce && close > swingLow) sweepBullBar = barIndex;
                if (swingHigh > 0 && high > swingHigh + minPierce && close < swingHigh) sweepBearBar = barIndex;
            }

            /// <summary>Gestion complete du cycle FVG : detection, mitigation et inversion breakers.</summary>
            private void DetectAndManageFvg(int barIndex, double high, double low, double close)
            {
                if (count >= 3)
                {
                    double l0 = L(0), h2 = H(2);
                    double h0 = H(0), l2 = L(2);

                    if (l0 > h2) // Bullish FVG
                    {
                        fvgBullBar = barIndex;
                        AddFvgZone(true, l0, h2, barIndex);
                    }
                    if (h0 < l2) // Bearish FVG
                    {
                        fvgBearBar = barIndex;
                        AddFvgZone(false, l2, h0, barIndex);
                    }
                }

                // Cycle de vie des zones FVG
                for (int i = 0; i < fvgCount; i++)
                {
                    if (barIndex - fvgZones[i].BarIndex > maxAgeBars * 2) continue;

                    if (fvgZones[i].IsBull)
                    {
                        if (!fvgZones[i].Inverted)
                        {
                            if (low <= fvgZones[i].Top && high >= fvgZones[i].Bottom && close >= fvgZones[i].Bottom)
                            {
                                fvgZones[i].Mitigated = true;
                                fvgBullBar = barIndex;
                            }
                            else if (close < fvgZones[i].Bottom)
                            {
                                fvgZones[i].Inverted = true;
                                inversionBearBar = barIndex; // Breaker FVG Bearish
                            }
                        }
                        else
                        {
                            if (high >= fvgZones[i].Bottom && low <= fvgZones[i].Top && close <= fvgZones[i].Top)
                                inversionBearBar = barIndex;
                        }
                    }
                    else
                    {
                        if (!fvgZones[i].Inverted)
                        {
                            if (high >= fvgZones[i].Bottom && low <= fvgZones[i].Top && close <= fvgZones[i].Top)
                            {
                                fvgZones[i].Mitigated = true;
                                fvgBearBar = barIndex;
                            }
                            else if (close > fvgZones[i].Top)
                            {
                                fvgZones[i].Inverted = true;
                                inversionBullBar = barIndex; // Breaker FVG Bullish
                            }
                        }
                        else
                        {
                            if (low <= fvgZones[i].Top && high >= fvgZones[i].Bottom && close >= fvgZones[i].Bottom)
                                inversionBullBar = barIndex;
                        }
                    }
                }
            }

            private void AddFvgZone(bool isBull, double top, double bottom, int barIndex)
            {
                // Purge préalable des zones mitigées ou obsolètes pour ne pas éjecter prématurément des zones actives
                int writeIdx = 0;
                for (int i = 0; i < fvgCount; i++)
                {
                    bool isOld = (barIndex - fvgZones[i].BarIndex > maxAgeBars * 2);
                    if (!fvgZones[i].Mitigated && !isOld)
                    {
                        if (writeIdx != i) fvgZones[writeIdx] = fvgZones[i];
                        writeIdx++;
                    }
                }
                fvgCount = writeIdx;

                if (fvgCount < MaxFvgZones)
                {
                    fvgZones[fvgCount] = new FvgZone { IsBull = isBull, Top = top, Bottom = bottom, BarIndex = barIndex, Mitigated = false, Inverted = false };
                    fvgCount++;
                }
                else
                {
                    for (int i = 0; i < MaxFvgZones - 1; i++)
                        fvgZones[i] = fvgZones[i + 1];
                    fvgZones[MaxFvgZones - 1] = new FvgZone { IsBull = isBull, Top = top, Bottom = bottom, BarIndex = barIndex, Mitigated = false, Inverted = false };
                }
            }

            private void DetectMitigation(int barIndex, double high, double low)
            {
                if (obBullBar >= 0 && obBullTop > 0 && barIndex >= obBullBar + 2
                    && low <= obBullTop && high >= obBullBottom) mitigBullBar = barIndex;
                if (obBearBar >= 0 && obBearTop > 0 && barIndex >= obBearBar + 2
                    && high >= obBearBottom && low <= obBearTop) mitigBearBar = barIndex;
            }

            private bool Fresh(int eventBar, int barIndex)
            {
                return eventBar >= 0 && barIndex - eventBar <= maxAgeBars;
            }

            public SmcConfluence Evaluate(bool isBuy, int barIndex)
            {
                if (isBuy && cacheBull != null && cacheBullBar == barIndex) return cacheBull;
                if (!isBuy && cacheBear != null && cacheBearBar == barIndex) return cacheBear;

                SmcConfluence r = new SmcConfluence();
                r.Bos = Fresh(isBuy ? bosBullBar : bosBearBar, barIndex);
                r.Choch = Fresh(isBuy ? chochBullBar : chochBearBar, barIndex);
                r.OrderBlock = Fresh(isBuy ? obBullBar : obBearBar, barIndex);
                r.LiquiditySweep = Fresh(isBuy ? sweepBullBar : sweepBearBar, barIndex);
                r.FairValueGap = Fresh(isBuy ? fvgBullBar : fvgBearBar, barIndex);
                r.InversionFvg = Fresh(isBuy ? inversionBullBar : inversionBearBar, barIndex);
                r.Mitigation = Fresh(isBuy ? mitigBullBar : mitigBearBar, barIndex);

                double pts = 0;
                System.Text.StringBuilder sb = evalSb;
                sb.Length = 0;
                if (r.Bos) { pts += w.Bos; sb.Append("BOS+").Append(w.Bos).Append(' '); }
                if (r.Choch) { pts += w.Choch; sb.Append("CHOCH+").Append(w.Choch).Append(' '); }
                if (r.OrderBlock) { pts += w.OrderBlock; sb.Append("OB+").Append(w.OrderBlock).Append(' '); }
                if (r.LiquiditySweep) { pts += w.LiquiditySweep; sb.Append("SWEEP+").Append(w.LiquiditySweep).Append(' '); }
                if (r.FairValueGap) { pts += w.FairValueGap; sb.Append("FVG+").Append(w.FairValueGap).Append(' '); }
                if (r.InversionFvg) { pts += w.InversionFvg; sb.Append("INV_FVG+").Append(w.InversionFvg).Append(' '); }
                if (r.Mitigation) { pts += w.Mitigation; sb.Append("MITIG+").Append(w.Mitigation).Append(' '); }

                r.Points = pts;
                r.MaxPoints = w.Total;
                r.Detail = sb.Length > 0 ? sb.ToString().Trim() : "aucun element SMC";

                if (isBuy) { cacheBull = r; cacheBullBar = barIndex; }
                else { cacheBear = r; cacheBearBar = barIndex; }
                return r;
            }
        }

        /// <summary>Bareme SMC mis a jour avec Inversion FVG Breakers.</summary>
        internal sealed class SmcWeights
        {
            public double Bos = 8;
            public double Choch = 7;
            public double OrderBlock = 6;
            public double LiquiditySweep = 6;
            public double FairValueGap = 5;
            public double InversionFvg = 5;
            public double Mitigation = 4;

            /// <summary>Total exact des poids SMC (8+7+6+6+5+5+4 = 41) pour éliminer la saturation.</summary>
            public double Total = 41;
        }

        #endregion

        #region SCALPING PRO - Validateur Footprint

        /// <summary>Footprint obligatoire avec Unfinished Business et Finished Auction.</summary>
        internal sealed class FootprintValidator : IFootprintValidator
        {
            private readonly double deltaZMin;
            private readonly System.Text.StringBuilder fpSb = new System.Text.StringBuilder(64);
            private readonly FootprintEvidence pooledEvidence = new FootprintEvidence();

            public FootprintValidator(double deltaZMin)
            {
                this.deltaZMin = Math.Max(0.2, deltaZMin);
            }

            public FootprintEvidence Validate(ScalpingProContext ctx)
            {
                FootprintEvidence e = pooledEvidence;
                e.Imbalance = ctx.HasImbalance;
                e.Absorption = ctx.HasAbsorption;
                e.DeltaCoherent = ctx.DeltaCoherent;
                e.Exhaustion = ctx.HasExhaustion;
                e.FinishedAuction = ctx.IsFinishedAuction;
                e.UnfinishedMagnet = ctx.HasUnfinishedMagnet;

                double s = 0;
                System.Text.StringBuilder sb = fpSb;
                sb.Length = 0;
                if (e.Imbalance)
                {
                    double v = Clamp(ctx.ImbalanceLevels / 6.0, 0.35, 1.0) * 0.20;
                    s += v; sb.Append("IMB x").Append(ctx.ImbalanceLevels).Append(' ');
                }
                if (e.Absorption)
                {
                    double v = Clamp(Math.Abs(ctx.AbsorptionZSum) / 4.0, 0.35, 1.0) * 0.20;
                    s += v; sb.Append("ABS ");
                }
                if (e.DeltaCoherent)
                {
                    double v = Clamp(Math.Abs(ctx.ZDelta) / (deltaZMin * 2.0), 0.35, 1.0) * 0.15;
                    s += v; sb.Append("DELTA ");
                }
                if (e.Exhaustion) { s += 0.10; sb.Append("EXH "); }
                if (e.FinishedAuction) { s += 0.20; sb.Append("FA_EXHAUST "); }
                if (e.UnfinishedMagnet) { s += 0.15; sb.Append("UNF_MAGNET "); }

                e.Strength = Clamp(s, 0, 1);
                e.EvidenceScore = e.Strength;
                e.Detail = string.Format(CultureInfo.InvariantCulture, "FOOTPRINT {0} (score={1:0.00}): {2}", 
                    e.Status, e.EvidenceScore, e.EvidenceScore > 0 ? sb.ToString().Trim() : "Aucune preuve");
                return e;
            }
        }

        #endregion

        #region SCALPING PRO - Scoring pondere et niveaux d'alerte

        /// <summary>Score pondere : Structure 30%, Footprint 30%, Volume 15%,
        /// Momentum 15%, Contexte 10% avec modulation Initial Balance (IB).</summary>
        internal sealed class WeightedScoreModel : IWeightedScoreModel
        {
            private readonly double wStructure;
            private readonly double wFootprint;
            private readonly double wVolume;
            private readonly double wMomentum;
            private readonly double wContext;
            private readonly WeightedScore pooledScore = new WeightedScore();

            public WeightedScoreModel()
            {
                // AMC PRO unified ScalpingPro model.
                // Adaptive scoring has been removed to keep the decision engine
                // deterministic, auditable and statistically calibratable.
                wStructure = 30.0;
                wFootprint = 30.0;
                wVolume = 15.0;
                wMomentum = 15.0;
                wContext = 10.0;
            }

            public WeightedScore Compute(ScalpingProContext ctx, SmcConfluence smc, FootprintEvidence fp)
            {
                WeightedScore r = pooledScore;

                bool isBreakout = ctx.SetupType == SetupType.Breakout;
                double effWStructure = isBreakout ? wStructure + (wFootprint * 0.5) : wStructure;
                double effWMomentum = isBreakout ? wMomentum + (wFootprint * 0.5) : wMomentum;
                double effWFootprint = isBreakout ? 0.0 : wFootprint;

                // Plafonnements stricts par famille (Anti Double-Counting)
                double structureNorm = Clamp(0.60 * smc.Normalized + 0.40 * (ctx.N2 / 30.0), 0, 1);
                double footprintNorm = isBreakout ? 0.0 : Clamp(0.70 * fp.Strength + 0.30 * (ctx.N3 / 25.0), 0, 1);
                double volumeNorm = Clamp(ctx.VolumeRank / 100.0, 0, 1);
                double momentumNorm = Clamp(0.60 * (ctx.N4 / 15.0) + 0.40 * Clamp(Math.Abs(ctx.ZDelta) / 2.0, 0, 1), 0, 1);
                double contextNorm = Clamp(ctx.N1 / 30.0, 0, 1);

                r.Structure = Math.Min(effWStructure, structureNorm * effWStructure);
                r.Footprint = isBreakout ? 0.0 : Math.Min(effWFootprint, footprintNorm * effWFootprint);
                r.Volume = Math.Min(wVolume, volumeNorm * wVolume);
                r.Momentum = Math.Min(effWMomentum, momentumNorm * effWMomentum);
                r.Context = Math.Min(wContext, contextNorm * wContext);

                // Modulateurs HTF, M5 et Initial Balance (IB) - Plafonnement anti-double pénalité
                double htfMod = ctx.HtfModifier;
                double m5Mod = ctx.M5Modifier;
                if (htfMod < 0 && m5Mod < 0)
                {
                    // Si les deux modificateurs sont négatifs (désalignement double M15 + H4/H5), on plafonne le cumul à -5.0 max
                    double combinedHtf = htfMod + m5Mod;
                    if (combinedHtf < -5.0)
                    {
                        htfMod = -3.0;
                        m5Mod = -2.0;
                    }
                }
                double ibMod = 0.0;

                if (ctx.IsIbComplete)
                {
                    bool isTrendDay = ctx.IbExtensionRatio >= 1.5;
                    bool isRangeDay = ctx.IbExtensionRatio < 0.5;

                    if (isTrendDay)
                    {
                        bool trendAligned = (ctx.IsBuy && ctx.IsIbUpExtension) || (!ctx.IsBuy && ctx.IsIbDownExtension);
                        bool isMacroInflection = ctx.IsNearClosedVwapSdExtreme || Math.Abs(ctx.VwapSigmaDistance) >= 2.0;

                        if (isBreakout)
                        {
                            bool againstExtremeWall = (ctx.IsBuy && ctx.IsNearClosedVwapSdExtreme && ctx.ClosedVwapSdExtremeName != null && ctx.ClosedVwapSdExtremeName.Contains("SD+"))
                                                   || (!ctx.IsBuy && ctx.IsNearClosedVwapSdExtreme && ctx.ClosedVwapSdExtremeName != null && ctx.ClosedVwapSdExtremeName.Contains("SD-"));

                            if (againstExtremeWall)
                                ibMod -= 5.0; // Interdiction d'acheter sur SD+2/+3 ou vendre sur SD-2/-3
                            else if (trendAligned)
                                ibMod += 4.0;
                            else
                                ibMod -= 5.0;
                        }
                        else if (ctx.SetupType == SetupType.Continuation && !isMacroInflection)
                        {
                            bool againstExtremeWall = (ctx.IsBuy && ctx.IsNearClosedVwapSdExtreme && ctx.ClosedVwapSdExtremeName != null && ctx.ClosedVwapSdExtremeName.Contains("SD+"))
                                                   || (!ctx.IsBuy && ctx.IsNearClosedVwapSdExtreme && ctx.ClosedVwapSdExtremeName != null && ctx.ClosedVwapSdExtremeName.Contains("SD-"));

                            if (againstExtremeWall)
                                ibMod -= 5.0;
                            else if (trendAligned)
                                ibMod += 4.0;
                            else
                                ibMod -= 5.0;
                        }
                        else // Reversal, Dual/Hybrid ou Inflexion à l'extrême
                        {
                            if (trendAligned)
                            {
                                ibMod += 2.0;
                            }
                            else
                            {
                                if (ctx.IsNearClosedVwapSdExtreme)
                                    ibMod += 2.0; // Reversal soutenu par support/résistance macro SD ±2 / ±3
                                else if (Math.Abs(ctx.VwapSigmaDistance) >= 2.5)
                                    ibMod += 1.0; // Épuisement statistique fort
                                else if (Math.Abs(ctx.VwapSigmaDistance) >= 2.0)
                                    ibMod = 0.0;  // Amorti/neutralisé
                                else
                                    ibMod -= 4.0;
                            }
                        }
                    }
                    else if (isRangeDay && ctx.SetupType == SetupType.Reversal)
                    {
                        bool nearIbHigh = ctx.IbHigh > 0 && Math.Abs(ctx.High - ctx.IbHigh) <= ctx.Atr * 0.5;
                        bool nearIbLow  = ctx.IbLow > 0 && Math.Abs(ctx.Low - ctx.IbLow) <= ctx.Atr * 0.5;
                        if ((ctx.IsBuy && nearIbLow) || (!ctx.IsBuy && nearIbHigh))
                            ibMod += 3.5;
                    }
                }

                // Bonus pour setups à haute fiabilité statistique (DELTA_FLIP, CUM_DELTA_DIV, NPOC_ABSORPTION)
                double setupBonus = 0.0;
                if (!string.IsNullOrEmpty(ctx.CandidateName))
                {
                    string cn = ctx.CandidateName.ToUpperInvariant();
                    if (cn.Contains("DELTA_FLIP") || cn.Contains("CUM_DELTA_DIV"))
                        setupBonus = 3.0;
                    else if (cn.Contains("NPOC_ABSORPTION"))
                        setupBonus = 4.0;

                    // Bonus de confluence d'inflexion macro (reversal/orderflow sur mur institutionnel SD ±2 / ±3 ou fenêtre de rebond)
                    if (ctx.IsNearClosedVwapSdExtreme)
                        setupBonus += 3.0;
                }

                r.Penalty = ctx.Penalty + htfMod + m5Mod + ibMod + setupBonus;
                r.Total = Clamp(r.Structure + r.Footprint + r.Volume + r.Momentum + r.Context + r.Penalty, 0, 100);

                r.Detail = string.Format(CultureInfo.InvariantCulture,
                    "PRO struct={0:0.0}/{1:0} foot={2:0.0}/{3:0} vol={4:0.0}/{5:0} mom={6:0.0}/{7:0} ctx={8:0.0}/{9:0} htfM15={10:+0.0;-0.0;0} m5Mod={11:+0.0;-0.0;0} ibMod={12:+0.0;-0.0;0} setupBonus={13:+0.0;-0.0;0} pen={14:0.0} => {15:0.0}/100",
                    r.Structure, effWStructure, r.Footprint, effWFootprint, r.Volume, wVolume,
                    r.Momentum, effWMomentum, r.Context, wContext, htfMod, m5Mod, ibMod, setupBonus, ctx.Penalty, r.Total);
                return r;
            }
        }

        /// <summary>Moyen / Fort / Tres Fort. Les seuils sont relatifs au score minimal
        /// d'alerte du preset (35) : Moyen 35-45, Fort 46-65, Tres Fort 66+.</summary>
        internal sealed class AlertTierResolver : IAlertTierResolver
        {
            private readonly double moyen, fort, tresFort;

            public AlertTierResolver(double moyen, double fort, double tresFort)
            {
                this.moyen = moyen; this.fort = fort; this.tresFort = tresFort;
            }

            public NinjaTrader.NinjaScript.Indicators.ScalpingProTier Resolve(double score)
            {
                if (score >= tresFort) return NinjaTrader.NinjaScript.Indicators.ScalpingProTier.TresFort;
                if (score >= fort) return NinjaTrader.NinjaScript.Indicators.ScalpingProTier.Fort;
                if (score >= moyen) return NinjaTrader.NinjaScript.Indicators.ScalpingProTier.Moyen;
                return NinjaTrader.NinjaScript.Indicators.ScalpingProTier.Aucun;
            }
        }

        #endregion

        #region SCALPING PRO - Collaborateurs injectes et parametres

        // Dependency Injection : les champs sont des INTERFACES. Les implementations
        // par defaut sont cablees dans InitScalpingPro() et peuvent etre remplacees
        // (tests unitaires, variantes de bareme) sans toucher au moteur.
        private ISmcConfluenceEvaluator smcEvaluator;
        private IFootprintValidator footprintValidator;
        private IWeightedScoreModel scalpingProScorer;
        private IAlertTierResolver alertTierResolver;
        /// <summary>Fenetre de fraicheur avec laquelle le detecteur SMC a ete construit.</summary>
        private int smcTrackerAgeBars = -1;

        // Series publiques pour le Strategy Analyzer
        [Browsable(false)]
        [XmlIgnore]
        public Series<double> ScalpingProScore { get; private set; }

        [Browsable(false)]
        [XmlIgnore]
        public Series<int> ScalpingProDirection { get; private set; }

        [Browsable(false)]
        [XmlIgnore]
        public Series<int> ScalpingProTier { get; private set; }

        // Valeurs courantes pour la stratégie
        private double currentScalpingProScore;
        private int currentScalpingProDirection;
        private int currentScalpingProTier;

        /// <summary>true car l'indicateur AuctionMarketCore exécute nativement le pipeline ScalpingPro.</summary>
        private bool IsScalpingPro
        {
            get { return true; }
        }

        [Display(Name = "HTF mode Soft (Scalping Pro)", Description = "true = le filtre HTF reste actif mais n'est jamais eliminatoire : un desalignement devient une penalite de score.", Order = 9, GroupName = "Sniper 02. Gates")]
        public bool HtfSoftMode { get; set; }

        [Range(3, 60)]
        [Display(Name = "Fraicheur des evenements SMC (barres)", Description = "Age maximal d'un BOS / CHOCH / OB / sweep / FVG pour compter dans la confluence.", Order = 10, GroupName = "Sniper 02. Gates")]
        public int SmcEventMaxAgeBars { get; set; }

        [Display(Name = "Footprint obligatoire (Scalping Pro)", Description = "true = un setup sans imbalance, absorption, delta coherent ni exhaustion est rejete.", Order = 11, GroupName = "Sniper 02. Gates")]
        public bool RequireFootprintEvidence { get; set; }

        [Range(0, 100)]
        [Display(Name = "Seuil alerte Fort", Order = 12, GroupName = "Sniper 01. Execution")]
        public int TierSilverScore { get; set; }

        [Range(0, 100)]
        [Display(Name = "Seuil alerte Tres Fort", Order = 13, GroupName = "Sniper 01. Execution")]
        public int TierGoldScore { get; set; }

        /// <summary>Valeurs par defaut des parametres ajoutes par ce fichier.
        /// Appele depuis ApplySniperDefaults() (State.SetDefaults).</summary>
        private void ApplyScalpingProDefaults()
        {
            HtfSoftMode = true;               // Actif par défaut : le désalignement HTF devient une pénalité modulatrice plutôt qu'un rejet bloquant
            SmcEventMaxAgeBars = 12;
            RequireFootprintEvidence = true;
            TierSilverScore = 45;
            TierGoldScore = 65;
            MinScoreToAlert = 45;
            NewsHardBlock = false;
            NewsWindowPenalty = 15;
            GateN1MinScore = 6;
            GateN2MinScore = 3;
            GateN3MinScore = 3;
            GateN4MinScore = 2;
            MaxStopPips = 0;                  // Désactivé : stop dynamique ATR + structure
            MinStopTicks = 12;
            MaxStopTicks = 160;
        }

        /// <summary>Cablage des dependances + remise a zero de l'etat structurel.
        /// Appele depuis InitSniperEngine() et a chaque rotation de session.</summary>
        private void InitScalpingPro()
        {
            // Initialisation des séries publiques (une seule fois)
            if (ScalpingProScore == null)
                ScalpingProScore = new Series<double>(this);
            if (ScalpingProDirection == null)
                ScalpingProDirection = new Series<int>(this);
            if (ScalpingProTier == null)
                ScalpingProTier = new Series<int>(this);

            // l'utilisateur modifie SmcEventMaxAgeBars, le detecteur doit etre recree,
            // sinon le parametre de l'interface reste sans effet.
            if (smcEvaluator == null || smcTrackerAgeBars != SmcEventMaxAgeBars)
            {
                smcEvaluator = new SmcStructureTracker(new SmcWeights(), SmcEventMaxAgeBars);
                smcTrackerAgeBars = SmcEventMaxAgeBars;
            }
            if (footprintValidator == null)
                footprintValidator = new FootprintValidator(1.0);
            scalpingProScorer = new WeightedScoreModel();
            alertTierResolver = new AlertTierResolver(MinScoreToAlert, TierSilverScore, TierGoldScore);

            smcEvaluator.Reset();
            
            // Réinitialiser les valeurs courantes
            currentScalpingProScore = 0;
            currentScalpingProDirection = 0;
            currentScalpingProTier = 0;
        }


        #endregion

        #region SCALPING PRO - Preset

        /// <summary>
        /// Profil ScalpingPro : preset d'exécution réelle orienté haute confluence.
        /// Équilibre entre qualité et fréquence (5 à 10 setups par session).
        /// </summary>
        private void ApplyScalpingProPreset()
        {
            // Un preset d'EXECUTION REELLE ne peut pas s'en accommoder.
            EvaluateOnBarClose = true;

            MinScoreToAlert = 45;                     // Seuil d'alerte équilibré à 45 (TierSilver / Grade FORT)
            MaxSniperAlertsPerSession = 0;            // Illimité (0 = illimité)
            MaxAlertsPerWeek = 0;                     // Illimité (0 = illimité)
            MaxAlertsPerSession = 0;                  // Illimité (0 = illimité)

            GateN1MinScore = 6;                       // Contexte      (/30)
            GateN2MinScore = 3;                       // Localisation  (/30) - Assoupli pour US session
            GateN3MinScore = 3;                       // Microstructure(/25) - Assoupli pour US session
            GateN4MinScore = 2;                       // Trigger       (/15)

            NewsHardBlock = false;                    // Mode pénalité pour laisser passer les setups ultra-forts
            NewsWindowPenalty = 15;                   // Pénalité de -15 points pendant les fenêtres de news

            SelectionBufferBars = 0;
            // Le buffer est nul mais le controle de derive reste actif : en reel,
            // un prix qui s'echappe d'un demi-ATR invalide l'entree.
            MaxEntryDriftAtr = 0.5;

            EnableHtfFilter = true;
            HtfStrictMode = false;
            HtfSoftMode = true; // Actif par défaut pour un scalping plus fluide
            HtfGateAppliesToMeanReversion = false;
            HtfMisalignmentPenalty = 4;
            EnableMarketIntelligence = true;

            MinRiskReward = 1.0;
            TargetR1 = 1.0;
            TargetR2 = 2.0;
            StopAtrMultiple = 1.75; // Élargi de 1.25 à 1.75 pour éviter les stops trop serrés
            StopBufferTicks = 6;  // Élargi de 4 à 6 ticks
            ExecutionCostTicks = 1;

            // de 200 / 5 contrats sur un profil d'execution reelle. On le fixe.
            RiskPerTradeCurrency = 100;
            MaxContracts = 2;
            MinStopTicks = 12;
            MaxStopTicks = 160; // Élargi à 160 pour supporter la volatilité US du NQ
            MaxStopPips = 0;   // 0 = Désactivé (stop dynamique ATR + structure)
            PipSize = 0.1;

            UseTrailingStop = true;
            TrailingStartPercent = 50;
            TrailWidthT2R = 2.0;

            KeyLevelToleranceAtr = 0.45;             // Scanner : 0.4, Scalping : 0.6
            NodeToleranceTicks = 4;
            AbsorptionKeyLevelTicks = 8;
            CompositeSessions = 15;

            AbsorptionZScore = -1.3;                 // Scanner : -1.5, Scalping : -0.8
            AbsorptionMinBars = 1;
            IcebergMinScore = 70;

            RequireFootprintEvidence = true;         // footprint obligatoire pour reversals
            SmcEventMaxAgeBars = 12;
            TierSilverScore = 45;
            TierGoldScore = 65;

            EnableShadowJournal = true;
            // et le mode debug sont desactives (charge CPU + volume de logs). Ils
            // restent disponibles manuellement pour les phases de calibration.
            EnableTradeJournal = true;
            JournalLiveOnly = false;
            JournalMaxBarsInTrade = 24;
            JournalShadowMode = false;
            EnableDebugMode = false;

            // le profil d'execution reelle.
            AutoCalibrationV3 = true;
            AutoProfileInstrument = true;
            EnableSessionBucketCalibration = true;
            AbsorptionDeltaPercentile = 90;
            AbsorptionDeltaThreshold = 300;          // plancher absolu

            InitScalpingPro();

            Print("AuctionMarketCore V8.0 : moteur institutionnel ScalpingPro initialise "
                + "(seuil " + MinScoreToAlert + "/100 pondere, gates " + GateN1MinScore + "/" + GateN2MinScore
                + "/" + GateN3MinScore + "/" + GateN4MinScore + ", buffer " + SelectionBufferBars
                + ", HTF SoftMode (modulateur de score), News Penalite -" + NewsWindowPenalty + " pts, R:R min " + MinRiskReward.ToString("F1", CultureInfo.InvariantCulture)
                + ", stop " + StopAtrMultiple.ToString("F1", CultureInfo.InvariantCulture)
                + " ATR, quota " + MaxSniperAlertsPerSession + "/seance). "
                + "Bareme : Structure 30% / Footprint 30% / Volume 15% / Momentum 15% / Contexte 10%. "
                + "Footprint obligatoire pour reversals, alertes Moyen(45)/Fort(50)/Tres Fort(65).");
        }

        #endregion

        #region SCALPING PRO - Greffe dans le pipeline

        /// <summary>Alimente le suivi de structure de marche avec la barre evaluee.
        /// Appele depuis SniperOnEvaluatedBar(), AVANT la construction des candidats
        /// (etape "Market Structure" du pipeline).</summary>
        private void ScalpingProOnEvaluatedBar()
        {
            if (!IsScalpingPro) return;
            if (smcEvaluator == null) InitScalpingPro();
            smcEvaluator.OnBar(evalBarIndex, snOpen, snHigh, snLow, snClose, SniperAtr());
        }

        /// <summary>Pipeline Scalping Pro applique a un candidat deja score par
        /// l'entonnoir N1..N4 :
        ///   Contexte (N1) -> Market Structure (SMC) -> Liquidity (sweep) ->
        ///   Order Block -> Footprint (obligatoire) -> Volume -> Momentum ->
        ///   Risk (deja calcule) -> Alert (Moyen / Fort / Tres Fort).
        /// Remplace le score somme par le score pondere et peut rejeter le candidat
        /// si aucune preuve de footprint n'est presente.
        /// Appele depuis Assemble(), uniquement quand le preset est actif.</summary>
        private void ApplyScalpingProPipeline(Candidate c, bool isBuy)
        {
            if (!IsScalpingPro || c == null) return;
            if (smcEvaluator == null || footprintValidator == null
                || scalpingProScorer == null || alertTierResolver == null) InitScalpingPro();

            ScalpingProContext ctx = BuildScalpingProContext(c, isBuy);
            if (ctx.MiPenalty != 0)
                c.Detail.Add(string.Format(CultureInfo.InvariantCulture,
                    "MI {0} conf={1}/100 penalty={2}", ctx.MiBias, ctx.MiConfidence, ctx.MiPenalty));

            SmcConfluence smc = smcEvaluator.Evaluate(isBuy, evalBarIndex);
            c.Detail.Add("SMC " + smc.Detail + string.Format(CultureInfo.InvariantCulture,
                " ({0:0.0}/{1:0} => {2:0.00})", smc.Points, smc.MaxPoints, smc.Normalized));

            FootprintEvidence fp = footprintValidator.Validate(ctx);
            c.Detail.Add(fp.Detail);

            WeightedScore ws = scalpingProScorer.Compute(ctx, smc, fp);
            c.Detail.Add(ws.Detail);

            // Le score pondere devient la reference du candidat (grade, journal,
            // dashboard et decision d'emission lisent ScoreRaw / Score).
            c.ScoreRaw = ws.Total;

            // Footprint est obligatoire pour les Reversals purs (Finished Auction, NPOC, Failed Auction),
            // mais assoupli pour les Breakouts (B1-B2) et les flux OrderFlow confirmés (Delta Flip, Cum Delta Div).
            bool isBreakout = c.SetupType == SetupType.Breakout || c.Name.Contains("BREAKOUT");
            bool isOrderflow = c.Name == "DELTA_FLIP" || c.Name == "CUM_DELTA_DIV";
            bool requireFootprint = RequireFootprintEvidence && !isBreakout && !isOrderflow;

            // LOGIQUE DE PORTE FOOTPRINT : 
            // Si le Footprint est "WEAK" (0.15-0.29) ou que le setup est OrderFlow/Breakout avec score solide, 
            // on autorise le signal pour ne pas rater des setups impulsifs clairs.
            bool footprintPass = fp.IsValid || isOrderflow || isBreakout || (fp.Status == "WEAK" && c.N3 >= 8.0);

            if (requireFootprint && !footprintPass && !c.Gated)
            {
                c.GateFailed = "FOOTPRINT_" + fp.Status;
                c.Gated = true;
                c.Detail.Add(string.Format(CultureInfo.InvariantCulture, 
                    "REJET SCALPING PRO: Footprint {0} (score={1:0.00}) insuffisant pour un reversal", fp.Status, fp.EvidenceScore));
            }

            // Filtre de qualité pour FINISHED_AUCTION : éliminer le bruit sous le seuil Silver (45)
            if (c.Name == "FINISHED_AUCTION" && c.ScoreRaw < TierSilverScore && !c.Gated)
            {
                c.GateFailed = "FA_SCORE_LOW";
                c.Gated = true;
                c.Detail.Add(string.Format(CultureInfo.InvariantCulture,
                    "FINISHED_AUCTION score {0:0.0} < {1} (seuil de qualite)", c.ScoreRaw, TierSilverScore));
            }

            // En ScalpingPro, si le score brut atteint le seuil d'alerte (>= MinScoreToAlert, ex: 50),
            // les portes de sous-notes secondaires (N2, N3, N4, FOOTPRINT) sont levées pour privilégier la confluence globale.
            // Pour RETEST_FVG, exiger un score solide (>= 52) pour lever la porte FOOTPRINT/Microstructure.
            double minRecoverableScore = (c.Name != null && c.Name.Contains("RETEST_FVG")) ? 52.0 : MinScoreToAlert;
            bool strongScore = c.ScoreRaw >= minRecoverableScore;
            bool isRecoverableGate = c.GateFailed == "N2_LOCALISATION" || c.GateFailed == "GATE_N2_FAILED" || c.GateFailed == "N2_LOW"
                                    || c.GateFailed == "N3_MICROSTRUCTURE" || c.GateFailed == "N4_TRIGGER" 
                                    || c.GateFailed == "FOOTPRINT_WEAK" || c.GateFailed == "FOOTPRINT_ABSENT";

            if (c.Gated && isRecoverableGate && (strongScore || (fp.IsValid && (isOrderflow || isBreakout))))
            {
                c.Gated = false;
                c.GateFailed = "";
                c.Detail.Add(string.Format(CultureInfo.InvariantCulture, "ScalpingPro: Porte levée ({0} admissible avec score {1:0.0})", c.Name, c.ScoreRaw));
            }

            // niveau (le dashboard laissait passer un "TRESFORT" sur un setup non emis).
            NinjaTrader.NinjaScript.Indicators.ScalpingProTier tier = c.Gated
                ? NinjaTrader.NinjaScript.Indicators.ScalpingProTier.Aucun
                : alertTierResolver.Resolve(c.ScoreRaw);
            c.Tier = tier == NinjaTrader.NinjaScript.Indicators.ScalpingProTier.Aucun ? null : tier.ToString().ToUpperInvariant();
            c.Score = c.Gated ? 0 : c.ScoreRaw;

            // Mise à jour des séries publiques pour le Strategy Analyzer
            currentScalpingProScore = c.Score;
            currentScalpingProDirection = isBuy ? 1 : -1;
            currentScalpingProTier = (int)tier;
            
            if (ScalpingProScore != null && CurrentBar >= 0 && CurrentBar < ScalpingProScore.Count)
                ScalpingProScore[CurrentBar] = currentScalpingProScore;
            if (ScalpingProDirection != null && CurrentBar >= 0 && CurrentBar < ScalpingProDirection.Count)
                ScalpingProDirection[CurrentBar] = currentScalpingProDirection;
            if (ScalpingProTier != null && CurrentBar >= 0 && CurrentBar < ScalpingProTier.Count)
                ScalpingProTier[CurrentBar] = currentScalpingProTier;
        }

        /// <summary>Assemble le contexte transmis aux modules : uniquement des valeurs
        /// deja produites par le moteur (aucun recalcul, aucun code duplique).</summary>
        private ScalpingProContext BuildScalpingProContext(Candidate c, bool isBuy)
        {
            ScalpingProContext ctx = new ScalpingProContext();
            ctx.BarIndex = evalBarIndex;
            ctx.Time = snTime;
            ctx.IsBuy = isBuy;
            ctx.Open = snOpen; ctx.High = snHigh; ctx.Low = snLow; ctx.Close = snClose;
            ctx.Atr = SniperAtr();
            ctx.TickSize = tickSize;
            ctx.N1 = c.N1; ctx.N2 = c.N2; ctx.N3 = c.N3; ctx.N4 = c.N4; ctx.Penalty = c.Penalty;
            ctx.HtfAligned = c.HtfAligned;
            ctx.MiBias = GetMarketIntelligenceBias();
            ctx.MiConfidence = GetMarketIntelligenceConfidence();
            ctx.MiPenalty = GetMarketIntelligenceDirectionalPenalty(isBuy);
            ctx.Penalty += ctx.MiPenalty;

            // Classification V7.8 & Modificateurs
            c.Family = GetCandidateFamily(c.Name);
            c.SetupType = GetSetupType(c.Family, c.Name);
            ctx.CandidateFamily = c.Family;
            ctx.SetupType = c.SetupType;
            ctx.CandidateName = c.Name;

            // Détection du test d'extrême VWAP HTF Clôturé (SD ±2 / ±3 Mois & Semaine) & Élongation
            string extremeVwapName;
            ctx.IsNearClosedVwapSdExtreme = IsNearClosedVwapSdExtreme(snClose, isBuy, out extremeVwapName);
            ctx.ClosedVwapSdExtremeName = extremeVwapName;
            ctx.VwapSigmaDistance = VwapSigmaDistance(snClose);

            // Rebound Window propagation : si le plancher/plafond a été touché récemment,
            // étendre le statut d'extrême pour que les modulateurs HTF/IB soient amortis.
            if (!ctx.IsNearClosedVwapSdExtreme)
            {
                const int ReboundWindowBars = 15;
                if (isBuy && (evalBarIndex - lastMacroSdFloorTouchBar) <= ReboundWindowBars)
                {
                    ctx.IsNearClosedVwapSdExtreme = true;
                    ctx.ClosedVwapSdExtremeName = "Rebound Window SD Floor (" + (evalBarIndex - lastMacroSdFloorTouchBar) + " bars)";
                }
                else if (!isBuy && (evalBarIndex - lastMacroSdCeilingTouchBar) <= ReboundWindowBars)
                {
                    ctx.IsNearClosedVwapSdExtreme = true;
                    ctx.ClosedVwapSdExtremeName = "Rebound Window SD Ceiling (" + (evalBarIndex - lastMacroSdCeilingTouchBar) + " bars)";
                }
            }

            ctx.HtfModifier = CalculateHtfModifier(c.SetupType, c.HtfAligned, c.Name, isBuy, ctx.IsNearClosedVwapSdExtreme, ctx.VwapSigmaDistance);
            ctx.M5Modifier = CalculateM5Modifier(isBuy, ctx.MiBias, ctx.MiConfidence);
            c.HtfModifier = ctx.HtfModifier;
            c.M5Modifier = ctx.M5Modifier;

            // Imbalance empilee encore fraiche, dans le sens du setup.
            int bestLevels = 0;
            for (int i = 0; i < imbalanceZones.Count; i++)
            {
                ImbalanceZone z = imbalanceZones[i];
                if (z.IsBull != isBuy) continue;
                if (evalBarIndex - z.BarIndex > SmcEventMaxAgeBars * 2) continue;
                if (z.Levels > bestLevels) bestLevels = z.Levels;
            }
            ctx.ImbalanceLevels = bestLevels;
            ctx.HasImbalance = bestLevels >= ImbalanceMinStack;

            // Absorption passive (cluster normalise) ou iceberg de meme famille.
            double zSum, clusterPrice;
            bool cluster = AbsorptionCluster(isBuy, out zSum, out clusterPrice);
            bool iceberg = (isBuy && isIcebergBullish) || (!isBuy && isIcebergBearish);
            ctx.HasAbsorption = cluster || iceberg;
            ctx.AbsorptionZSum = cluster ? zSum : 0;

            // Delta coherent : Z-delta dans le sens du setup, ou divergence cumulative.
            double zDelta = ZDeltaCurrent();
            ctx.ZDelta = zDelta;
            ctx.DeltaCoherent = (isBuy && zDelta >= 1.0) || (!isBuy && zDelta <= -1.0)
                                || (isBuy && isCumDeltaDivBullish) || (!isBuy && isCumDeltaDivBearish);

            // Exhaustion du camp OPPOSE : c'est une preuve favorable au setup.
            ctx.HasExhaustion = (isBuy && isExhaustionSell) || (!isBuy && isExhaustionBuy);

            // NOUVEAU STEP 2 : Initial Balance (IB) & Profiling de Session
            ctx.IsIbComplete = isIbComplete;
            ctx.IbHigh = ibHigh;
            ctx.IbLow = ibLow;
            ctx.IbRange = (ibHigh > double.MinValue && ibLow < double.MaxValue && ibHigh > ibLow) ? (ibHigh - ibLow) : 0;
            ctx.IbExtensionRatio = ibExtensionRatio;
            ctx.DayType = currentDayType;
            ctx.IsIbUpExtension = isIbUpExtension;
            ctx.IsIbDownExtension = isIbDownExtension;

            // NOUVEAU STEP 2 : Finished Auction & Unfinished Business
            ctx.IsFinishedAuction = isBuy ? isFinishedAuctionBuy : isFinishedAuctionSell;
            ctx.HasUnfinishedMagnet = isBuy ? (hasUnfinishedHigh && unfinishedHighPrice > snClose)
                                            : (hasUnfinishedLow && unfinishedLowPrice < snClose);
            ctx.UnfinishedMagnetPrice = isBuy ? unfinishedHighPrice : unfinishedLowPrice;

            ctx.VolumeRank = VolumeRankCurrent();
            return ctx;
        }

        private CandidateFamily GetCandidateFamily(string name)
        {
            if (string.IsNullOrEmpty(name)) return CandidateFamily.Reversal;
            string n = name.ToUpperInvariant();

            if (n.Contains("RETEST") || n.Contains("ACCEPTANCE") || n.Contains("DELTA_FLIP") || n.Contains("CUM_DELTA_DIV"))
                return CandidateFamily.Continuation;

            if (n.Contains("BREAKOUT") || n.Contains("IMBALANCE") || n.Contains("OPEN_DRIVE"))
                return CandidateFamily.Breakout;

            if (n == "POC" || n == "VAH" || n == "VAL" || n == "LVN" || n == "HVN" || n == "NPOC")
                return CandidateFamily.Location;

            return CandidateFamily.Reversal;
        }

        private SetupType GetSetupType(CandidateFamily family, string name)
        {
            if (family == CandidateFamily.Continuation) return SetupType.Continuation;
            if (family == CandidateFamily.Breakout) return SetupType.Breakout;
            return SetupType.Reversal;
        }

        private double CalculateHtfModifier(SetupType setupType, bool htfAligned, string candidateName, bool isBuy, bool isNearClosedVwapSdExtreme = false, double vwapSigma = 0.0)
        {
            if (htfAligned) return 4.0; // Bonus +4.0 pour HTF M15 aligne

            // Reversal sur extrême macro VWAP (SD ±2/±3) : bonus de mean-reversion au lieu de pénalité
            if (isNearClosedVwapSdExtreme) return 2.0;

            // Épuisement statistique fort (|Z| >= 2.0σ) : amortissement progressif
            double absSig = Math.Abs(vwapSigma);
            if (absSig >= 2.5) return 1.0;
            if (absSig >= 2.0) return 0.0;

            // HTF Oppose : penalite adaptative selon le setup et le sens
            string n = candidateName != null ? candidateName.ToUpperInvariant() : "";
            bool isExtremeReversal = n.Contains("NPOC") || n.Contains("FAILED_AUCTION") || n.Contains("EXHAUSTION") || n.Contains("DELTA_FLIP") || n.Contains("CUM_DELTA_DIV");

            // Penalite asymetrique adoucie pour eviter un blocage excessif en session US
            double shortExtraPenalty = (!isBuy) ? -1.0 : 0.0;

            if (isExtremeReversal) return -0.5 + shortExtraPenalty;
            if (setupType == SetupType.Reversal) return -1.0 + shortExtraPenalty;
            if (setupType == SetupType.Breakout) return -2.0 + shortExtraPenalty;
            if (setupType == SetupType.Continuation) return -3.0 + shortExtraPenalty;

            return 0.0;
        }

        private double CalculateM5Modifier(bool isBuy, SMI.MiBias miBias, int miConfidence)
        {
            if (miBias == SMI.MiBias.BuyOnly)
            {
                double val = Math.Min(10.0, 3.0 + (miConfidence / 15.0));
                return isBuy ? val : -val;
            }
            else if (miBias == SMI.MiBias.SellOnly)
            {
                double val = Math.Min(10.0, 3.0 + (miConfidence / 15.0));
                return !isBuy ? val : -val;
            }
            return 0.0;
        }

        /// <summary>Agregation ScalpingPro : Un seul setup composite consolide par bougie.</summary>
        private void ConsolidateScalpingProCandidatesPerBar(Candidate c)
        {
            if (c == null) return;

            Candidate existing = null;
            for (int i = 0; i < pendingCandidates.Count; i++)
            {
                Candidate p = pendingCandidates[i];
                if (p.BarIdx == c.BarIdx && p.IsBuy == c.IsBuy)
                {
                    existing = p;
                    break;
                }
            }

            if (existing == null)
            {
                c.PrimaryCandidate = c.Name;
                if (!c.EvidenceList.Contains(c.Name))
                    c.EvidenceList.Add(c.Name);

                if (pendingCandidates.Count >= MaxPendingCandidates)
                {
                    pendingCandidates.RemoveAt(0);
                    pendingOverflowCount++;
                }
                pendingCandidates.Add(c);
            }
            else
            {
                if (!existing.EvidenceList.Contains(c.Name))
                {
                    existing.EvidenceList.Add(c.Name);
                    double confluenceBonus = Math.Min(6.0, (existing.EvidenceList.Count - 1) * 2.0);
                    existing.ScoreRaw = Clamp(existing.ScoreRaw + confluenceBonus, 0, 100);
                    existing.Score = existing.Gated ? 0 : existing.ScoreRaw;

                    if (!existing.Gated && alertTierResolver != null)
                    {
                        NinjaTrader.NinjaScript.Indicators.ScalpingProTier tier = alertTierResolver.Resolve(existing.ScoreRaw);
                        existing.Tier = tier == NinjaTrader.NinjaScript.Indicators.ScalpingProTier.Aucun ? null : tier.ToString().ToUpperInvariant();
                    }

                    existing.Detail.Add(string.Format(CultureInfo.InvariantCulture,
                        "Preuve ajoutee: {0} (bonus confluence +{1:0.0})", c.Name, confluenceBonus));
                }

                if (c.ScoreRaw > existing.ScoreRaw)
                {
                    existing.PrimaryCandidate = c.Name;
                    existing.Name = c.Name;
                    existing.Entry = c.Entry;
                    existing.Stop = c.Stop;
                    existing.Target1 = c.Target1;
                    existing.Target2 = c.Target2;
                    existing.Rr = c.Rr;
                    existing.ScoreRaw = c.ScoreRaw;
                    existing.Score = c.Gated ? 0 : c.ScoreRaw;
                }
            }
        }

        #endregion
    }
}
