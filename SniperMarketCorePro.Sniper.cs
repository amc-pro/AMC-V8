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
using NinjaTrader.NinjaScript.Indicators.VolumeProfilePro;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public partial class SniperMarketCorePro
    {

        // #                                                                               #
        // #                                                                               #
        // #   AMC Pro. Le moteur consomme les evenements et les niveaux deja calcules par  #
        // #   AMC Pro (profil incremental, absorption, iceberg, imbalance, finished        #
        // #   auction, exhaustion, Naked POC, IB / Day Type, VWAP, HTF, ATR) et n'ajoute   #
        // #   que : la normalisation Z par bucket horaire, le profil composite multi-      #
        // #   sessions, le scoring /100 a gates eliminatoires, le buffer best-of-window,   #
        // #                                                                               #
        // #   SECTION 1  : structures                                                      #
        // #   SECTION 2  : parametres                                                      #
        // #   SECTION 3  : etat interne                                                     #
        // #   SECTION 4  : cycle de vie (defaults / init / session / barre)                #
        // #   SECTION 5  : profils sniper (session + composite)                            #
        // #   SECTION 6  : calibration par bucket horaire                                   #
        // #   SECTION 7  : scoring N1..N4 + penalites + assemblage + risque                 #
        // #   SECTION 8  : les 5 setups                                                     #
        // #   SECTION 9  : buffer de selection best-of-window + quotas                      #
        // #   SECTION 10 : emission (Telegram / chart)                                      #
        // #   SECTION 11 : suivi des trades + journal shadow                                #
        // #                                                                               #

        #region SNIPER - Section 1 : structures

        /// <summary>Buffer circulaire avec statistiques roulantes (mean / std / percentile).
        /// Complement du RingBuffer&lt;T&gt; de l'AMC Pro, dedie au scoring.</summary>
        /// <summary>
        /// - Le cache trie est maintenu INCREMENTALEMENT (suppression binaire de la valeur
        ///   evincee + insertion binaire de la nouvelle) : plus aucun Array.Sort par appel.
        ///   Percentile() / PercentileRank() passent en O(log n), Add() en O(n) memmove.
        /// - Mean/Std en O(1) via sommes roulantes (re-synchronisees periodiquement pour
        ///   eviter la derive flottante).
        /// - Ajout du Z-Score robuste MAD, insensible aux fat tails de l'order flow.
        /// </summary>
        private sealed class SniperRingStat
        {
            private const double MadToSigma = 1.4826;   // coherence avec la loi normale
            private const int ResyncEvery = 4096;       // anti-derive des sommes roulantes

            private readonly double[] buf;
            private int head;
            private int count;
            private double[] sortCache;
            private int sortedLen;

            private double sum;
            private double sumSq;
            private int addsSinceResync;

            private double[] madScratch = new double[0];
            private double cachedMedian;
            private double cachedMad;
            private bool robustDirty = true;

            public SniperRingStat(int capacity)
            {
                buf = new double[Math.Max(8, capacity)];
                sortCache = new double[buf.Length];
            }

            public int Count { get { return count; } }
            public bool IsReady(int min) { return count >= min; }

            public void Add(double v)
            {
                if (count == buf.Length)
                {
                    double evicted = buf[head];       // la case ecrasee est la plus ancienne
                    SortedRemove(evicted);
                    sum -= evicted;
                    sumSq -= evicted * evicted;
                }
                else
                {
                    count++;
                }

                buf[head] = v;
                head = (head + 1) % buf.Length;

                SortedInsert(v);
                sum += v;
                sumSq += v * v;
                robustDirty = true;

                if (++addsSinceResync >= ResyncEvery) Resync();
            }

            public double this[int i]
            {
                get { return buf[(head - count + i + buf.Length) % buf.Length]; }
            }

            public void Clear()
            {
                head = 0; count = 0; sortedLen = 0;
                sum = 0; sumSq = 0; addsSinceResync = 0;
                cachedMedian = 0; cachedMad = 0;
                robustDirty = true;
            }

            public double Mean()
            {
                if (count == 0) return 0;
                return sum / count;
            }

            public double Std()
            {
                if (count < 2) return 0;
                double m = sum / count;
                double var = (sumSq / count) - (m * m);
                var = var * count / (count - 1);
                return var <= 0 ? 0 : Math.Sqrt(var);
            }

            /// <summary>Z-score classique : 0 tant que l'echantillon est insuffisant.</summary>
            public double Z(double v, int minSample)
            {
                if (count < minSample) return 0;
                double sd = Std();
                if (sd <= 1e-9) return 0;
                return (v - Mean()) / sd;
            }

            /// <summary>Mediane du buffer (O(1), cache trie deja maintenu).</summary>
            public double Median()
            {
                if (count == 0) return 0;
                if ((count & 1) == 1) return sortCache[count / 2];
                return 0.5 * (sortCache[count / 2 - 1] + sortCache[count / 2]);
            }

            /// <summary>Median Absolute Deviation (recalculee au plus une fois par Add utile).</summary>
            public double Mad()
            {
                EnsureRobust();
                return cachedMad;
            }

            /// <summary>
            /// Z = (v - median) / (1.4826 * MAD). Un spike geant d'absorption sur NQ
            /// ne desensibilise plus les barres suivantes (contrairement au Z classique).
            /// Repli automatique sur le Z classique si la MAD est degeneree (marche fige).
            /// </summary>
            public double ZMad(double v, int minSample)
            {
                if (count < minSample) return 0;
                EnsureRobust();
                double scale = MadToSigma * cachedMad;
                if (scale <= 1e-9) return Z(v, minSample);
                return (v - cachedMedian) / scale;
            }

            public double Percentile(double p)
            {
                if (count == 0) return 0;
                double rank = Math.Max(0, Math.Min(count - 1, (p / 100.0) * (count - 1)));
                int lo = (int)Math.Floor(rank);
                int hi = (int)Math.Ceiling(rank);
                if (lo == hi) return sortCache[lo];
                double frac = rank - lo;
                return sortCache[lo] * (1 - frac) + sortCache[hi] * frac;
            }

            /// <summary>Rang en percentile (0..100) de v dans le buffer glissant. O(log n).</summary>
            public double PercentileRank(double v) { return RankOf(v); }

            public double RankOf(double v)
            {
                if (count == 0) return 50;
                return 100.0 * UpperBound(v, sortedLen) / count;
            }

            // sortedLen est distinct de count : pendant Add(), le cache passe
            // transitoirement a count-1 elements valides (eviction puis insertion).

            private int LowerBound(double v, int len)
            {
                int lo = 0, hi = len;
                while (lo < hi)
                {
                    int mid = lo + ((hi - lo) >> 1);
                    if (sortCache[mid] < v) lo = mid + 1; else hi = mid;
                }
                return lo;
            }

            private int UpperBound(double v, int len)
            {
                int lo = 0, hi = len;
                while (lo < hi)
                {
                    int mid = lo + ((hi - lo) >> 1);
                    if (sortCache[mid] <= v) lo = mid + 1; else hi = mid;
                }
                return lo;
            }

            private void SortedInsert(double v)
            {
                int idx = LowerBound(v, sortedLen);
                if (idx < sortedLen)
                    Array.Copy(sortCache, idx, sortCache, idx + 1, sortedLen - idx);
                sortCache[idx] = v;
                sortedLen++;
            }

            // FIX AUDIT E5 : derive flottante corrigee. Au lieu d'un garde-fou aveugle
            // qui supprime le dernier element quand LowerBound depasse la borne,
            // on recherche la valeur la plus proche dans une fenetre de tolerance.
            private void SortedRemove(double v)
            {
                if (sortedLen <= 0) return;
                int idx = LowerBound(v, sortedLen);

                // Recherche de la meilleure correspondance dans un voisinage de ±1
                // autour du point d'insertion retourne par LowerBound.
                int bestIdx = -1;
                double bestDiff = double.MaxValue;
                int lo = Math.Max(0, idx - 1);
                int hi = Math.Min(sortedLen - 1, idx);
                for (int k = lo; k <= hi; k++)
                {
                    double diff = Math.Abs(sortCache[k] - v);
                    if (diff < bestDiff) { bestDiff = diff; bestIdx = k; }
                }

                if (bestIdx < 0) bestIdx = sortedLen - 1; // ultime garde-fou (ne devrait jamais arriver)

                if (bestIdx < sortedLen - 1)
                    Array.Copy(sortCache, bestIdx + 1, sortCache, bestIdx, sortedLen - 1 - bestIdx);
                sortedLen--;
            }

            private void EnsureRobust()
            {
                if (!robustDirty || count == 0) return;
                cachedMedian = Median();
                if (madScratch.Length < count) madScratch = new double[buf.Length];
                for (int i = 0; i < count; i++) madScratch[i] = Math.Abs(sortCache[i] - cachedMedian);
                Array.Sort(madScratch, 0, count);
                cachedMad = (count & 1) == 1
                    ? madScratch[count / 2]
                    : 0.5 * (madScratch[count / 2 - 1] + madScratch[count / 2]);
                robustDirty = false;
            }

            private void Resync()
            {
                addsSinceResync = 0;
                double s = 0, s2 = 0;
                for (int i = 0; i < count; i++) { double x = this[i]; s += x; s2 += x * x; }
                sum = s; sumSq = s2;
                for (int i = 0; i < count; i++) sortCache[i] = this[i];
                Array.Sort(sortCache, 0, count);
                sortedLen = count;
            }
        }


        private enum SniperLiquidityClass { Unknown, ThickBook, ThinFast, Commodity, Crypto }

        /// <summary>
        /// Le nom du contrat n'est utilise que comme indice secondaire : classer ES/NQ/CL
        /// en dur casserait sur les micros, FDAX/FESX, actions, CFD et contrats etrangers.
        /// Metriques MESURABLES sans donnees L2 : amplitude de barre en ticks (ATR/TickSize),
        /// volume median par barre, et densite du livre approchee par le volume median
        /// echange par tick parcouru (proxy de profondeur, superieur a un spread non dispo).
        /// Hysteresis pour eviter tout basculement de classe en cours de session.
        /// </summary>
        private sealed class SniperInstrumentProfiler
        {
            private const int SwitchConfirmations = 30;   // hysteresis : 30 barres avant bascule

            private SniperLiquidityClass current = SniperLiquidityClass.Unknown;
            private SniperLiquidityClass pending = SniperLiquidityClass.Unknown;
            private int pendingHits;
            private int samples;

            public SniperLiquidityClass Class { get { return current; } }
            public int Samples { get { return samples; } }
            public bool IsReady { get { return samples >= 60 && current != SniperLiquidityClass.Unknown; } }

            // Presets calibres, exposes en lecture seule au moteur -----------------
            public double AbsorptionDeltaPercentile { get; private set; }
            public double ExhaustionPercentile { get; private set; }
            public double DeltaFlipMinPercentile { get; private set; }
            public double IcebergZMadMin { get; private set; }
            public double VolumeFloorFactor { get; private set; }   // plancher = facteur x volume median
            public int ScanWindowBars { get; private set; }

            public string NameHint = "";
            public double LastBarTicks;
            public double LastMedianVolume;
            public double LastDepthPerTick;

            public SniperInstrumentProfiler()
            {
                ApplyPreset(SniperLiquidityClass.ThickBook);   // preset neutre par defaut
            }

            public void Reset()
            {
                current = pending = SniperLiquidityClass.Unknown;
                pendingHits = 0; samples = 0;
                ApplyPreset(SniperLiquidityClass.ThickBook);
            }

            /// <summary>Alimente le profiler (appelable des State.Historical : pas de cold-start).</summary>
            public void Observe(double atr, double tickSize, double medianVolume)
            {
                if (tickSize <= 0 || atr <= 0) return;
                samples++;

                double barTicks = atr / tickSize;
                double depthPerTick = medianVolume / Math.Max(1.0, barTicks);
                LastBarTicks = barTicks;
                LastMedianVolume = medianVolume;
                LastDepthPerTick = depthPerTick;

                if (samples < 40) return;   // on ne classe pas sur un echantillon vide

                SniperLiquidityClass detected = Classify(barTicks, medianVolume, depthPerTick);

                if (detected == current) { pending = current; pendingHits = 0; return; }

                if (detected == pending) pendingHits++;
                else { pending = detected; pendingHits = 1; }

                if (pendingHits >= SwitchConfirmations)
                {
                    current = pending;
                    pendingHits = 0;
                    ApplyPreset(current);
                }
            }

            /// <summary>
            /// Classification purement metrique. Le NameHint ne sert que de departage
            /// (override optionnel) quand les mesures sont ambigues.
            /// </summary>
            private SniperLiquidityClass Classify(double barTicks, double medianVolume, double depthPerTick)
            {
                // Crypto : tick tres large -> tres peu de ticks parcourus par barre
                // et volume fragmente (peu de contrats par niveau).
                if (barTicks <= 6 && medianVolume < 200) return SniperLiquidityClass.Crypto;

                // ThickBook : livre profond -> forte densite de volume par tick parcouru,
                // amplitude contenue (ES, ZN, ZB, FGBL, FESX...).
                if (depthPerTick >= 120 && barTicks <= 16) return SniperLiquidityClass.ThickBook;

                // ThinFast : livre reellement fin (peu de contrats par tick parcouru)
                // ou vitesse extreme (NQ, MNQ, M6E, micros, indices etrangers rapides).
                if (barTicks >= 40 || (barTicks >= 18 && depthPerTick < 30))
                    return SniperLiquidityClass.ThinFast;

                // Commodity : regime intermediaire (CL, GC, NG) - amplitude large mais
                // densite encore exploitable, spikes d'agression sur inventaires.
                if (barTicks > 8 && depthPerTick < 120) return SniperLiquidityClass.Commodity;

                // Ambigu : on conserve la classe courante, sinon indice par nom (dernier recours).
                if (current != SniperLiquidityClass.Unknown) return current;
                return HintFromName();
            }

            private SniperLiquidityClass HintFromName()
            {
                string n = (NameHint ?? "").ToUpperInvariant();
                if (n.Contains("BTC") || n.Contains("ETH")) return SniperLiquidityClass.Crypto;
                if (n.Contains("CL") || n.Contains("GC") || n.Contains("NG")) return SniperLiquidityClass.Commodity;
                if (n.Contains("NQ")) return SniperLiquidityClass.ThinFast;
                return SniperLiquidityClass.ThickBook;
            }

            private void ApplyPreset(SniperLiquidityClass c)
            {
                switch (c)
                {
                    case SniperLiquidityClass.ThinFast:
                        // Livre fin : il faut etre tres selectif sinon le bruit declenche.
                        AbsorptionDeltaPercentile = 95;
                        ExhaustionPercentile = 93;
                        DeltaFlipMinPercentile = 90;
                        IcebergZMadMin = 3.0;
                        VolumeFloorFactor = 1.6;
                        ScanWindowBars = 4;
                        break;

                    case SniperLiquidityClass.Commodity:
                        AbsorptionDeltaPercentile = 90;
                        ExhaustionPercentile = 88;
                        DeltaFlipMinPercentile = 85;
                        IcebergZMadMin = 2.7;
                        VolumeFloorFactor = 1.4;
                        ScanWindowBars = 5;
                        break;

                    case SniperLiquidityClass.Crypto:
                        AbsorptionDeltaPercentile = 92;
                        ExhaustionPercentile = 90;
                        DeltaFlipMinPercentile = 88;
                        IcebergZMadMin = 2.8;
                        VolumeFloorFactor = 1.5;
                        ScanWindowBars = 5;
                        break;

                    default: // ThickBook / Unknown
                        // Livre profond : l'absorption se concentre sur 1-2 ticks, un Z modere suffit.
                        AbsorptionDeltaPercentile = 85;
                        ExhaustionPercentile = 85;
                        DeltaFlipMinPercentile = 80;
                        IcebergZMadMin = 2.5;
                        VolumeFloorFactor = 1.3;
                        ScanWindowBars = 6;
                        break;
                }
            }

            public override string ToString()
            {
                return string.Format(
                    "classe={0} n={1} barTicks={2:F1} volMed={3:F0} depth={4:F0}/tick | absP={5:F0} exhP={6:F0} flipP={7:F0} iceZ={8:F1} floor={9:F2}",
                    current, samples, LastBarTicks, LastMedianVolume, LastDepthPerTick,
                    AbsorptionDeltaPercentile, ExhaustionPercentile, DeltaFlipMinPercentile,
                    IcebergZMadMin, VolumeFloorFactor);
            }
        }

        /// <summary>Profil de volume par tick, utilise UNIQUEMENT par le moteur de score
        /// pour le composite multi-sessions et la qualite des LVN. Le profil de trading
        /// reste le profil incremental dense de l'AMC Pro.</summary>
        private sealed class SniperProfile
        {
            public Dictionary<long, long> Vol = new Dictionary<long, long>(4096);
            public long Total;
            public long MinTick = long.MaxValue;
            public long MaxTick = long.MinValue;
            public double Poc, Vah, Val;
            public bool Valid;

            public void Clear()
            {
                Vol.Clear(); Total = 0;
                MinTick = long.MaxValue; MaxTick = long.MinValue;
                Poc = Vah = Val = 0; Valid = false;
                
                // FIX AUDIT #5: Force la réduction de capacité pour libérer la mémoire
                // après une longue session avec beaucoup de données
                if (Vol.Count > 1000)
                {
                    var newVol = new Dictionary<long, long>(Math.Max(16, Vol.Count / 4));
                    Vol = newVol;
                }
            }

            public void Add(long tick, long v)
            {
                if (v <= 0) return;
                long cur;
                Vol.TryGetValue(tick, out cur);
                Vol[tick] = cur + v;
                Total += v;
                if (tick < MinTick) MinTick = tick;
                if (tick > MaxTick) MaxTick = tick;
            }

            public void Merge(SniperProfile other)
            {
                foreach (KeyValuePair<long, long> kv in other.Vol) Add(kv.Key, kv.Value);
            }

            public long At(long tick)
            {
                long v;
                return Vol.TryGetValue(tick, out v) ? v : 0L;
            }

            public void Compute(double ts, int valueAreaPercent)
            {
                Valid = false;
                if (Total <= 0 || Vol.Count == 0 || ts <= 0) return;

                long pocTick = 0; long best = -1;
                foreach (KeyValuePair<long, long> kv in Vol)
                    if (kv.Value > best) { best = kv.Value; pocTick = kv.Key; }

                long target = (long)(Total * (valueAreaPercent / 100.0));
                long acc = At(pocTick);
                long up = pocTick, dn = pocTick;

                int guard = 0;
                while (acc < target && guard++ < 200000)
                {
                    long nextUp = up + 1, nextDn = dn - 1;
                    long vUp = nextUp <= MaxTick ? At(nextUp) : -1;
                    long vDn = nextDn >= MinTick ? At(nextDn) : -1;
                    if (vUp < 0 && vDn < 0) break;
                    if (vUp >= vDn) { up = nextUp; acc += Math.Max(0, vUp); }
                    else { dn = nextDn; acc += Math.Max(0, vDn); }
                }

                Poc = pocTick * ts;
                Vah = up * ts;
                Val = dn * ts;
                Valid = true;
            }

            // allouee et triee a chaque appel (2 a 16 KB par barre economises).
            private double[] percentileScratch = new double[0];

            public double LevelPercentile(double p)
            {
                int n = Vol.Count;
                if (n == 0) return 0;
                if (percentileScratch.Length < n) percentileScratch = new double[n * 2];
                int k = 0;
                foreach (KeyValuePair<long, long> kv in Vol) percentileScratch[k++] = kv.Value;
                Array.Sort(percentileScratch, 0, k);
                double rank = Math.Max(0, Math.Min(k - 1, (p / 100.0) * (k - 1)));
                int lo = Math.Max(0, Math.Min(k - 1, (int)Math.Floor(rank)));
                int hi = Math.Max(0, Math.Min(k - 1, (int)Math.Ceiling(rank)));
                if (lo == hi) return percentileScratch[lo];
                double frac = rank - lo;
                return percentileScratch[lo] * (1 - frac) + percentileScratch[hi] * frac;
            }
        }

        private struct SniperAbsorptionEvent
        {
            public int BarIdx;
            public double Price;
            public double ZDelta;
            public bool IsBull;      // absorption vendeuse au support => signal haussier
            public double Volume;
        }

        /// Remplace SignalCandidate pour la voie "Sniper".</summary>
        private sealed class Candidate
        {
            public string Name;
            public bool IsBuy;
            public int BarIdx;
            public DateTime Time;
            public double Entry, Stop, Target1, Target2;
            public double N1, N2, N3, N4, Penalty;
            /// <summary>Score retenu pour la DECISION d'emission (0 si un gate a echoue).</summary>
            public double Score;
            /// C'est cette valeur qui alimente le journal shadow et le grade.</summary>
            public double ScoreRaw;
            public double Rr;
            public bool Gated;
            public bool HtfAligned;
            public double EntryAtEmission;
            public string GateFailed = "";
            public string GateBypassed = "";
            public readonly List<string> Detail = new List<string>(24);
            /// OR. null pour tous les autres presets, qui conservent la notation
            /// A+/A/B/C d'origine (aucune API cassee).</summary>
            public string Tier;

            // Scalping Pro V7.8 Soft - Classification & Preuves
            public CandidateFamily Family = CandidateFamily.Reversal;
            public SetupType SetupType = SetupType.Reversal;
            public string PrimaryCandidate = "";
            public readonly List<string> EvidenceList = new List<string>(8);
            public double HtfModifier;
            public double M5Modifier;
            public double SetupModifier;

            // Volume Profile V2 (Closed References & Confluences)
            public VolumeProfileContext VolumeProfile;

            public string Grade
            {
                get
                {
                    if (!string.IsNullOrEmpty(Tier)) return Tier;
                    if (ScoreRaw >= 85) return "A+";
                    if (ScoreRaw >= 75) return "A";
                    if (ScoreRaw >= 60) return "B";
                    return "C";
                }
            }
        }

        /// <summary>Trade suivi par le moteur Sniper (distinct de TrackedSignal, qui
        /// reste attache aux statistiques de familles de l'AMC Pro).</summary>
        private sealed class TrackedTrade
        {
            public string Tag;
            public string Name;
            public bool IsBuy;
            public double Entry, Stop, T1, T2, Score;
            public int BarIdx;
            public DateTime Time;
            public string Grade;
            public bool Closed;
        }

        #endregion

        #region SNIPER - Section 2 : parametres

        [Display(Name = "Mode d'execution", Description = "Sniper = gates actifs et alertes. Research = tout est journalise, aucune alerte.", Order = 1, GroupName = "Sniper 01. Execution")]
        public SniperExecutionMode ExecutionMode { get; set; }

        [Display(Name = "Activer le moteur Sniper", Order = 2, GroupName = "Sniper 01. Execution")]
        public bool EnableSniperEngine { get; set; }

        [Range(0, 100)]
        [Display(Name = "Score minimal d'alerte (/100)", Order = 3, GroupName = "Sniper 01. Execution")]
        public int MinScoreToAlert { get; set; }

        // soit ~100/semaine : l'ancienne borne (50) rendait la valeur du preset
        // non saisissable dans la grille de proprietes NinjaTrader.
        [Range(0, 300)]
        [Display(Name = "Alertes max / semaine", Order = 4, GroupName = "Sniper 01. Execution")]
        public int MaxAlertsPerWeek { get; set; }

        // (20/session). La valeur appliquee par le code etait rejetee/clampee des
        // que l'utilisateur ouvrait la fenetre de parametres.
        [Range(0, 50)]
        [Display(Name = "Alertes Sniper max / session", Order = 5, GroupName = "Sniper 01. Execution")]
        public int MaxSniperAlertsPerSession { get; set; }

        [Range(0, 10)]
        [Display(Name = "Buffer de selection (barres)", Description = "On attend N barres et on emet le MEILLEUR score de la fenetre (supprime le biais first-come du cooldown).", Order = 6, GroupName = "Sniper 01. Execution")]
        public int SelectionBufferBars { get; set; }

        [Display(Name = "RTH uniquement (Sniper)", Order = 7, GroupName = "Sniper 01. Execution")]
        public bool SniperRthOnly { get; set; }

        [Range(0, 60)]
        [Display(Name = "Blackout news (min)", Order = 8, GroupName = "Sniper 01. Execution")]
        public int NewsBlackoutMinutes { get; set; }

        // de la liste codee en dur 830/1000/1400/1430.
        [Display(Name = "Horaires news (HHMM, CSV)", Description = "Horaires exprimes dans le fuseau du graphique, separes par des virgules. Vide = aucune fenetre news.", Order = 9, GroupName = "Sniper 01. Execution")]
        public string NewsTimesCsv { get; set; }

        [Range(0, 40)]
        [Display(Name = "Penalite fenetre news", Description = "Proxy statistique, pas un filtre calendaire : garder faible.", Order = 10, GroupName = "Sniper 01. Execution")]
        public int NewsWindowPenalty { get; set; }

        [Display(Name = "News : jours de semaine uniquement", Order = 11, GroupName = "Sniper 01. Execution")]
        public bool NewsWeekdaysOnly { get; set; }

        [Display(Name = "Blackout news strict (Hard Gate)", Description = "true = bloque totalement les signaux pendant la fenêtre de news (Gate = NEWS_BLACKOUT). false = simple pénalité de score.", Order = 12, GroupName = "Sniper 01. Execution")]
        public bool NewsHardBlock { get; set; }

        // l'attente du buffer de selection.
        [Range(0.0, 5.0)]
        [Display(Name = "Derive max entree (x ATR)", Description = "0 = desactive. Le candidat est abandonne si |prix courant - entree| depasse ce multiple d'ATR.", Order = 13, GroupName = "Sniper 01. Execution")]
        public double MaxEntryDriftAtr { get; set; }

        [Range(0, 30)]
        [Display(Name = "Gate N1 Contexte (min /30)", Order = 1, GroupName = "Sniper 02. Gates")]
        public int GateN1MinScore { get; set; }

        [Range(0, 30)]
        [Display(Name = "Gate N2 Localisation (min /30)", Order = 2, GroupName = "Sniper 02. Gates")]
        public int GateN2MinScore { get; set; }

        [Range(0, 25)]
        [Display(Name = "Gate N3 Microstructure (min /25)", Order = 3, GroupName = "Sniper 02. Gates")]
        public int GateN3MinScore { get; set; }

        [Range(0, 15)]
        [Display(Name = "Gate N4 Trigger (min /15)", Order = 4, GroupName = "Sniper 02. Gates")]
        public int GateN4MinScore { get; set; }

        [Display(Name = "Gate HTF sur mean-reversion", Description = "false = le HTF devient un simple modulateur de score pour les setups contre-tendance.", Order = 5, GroupName = "Sniper 02. Gates")]
        public bool HtfGateAppliesToMeanReversion { get; set; }

        [Range(0, 20)]
        [Display(Name = "Penalite HTF oppose (mean-reversion)", Order = 6, GroupName = "Sniper 02. Gates")]
        public int HtfMisalignmentPenalty { get; set; }

        // famille de setup, pas sur le maximum theorique commun.
        [Display(Name = "Normaliser les scores par setup", Order = 7, GroupName = "Sniper 02. Gates")]
        public bool NormalizeScoresPerSetup { get; set; }

        [Range(1, 5)]
        [Display(Name = "Retests d'imbalance autorises", Order = 8, GroupName = "Sniper 02. Gates")]
        public int MaxImbalanceRetests { get; set; }

        [Range(0.0, 3.0)]
        [Display(Name = "IB Extension min (mean-reversion)", Order = 1, GroupName = "Sniper 03. Contexte")]
        public double IbExtensionMin { get; set; }

        [Range(0.1, 5.0)]
        [Display(Name = "IB Extension max (mean-reversion)", Order = 2, GroupName = "Sniper 03. Contexte")]
        public double IbExtensionMax { get; set; }

        [Range(0.5, 6.0)]
        [Display(Name = "IB Extension min (trend)", Order = 3, GroupName = "Sniper 03. Contexte")]
        public double IbExtensionTrendMin { get; set; }

        [Range(0.1, 1.0)]
        [Display(Name = "Seuil overlap VA = range", Order = 4, GroupName = "Sniper 03. Contexte")]
        public double VaOverlapRangeThreshold { get; set; }

        [Range(0, 100)]
        [Display(Name = "ATR percentile min", Order = 5, GroupName = "Sniper 03. Contexte")]
        public int AtrPercentileMin { get; set; }

        [Range(0, 100)]
        [Display(Name = "ATR percentile max", Order = 6, GroupName = "Sniper 03. Contexte")]
        public int AtrPercentileMax { get; set; }

        [Range(0.05, 1.00)]
        [Display(Name = "Tolerance niveau (x ATR)", Order = 1, GroupName = "Sniper 04. Localisation")]
        public double KeyLevelToleranceAtr { get; set; }

        [Range(1, 40)]
        [Display(Name = "Sessions du profil composite", Order = 2, GroupName = "Sniper 04. Localisation")]
        public int CompositeSessions { get; set; }

        [Range(1.0, 30.0)]
        [Display(Name = "Demi-vie de fraicheur NPOC", Order = 3, GroupName = "Sniper 04. Localisation")]
        public double NpocDecayHalfLife { get; set; }

        [Range(-6.0, -0.5)]
        [Display(Name = "Z-score d'absorption (Sniper)", Order = 1, GroupName = "Sniper 05. Microstructure")]
        public double AbsorptionZScore { get; set; }

        [Range(1, 6)]
        [Display(Name = "Barres d'absorption min", Order = 2, GroupName = "Sniper 05. Microstructure")]
        public int AbsorptionMinBars { get; set; }

        [Range(0.03, 0.60)]
        [Display(Name = "Deplacement max absorption (x ATR)", Order = 3, GroupName = "Sniper 05. Microstructure")]
        public double AbsorptionMaxDisplacementAtr { get; set; }

        [Range(0.5, 6.0)]
        [Display(Name = "Seuil Z pente CVD", Order = 4, GroupName = "Sniper 05. Microstructure")]
        public double CvdSlopeZThreshold { get; set; }

        [Range(5, 200)]
        [Display(Name = "Barres de regression CVD", Order = 5, GroupName = "Sniper 05. Microstructure")]
        public int CvdRegressionBars { get; set; }

        [Range(2, 12)]
        [Display(Name = "Stack d'imbalance min (Sniper)", Order = 6, GroupName = "Sniper 05. Microstructure")]
        public int ImbalanceMinStack { get; set; }

        [Range(0.0, 0.95)]
        [Display(Name = "Contraction volume au retest", Order = 7, GroupName = "Sniper 05. Microstructure")]
        public double ImbalanceRetestVolumeContraction { get; set; }

        [Range(50, 3000)]
        [Display(Name = "Barres de calibration par bucket", Order = 8, GroupName = "Sniper 05. Microstructure")]
        public int BucketCalibrationBars { get; set; }

        [Range(10, 95)]
        [Display(Name = "Meche de rejet min (%)", Order = 1, GroupName = "Sniper 06. Trigger")]
        public int RejectionWickPercent { get; set; }

        [Range(0, 40)]
        [Display(Name = "Penalite signal oppose", Order = 2, GroupName = "Sniper 06. Trigger")]
        public int OppositeSignalPenalty { get; set; }

        [Display(Name = "Journal Sniper (shadow mode)", Description = "Journalise TOUS les candidats, y compris ceux bloques par un gate.", Order = 1, GroupName = "Sniper 07. Journal")]
        public bool EnableShadowJournal { get; set; }

        #endregion

        #region SNIPER - Section 3 : etat interne

        private readonly SniperProfile sniperSessionProfile = new SniperProfile();
        private readonly SniperProfile sniperCompositeProfile = new SniperProfile();
        private readonly List<SniperProfile> sniperSessionProfiles = new List<SniperProfile>(32);
        private readonly Stack<SniperProfile> sniperProfilePool = new Stack<SniperProfile>(32);

        private double sniperPrevPoc, sniperPrevVah, sniperPrevVal;
        private bool sniperPrevProfileValid;
        private double sniperVaOverlap;
        private SniperDayType sniperDayType = SniperDayType.Undetermined;

        private readonly SniperRingStat[] bucketDelta = new SniperRingStat[24];
        private readonly SniperRingStat[] bucketVolume = new SniperRingStat[24];
        private SniperRingStat sniperAtrHistory;
        private SniperRingStat sniperCvdSlopeHistory;

        // bucketAbsDelta stocke |delta| : c'est la distribution utile pour un
        // seuil d'absorption (bucketDelta signe sert au biais directionnel).
        private readonly SniperRingStat[] bucketAbsDelta = new SniperRingStat[24];
        private readonly SniperInstrumentProfiler sniperProfiler = new SniperInstrumentProfiler();
        private SniperRingStat sniperBarVolumeHistory;
        private SniperRingStat sniperSpreadTicksHistory;
        private double sniperZMadDeltaCached;
        private double sniperDeltaPercentileCached;
        private int sniperV3DeltaThresholdCached;
        private string sniperCalibTag = "v3=off";

        private RingBuffer<double> cvdSeries = new RingBuffer<double>(1024);
        private RingBuffer<double> priceSeries = new RingBuffer<double>(1024);
        private long sniperCumulativeDelta;

        private readonly List<SniperAbsorptionEvent> sniperAbsorptionEvents = new List<SniperAbsorptionEvent>(64);

        private readonly List<Candidate> pendingCandidates = new List<Candidate>(32);
        private const int MaxPendingCandidates = 40;
        private int pendingOverflowCount = 0;
        private readonly List<Candidate> lastBarCandidates = new List<Candidate>(16);
        private readonly List<TrackedTrade> openTrades = new List<TrackedTrade>(16);

        private int sniperAlertsThisSession;
        private readonly Queue<DateTime> alertsThisWeek = new Queue<DateTime>();
        private bool sniperJournalHeaderWritten;

        private int sniperLastEvaluatedBar = -1;
        private int sniperLastIngestedBar = -1;
        private double sniperSessionOpen;
        private string sniperLastStatus = "init";

        // Cache de la barre evaluee (index = evalBarIndex sur la serie volumetrique).
        private double snClose, snOpen, snHigh, snLow, snClose1, snHigh1, snHigh2, snLow1, snLow2;
        private long snVolume;
        private DateTime snTime;

        #endregion

        #region SNIPER - Section 4 : cycle de vie

        /// <summary>Valeurs par defaut du moteur Sniper. Appele depuis State.SetDefaults,
        /// apres les defauts de l'AMC Pro (les seuils calibres "Sniper" de l'AMC Pro
        /// — MinConfluencePercentToAlert=70, DirectionalConflictPercent=70, etc. —
        /// restent la reference et ne sont pas touches ici).</summary>
        private void ApplySniperDefaults()
        {
            ExecutionMode = SniperExecutionMode.Sniper;
            EnableSniperEngine = true;
            // est inferieur a l'ancien seuil de 85 — aucune alerte ne pouvait sortir
            // et seuls les A+ etaient emissibles. Seuil ramene au niveau "A".
            MinScoreToAlert = 72;
            MaxAlertsPerWeek = 0;                 // Illimité (0 = illimité)
            MaxSniperAlertsPerSession = 2;
            SelectionBufferBars = 3;
            SniperRthOnly = true;
            NewsBlackoutMinutes = 10;
            NewsTimesCsv = "0830,1000,1430";
            NewsWindowPenalty = 5;
            NewsWeekdaysOnly = true;
            NewsHardBlock = true;
            MaxEntryDriftAtr = 0.5;

            ApplyScalpingProDefaults();

            GateN1MinScore = 18;
            GateN2MinScore = 20;
            GateN3MinScore = 15;
            GateN4MinScore = 8;
            HtfGateAppliesToMeanReversion = false;
            HtfMisalignmentPenalty = 4;
            NormalizeScoresPerSetup = true;
            MaxImbalanceRetests = 2;

            IbExtensionMin = 0.3;
            IbExtensionMax = 1.2;
            IbExtensionTrendMin = 1.5;
            VaOverlapRangeThreshold = 0.70;
            AtrPercentileMin = 25;
            AtrPercentileMax = 90;

            KeyLevelToleranceAtr = 0.25;
            CompositeSessions = 10;
            NpocDecayHalfLife = 8.0;

            AbsorptionZScore = -2.0;
            AbsorptionMinBars = 2;
            AbsorptionMaxDisplacementAtr = 0.15;
            CvdSlopeZThreshold = 1.5;
            CvdRegressionBars = 20;
            ImbalanceMinStack = 4;
            ImbalanceRetestVolumeContraction = 0.50;
            BucketCalibrationBars = 500;

            RejectionWickPercent = 60;
            OppositeSignalPenalty = 10;

            EnableShadowJournal = true;
        }

        /// Transforme le systeme ultra-selectif (Sniper) en scanner d'opportunites :
        /// les gates cessent d'etre des verrous eliminatoires et deviennent des
        /// modulateurs, les quotas sont elargis, le buffer de selection est ramene a
        /// Appele depuis State.Configure via ApplyTradingPreset().
        /// ATTENTION : ce profil augmente fortement le nombre de signaux, donc aussi
        /// le nombre de faux positifs. Il est concu pour l'observation, l'analyse et
        /// la calibration (journal shadow + debug actifs), pas pour l'execution
        /// automatique en taille reelle.</summary>
        private void ApplyScannerPreset()
        {
            MinScoreToAlert = 55;                    // Sniper : 72
            MaxSniperAlertsPerSession = 6;           // Sniper : 2
            // scanner des le 4e jour et biaisait le journal de calibration.
            MaxAlertsPerWeek = 0;                    // Illimité (0 = illimité)
            MaxAlertsPerSession = 0;                 // Illimité (0 = illimité)

            GateN1MinScore = 8;                      // Sniper : 18 (Contexte)
            GateN2MinScore = 10;                     // Sniper : 20 (Localisation)
            GateN3MinScore = 6;                      // Sniper : 15 (Microstructure)
            GateN4MinScore = 3;                      // Sniper : 8  (Trigger)

            // 1 barre : emission a la barre suivante, sans repaint (0 = intrabar,
            // volontairement non retenu ici).
            SelectionBufferBars = 1;                 // Sniper : 3

            HtfStrictMode = false;                   // Sniper : true
            // setups de mean-reversion (cf. htfIsGate ligne ~2036) et desactivait au
            // passage la penalite modulatrice. Le mode Scanner etait donc PLUS strict
            // que Sniper sur les reversals, l'inverse de l'objectif annonce.
            HtfGateAppliesToMeanReversion = false;   // HTF = simple modulateur de score
            HtfMisalignmentPenalty = 2;              // Sniper : 4

            MinRiskReward = 1.2;                     // Sniper : 2.0
            TargetR1 = 1.2;                          // aligne sur MinRiskReward
            if (TargetR2 <= TargetR1) TargetR2 = TargetR1 * 2.0;
            ExecutionCostTicks = 1;                  // inchange

            KeyLevelToleranceAtr = 0.4;              // Sniper : 0.25
            NodeToleranceTicks = 4;                  // Sniper : 2
            AbsorptionKeyLevelTicks = 8;             // Sniper : 5
            CompositeSessions = 15;                  // Sniper : 10

            AbsorptionZScore = -1.5;                 // Sniper : -2.0
            AbsorptionMinBars = 1;                   // Sniper : 2
            AbsorptionMaxDisplacementAtr = 0.25;     // Sniper : 0.15
            ImbalanceMinStack = 3;                   // Sniper : 4
            ImbalanceRetestVolumeContraction = 0.30; // Sniper : 0.50
            MaxImbalanceRetests = 3;                 // Sniper : 2
            // (Engine.cs ~1712). Conserve comme valeur de repli si l'utilisateur
            // desactive la calibration auto.
            DeltaFlipMinPercentile = 50;             // Sniper : 80
            IcebergMinScore = 70;                    // Sniper : 85
            IcebergMinAggression = 300;              // Sniper : 750

            IbExtensionMin = 0.15;                   // Sniper : 0.30
            IbExtensionMax = 1.8;                    // Sniper : 1.2
            IbExtensionTrendMin = 1.0;               // Sniper : 1.5
            AtrPercentileMin = 10;                   // Sniper : 25
            AtrPercentileMax = 95;                   // Sniper : 90
            NewsWindowPenalty = 2;                   // Sniper : 5

            // Indispensable : les scores de setups de natures differentes ne sont
            // comparables qu'une fois normalises. Un seuil unique a 55 sans
            // normalisation favoriserait mecaniquement les setups au bareme le plus
            // genereux.
            NormalizeScoresPerSetup = true;

            // regime actif, conflit directionnel a 70%) pendant que la chaine Sniper
            // etait relachee : deux moteurs de selectivite opposes dans le meme mode.
            UseRegimeFilter = false;                 // Sniper : true
            DirectionalConflictPercent = 80;         // Sniper : 70 (plus permissif)
            // M3 : seuil de risque sous controle exclusif de l'utilisateur).
            UseSessionProfile = true;                // niveaux partages : inchange
            EnableBreakoutSignals = true;
            AcceptanceBars = 1;                      // Sniper : 2
            RetestToleranceTicks = 6;                // Sniper : 4
            RetestMaxBars = 12;                      // Sniper : 8
            FailedAuctionMaxBars = 5;                // Sniper : 3
            LvnThresholdPercent = 25;                // Sniper : 30
            HvnThresholdPercent = 130;               // Sniper : 150

            EnableShadowJournal = true;
            JournalShadowMode = true;
            EnableDebugMode = true;

            AutoCalibrationV3 = true;
            AutoProfileInstrument = true;
            EnableSessionBucketCalibration = true;

            Print("SniperMarketCorePro V7 : preset SCANNER applique "
                + "(seuil " + MinScoreToAlert + "/100, gates " + GateN1MinScore + "/" + GateN2MinScore
                + "/" + GateN3MinScore + "/" + GateN4MinScore + ", buffer " + SelectionBufferBars
                + " barre(s), HTF non bloquant, R:R min " + MinRiskReward.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                + ", HTF modulateur sur mean-reversion). Volume de signaux fortement augmente : profil d'observation/calibration, "
                + "a valider en journal avant toute execution.");
        }

        /// Prolonge le mode SCANNER jusqu'a sa limite : seuil d'alerte tres bas,
        /// quotas eleves, gates ramenes a des valeurs symboliques, buffer de
        /// selection a 0 (emission sur la barre courante), aucune verification de
        /// derive d'entree, filtre HTF totalement desactive, R:R faible (0.5 / 1.0)
        /// et stop serre (0.8 ATR). Detections microstructurelles portees a leur
        /// sensibilite maximale.
        /// Appele depuis State.Configure via ApplyTradingPreset().
        /// ATTENTION : 20-50 signaux par jour attendus, donc un taux de faux positifs
        /// eleve. Profil destine au mode Research avec journal shadow complet
        /// (calibration, mesure, R-multiples), pas au trading reel.</summary>
        private void ApplyScalpingPreset()
        {
            MinScoreToAlert = 30;                    // Scanner : 55, Sniper : 72
            MaxSniperAlertsPerSession = 20;          // Scanner : 6,  Sniper : 2
            // muet en fin de semaine, sans message explicite).
            MaxAlertsPerWeek = 0;                    // Illimité (0 = illimité)
            MaxAlertsPerSession = 0;                 // Illimité (0 = illimité)

            GateN1MinScore = 3;                      // Scanner : 8  (Contexte)
            GateN2MinScore = 3;                      // Scanner : 10 (Localisation)
            GateN3MinScore = 3;                      // Scanner : 6  (Microstructure)
            GateN4MinScore = 2;                      // Scanner : 3  (Trigger)

            // 0 = le candidat mature des la barre courante (cf. selection :
            // evalBarIndex - c.BarIdx >= SelectionBufferBars).
            SelectionBufferBars = 0;                 // Scanner : 1, Sniper : 3
            // 0 desactive le controle de derive : on accepte le prix immediat.
            MaxEntryDriftAtr = 0;                    // Scanner/Sniper : 0.5

            EnableHtfFilter = false;                 // Scanner/Sniper : true
            HtfStrictMode = false;
            HtfGateAppliesToMeanReversion = false;
            HtfMisalignmentPenalty = 0;              // Scanner : 2, Sniper : 4

            MinRiskReward = 0.5;                     // Scanner : 1.2, Sniper : 2.0
            TargetR1 = 0.5;                          // Scanner : 1.2, Sniper : 1.5
            TargetR2 = 1.0;                          // Scanner : 2.4, Sniper : 3.0
            StopAtrMultiple = 0.8;                   // Scanner/Sniper : 1.5
            StopBufferTicks = 1;                     // Scanner/Sniper : 2
            // TargetR1 = 0.5 R, le spread + slippage represente la part la plus
            // lourde du resultat en scalping. Ignorer le cout d'execution donne un
            // R:R theorique impossible a realiser (biais optimiste du journal).
            ExecutionCostTicks = 1;                  // Scanner/Sniper : 1

            KeyLevelToleranceAtr = 0.6;              // Scanner : 0.4, Sniper : 0.25
            NodeToleranceTicks = 6;                  // Scanner : 4, Sniper : 2
            AbsorptionKeyLevelTicks = 10;            // Scanner : 8, Sniper : 5
            CompositeSessions = 20;                  // Scanner : 15, Sniper : 10

            // Absorption
            AbsorptionZScore = -0.8;                 // Scanner : -1.5, Sniper : -2.0
            AbsorptionMinBars = 1;                   // Scanner : 1, Sniper : 2
            AbsorptionMaxDisplacementAtr = 0.35;     // Scanner : 0.25, Sniper : 0.15
            AbsorptionProbeTicks = 5;                // Scanner/Sniper : 3
            AbsorptionMinAggressionPercent = 20;     // Scanner/Sniper : 40
            AbsorptionRequireStrongSignal = false;   // Sniper : true
            AbsorptionRequireCloseVsOpen = false;    // Sniper : true
            AbsorptionSymmetricTicks = 3;            // Scanner/Sniper : 2

            // Iceberg
            IcebergMinAggression = 150;              // Scanner : 300, Sniper : 750
            IcebergMinScore = 60;                    // Scanner : 70, Sniper : 85
            IcebergMaxDisplacementTicks = 5;         // Scanner/Sniper : 3
            IcebergMaxRangeTicks = 12;               // Scanner/Sniper : 8
            IcebergMinDominancePercent = 25;         // Scanner/Sniper : 35

            // Imbalance
            ImbalanceMinStack = 2;                   // Scanner : 3, Sniper : 4
            ImbalanceRetestVolumeContraction = 0.20; // Scanner : 0.30, Sniper : 0.50
            MaxImbalanceRetests = 5;                 // Scanner : 3, Sniper : 2
            ImbalanceZoneMinLevels = 2;              // Scanner/Sniper : 3

            // Delta flip (surcharge par le profiler si AutoCalibrationV3 = true)
            DeltaFlipMinPercentile = 40;             // Scanner : 50, Sniper : 80
            DeltaFlipLookback = 2;                   // Scanner/Sniper : 3

            // Finished auction
            FinishedAuctionMaxVolume = 5;            // Scanner/Sniper : 2
            FinishedAuctionVolumePercent = 25;       // Scanner/Sniper : 15

            IbExtensionMin = 0.05;                   // Scanner : 0.15, Sniper : 0.30
            IbExtensionMax = 3.0;                    // Scanner : 1.8, Sniper : 1.2
            IbExtensionTrendMin = 0.5;               // Scanner : 1.0, Sniper : 1.5
            AtrPercentileMin = 5;                    // Scanner : 10, Sniper : 25
            AtrPercentileMax = 98;                   // Scanner : 95, Sniper : 90
            NewsWindowPenalty = 1;                   // Scanner : 2, Sniper : 5
            OppositeSignalPenalty = 3;               // Scanner/Sniper : 10

            // Obligatoire avec un seuil unique aussi bas : sans normalisation, les
            // setups au bareme le plus genereux monopoliseraient les alertes.
            NormalizeScoresPerSetup = true;

            UseSessionProfile = true;                // niveaux partages : inchange
            EnableBreakoutSignals = true;
            AcceptanceBars = 1;                      // Scanner : 1, Sniper : 2
            RetestToleranceTicks = 8;                // Scanner : 6, Sniper : 4
            RetestMaxBars = 15;                      // Scanner : 12, Sniper : 8
            FailedAuctionMaxBars = 6;                // Scanner : 5, Sniper : 3
            LvnThresholdPercent = 20;                // Scanner : 25, Sniper : 30
            HvnThresholdPercent = 120;               // Scanner : 130, Sniper : 150
            UseRegimeFilter = false;                 // Scanner : false, Sniper : true
            DirectionalConflictPercent = 90;         // Scanner : 80, Sniper : 70
            // M3 : seuil de risque sous controle exclusif de l'utilisateur). En
            // scalping, 40-50% est coherent avec MinScoreToAlert = 30.

            EnableShadowJournal = true;
            JournalShadowMode = true;
            EnableDebugMode = true;

            AutoCalibrationV3 = true;
            AutoProfileInstrument = true;
            EnableSessionBucketCalibration = true;

            Print("SniperMarketCorePro V7.2 : preset SCALPING applique "
                + "(seuil " + MinScoreToAlert + "/100, gates " + GateN1MinScore + "/" + GateN2MinScore
                + "/" + GateN3MinScore + "/" + GateN4MinScore + ", buffer " + SelectionBufferBars
                + " barre(s), HTF desactive, R:R min "
                + MinRiskReward.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                + ", stop " + StopAtrMultiple.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                + " ATR, quotas " + MaxSniperAlertsPerSession + "/seance et " + MaxAlertsPerWeek + "/semaine). "
                + "20-50 signaux/jour attendus : profil de RECHERCHE, a exploiter en journal shadow avant tout trading reel.");

            if (MinConfluencePercentToAlert > 50)
                Print("SniperMarketCorePro V7.2 (SCALPING) : MinConfluencePercentToAlert = "
                    + MinConfluencePercentToAlert + "% reste sous votre controle et bornera la chaine AMC Pro "
                    + "bien avant MinScoreToAlert = " + MinScoreToAlert + ". Valeur coherente en scalping : 40-50%.");

            if (EvaluateOnBarClose)
                Print("SniperMarketCorePro V7.2 (SCALPING) : SelectionBufferBars = 0 mais EvaluateOnBarClose = true, "
                    + "l'evaluation reste donc a la cloture de barre. Pour une emission reellement intrabar, "
                    + "passez EvaluateOnBarClose a false en acceptant le repaint (statistiques de journal non fiables).");
        }

        /// <summary>Initialisation des caches et buffers du moteur (State.DataLoaded).</summary>
        private void InitSniperEngine()
        {
            for (int i = 0; i < 24; i++)
            {
                bucketDelta[i] = new SniperRingStat(BucketCalibrationBars);
                bucketVolume[i] = new SniperRingStat(BucketCalibrationBars);
                bucketAbsDelta[i] = new SniperRingStat(BucketCalibrationBars);
            }
            sniperAtrHistory = new SniperRingStat(Math.Max(500, BucketCalibrationBars));
            sniperCvdSlopeHistory = new SniperRingStat(Math.Max(200, BucketCalibrationBars / 2));

            sniperBarVolumeHistory = new SniperRingStat(Math.Max(500, BucketCalibrationBars));
            sniperSpreadTicksHistory = new SniperRingStat(Math.Max(200, BucketCalibrationBars / 2));
            sniperProfiler.Reset();
            sniperProfiler.NameHint = Instrument != null && Instrument.MasterInstrument != null
                ? Instrument.MasterInstrument.Name : "";
            sniperV3DeltaThresholdCached = 0;
            sniperCalibTag = "v3=warmup";

            sniperSessionProfile.Clear();
            sniperCompositeProfile.Clear();
            for (int i = 0; i < sniperSessionProfiles.Count; i++) sniperSessionProfiles[i].Clear();
            sniperSessionProfiles.Clear();
            sniperProfilePool.Clear();

            cvdSeries.Clear();
            priceSeries.Clear();
            sniperAbsorptionEvents.Clear();
            pendingCandidates.Clear();
            lastBarCandidates.Clear();
            RemoveAllTradeLevels();
            openTrades.Clear();
            alertsThisWeek.Clear();

            sniperCumulativeDelta = 0;
            sniperAlertsThisSession = 0;
            sniperLastEvaluatedBar = -1;
            sniperLastIngestedBar = -1;
            sniperPrevProfileValid = false;
            sniperDayType = SniperDayType.Undetermined;
            sniperLastStatus = "pret";
            SniperResetExports();

            InitScalpingPro();
        }

        /// <summary>Rotation de session : archive le profil sniper, purge l'etat de
        /// fenetre et cloture les trades encore ouverts. Appele en meme temps que
        /// FlushOpenSignalsAtSessionEnd / ResetSessionTrackers de l'AMC Pro.</summary>
        private void SniperRollSession(double lastSessionClose)
        {
            if (sniperSessionProfile.Total > 0)
            {
                sniperSessionProfile.Compute(tickSize, ValueAreaPercent);
                if (sniperSessionProfile.Valid)
                {
                    sniperPrevPoc = sniperSessionProfile.Poc;
                    sniperPrevVah = sniperSessionProfile.Vah;
                    sniperPrevVal = sniperSessionProfile.Val;
                    sniperPrevProfileValid = true;
                }

                SniperProfile archived = RentSniperProfile();
                archived.Merge(sniperSessionProfile);
                sniperSessionProfiles.Add(archived);
                while (sniperSessionProfiles.Count > CompositeSessions)
                {
                    ReturnSniperProfile(sniperSessionProfiles[0]);
                    sniperSessionProfiles.RemoveAt(0);
                }
            }

            if (lastSessionClose > 0) CloseAllTrades("SESSION_END", lastSessionClose);
            else { RemoveAllTradeLevels(); openTrades.Clear(); }

            sniperSessionProfile.Clear();
            sniperAbsorptionEvents.Clear();
            pendingCandidates.Clear();
            lastBarCandidates.Clear();
            cvdSeries.Clear();
            priceSeries.Clear();
            sniperCumulativeDelta = 0;
            sniperAlertsThisSession = 0;
            sniperDayType = SniperDayType.Undetermined;
            sniperVaOverlap = 0;
            sniperSessionOpen = 0;
            sniperLastIngestedBar = -1;
        }

        /// <summary>Point d'entree du moteur, appele apres EvaluateVolumeProfileSignal()
        /// donc APRES que l'AMC Pro a calcule le profil et toutes les detections
        /// microstructurelles de la barre evaluee.</summary>
        private void SniperOnEvaluatedBar()
        {
            if (!EnableSniperEngine) return;

            try
            {
                if (evalBarIndex < 0) return;
                if (evalBarIndex == sniperLastEvaluatedBar) return;
                sniperLastEvaluatedBar = evalBarIndex;

                if (!CacheEvaluatedBar()) return;

                VolumetricBarsType volType = BarsArray[volumetricBarsIndex].BarsType as VolumetricBarsType;
                if (volType == null) { sniperLastStatus = "serie volumetrique indisponible"; return; }

                if (sniperSessionOpen <= 0) sniperSessionOpen = snOpen;

                // 1. Profils sniper (session + composite multi-sessions)
                IngestSniperProfile(volType);
                UpdateSniperProfiles();

                // 1b. Volume Profile V2 (Closed References & Multi-Timeframe)
                VolumeProfileOnEvaluatedBar(volType);

                // 2. Calibration par bucket horaire (Z-scores comparables ES / NQ / nuit)
                UpdateSniperCalibration(volType);

                // 3. Contexte : day type et overlap de value area
                UpdateSniperContext();

                // 4. Microstructure : evenements d'absorption normalises
                DetectSniperAbsorption();

                //       Le suivi SMC doit voir la barre AVANT le scoring des candidats.
                ScalpingProOnEvaluatedBar();

                // 5. Candidats + scoring hierarchique
                lastBarCandidates.Clear();
                BuildCandidates();

                // 6. Selection best-of-window et emission
                ProcessSelectionBuffer();

                SniperSyncExports();

                // 7. Suivi des trades sniper
                UpdateOpenTrades();

                // ne le reinitialisait apres une barre traitee sans exception.
                sniperLastStatus = "ok";
            }
            catch (Exception ex)
            {
                string site = SniperErrorSite(ex);
                sniperLastStatus = "erreur moteur (" + site + ") : " + ex.Message;
                if (EnableDebugMode) Print("Sniper: " + ex.GetType().Name + " in " + site + " - " + ex.Message + "\n" + ex.StackTrace);
            }
        }

        /// <summary>Recopie les prix de la barre EVALUEE (offset AMC Pro) dans des
        /// champs locaux : tout le moteur de score lit ces champs, jamais Close[0].</summary>
        private bool CacheEvaluatedBar()
        {
            int off = evalOffset;
            if (volumetricBarsIndex < 0 || volumetricBarsIndex >= BarsArray.Length) return false;
            if (off < 0) return false;

            // Series.Count renvoie le nombre de barres CHARGEES, pas le nombre de
            // barres deja traitees. Un acces [off + 2] avec off + 2 > CurrentBars
            // leve donc une exception meme quand Count est grand (warmup, premieres
            // barres, changement d'instrument). La seule borne valable est CurrentBars.
            int curBar = CurrentBars[volumetricBarsIndex];
            if (curBar < off + 2) return false;

            // MaximumBarsLookBack limite egalement la profondeur accessible.
            int maxLookBack = MaximumBarsLookBack == MaximumBarsLookBack.TwoHundredFiftySix ? 255 : int.MaxValue;
            if (off + 2 > maxLookBack) return false;

            int req = off + 3;
            if (Closes[volumetricBarsIndex].Count < req) return false;
            if (Opens[volumetricBarsIndex].Count < req) return false;
            if (Highs[volumetricBarsIndex].Count < req) return false;
            if (Lows[volumetricBarsIndex].Count < req) return false;
            if (Volumes[volumetricBarsIndex].Count < req) return false;

            // FIX AUDIT #3: Re-vérification des bounds après chaque accès potentiellement dangereux
            // et utilisation de variables locales pour éviter les réévaluations
            var closes = Closes[volumetricBarsIndex];
            var opens = Opens[volumetricBarsIndex];
            var highs = Highs[volumetricBarsIndex];
            var lows = Lows[volumetricBarsIndex];
            var volumes = Volumes[volumetricBarsIndex];

            // Vérification finale avant accès pour éviter les race conditions
            if (off >= closes.Count || off + 1 >= closes.Count || off + 2 >= closes.Count) return false;
            if (off >= highs.Count || off + 1 >= highs.Count || off + 2 >= highs.Count) return false;
            if (off >= lows.Count || off + 1 >= lows.Count || off + 2 >= lows.Count) return false;
            if (off >= volumes.Count) return false;

            snClose = closes[off];
            snOpen = opens[off];
            snHigh = highs[off];
            snLow = lows[off];
            snClose1 = closes[off + 1];
            snHigh1 = highs[off + 1];
            snLow1 = lows[off + 1];
            snHigh2 = highs[off + 2];
            snLow2 = lows[off + 2];
            snVolume = (long)volumes[off];
            snTime = GetVolumetricTime();
            return true;
        }

        /// <summary>Extrait la premiere methode de l'application dans la stack : TargetSite
        /// pointe sinon sur ThrowArgumentOutOfRangeException, ce qui n'aide pas au debug.</summary>
        private static string SniperErrorSite(Exception ex)
        {
            try
            {
                System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace(ex, false);
                for (int i = 0; i < st.FrameCount; i++)
                {
                    System.Reflection.MethodBase m = st.GetFrame(i).GetMethod();
                    if (m == null || m.DeclaringType == null) continue;
                    if (m.DeclaringType.FullName != null && m.DeclaringType.FullName.IndexOf("SniperMarketCorePro", StringComparison.Ordinal) >= 0)
                        return m.Name;
                }
            }
            catch { }
            return ex.TargetSite != null ? ex.TargetSite.Name : "unk";
        }

        private double SniperAtr()
        {
            int atrOffset = Math.Min(evalOffset, Math.Max(0, CurrentBars[volumetricBarsIndex]));
            if (riskAtr != null && riskAtr.IsValidDataPoint(atrOffset)) return riskAtr[atrOffset];
            if (regimeAtr != null && regimeAtr.IsValidDataPoint(atrOffset)) return regimeAtr[atrOffset];
            return adaptiveAvgBarRange > 0 ? adaptiveAvgBarRange : 8 * tickSize;
        }

        #endregion

        #region SNIPER - Section 5 : profils sniper

        private SniperProfile RentSniperProfile()
        {
            SniperProfile p = sniperProfilePool.Count > 0 ? sniperProfilePool.Pop() : new SniperProfile();
            p.Clear();
            return p;
        }

        private void ReturnSniperProfile(SniperProfile p)
        {
            p.Clear();
            if (sniperProfilePool.Count < 64) sniperProfilePool.Push(p);
        }

        private long SniperPriceToTick(double price)
        {
            if (tickSize <= 0) return 0;
            return (long)Math.Round(price / tickSize, MidpointRounding.AwayFromZero);
        }

        /// <summary>Agrege dans le profil de session sniper toutes les barres
        /// volumetriques cloturees depuis le dernier appel (aucune re-lecture).</summary>
        private void IngestSniperProfile(VolumetricBarsType volType)
        {
            int last = evalBarIndex;
            if (sniperLastIngestedBar < 0) sniperLastIngestedBar = Math.Max(-1, last - 1);

            int currentBar = CurrentBars[volumetricBarsIndex];
            if (currentBar < 0) return;

            for (int idx = sniperLastIngestedBar + 1; idx <= last; idx++)
            {
                if (idx < 0 || idx >= volType.Volumes.Length) continue;
                VolumetricData vd = volType.Volumes[idx];
                if (vd == null) continue;

                // sur Series.Count (qui compte les barres chargees, pas traitees).
                int barsAgo = currentBar - idx;
                if (barsAgo < 0 || barsAgo > currentBar) continue;
                if (barsAgo >= Lows[volumetricBarsIndex].Count) continue;
                if (MaximumBarsLookBack == MaximumBarsLookBack.TwoHundredFiftySix && barsAgo > 255) continue;
                // Clamp to maximum safe limit for series access
                barsAgo = Math.Min(barsAgo, Lows[volumetricBarsIndex].Count - 1);

                double barLow = Lows[volumetricBarsIndex][barsAgo];
                double barHigh = Highs[volumetricBarsIndex][barsAgo];
                long lowTick = (long)Math.Round(barLow / tickSize);
                long highTick = (long)Math.Round(barHigh / tickSize);

                for (long t = lowTick; t <= highTick; t++)
                {
                    double price = t * tickSize;
                    long v = vd.GetTotalVolumeForPrice(price);
                    if (v > 0)
                        sniperSessionProfile.Add(t, v);
                }
                sniperCumulativeDelta += vd.BarDelta;
            }
            sniperLastIngestedBar = last;
        }

        private void UpdateSniperProfiles()
        {
            sniperSessionProfile.Compute(tickSize, ValueAreaPercent);

            sniperCompositeProfile.Clear();
            for (int i = 0; i < sniperSessionProfiles.Count; i++)
                sniperCompositeProfile.Merge(sniperSessionProfiles[i]);
            sniperCompositeProfile.Merge(sniperSessionProfile);
            sniperCompositeProfile.Compute(tickSize, ValueAreaPercent);
        }

        #endregion

        #region SNIPER - Section 6 : calibration par bucket horaire

        private int CurrentBucket()
        {
            int h = snTime.Hour;
            return h < 0 || h > 23 ? 0 : h;
        }

        private double sniperZDeltaCached;
        private double sniperVolumeRankCached;
        private double sniperAtrPercentileCached;

        private void UpdateSniperCalibration(VolumetricBarsType volType)
        {
            long delta = currentBarDelta;
            long vol = snVolume;
            if (volType != null && evalBarIndex >= 0 && evalBarIndex < volType.Volumes.Length)
            {
                VolumetricData vd = volType.Volumes[evalBarIndex];
                if (vd != null) { delta = vd.BarDelta; vol = vd.TotalVolume; }
            }

            int b = CurrentBucket();

            sniperZDeltaCached = bucketDelta[b].Z(delta, 60);
            sniperVolumeRankCached = bucketVolume[b].RankOf(vol);

            // mises en cache. Aucun percentile n'est evalue dans OnMarketData.
            double absDelta = Math.Abs((double)delta);
            sniperZMadDeltaCached = bucketAbsDelta[b].ZMad(absDelta, 60);
            sniperDeltaPercentileCached = bucketAbsDelta[b].PercentileRank(absDelta);

            double a = SniperAtr();
            sniperAtrPercentileCached = sniperAtrHistory != null && sniperAtrHistory.IsReady(100)
                ? sniperAtrHistory.RankOf(a)
                : 50.0;

            bucketDelta[b].Add(delta);
            bucketVolume[b].Add(vol);
            bucketAbsDelta[b].Add(absDelta);
            if (vol > 0 && sniperBarVolumeHistory != null) sniperBarVolumeHistory.Add(vol);

            // ce qui supprime le probleme de cold-start au chargement du graphique.
            UpdateInstrumentProfile(a, b);

            if (a > 0) sniperAtrHistory.Add(a);

            EnsureCvdSeriesCapacity(Math.Max(200, CvdRegressionBars * 6));
            cvdSeries.Add(sniperCumulativeDelta);
            priceSeries.Add(snClose);
        }


        /// <summary>Alimente le profiler d'instrument puis rafraichit les seuils derives.
        /// Appele une fois par barre (y compris en State.Historical : pas de cold-start).</summary>
        private void UpdateInstrumentProfile(double atr, int bucket)
        {
            if (sniperProfiler == null) return;

            if (AutoProfileInstrument)
            {
                double medVol = sniperBarVolumeHistory != null && sniperBarVolumeHistory.IsReady(40)
                    ? sniperBarVolumeHistory.Median()
                    : 0.0;
                if (medVol > 0) sniperProfiler.Observe(atr, tickSize, medVol);
            }

            RefreshV3Thresholds(bucket);
        }

        /// <summary>
        /// Derive le seuil de delta d'absorption du percentile calibre par le profiler,
        /// encadre par un PLANCHER et un PLAFOND. Sans ces garde-fous, un percentile pur
        /// declenche sur du bruit en marche mort et devient aveugle apres un spike geant.
        /// </summary>
        private void RefreshV3Thresholds(int bucket)
        {
            sniperV3DeltaThresholdCached = 0;

            if (!AutoCalibrationV3) { sniperCalibTag = "v3=off"; return; }

            SniperRingStat st = bucketAbsDelta[bucket];
            if (st == null || !st.IsReady(60))
            {
                sniperCalibTag = "v3=warmup n=" + (st == null ? 0 : st.Count);
                return;
            }

            double p = sniperProfiler.AbsorptionDeltaPercentile;
            double raw = st.Percentile(p);

            // a 12 lots et declencher sur du bruit. Plancher proportionnel a la mediane.
            double floor = sniperProfiler.VolumeFloorFactor * st.Median();

            // GARDE-FOU 2 - plafond : un unique spike geant ne doit pas rendre le moteur
            // aveugle pour le reste de la session.
            double cap = Math.Max(1.0, st.Percentile(99) * 1.5);

            double thr = Math.Min(cap, Math.Max(raw, floor));
            sniperV3DeltaThresholdCached = (int)Math.Max(10, Math.Round(thr));

            // TRACABILITE (non negociable) : un signal manque doit rester debuggable.
            sniperCalibTag = string.Format(CultureInfo.InvariantCulture,
                "v3 {0} h={1} P{2:F0}={3:F0} floor={4:F0} cap={5:F0} thr={6} zmad={7:F2} dpct={8:F1}",
                sniperProfiler.Class, bucket, p, raw, floor, cap,
                sniperV3DeltaThresholdCached, sniperZMadDeltaCached, sniperDeltaPercentileCached);

            if (EnableShadowJournal && EnableDebugMode)
                SafePrint("VP_V3Calib: " + sniperCalibTag + " | " + sniperProfiler);
        }

        private bool SniperV3Ready() { return AutoCalibrationV3 && sniperV3DeltaThresholdCached > 0; }
        private int SniperV3DeltaThreshold() { return sniperV3DeltaThresholdCached; }
        private double SniperZMadDelta() { return sniperZMadDeltaCached; }
        private double SniperDeltaPercentile() { return sniperDeltaPercentileCached; }
        private double SniperIcebergZMin() { return sniperProfiler.IcebergZMadMin; }
        private double SniperProfilerAbsorptionPercentile() { return sniperProfiler.AbsorptionDeltaPercentile; }
        private double SniperExhaustionPercentile() { return sniperProfiler.ExhaustionPercentile; }
        private double SniperDeltaFlipPercentile() { return sniperProfiler.DeltaFlipMinPercentile; }
        private string SniperCalibTag() { return sniperCalibTag; }
        private string SniperClassName() { return sniperProfiler.Class.ToString(); }

        private double ZDeltaCurrent()
        {
            return sniperZDeltaCached;
        }

        private double VolumeRankCurrent()
        {
            return sniperVolumeRankCached;
        }

        private double AtrPercentileRank()
        {
            return sniperAtrPercentileCached;
        }

        #endregion

        #region SNIPER - Section 7 : contexte, microstructure normalisee, scoring

        /// <summary>Traduit le Day Type textuel de l'AMC Pro en enum Sniper et
        /// calcule l'overlap de value area avec la session precedente.</summary>
        private void UpdateSniperContext()
        {
            if (!isIbComplete)
                sniperDayType = SniperDayType.Undetermined;
            else if (currentDayType.StartsWith("Trend Day"))
                sniperDayType = SniperDayType.Trend;
            else if (currentDayType.StartsWith("Normal Variation"))
                sniperDayType = SniperDayType.NormalVariation;
            else if (currentDayType.StartsWith("Normal Day"))
                sniperDayType = SniperDayType.Normal;
            else if (currentDayType.StartsWith("Range Day"))
                sniperDayType = SniperDayType.Neutral;
            else
                sniperDayType = SniperDayType.Undetermined;

            sniperVaOverlap = 0;
            if (sniperPrevProfileValid && sniperSessionProfile.Valid)
            {
                double lo = Math.Max(sniperSessionProfile.Val, sniperPrevVal);
                double hi = Math.Min(sniperSessionProfile.Vah, sniperPrevVah);
                double inter = Math.Max(0, hi - lo);
                double prevWidth = Math.Max(tickSize, sniperPrevVah - sniperPrevVal);
                sniperVaOverlap = inter / prevWidth;
            }
        }

        /// <summary>Evenement d'absorption normalise : le declencheur reste la detection
        /// robuste de l'AMC Pro (EvaluateAbsorption, fenetres de scan + agression), le
        /// moteur n'ajoute que le Z-score par bucket et le filtre de deplacement.</summary>
        private void DetectSniperAbsorption()
        {
            double a = SniperAtr();
            if (a <= 0) return;

            double z = ZDeltaCurrent();
            double displacement = Math.Abs(snClose - snOpen) / a;
            double volRank = VolumeRankCurrent();

            bool amcBull = isBullishAbsorptionActive && lastAbsorptionBarIndex == evalBarIndex;
            bool amcBear = isBearishAbsorptionActive && lastAbsorptionBarIndex == evalBarIndex;

            bool zBull = z <= AbsorptionZScore;          // vendeurs agressifs absorbes -> haussier
            bool zBear = z >= -AbsorptionZScore;         // acheteurs agressifs absorbes -> baissier
            bool lowDisplacement = displacement < AbsorptionMaxDisplacementAtr;
            bool enoughVolume = volRank >= 75;

            bool isBull = amcBull || (zBull && !amcBear);
            bool isBear = amcBear || (zBear && !amcBull);

            if ((amcBull || amcBear || zBull || zBear) && lowDisplacement && enoughVolume && (isBull ^ isBear))
            {
                sniperAbsorptionEvents.Add(new SniperAbsorptionEvent
                {
                    BarIdx = evalBarIndex,
                    Price = (snHigh + snLow) / 2.0,
                    ZDelta = z,
                    IsBull = isBull,
                    Volume = snVolume
                });
            }

            // RemoveAt(0) en O(n) par element expire.
            int expired = 0;
            while (expired < sniperAbsorptionEvents.Count
                   && evalBarIndex - sniperAbsorptionEvents[expired].BarIdx > 20) expired++;
            if (expired > 0) sniperAbsorptionEvents.RemoveRange(0, expired);
        }

        /// <summary>Cluster d'absorption : N barres dans les 4 dernieres, meme prix +/- 3 ticks.</summary>
        private bool AbsorptionCluster(bool wantBull, out double zSum, out double clusterPrice)
        {
            zSum = 0; clusterPrice = 0;
            int hits = 0;
            double sumP = 0;
            double tol = 3 * tickSize;
            double anchor = 0; bool anchored = false;

            for (int i = sniperAbsorptionEvents.Count - 1; i >= 0; i--)
            {
                SniperAbsorptionEvent e = sniperAbsorptionEvents[i];
                if (evalBarIndex - e.BarIdx > 4) break;
                if (e.IsBull != wantBull) continue;
                if (!anchored) { anchor = e.Price; anchored = true; }
                if (Math.Abs(e.Price - anchor) > tol) continue;
                hits++; sumP += e.Price; zSum += Math.Abs(e.ZDelta);
            }

            if (hits >= AbsorptionMinBars) { clusterPrice = sumP / hits; return true; }
            return false;
        }

        private int sniperCvdSlopeLastBar = -1;

        /// <summary>Pente CVD par OLS + Z-score (stationnaire, contrairement a une
        /// divergence de niveau).</summary>
        private bool CvdSlopeDivergence(bool isBuy, out double zSlope)
        {
            zSlope = 0;
            int n = Math.Min(CvdRegressionBars, cvdSeries.Count);
            if (n < 8) return false;

            double bCvd = SniperOls(cvdSeries, n);
            double bPrice = SniperOls(priceSeries, n);

            double sd = sniperCvdSlopeHistory.Std();
            if (sd <= 1e-9)
            {
                if (sniperCvdSlopeLastBar != evalBarIndex)
                {
                    sniperCvdSlopeHistory.Add(bCvd);
                    sniperCvdSlopeLastBar = evalBarIndex;
                }
                return false;
            }

            zSlope = Math.Abs(bCvd) / sd;

            if (sniperCvdSlopeLastBar != evalBarIndex)
            {
                sniperCvdSlopeHistory.Add(bCvd);
                sniperCvdSlopeLastBar = evalBarIndex;
            }

            if (zSlope < CvdSlopeZThreshold) return false;

            if (isBuy) return bPrice < 0 && bCvd > 0;
            return bPrice > 0 && bCvd < 0;
        }

        /// <summary>Redimensionne les series CVD/prix quand CvdRegressionBars change,
        /// en conservant les valeurs les plus recentes. O(cap), execute uniquement
        /// lors d'un changement de capacite.</summary>
        private void EnsureCvdSeriesCapacity(int cap)
        {
            if (cap < 16) cap = 16;
            if (cvdSeries.Capacity == cap && priceSeries.Capacity == cap) return;

            cvdSeries = ResizeSeries(cvdSeries, cap);
            priceSeries = ResizeSeries(priceSeries, cap);
        }

        private static RingBuffer<double> ResizeSeries(RingBuffer<double> src, int cap)
        {
            RingBuffer<double> dst = new RingBuffer<double>(cap);
            if (src != null)
            {
                int skip = Math.Max(0, src.Count - cap);
                for (int i = skip; i < src.Count; i++) dst.Add(src[i]);
            }
            return dst;
        }

        private static double SniperOls(RingBuffer<double> series, int n)
        {
            if (series == null || n <= 0 || series.Count < n) return 0;
            int start = series.Count - n;
            double sx = 0, sy = 0, sxy = 0, sxx = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = start + i;
                if (idx < 0 || idx >= series.Count) continue;
                double x = i, y = series[idx];
                sx += x; sy += y; sxy += x * y; sxx += x * x;
            }
            double denom = n * sxx - sx * sx;
            if (Math.Abs(denom) < 1e-12) return 0;
            return (n * sxy - sx * sy) / denom;
        }

        private double SniperKeyLevelTolerance()
        {
            double a = SniperAtr();
            if (a <= 0) a = 4 * tickSize;
            return KeyLevelToleranceAtr * a;
        }

        /// <summary>Naked POC le plus proche, avec fraicheur exponentielle. S'appuie sur
        /// sessionHistory / PocNaked de l'AMC Pro (UpdateNakedPocs).</summary>
        private bool NearestNpoc(double price, out double npocPrice, out double freshness)
        {
            npocPrice = 0; freshness = 0;
            double tol = SniperKeyLevelTolerance();
            double bestDist = double.MaxValue;
            bool found = false;

            for (int i = 0; i < sessionHistory.Count; i++)
            {
                SessionLevels s = sessionHistory[i];
                if (!s.PocNaked || s.Poc <= 0) continue;
                double d = Math.Abs(price - s.Poc);
                if (d <= tol && d < bestDist)
                {
                    bestDist = d;
                    npocPrice = s.Poc;
                    // une session d'age. La decroissance part donc de age = 1, sinon le
                    // maximum de +5 est structurellement inatteignable.
                    int ageSessions = Math.Max(0, sessionHistory.Count - 2 - i);
                    freshness = Math.Exp(-ageSessions / Math.Max(1.0, NpocDecayHalfLife));
                    found = true;
                }
            }
            return found;
        }

        /// <summary>Confluence multi-niveaux : composite (classe A), session courante,
        /// session precedente et Naked POC.</summary>
        private int CountConfluentLevels(double price, out bool hasClassA)
        {
            double tol = SniperKeyLevelTolerance();
            int count = 0;
            hasClassA = false;

            if (sniperCompositeProfile.Valid)
            {
                if (Math.Abs(price - sniperCompositeProfile.Poc) <= tol) { count++; hasClassA = true; }
                if (Math.Abs(price - sniperCompositeProfile.Vah) <= tol) { count++; hasClassA = true; }
                if (Math.Abs(price - sniperCompositeProfile.Val) <= tol) { count++; hasClassA = true; }
            }
            if (pocPrice > 0 && Math.Abs(price - pocPrice) <= tol) count++;
            if (vahPrice > 0 && Math.Abs(price - vahPrice) <= tol) count++;
            if (valPrice > 0 && Math.Abs(price - valPrice) <= tol) count++;

            bool isNakedPoc;
            if (IsNearPriorSessionLevel(price, tol, out isNakedPoc))
            {
                count++;
                if (isNakedPoc) hasClassA = true;
            }
            return count;
        }

        /// <summary>Qualite LVN : creux local (derivee seconde) + largeur minimale.
        /// Le veto binaire IsLowVolumeNode de l'AMC Pro reste la condition prealable.</summary>
        private double LvnQuality(double price)
        {
            if (!sniperCompositeProfile.Valid || sniperCompositeProfile.Total <= 0) return 0;

            long t = SniperPriceToTick(price);
            const int w = 6;
            long center = 0, flanks = 0;
            int widthTicks = 0;
            double median = sniperCompositeProfile.LevelPercentile(50);

            for (int k = -1; k <= 1; k++) center += sniperCompositeProfile.At(t + k);
            for (int k = 2; k <= w; k++) flanks += sniperCompositeProfile.At(t + k) + sniperCompositeProfile.At(t - k);

            double centerAvg = center / 3.0;
            double flankAvg = flanks / (double)(2 * (w - 1));
            if (flankAvg <= 0) return 0;

            for (int k = -w; k <= w; k++)
                if (sniperCompositeProfile.At(t + k) < median * 0.45) widthTicks++;

            double depth = 1.0 - (centerAvg / flankAvg);
            if (depth <= 0 || widthTicks < 3) return 0;
            return Clamp(depth * Math.Min(1.0, widthTicks / 5.0), 0, 1);
        }

        private double VwapSigmaDistance(double price)
        {
            if (currentVwapPrice <= 0) return 0;
            double sigma = adaptiveAvgBarRange > 0 ? adaptiveAvgBarRange * 2.0 : SniperAtr() * 2.0;
            if (sigma <= 0) return 0;
            return (price - currentVwapPrice) / sigma;
        }

        private int[] newsMinutesCache = new int[0];
        private string newsMinutesCacheSrc = null;

        private int[] NewsMinutesOfDay()
        {
            string src = NewsTimesCsv ?? "";
            if (string.Equals(src, newsMinutesCacheSrc, StringComparison.Ordinal)) return newsMinutesCache;

            List<int> parsed = new List<int>(8);
            string[] parts = src.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                int hhmm;
                if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out hhmm)) continue;
                int hh = hhmm / 100, mm = hhmm % 100;
                if (hh < 0 || hh > 23 || mm < 0 || mm > 59) continue;
                parsed.Add(hh * 60 + mm);
            }

            newsMinutesCache = parsed.ToArray();
            newsMinutesCacheSrc = src;
            return newsMinutesCache;
        }

        /// <summary>Fenetre news : proxy statistique horaire, exprime dans le fuseau du
        /// graphique. N'est PAS un calendrier economique — la penalite associee est
        private bool IsSniperNewsBlackout()
        {
            if (NewsBlackoutMinutes <= 0 || (NewsWindowPenalty <= 0 && !NewsHardBlock)) return false;
            if (NewsWeekdaysOnly && (snTime.DayOfWeek == DayOfWeek.Saturday || snTime.DayOfWeek == DayOfWeek.Sunday))
                return false;

            int[] times = NewsMinutesOfDay();
            if (times.Length == 0) return false;

            int now = snTime.Hour * 60 + snTime.Minute;
            for (int i = 0; i < times.Length; i++)
            {
                int diff = Math.Abs(now - times[i]);
                if (diff > 720) diff = 1440 - diff; // Gestion du passage de minuit
                if (diff <= NewsBlackoutMinutes) return true;
            }

            return false;
        }

        private static int SniperMinutesOfDay(int hhmm) { return (hhmm / 100) * 60 + (hhmm % 100); }

        private double ScoreContextOpenDrive(bool isBuy, List<string> detail)
        {
            double s = 0;

            if (!isIbComplete)
            {
                s += 10;
                detail.Add("IB en cours (+10)");
            }

            if (sniperPrevProfileValid)
            {
                s += 8;
                detail.Add("Profil precedent valide (+8)");
            }

            double ar = AtrPercentileRank();
            if (ar >= AtrPercentileMin && ar <= AtrPercentileMax)
            {
                s += 4;
                detail.Add(string.Format(CultureInfo.InvariantCulture, "ATRpct={0:0} (+4)", ar));
            }

            if (IsHtfAligned(isBuy))
            {
                s += 4;
                detail.Add("HTF aligne (+4)");
            }
            else
            {
                detail.Add("HTF non aligne (+0)");
            }

            // Confirmation du rejet : flux delta dans le sens du retournement
            bool deltaReversal = isBuy ? currentBarDelta > 0 : currentBarDelta < 0;
            if (deltaReversal)
            {
                s += 4;
                detail.Add("Delta confirme rejet (+4)");
            }
            else
            {
                detail.Add("Delta non confirmatif (+0)");
            }

            return Math.Min(30.0, s);
        }

        private double ScoreContext(bool isBuy, string setup, List<string> detail)
        {
            if (setup == "OPEN_DRIVE_FAILURE")
                return ScoreContextOpenDrive(isBuy, detail);

            double s = 0;
            
            // Classification dynamique des setups en 3 familles (harmonisée avec N3/N4)
            string upperSetup = setup != null ? setup.ToUpperInvariant() : "";
            bool isBreakoutOrAcceptance = upperSetup.Contains("BREAKOUT") || upperSetup.Contains("ACCEPTANCE");
            bool pureTrend = setup == "STACKED_IMB_RETEST" || isBreakoutOrAcceptance;
            bool dualHybrid = setup == "DELTA_FLIP" || setup == "CUM_DELTA_DIV" || setup == "LVN_REJECTION";
            bool meanReversion = !pureTrend && !dualHybrid;

            // DayType: les setups Dual/Hybride sont valides en Trend Day ET Normal Day
            bool dtOk;
            if (dualHybrid)
            {
                // Setups hybrides: valides dans tous les types de journée
                dtOk = true;
                s += 10;
                detail.Add("DayType=" + sniperDayType + " (Setup Hybride valide) (+10)");
            }
            else if (meanReversion)
            {
                dtOk = (sniperDayType == SniperDayType.Normal || sniperDayType == SniperDayType.NormalVariation || sniperDayType == SniperDayType.Neutral);
                if (dtOk) { s += 10; detail.Add("DayType=" + sniperDayType + " (+10)"); }
                else detail.Add("DayType=" + sniperDayType + " INCOMPATIBLE (+0)");
            }
            else // pureTrend
            {
                bool trendDirectionOk = (isBuy && isIbUpExtension) || (!isBuy && isIbDownExtension);
                dtOk = (sniperDayType == SniperDayType.Trend && trendDirectionOk);
                if (dtOk) { s += 10; detail.Add("DayType=" + sniperDayType + " Aligne (+10)"); }
                else detail.Add("DayType=" + sniperDayType + " " + (trendDirectionOk ? "INCOMPATIBLE" : "CONTRE-TENDANCE") + " (+0)");
            }

            bool ibDirectionAligned = meanReversion || (isBuy && isIbUpExtension) || (!isBuy && isIbDownExtension);
            bool ibOk = meanReversion
                ? (ibExtensionRatio >= IbExtensionMin && ibExtensionRatio <= IbExtensionMax)
                : (ibExtensionRatio >= IbExtensionTrendMin && ibDirectionAligned);
            if (ibOk) { s += 6; detail.Add(string.Format(CultureInfo.InvariantCulture, "IBext={0:0.00} (+6)", ibExtensionRatio)); }
            else detail.Add(string.Format(CultureInfo.InvariantCulture, "IBext={0:0.00} " + (ibDirectionAligned ? "hors plage" : "contre-sens") + " (+0)", ibExtensionRatio));

            bool ovOk = meanReversion ? sniperVaOverlap >= VaOverlapRangeThreshold : sniperVaOverlap < VaOverlapRangeThreshold;
            if (ovOk) { s += 6; detail.Add(string.Format(CultureInfo.InvariantCulture, "VAoverlap={0:0.00} (+6)", sniperVaOverlap)); }
            else detail.Add(string.Format(CultureInfo.InvariantCulture, "VAoverlap={0:0.00} (+0)", sniperVaOverlap));

            double ar = AtrPercentileRank();
            if (ar >= AtrPercentileMin && ar <= AtrPercentileMax)
            {
                s += 4; detail.Add(string.Format(CultureInfo.InvariantCulture, "ATRpct={0:0} (+4)", ar));
            }
            else detail.Add(string.Format(CultureInfo.InvariantCulture, "ATRpct={0:0} hors plage (+0)", ar));

            // Alignement HTF : migration de POC (Sniper) confirmee par le biais EMA HTF
            // de l'AMC Pro. Les deux doivent etre coherents pour marquer les 4 points.
            if (sniperPrevProfileValid && sniperSessionProfile.Valid)
            {
                double migration = sniperSessionProfile.Poc - sniperPrevPoc;
                bool aligned = meanReversion
                    ? Math.Abs(migration) < SniperKeyLevelTolerance() * 2
                    : (isBuy ? migration > 0 : migration < 0);
                if (aligned && IsHtfAligned(isBuy)) { s += 4; detail.Add("POCmigration + HTF OK (+4)"); }
                else if (aligned) { s += 2; detail.Add("POCmigration OK, HTF neutre/oppose (+2)"); }
                else detail.Add("POCmigration KO (+0)");
            }

            return s;
        }

        private double ScoreLocation(bool isBuy, double price, string setup, List<string> detail)
        {
            double s = 0;

            // Classification des setups d'orderflow pour scoring adaptatif
            bool isOrderflowSetup = setup == "DELTA_FLIP" || setup == "CUM_DELTA_DIV" || setup == "LVN_REJECTION";

            // Pour les setups d'orderflow, on utilise un scoring adaptatif qui ne requiert
            // pas un contact exact avec un niveau profil fixe (POC/VAH/VAL/NPOC)
            if (isOrderflowSetup && IsScalpingPro)
            {
                // Utiliser les zones d'imbalance existantes comme proxy de confluence SMC
                bool hasImbalance = false;
                for (int i = 0; i < imbalanceZones.Count; i++)
                {
                    ImbalanceZone z = imbalanceZones[i];
                    if (z.IsBull == isBuy && evalBarIndex - z.BarIndex <= SmcEventMaxAgeBars)
                    {
                        hasImbalance = true;
                        break;
                    }
                }
                
                if (hasImbalance)
                {
                    s += 8;
                    detail.Add("Confluence Imbalance/FVG (+8)");
                }
                
                // Si confluence solide, on autorise le score même sans niveau profil exact
                if (s >= 6)
                {
                    // Ajouter points VWAP si applicable
                    double sig = Math.Abs(VwapSigmaDistance(price));
                    if (sig >= 1.0)
                    {
                        double vwPts = sig >= 2.0 ? 4 : 2;
                        s += vwPts;
                        detail.Add(string.Format(CultureInfo.InvariantCulture, "VWAP {0:0.0}sigma (+{1:0.0})", sig, vwPts));
                    }
                    return Math.Min(30.0, s);
                }
            }

            // Scoring standard par niveau profil (fallback ou pour setups non-orderflow)
            // s'excluent mutuellement au meme prix. On les traite en ALTERNATIVE sur la
            // meme enveloppe de 12 points au lieu de les additionner (+12 / +3 jamais
            // cumulables), sinon le maximum de N2 est structurellement inatteignable.
            bool hasClassA;
            int confluent = CountConfluentLevels(price, out hasClassA);
            double lvn = LvnQuality(price);
            if (hasClassA) { s += 12; detail.Add("Niveau classe A (+12)"); }
            else if (lvn > 0)
            {
                double lvnPts = 12.0 * lvn;
                s += lvnPts;
                detail.Add(string.Format(CultureInfo.InvariantCulture, "Niveau LVN q={0:0.00} (+{1:0.0})", lvn, lvnPts));
            }
            else if (isOrderflowSetup)
            {
                // Pour setups orderflow sans confluence SMC, accepter proximité VWAP ≥1σ
                double sig = Math.Abs(VwapSigmaDistance(price));
                if (sig >= 2.0) { s += 4; detail.Add(string.Format(CultureInfo.InvariantCulture, "VWAP {0:0.0}sigma (+4)", sig)); }
                else if (sig >= 1.0) { s += 2; detail.Add(string.Format(CultureInfo.InvariantCulture, "VWAP {0:0.0}sigma (+2)", sig)); }
                else detail.Add("Setup orderflow sans niveau profil ni VWAP (+0)");
            }
            else detail.Add("Ni classe A ni LVN (+0)");

            if (confluent >= 2) { s += 6; detail.Add("Confluence x" + confluent + " (+6)"); }
            else if (confluent == 1) { s += 2; detail.Add("Confluence x1 (+2)"); }

            double np, fresh;
            if (NearestNpoc(price, out np, out fresh))
            {
                double pts = 5.0 * fresh;
                s += pts;
                detail.Add(string.Format(CultureInfo.InvariantCulture, "NPOC {0:0.00} fresh={1:0.00} (+{2:0.0})", np, fresh, pts));
            }

            double vwapSig = Math.Abs(VwapSigmaDistance(price));
            if (vwapSig >= 2.0) { s += 4; detail.Add(string.Format(CultureInfo.InvariantCulture, "VWAP {0:0.0}sigma (+4)", vwapSig)); }
            else if (vwapSig >= 1.0) { s += 2; detail.Add(string.Format(CultureInfo.InvariantCulture, "VWAP {0:0.0}sigma (+2)", vwapSig)); }

            return s;
        }

        // Anti-correlation : absorption / iceberg / delta flip decrivent le MEME facteur
        // latent. On ne retient que le maximum de la famille, jamais la somme.
        private double ScoreMicrostructure(bool isBuy, string setupName, List<string> detail)
        {
            double s = 0;
            string upperSetup = setupName != null ? setupName.ToUpperInvariant() : "";
            bool isBreakoutOrAcceptance = upperSetup.Contains("BREAKOUT") || upperSetup.Contains("ACCEPTANCE");
            bool isDeltaFlip = upperSetup.Contains("DELTA_FLIP");
            bool isRetest = upperSetup.Contains("RETEST");

            double zSum, clusterPrice;
            double passiveScore = 0;
            if (AbsorptionCluster(isBuy, out zSum, out clusterPrice))
            {
                passiveScore = Clamp(zSum / (Math.Abs(AbsorptionZScore) * AbsorptionMinBars), 0, 1.0) * 10.0;
                passiveScore *= Clamp(absorptionQualityFactor, 0.5, 1.2);
                detail.Add(string.Format(CultureInfo.InvariantCulture, "AbsorptionCluster Zsum={0:0.0} q={1:0.00} (+{2:0.0})", zSum, absorptionQualityFactor, passiveScore));
            }
            // Iceberg de l'AMC Pro : meme famille, on prend le max, pas la somme.
            if ((isBuy && isIcebergBullish) || (!isBuy && isIcebergBearish))
            {
                double icePts = 8.0;
                if (icePts > passiveScore)
                {
                    detail.Add(string.Format(CultureInfo.InvariantCulture, "Iceberg {0} (+{1:0.0}, remplace absorption)", isBuy ? "acheteur" : "vendeur", icePts));
                    passiveScore = icePts;
                }
            }
            // Setup-aware: Breakouts & Acceptances rely on impulse rather than passive absorption
            if (isBreakoutOrAcceptance) passiveScore *= 0.5;
            s += Math.Min(10.0, passiveScore);

            double zSlope;
            if (CvdSlopeDivergence(isBuy, out zSlope))
            {
                double pts = Clamp(zSlope / (CvdSlopeZThreshold * 2.0), 0, 1.0) * 7.0;
                if (isDeltaFlip || isBreakoutOrAcceptance) pts *= 1.3;
                s += pts;
                detail.Add(string.Format(CultureInfo.InvariantCulture, "CVDslopeDiv Z={0:0.00} (+{1:0.0})", zSlope, pts));
            }
            else if ((isBuy && isCumDeltaDivBullish) || (!isBuy && isCumDeltaDivBearish))
            {
                double pts = isDeltaFlip ? 4.0 : 3.0;
                s += pts;
                detail.Add("CumDeltaDiv AMC (+" + pts + ")");
            }

            int bestLevels = 0;
            for (int i = 0; i < imbalanceZones.Count; i++)
            {
                ImbalanceZone z = imbalanceZones[i];
                if (z.IsBull != isBuy) continue;
                if (evalBarIndex - z.BarIndex > 40) continue;
                if (z.Levels > bestLevels) bestLevels = z.Levels;
            }
            if (bestLevels >= ImbalanceMinStack)
            {
                double pts = Clamp(bestLevels / (double)(ImbalanceMinStack * 2), 0.5, 1.0) * 5.0;
                if (isBreakoutOrAcceptance) pts *= 1.4; // Imbalances are key for breakout / acceptance
                s += pts;
                detail.Add("StackedImb x" + bestLevels + string.Format(CultureInfo.InvariantCulture, " (+{0:0.0})", pts));
            }

            if (FinishedAuctionAtExtreme(isBuy)) 
            { 
                double pts = isRetest ? 4.0 : 3.0;
                s += pts; 
                detail.Add("FinishedAuction (+" + pts + ")"); 
            }

            return Math.Min(25.0, s);
        }

        /// <summary>Finished auction a l'extreme : le verdict de l'AMC Pro (seuil
        /// adaptatif EffectiveFinishedAuctionMaxVolume) prime, avec repli sur le test
        /// de percentile du profil sniper.</summary>
        private bool FinishedAuctionAtExtreme(bool isBuy)
        {
            if (isBuy && isFinishedAuctionBuy) return true;
            if (!isBuy && isFinishedAuctionSell) return true;

            if (!sniperSessionProfile.Valid) return false;
            double extreme = isBuy ? snLow : snHigh;
            long t = SniperPriceToTick(extreme);
            long v = sniperSessionProfile.At(t) + sniperSessionProfile.At(t + 1) + sniperSessionProfile.At(t - 1);
            double p15 = sniperSessionProfile.LevelPercentile(15) * 3;
            return v > 0 && v <= p15;
        }

        private double ScoreTrigger(bool isBuy, string setupName, List<string> detail)
        {
            double s = 0;
            string upperSetup = setupName != null ? setupName.ToUpperInvariant() : "";
            bool isBreakoutOrAcceptance = upperSetup.Contains("BREAKOUT") || upperSetup.Contains("ACCEPTANCE");
            bool isDeltaFlip = upperSetup.Contains("DELTA_FLIP");
            bool isRetest = upperSetup.Contains("RETEST");

            double range = snHigh - snLow;
            if (range <= 0) return 0;

            double wick = isBuy
                ? (Math.Min(snOpen, snClose) - snLow) / range
                : (snHigh - Math.Max(snOpen, snClose)) / range;
            
            // Retests rely on rejection wicks; breakouts rely on body expansion / closing outside
            if (!isBreakoutOrAcceptance && wick * 100.0 >= RejectionWickPercent)
            {
                double pts = isRetest ? 7.0 : 6.0;
                s += pts;
                detail.Add(string.Format(CultureInfo.InvariantCulture, "Rejet meche {0:0}% (+{1:0.0})", wick * 100, pts));
            }
            else if (isBreakoutOrAcceptance)
            {
                double bodyRatio = Math.Abs(snClose - snOpen) / range;
                if (bodyRatio >= 0.5)
                {
                    s += 6.0;
                    detail.Add(string.Format(CultureInfo.InvariantCulture, "Breakout Body Expansion {0:0}% (+6.0)", bodyRatio * 100));
                }
            }

            if (vahPrice > 0 && valPrice > 0)
            {
                bool reentry = isBuy
                    ? (snClose > valPrice && snLow < valPrice)
                    : (snClose < vahPrice && snHigh > vahPrice);
                if (reentry) { s += 5; detail.Add("Reintegration VA (+5)"); }
            }

            double z = ZDeltaCurrent();
            double zThreshold = isDeltaFlip ? 1.5 : 1.0;
            if ((isBuy && z >= zThreshold) || (!isBuy && z <= -zThreshold))
            {
                double pts = (isDeltaFlip || isBreakoutOrAcceptance) ? 5.0 : 4.0;
                s += pts;
                detail.Add(string.Format(CultureInfo.InvariantCulture, "Delta Z={0:0.00} (+" + pts + ")", z));
            }

            return s;
        }

        private double ScorePenalties(bool isBuy, List<string> detail)
        {
            double p = 0;

            for (int i = pendingCandidates.Count - 1; i >= 0; i--)
            {
                Candidate c = pendingCandidates[i];
                if (evalBarIndex - c.BarIdx > 5) continue;
                if (c.IsBuy != isBuy && c.ScoreRaw >= 50)
                {
                    p -= OppositeSignalPenalty;
                    detail.Add("Signal oppose recent (-" + OppositeSignalPenalty + ")");
                    break;
                }
            }

            double volRank = VolumeRankCurrent();
            bool exhaustion = (volRank >= 95 && ((isBuy && snClose >= sessionHigh - tickSize) || (!isBuy && snClose <= sessionLow + tickSize)))
                              || (isBuy && isExhaustionSell) || (!isBuy && isExhaustionBuy);
            if (exhaustion) { p -= 8; detail.Add("Exhaustion dans le sens (-8)"); }

            // applique tous les jours sur des horaires codes en dur.
            if (IsSniperNewsBlackout()) { p -= NewsWindowPenalty; detail.Add("Fenetre news (-" + NewsWindowPenalty + ")"); }

            if (volRank <= 20) { p -= 5; detail.Add("Liquidite faible (-5)"); }

            if (!IsRegimeValid()) { p -= 10; detail.Add("Regime ATR hors plage (-10)"); }

            return p;
        }

        private Candidate Assemble(string name, bool isBuy, double entry, double refLevel)
        {
            Candidate c = new Candidate
            {
                Name = name,
                IsBuy = isBuy,
                BarIdx = evalBarIndex,
                Time = snTime,
                Entry = entry
            };

            string upperSetupName = name != null ? name.ToUpperInvariant() : "";
            bool isBreakoutOrAcceptanceName = upperSetupName.Contains("BREAKOUT") || upperSetupName.Contains("ACCEPTANCE");
            bool meanReversion = name != "STACKED_IMB_RETEST" && !isBreakoutOrAcceptanceName;

            // Volume Profile V2 Context Attachment
            if (currentVpContext != null)
            {
                c.VolumeProfile = currentVpContext.Clone();
                if (c.VolumeProfile.ConfluenceCount >= 2 && !string.IsNullOrEmpty(c.VolumeProfile.ConfluenceType))
                {
                    c.Detail.Add(c.VolumeProfile.ConfluenceType);
                }
            }

            c.N1 = ScoreContext(isBuy, name, c.Detail);
            c.N2 = ScoreLocation(isBuy, refLevel, name, c.Detail);
            c.N3 = ScoreMicrostructure(isBuy, name, c.Detail);
            c.N4 = ScoreTrigger(isBuy, name, c.Detail);
            NormalizeLevels(c, meanReversion);
            c.Penalty = ScorePenalties(isBuy, c.Detail);

            ComputeSniperRisk(c, refLevel);

            double risk = Math.Abs(c.Entry - c.Stop);
            double reward = Math.Abs(c.Target1 - c.Entry);
            c.Rr = risk > 0 ? reward / risk : 0;

            // (et double-compte un facteur deja score en N1). Il reste un gate pour le
            // setup de tendance, et devient un simple modulateur ailleurs.
            c.HtfAligned = IsHtfAligned(isBuy);
            // (il continue d'alimenter le score et la penalite) mais il n'est jamais
            // eliminatoire : un desalignement coute HtfMisalignmentPenalty points.
            bool htfIsGate = (!meanReversion || HtfGateAppliesToMeanReversion) && !HtfSoftMode;
            if (!c.HtfAligned && !htfIsGate && HtfMisalignmentPenalty > 0)
            {
                c.Penalty -= HtfMisalignmentPenalty;
                c.Detail.Add("HTF non aligne, modulateur (-" + HtfMisalignmentPenalty + ")");
            }

            bool g1 = c.N1 >= GateN1MinScore;
            bool g2 = c.N2 >= GateN2MinScore;
            
            // Synchronisation AMC Core: si le candidat correspond au signal validé par AMC Core,
            // on contourne la porte N2 pour éviter le rejet artificiel
            bool matchesAmcCore = !string.IsNullOrEmpty(amcCoreValidatedSignal) && amcCoreSignalDirectional;
            if (matchesAmcCore)
            {
                bool isBuyMatch = c.IsBuy && amcCoreValidatedSignal.Contains("BUY");
                bool isSellMatch = !c.IsBuy && amcCoreValidatedSignal.Contains("SELL");
                bool setupMatch = amcCoreValidatedSignal.Contains(c.Name.Replace("_", " "));
                
                if ((isBuyMatch || isSellMatch) && setupMatch)
                {
                    g2 = true; // Bypass N2 gate pour signaux synchronisés AMC Core
                    c.GateBypassed = "Sync AMC Core";
                    c.Detail.Add("Sync AMC Core: N2 gate bypassé");
                }
            }
            
            // Pour les setups d'orderflow purs (DELTA_FLIP, CUM_DELTA_DIV) en mode ScalpingPro, assouplir la porte N2
            bool isOrderflowSetup = c.Name == "DELTA_FLIP" || c.Name == "CUM_DELTA_DIV" || c.Name == "FINISHED_AUCTION";
            if (IsScalpingPro && isOrderflowSetup && c.N2 >= 1)
            {
                g2 = true; // Accepter N2 >= 1 pour les setups orderflow purs en ScalpingPro
                c.GateBypassed = "ScalpingPro Orderflow";
                c.Detail.Add("ScalpingPro Orderflow: N2 gate assoupli (>=1)");
            }
            
            // FILTRE ANTI-CONTRE-TENDANCE ROBUSTE (respectant HtfSoftMode via htfIsGate) :
            // Si htfIsGate est false (HtfSoftMode actif), un désalignement HTF ne provoque pas un rejet dur (null),
            // mais applique uniquement la pénalité de score (géré en amont).
            if (IsScalpingPro && (c.Name == "FINISHED_AUCTION" || c.Name == "DELTA_FLIP") && !c.HtfAligned && htfIsGate)
            {
                c.Detail.Add("REJET SCALPING PRO: " + c.Name + " rejeté car non aligné avec la tendance HTF (mode strict)");
                return null;
            }

            // EXIGENCE MICROSTRUCTURE STRICTE & VOLATILITÉ : Pour Finished Auction, exiger un score N3 minimum de 6.0 
            // et rejeter si la volatilité (ATR) est trop élevée (éviter les faux retournements en tendance forte).
            if (IsScalpingPro && c.Name == "FINISHED_AUCTION")
            {
                if (c.N3 < 6.0)
                {
                    c.Detail.Add("REJET SCALPING PRO: Finished Auction rejeté car microstructure N3 insuffisante (< 6.0)");
                    return null;
                }
                
                // Si l'ATR de la barre dépasse 3.5 fois la taille de tick moyenne, le marché est en mode trend/breakout, 
                // le finished auction (mean reversion) est donc proscrit pour éviter de "prendre un couteau qui tombe".
                double avgTickSize = tickSize > 0 ? tickSize : 0.25;
                if (SniperAtr() > avgTickSize * 140)
                {
                    c.Detail.Add("REJET SCALPING PRO: Finished Auction rejeté car volatilité (ATR) trop excessive pour du mean-reversion");
                    return null;
                }
            }

            bool g3 = c.N3 >= GateN3MinScore;
            bool g4 = c.N4 >= GateN4MinScore;
            // Quand MinRiskReward == TargetR1 (cas du preset Scanner : 1.2 / 1.2), un
            // arrondi vers le bas d'un demi-tick suffit a faire echouer le gate alors
            // que le setup est conforme. Tolerance = 1 tick ramene en unites de R.
            double rrTolerance = (risk > 0 && tickSize > 0) ? (tickSize / risk) : 0.0;
            bool gRR = c.Rr >= MinRiskReward - rrTolerance;
            bool gRegime = (!SniperRthOnly || IsRthBar(snTime));
            bool gHtf = !htfIsGate || c.HtfAligned;
            bool gNews = !NewsHardBlock || !IsSniperNewsBlackout();

            if (!g1) c.GateFailed = "N1_CONTEXTE";
            else if (!g2) c.GateFailed = "N2_LOCALISATION";
            else if (!g3) c.GateFailed = "N3_MICROSTRUCTURE";
            else if (!g4) c.GateFailed = "N4_TRIGGER";
            else if (!gRR) c.GateFailed = "RR";
            else if (!gRegime) c.GateFailed = "REGIME_RTH";
            else if (!gHtf) c.GateFailed = "HTF";
            else if (!gNews) c.GateFailed = "NEWS_BLACKOUT";

            c.Gated = c.GateFailed.Length > 0;

            // grade). Score = 0 ne sert plus qu'a la decision d'emission.
            c.ScoreRaw = Clamp(c.N1 + c.N2 + c.N3 + c.N4 + c.Penalty, 0, 100);
            c.Score = c.Gated ? 0 : c.ScoreRaw;

            // footprint obligatoire, score pondere, niveau Moyen/Fort/Tres Fort).
            // No-op pour tous les autres presets.
            ApplyScalpingProPipeline(c, isBuy);

            c.Detail.Add(string.Format(CultureInfo.InvariantCulture,
                // Le libelle le precise pour que le journal reste interpretable.
                "N1={0:0.0}/30 N2={1:0.0}/30 N3={2:0.0}/25 N4={3:0.0}/15 pen={4:0.0} RR={5:0.00} "
                + (IsScalpingPro ? "score pondere={6:0.0}/100" : "raw={6:0.0}") + " htf={7}",
                c.N1, c.N2, c.N3, c.N4, c.Penalty, c.Rr, c.ScoreRaw, c.HtfAligned ? "ok" : "ko"));

            lastBarCandidates.Add(c);
            if (EnableShadowJournal) JournalCandidate(c, c.Rr);
            if (!c.Gated)
            {
                if (IsScalpingPro)
                {
                    ConsolidateScalpingProCandidatesPerBar(c);
                }
                else
                {
                    // en O(n^2) sur les candidats en attente ; si QuotaAvailable() refuse
                    // en boucle la liste pouvait croitre sans borne. On evince le plus
                    // ancien et on trace le depassement au lieu de degrader le CPU.
                    if (pendingCandidates.Count >= MaxPendingCandidates)
                    {
                        pendingCandidates.RemoveAt(0);
                        pendingOverflowCount++;
                        if (EnableDebugMode && pendingOverflowCount <= 20)
                            SafePrint("VP_Sniper: buffer de selection sature ("
                                + MaxPendingCandidates + " candidats), le plus ancien est evince.");
                    }
                    pendingCandidates.Add(c);
                }
            }
            return c;
        }

        /// atteignable par famille de setup. Certains sous-scores sont mutuellement
        /// exclusifs selon le setup (reintegration VA vs NPOC en extension, stacked
        /// imbalance de tendance vs reversal, plein score CVD a Z>=2x le seuil) : sans
        /// renormalisation, le plafond effectif est ~20 points sous le maximum theorique
        /// et aucun seuil d'emission coherent ne peut etre fixe.</summary>
        private void NormalizeLevels(Candidate c, bool meanReversion)
        {
            if (!NormalizeScoresPerSetup) return;

            double n2Max = meanReversion ? 26.0 : 28.0;
            double n3Max = meanReversion ? 17.0 : 22.0;
            double n4Max = meanReversion ? 10.0 : 13.0;

            c.N2 = Math.Min(30.0, c.N2 * (30.0 / n2Max));
            c.N3 = Math.Min(25.0, c.N3 * (25.0 / n3Max));
            c.N4 = Math.Min(15.0, c.N4 * (15.0 / n4Max));

            c.Detail.Add(string.Format(CultureInfo.InvariantCulture,
                "Normalisation {0} (N2/{1:0} N3/{2:0} N4/{3:0})",
                meanReversion ? "mean-reversion" : "tendance", n2Max, n3Max, n4Max));
        }


        /// <summary>Risque du candidat. On delegue d'abord a ComputeRiskLevels() de
        /// l'AMC Pro (stop = max(ATR, structure) + buffer, cible structurelle via
        /// FindStructuralTarget, cout d'execution, taille de position), puis on
        /// resserre le stop sur le niveau de reference du setup.</summary>
        private void ComputeSniperRisk(Candidate c, double refLevel)
        {
            double a = SniperAtr();
            if (a <= 0) a = 8 * tickSize;
            double buffer = StopBufferTicks * tickSize;

            if (ComputeRiskLevels(c.IsBuy, c.Entry, true) && lastStopPrice > 0 && lastTarget1 > 0)
            {
                c.Stop = lastStopPrice;
                c.Target1 = lastTarget1;
                c.Target2 = lastTarget2;
            }
            else
            {
                c.Stop = c.IsBuy ? c.Entry - StopAtrMultiple * a : c.Entry + StopAtrMultiple * a;
                c.Target1 = c.IsBuy ? c.Entry + MinRiskReward * StopAtrMultiple * a : c.Entry - MinRiskReward * StopAtrMultiple * a;
                c.Target2 = c.IsBuy ? c.Entry + 2 * (c.Target1 - c.Entry) : c.Entry - 2 * (c.Entry - c.Target1);
            }

            // Le stop doit toujours proteger le niveau structurel du setup.
            if (c.IsBuy)
            {
                double structural = (refLevel > 0 && refLevel < c.Entry) ? refLevel - buffer : snLow - buffer;
                c.Stop = Math.Min(c.Stop, structural);
            }
            else
            {
                double structural = (refLevel > 0 && refLevel > c.Entry) ? refLevel + buffer : snHigh + buffer;
                c.Stop = Math.Max(c.Stop, structural);
            }

            // Sécurité anti-bruit : le stop ne doit jamais être inférieur à MinStopTicks
            double minStopDist = Math.Max(MinStopTicks > 0 ? MinStopTicks : 8, 8) * tickSize;
            if (Math.Abs(c.Entry - c.Stop) < minStopDist)
            {
                c.Stop = c.IsBuy ? c.Entry - minStopDist : c.Entry + minStopDist;
            }

            // Cap en pips : le stop ne doit JAMAIS depasser MaxStopPips de l'entree,
            // meme apres l'ajustement structurel.
            if (MaxStopPips > 0 && PipSize > 0)
            {
                double maxRiskPips = MaxStopPips * PipSize;
                double currentRisk = Math.Abs(c.Entry - c.Stop);
                if (currentRisk > maxRiskPips)
                {
                    double cappedRisk = Math.Round(maxRiskPips / tickSize) * tickSize;
                    c.Stop = c.IsBuy ? c.Entry - cappedRisk : c.Entry + cappedRisk;
                    if (EnableDebugMode)
                        Print(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "SNIPER RISK CAP PIPS : stop de {0:F1} pips ramene a {1} pips max (entry={2:F2} stop={3:F2}).",
                            currentRisk / PipSize, MaxStopPips, c.Entry, c.Stop));
                }
            }

            // Recalage de la cible si le nouveau risque rend le R:R insuffisant :
            // on cherche le premier niveau structurel au-dela de MinRiskReward x risque.
            double risk = Math.Abs(c.Entry - c.Stop);
            if (risk <= 0) risk = a * StopAtrMultiple;
            double minDist = risk * MinRiskReward;
            double reward = Math.Abs(c.Target1 - c.Entry);

            if (reward < minDist)
            {
                double best = c.IsBuy ? double.MaxValue : double.MinValue;
                Action<double> consider = lvl =>
                {
                    if (lvl <= 0) return;
                    if (c.IsBuy && lvl > c.Entry + minDist && lvl < best) best = lvl;
                    if (!c.IsBuy && lvl < c.Entry - minDist && lvl > best) best = lvl;
                };
                if (sniperCompositeProfile.Valid)
                {
                    consider(sniperCompositeProfile.Poc);
                    consider(sniperCompositeProfile.Vah);
                    consider(sniperCompositeProfile.Val);
                }
                consider(pocPrice); consider(vahPrice); consider(valPrice);
                for (int i = 0; i < sessionHistory.Count; i++)
                {
                    consider(sessionHistory[i].Poc);
                    consider(sessionHistory[i].Vah);
                    consider(sessionHistory[i].Val);
                    consider(sessionHistory[i].High);
                    consider(sessionHistory[i].Low);
                }

                c.Target1 = (best == double.MaxValue || best == double.MinValue)
                    ? (c.IsBuy ? c.Entry + minDist : c.Entry - minDist)
                    : best;

                double cost = ExecutionCostTicks * tickSize;
                c.Target1 = c.IsBuy ? c.Target1 - cost : c.Target1 + cost;
                c.Target2 = c.IsBuy ? c.Entry + 2 * (c.Target1 - c.Entry) : c.Entry - 2 * (c.Entry - c.Target1);
            }
        }

        #endregion

        #region SNIPER - Section 8 : les 8 setups

        private void BuildCandidates()
        {
            TryNpocAbsorptionReversal();
            TryFailedAuctionCompositeVa();
            TryStackedImbalanceRetest();
            TryLvnRejectionExpress();
            TryOpenDriveFailure();
            TryDeltaFlipEntry();
            TryCumDeltaDivEntry();
            TryFinishedAuctionEntry();
        }

        /// <summary>TOP 1 — NPOC Absorption Reversal. E ~ +1.73R</summary>
        private void TryNpocAbsorptionReversal()
        {
            for (int dir = 0; dir < 2; dir++)
            {
                bool isBuy = dir == 0;
                double probe = isBuy ? snLow : snHigh;

                double npoc, fresh;
                if (!NearestNpoc(probe, out npoc, out fresh)) continue;

                // Le NPOC doit etre en EXTENSION (hors de la value area du jour).
                if (valPrice > 0 && vahPrice > 0)
                {
                    bool outside = isBuy ? npoc < valPrice : npoc > vahPrice;
                    if (!outside) continue;
                }

                double zSum, cluster;
                if (!AbsorptionCluster(isBuy, out zSum, out cluster)) continue;

                Assemble("NPOC_ABSORPTION_REVERSAL", isBuy, snClose, npoc);
            }
        }

        /// <summary>TOP 2 — Failed Auction at Composite VA Edge. E ~ +1.10R</summary>
        private void TryFailedAuctionCompositeVa()
        {
            if (!sniperCompositeProfile.Valid || evalBarIndex < 3) return;
            double a = SniperAtr();
            if (a <= 0) return;

            if (snHigh2 > sniperCompositeProfile.Vah
                && snClose1 < sniperCompositeProfile.Vah
                && snClose < sniperCompositeProfile.Vah
                && (snHigh2 - sniperCompositeProfile.Vah) <= 0.4 * a
                && FinishedAuctionAtExtreme(false))
            {
                Assemble("FAILED_AUCTION_VA", false, snClose, sniperCompositeProfile.Vah);
            }

            if (snLow2 < sniperCompositeProfile.Val
                && snClose1 > sniperCompositeProfile.Val
                && snClose > sniperCompositeProfile.Val
                && (sniperCompositeProfile.Val - snLow2) <= 0.4 * a
                && FinishedAuctionAtExtreme(true))
            {
                Assemble("FAILED_AUCTION_VA", true, snClose, sniperCompositeProfile.Val);
            }
        }

        /// <summary>TOP 3 — Stacked Imbalance Retest in Trend. E ~ +1.22R.
        /// Consomme les zones memorisees par RegisterImbalanceZone() de l'AMC Pro.</summary>
        private void TryStackedImbalanceRetest()
        {
            if (sniperDayType != SniperDayType.Trend) return;

            for (int i = 0; i < imbalanceZones.Count; i++)
            {
                ImbalanceZone z = imbalanceZones[i];
                if (z.Retested || z.RetestCount >= Math.Max(1, MaxImbalanceRetests)) continue;
                if (z.Levels < ImbalanceMinStack) continue;

                double freshness = 1.0 - (evalBarIndex - z.BarIndex) / 200.0;
                if (freshness < 0.5) continue;

                bool touched = snLow <= z.Top && snHigh >= z.Bottom;
                if (!touched) continue;

                double contraction = z.ReferenceBarVolume > 0
                    ? 1.0 - ((double)snVolume / z.ReferenceBarVolume)
                    : 0.0;
                if (contraction < ImbalanceRetestVolumeContraction) continue;

                bool resume = z.IsBull ? snClose > snOpen : snClose < snOpen;
                if (!resume) continue;

                double refLevel = z.IsBull ? z.Bottom : z.Top;
                Candidate cand = Assemble("STACKED_IMB_RETEST", z.IsBull, snClose, refLevel);

                // candidat a passe les gates. Sinon on compte la tentative et on
                // autorise un retest ulterieur de meilleure qualite.
                z.RetestCount++;
                if (!cand.Gated) z.Retested = true;
            }
        }

        /// <summary>TOP 4 — LVN Rejection Express. E ~ +0.73R.
        /// Double condition : veto binaire IsLowVolumeNode (AMC Pro) + qualite du creux.</summary>
        private void TryLvnRejectionExpress()
        {
            if (!sniperCompositeProfile.Valid || evalBarIndex < 2) return;

            double qHigh = LvnQuality(snHigh);
            if (qHigh >= 0.5 && IsLowVolumeNode(snHigh, NodeToleranceTicks) && snClose < snOpen && snHigh > snHigh1)
                Assemble("LVN_REJECTION", false, snClose, snHigh);

            double qLow = LvnQuality(snLow);
            if (qLow >= 0.5 && IsLowVolumeNode(snLow, NodeToleranceTicks) && snClose > snOpen && snLow < snLow1)
                Assemble("LVN_REJECTION", true, snClose, snLow);
        }

        /// <summary>TOP 5 — Open Drive Failure. E ~ +0.72R</summary>
        private void TryOpenDriveFailure()
        {
            if (!sniperPrevProfileValid || sniperSessionOpen <= 0) return;
            if (isIbComplete) return;              // fenetre = Initial Balance uniquement

            double a = SniperAtr();
            if (a <= 0) return;

            // 1. Échec d'Open Drive Haussier (Tentative d'extension au-dessus de PrevVAH rejetée) -> SHORT
            // Le marché a poussé au-dessus de PrevVAH (sessionHigh > PrevVAH), mais les acheteurs échouent
            // et le prix réintègre la Value Area à la baisse (snClose < PrevVAH && snClose < snOpen).
            if (sessionHigh > sniperPrevVah + 0.2 * a && snClose < sniperPrevVah && snClose >= sniperPrevVal && snClose < snOpen)
            {
                Assemble("OPEN_DRIVE_FAILURE", false, snClose, sniperPrevVah);
            }

            // 2. Échec d'Open Drive Baissier (Tentative d'extension sous PrevVAL rejetée) -> LONG
            // Le marché a poussé sous PrevVAL (sessionLow < PrevVAL), mais les vendeurs échouent
            // et le prix réintègre la Value Area à la hausse (snClose > PrevVAL && snClose <= sniperPrevVah && snClose > snOpen).
            if (sessionLow < sniperPrevVal - 0.2 * a && snClose > sniperPrevVal && snClose <= sniperPrevVah && snClose > snOpen)
            {
                Assemble("OPEN_DRIVE_FAILURE", true, snClose, sniperPrevVal);
            }
        }

        /// <summary>TOP 6 — Delta Flip Entry</summary>
        private void TryDeltaFlipEntry()
        {
            // Mode playback : ignorer bidAskDataMissing pour permettre les signaux
            if (State == State.Historical || !bidAskDataMissing)
            {
                if (isDeltaFlipBullish)
                    Assemble("DELTA_FLIP", true, snClose, snLow);
                if (isDeltaFlipBearish)
                    Assemble("DELTA_FLIP", false, snClose, snHigh);
            }
        }

        /// <summary>TOP 7 — Cumulative Delta Divergence Entry</summary>
        private void TryCumDeltaDivEntry()
        {
            // Mode playback : ignorer bidAskDataMissing pour permettre les signaux
            if (State == State.Historical || !bidAskDataMissing)
            {
                if (isCumDeltaDivBullish)
                    Assemble("CUM_DELTA_DIV", true, snClose, snLow);
                if (isCumDeltaDivBearish)
                    Assemble("CUM_DELTA_DIV", false, snClose, snHigh);
            }
        }

        /// <summary>TOP 8 — Finished Auction Entry</summary>
        private void TryFinishedAuctionEntry()
        {
            if (isFinishedAuctionBuy)
                Assemble("FINISHED_AUCTION", true, snClose, snLow);
            if (isFinishedAuctionSell)
                Assemble("FINISHED_AUCTION", false, snClose, snHigh);
        }

        #endregion

        #region SNIPER - Section 9 : buffer de selection best-of-window

        // Buffer reutilise pour la passe de selection (pas d'allocation par barre).
        private readonly List<Candidate> matureCandidates = new List<Candidate>(16);

        /// (1) on collecte les candidats murs SANS rien retirer (l'ancienne suppression
        ///     en cours d'iteration faussait la comparaison "best-of-window") ;
        /// (2) on departage a score egal par un critere deterministe (barre la plus
        ///     ancienne, puis R/R decroissant) pour ne jamais emettre deux alertes
        ///     pour la meme opportunite ;
        /// (3) on retire, et on emet le gagnant.</summary>
        private void ProcessSelectionBuffer()
        {
            if (ExecutionMode == SniperExecutionMode.Research) { pendingCandidates.Clear(); return; }

            matureCandidates.Clear();
            for (int i = 0; i < pendingCandidates.Count; i++)
            {
                Candidate c = pendingCandidates[i];
                if (evalBarIndex - c.BarIdx >= SelectionBufferBars) matureCandidates.Add(c);
            }
            if (matureCandidates.Count == 0) return;

            for (int i = 0; i < matureCandidates.Count; i++)
            {
                Candidate c = matureCandidates[i];

                // Passe 2 : le meilleur de la fenetre, compare a TOUS les candidats
                // encore en attente (murs ou non) de meme sens.
                bool isBest = true;
                for (int j = 0; j < pendingCandidates.Count; j++)
                {
                    Candidate o = pendingCandidates[j];
                    if (ReferenceEquals(o, c)) continue;
                    if (o.IsBuy != c.IsBuy) continue;
                    if (Math.Abs(o.BarIdx - c.BarIdx) > SelectionBufferBars) continue;
                    if (BeatsForSelection(o, c)) { isBest = false; break; }
                }

                pendingCandidates.Remove(c);

                if (!isBest) continue;
                if (c.Score < MinScoreToAlert) continue;

                double drift = Math.Abs(snClose - c.Entry);
                double atr = SniperAtr();
                if (MaxEntryDriftAtr > 0 && atr > 0 && drift > MaxEntryDriftAtr * atr)
                {
                    sniperLastStatus = "ANNULE derive " + c.Name + " ("
                        + (tickSize > 0 ? (drift / tickSize).ToString("0", CultureInfo.InvariantCulture) : "?") + " ticks)";
                    continue;
                }

                if (!QuotaAvailable()) continue;

                c.EntryAtEmission = snClose;
                EmitAlert(c);
            }

            matureCandidates.Clear();
        }

        /// <summary>Ordre total deterministe : score, puis anteriorite, puis R/R.
        /// Elimine les doublons d'alerte a score strictement egal (C3).</summary>
        private static bool BeatsForSelection(Candidate a, Candidate b)
        {
            if (a.Score != b.Score) return a.Score > b.Score;
            if (a.BarIdx != b.BarIdx) return a.BarIdx < b.BarIdx;
            if (a.Rr != b.Rr) return a.Rr > b.Rr;
            return string.CompareOrdinal(a.Name ?? "", b.Name ?? "") < 0;
        }

        private bool QuotaAvailable()
        {
            if (MaxSniperAlertsPerSession > 0 && sniperAlertsThisSession >= MaxSniperAlertsPerSession) return false;

            DateTime cutoff = snTime.AddDays(-7);
            while (alertsThisWeek.Count > 0 && alertsThisWeek.Peek() < cutoff) alertsThisWeek.Dequeue();
            if (MaxAlertsPerWeek > 0 && alertsThisWeek.Count >= MaxAlertsPerWeek) return false;

            return true;
        }

        #endregion

        #region SNIPER - Section 10 : emission

        private void EmitAlert(Candidate c)
        {
            if (c.EntryAtEmission <= 0) c.EntryAtEmission = snClose;
            double driftTicks = tickSize > 0 ? (c.EntryAtEmission - c.Entry) / tickSize : 0;

            string tradeTag = "SLines_" + c.BarIdx + "_" + openTrades.Count + "_" + (c.Name ?? "").Replace(" ", "_");

            TrackedTrade trade = new TrackedTrade
            {
                Tag = tradeTag,
                Name = c.Name,
                IsBuy = c.IsBuy,
                Entry = c.Entry,
                Stop = c.Stop,
                T1 = c.Target1,
                T2 = c.Target2,
                Score = c.Score,
                BarIdx = c.BarIdx,
                Time = c.Time,
                Grade = c.Grade
            };

            sniperAlertsThisSession++;
            alertsThisWeek.Enqueue(c.Time);
            openTrades.Add(trade);

            // Exportation des signaux pour stratégies / bridge
            SniperPublishExports(c);

            // Les alertes Telegram et les tracés visuels sont réservés au temps réel
            if (State == State.Realtime)
            {
                DrawTradeLevels(trade);

                // Construction centralisée du message Telegram (Network.cs)
                string sniperMsg = BuildSniperTelegramAlert(c);
                SendTelegramMessage(sniperMsg, null, c.Score >= ScoreThresholdChat2 ? 2 : 1);

                try
                {
                    // Ancrage vertical sur le prix de référence du signal
                    string tag = "sniper" + c.BarIdx + c.Name;
                    int barsAgo = Math.Min(CurrentBars[0], Math.Max(0, CurrentBar - c.BarIdx));
                    double anchor = c.Entry > 0 ? c.Entry : (c.IsBuy ? snLow : snHigh);

                    if (c.IsBuy)
                        Draw.ArrowUp(this, tag, true, barsAgo, anchor - 2 * tickSize, Brushes.Lime);
                    else
                        Draw.ArrowDown(this, tag, true, barsAgo, anchor + 2 * tickSize, Brushes.OrangeRed);

                    Draw.Text(this, tag + "Txt", c.Grade + " " + c.Score.ToString("0", CultureInfo.InvariantCulture), barsAgo,
                        c.IsBuy ? anchor - 6 * tickSize : anchor + 6 * tickSize,
                        c.IsBuy ? Brushes.Lime : Brushes.OrangeRed);
                }
                catch { /* le rendu ne doit jamais casser l'emission */ }
            }

            sniperLastStatus = c.Grade + " " + c.Name + " @" + SniperFormatPrice(c.Entry);
        }

        private string SniperFormatPrice(double p)
        {
            if (p <= 0) return "-";
            return Instrument != null && Instrument.MasterInstrument != null
                ? Instrument.MasterInstrument.FormatPrice(p)
                : p.ToString("0.00", CultureInfo.InvariantCulture);
        }

        /// <summary>Bloc dashboard du moteur Sniper, concatene par UpdateDashboard().</summary>
        // du dashboard, calculee sans allocation.
        private long SniperDashboardFingerprint()
        {
            unchecked
            {
                long h = (long)sniperAlertsThisSession * 397 + alertsThisWeek.Count;
                // chaud n'aurait pas rafraichi le panneau.
                h = (h * 31) ^ (long)TradingPreset;
                h = (h * 31) ^ (long)ExecutionMode;
                h = (h * 31) ^ MinScoreToAlert;
                h = (h * 31) ^ (long)sniperDayType;
                h = (h * 31) ^ (sniperLastStatus == null ? 0 : sniperLastStatus.GetHashCode());
                h = (h * 31) ^ BitConverter.DoubleToInt64Bits(ibExtensionRatio);
                h = (h * 31) ^ BitConverter.DoubleToInt64Bits(sniperVaOverlap);
                h = (h * 31) ^ BitConverter.DoubleToInt64Bits(AtrPercentileRank());
                h = (h * 31) ^ BitConverter.DoubleToInt64Bits(ZDeltaCurrent());
                h = (h * 31) ^ lastBarCandidates.Count;
                for (int i = 0; i < lastBarCandidates.Count && i < 4; i++)
                {
                    Candidate c = lastBarCandidates[i];
                    h = (h * 31) ^ (c.Name == null ? 0 : c.Name.GetHashCode());
                    h = (h * 31) ^ (c.IsBuy ? 1 : 2);
                    h = (h * 31) ^ (c.Gated ? 3 : 5);
                    h = (h * 31) ^ (c.GateFailed == null ? 0 : c.GateFailed.GetHashCode());
                    h = (h * 31) ^ (long)Math.Round(c.Score);
                    h = (h * 31) ^ (long)Math.Round(c.ScoreRaw);
                    h = (h * 31) ^ (c.Grade == null ? 0 : c.Grade.GetHashCode());
                    h = (h * 31) ^ BitConverter.DoubleToInt64Bits(c.HtfModifier);
                    h = (h * 31) ^ BitConverter.DoubleToInt64Bits(c.M5Modifier);
                    if (c.EvidenceList != null) h = (h * 31) ^ c.EvidenceList.Count;
                }
                return h;
            }
        }

        private string BuildSniperDashboardBlock(int maxLen = 44)
        {
            StringBuilder sb = new StringBuilder(512);
            AppendWrappedLine(sb, "  SNIPER : ", string.Format("[{0} / {1}] seuil {2}/100", TradingPreset, ExecutionMode, MinScoreToAlert), maxLen);
            AppendWrappedLine(sb, "  TypeJour: ", string.Format(CultureInfo.InvariantCulture, "{0} | IBext {1:0.00}", sniperDayType, ibExtensionRatio), maxLen);
            AppendWrappedLine(sb, "  Contexte: ", string.Format(CultureInfo.InvariantCulture, "ATRpct {0:0} | Zdelta {1:0.00} | VAov {2:0.00}",
                AtrPercentileRank(), ZDeltaCurrent(), sniperVaOverlap), maxLen);
            AppendWrappedLine(sb, "  Alertes: ", string.Format("{0}/{1} sess, {2}/{3} sem",
                sniperAlertsThisSession, MaxSniperAlertsPerSession, alertsThisWeek.Count, MaxAlertsPerWeek), maxLen);

            if (lastBarCandidates.Count == 0)
            {
                sb.AppendLine("  Candid.: Aucun candidat cette barre");
            }
            else
            {
                for (int i = 0; i < lastBarCandidates.Count && i < 4; i++)
                {
                    Candidate c = lastBarCandidates[i];
                    string candidateLine = (c.IsBuy ? "🟢 BUY " : "🔴 SELL ") + c.Name;

                    if (c.Gated)
                        candidateLine += " 🔒 " + c.GateFailed + " (" + c.ScoreRaw.ToString("0", CultureInfo.InvariantCulture) + ")";
                    else
                        candidateLine += " " + c.Score.ToString("0", CultureInfo.InvariantCulture) + "/100 " + c.Grade;

                    if (IsScalpingPro && (c.HtfModifier != 0 || c.M5Modifier != 0))
                    {
                        candidateLine += string.Format(CultureInfo.InvariantCulture,
                            " (HTF M15:{0:+0.0;-0.0;0} M5:{1:+0.0;-0.0;0})", c.HtfModifier, c.M5Modifier);
                    }

                    AppendWrappedLine(sb, "  Cand.  : ", CleanTextForDashboard(candidateLine), maxLen);

                    if (IsScalpingPro && c.EvidenceList.Count > 0)
                    {
                        AppendWrappedLine(sb, "    Preuv: ", CleanTextForDashboard(string.Join(", ", c.EvidenceList)), maxLen);
                    }
                }
            }
            if (!string.IsNullOrEmpty(sniperLastStatus) && sniperLastStatus != "ok" && sniperLastStatus != "init" && sniperLastStatus != "pret")
            {
                AppendWrappedLine(sb, "  Statut : ", CleanTextForDashboard(sniperLastStatus), maxLen);
            }
            return sb.ToString();
        }

        #endregion

        #region SNIPER - Section 11 : trades et journal shadow

        private void DrawTradeLevels(TrackedTrade t)
        {
            if (t == null || string.IsNullOrEmpty(t.Tag)) return;
            try
            {
                if (t.Entry > 0)
                    Draw.HorizontalLine(this, t.Tag + "_Entry", t.Entry, Brushes.Gold, DashStyleHelper.Dash, 2);

                if (t.T1 > 0)
                    Draw.HorizontalLine(this, t.Tag + "_TP1", t.T1, Brushes.Lime, DashStyleHelper.Dash, 2);

                if (t.T2 > 0)
                    Draw.HorizontalLine(this, t.Tag + "_TP2", t.T2, Brushes.LimeGreen, DashStyleHelper.Dash, 1);

                if (t.Stop > 0)
                    Draw.HorizontalLine(this, t.Tag + "_SL", t.Stop, Brushes.OrangeRed, DashStyleHelper.Dash, 2);
            }
            catch (Exception ex)
            {
                RegisterRuntimeError("DrawTradeLevels", ex);
            }
        }

        private void RemoveTradeLevels(TrackedTrade t)
        {
            if (t == null || string.IsNullOrEmpty(t.Tag)) return;
            try
            {
                RemoveDrawObject(t.Tag + "_Entry");
                RemoveDrawObject(t.Tag + "_TP1");
                RemoveDrawObject(t.Tag + "_TP2");
                RemoveDrawObject(t.Tag + "_SL");
            }
            catch (Exception ex)
            {
                RegisterRuntimeError("RemoveTradeLevels", ex);
            }
        }

        private void RemoveAllTradeLevels()
        {
            for (int i = 0; i < openTrades.Count; i++)
            {
                RemoveTradeLevels(openTrades[i]);
            }
        }

        private void UpdateOpenTrades()
        {
            for (int i = openTrades.Count - 1; i >= 0; i--)
            {
                TrackedTrade t = openTrades[i];
                if (t.Closed) { RemoveTradeLevels(t); openTrades.RemoveAt(i); continue; }

                // Ne pas évaluer la sortie sur la bougie même de l'entrée (les mèches ont pu se former avant le signal)
                if (evalBarIndex <= t.BarIdx) continue;

                double risk = Math.Abs(t.Entry - t.Stop);
                if (risk <= 0) { RemoveTradeLevels(t); openTrades.RemoveAt(i); continue; }

                bool stopHit = t.IsBuy ? snLow <= t.Stop : snHigh >= t.Stop;
                bool t1Hit = t.IsBuy ? snHigh >= t.T1 : snLow <= t.T1;
                bool t2Hit = t.IsBuy ? snHigh >= t.T2 : snLow <= t.T2;

                if (stopHit && !t1Hit)
                {
                    double exitPrice = t.IsBuy ? (snOpen < t.Stop ? snOpen : t.Stop) : (snOpen > t.Stop ? snOpen : t.Stop);
                    double lossR = -Math.Max(1.0, Math.Abs(t.Entry - exitPrice) / risk);
                    JournalOutcome(t, "STOP", lossR);
                    RemoveTradeLevels(t);
                    openTrades.RemoveAt(i);
                }
                else if (t1Hit && !stopHit)
                {
                    if (t2Hit)
                    {
                        JournalOutcome(t, "TARGET2", Math.Abs(t.T2 - t.Entry) / risk);
                    }
                    else
                    {
                        JournalOutcome(t, "TARGET1", Math.Abs(t.T1 - t.Entry) / risk);
                    }
                    RemoveTradeLevels(t);
                    openTrades.RemoveAt(i);
                }
                else if (t1Hit && stopHit)
                {
                    // Si la barre touche le Stop et le TP sur la même bougie, arbitrage selon l'open
                    double distToTp = Math.Abs(snOpen - t.T1);
                    double distToStop = Math.Abs(snOpen - t.Stop);
                    if (distToTp < distToStop)
                    {
                        JournalOutcome(t, "TARGET1", Math.Abs(t.T1 - t.Entry) / risk);
                    }
                    else
                    {
                        JournalOutcome(t, "STOP", -1.0);
                    }
                    RemoveTradeLevels(t);
                    openTrades.RemoveAt(i);
                }
                else if (evalBarIndex - t.BarIdx > Math.Max(24, JournalMaxBarsInTrade * 5))
                {
                    JournalOutcome(t, "TIMEOUT", (t.IsBuy ? snClose - t.Entry : t.Entry - snClose) / risk);
                    RemoveTradeLevels(t);
                    openTrades.RemoveAt(i);
                }
            }
        }

        private void CloseAllTrades(string reason, double exitPrice)
        {
            for (int i = openTrades.Count - 1; i >= 0; i--)
            {
                TrackedTrade t = openTrades[i];
                double risk = Math.Abs(t.Entry - t.Stop);
                double r = risk > 0 ? (t.IsBuy ? exitPrice - t.Entry : t.Entry - exitPrice) / risk : 0;
                JournalOutcome(t, reason, r);
                RemoveTradeLevels(t);
                openTrades.RemoveAt(i);
            }
        }

        // L'ancienne resolution etait refaite a chaque candidat journalise
        // (Path.Combine + Directory.Exists sur le thread de donnees).
        private string sniperJournalPathCached = null;
        private string sniperOutcomePathCached = null;

        private string SniperOutcomePath()
        {
            if (!string.IsNullOrEmpty(sniperOutcomePathCached)) return sniperOutcomePathCached;
            string path = ResolveSniperJournalPath();
            if (string.IsNullOrEmpty(path)) return null;
            sniperOutcomePathCached = path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                ? path.Substring(0, path.Length - 4) + "_outcomes.csv"
                : path + "_outcomes.csv";
            return sniperOutcomePathCached;
        }

        private string ResolveSniperJournalPath()
        {
            if (!string.IsNullOrEmpty(sniperJournalPathCached)) return sniperJournalPathCached;
            string basePath = journalPathResolved;
            if (string.IsNullOrEmpty(basePath)) basePath = ResolveJournalPath();
            if (string.IsNullOrEmpty(basePath))
            {
                string dir = Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "sniper");
                try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); }
                catch (Exception ex) { if (EnableDebugMode) Print("VP_JournalDir: impossible de creer " + dir + " : " + ex.Message); }
                string inst = Instrument != null ? Instrument.MasterInstrument.Name : "UNK";
                foreach (char bad in Path.GetInvalidFileNameChars()) inst = inst.Replace(bad, '_');
                sniperJournalPathCached = Path.Combine(dir, "sniper_journal_" + inst + ".csv");
                return sniperJournalPathCached;
            }
            sniperJournalPathCached = basePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                ? basePath.Substring(0, basePath.Length - 4) + "_sniper.csv"
                : basePath + "_sniper.csv";
            return sniperJournalPathCached;
        }

        /// <summary>Journalise TOUS les candidats, y compris ceux bloques par un gate :
        /// c'est la population non biaisee necessaire a l'analyse de monotonie
        /// (win rate et E[R] par decile de score).</summary>
        private void JournalCandidate(Candidate c, double rr)
        {
            try
            {
                string path = ResolveSniperJournalPath();
                // ni lock sur le thread de donnees. L'en-tete est ecrit par le thread
                // journal lors de la creation effective du fichier.
                {
                    sniperJournalHeaderWritten = true;

                    StringBuilder sb = new StringBuilder(768);
                    sb.Append(c.Time.ToString("yyyy-MM-dd HH:mm:ss")).Append(';');
                    sb.Append(Instrument != null ? Instrument.MasterInstrument.Name : "?").Append(';');
                    sb.Append(c.Name).Append(';');
                    sb.Append(c.IsBuy ? "LONG" : "SHORT").Append(';');
                    sb.Append(c.Score.ToString("0.0", CultureInfo.InvariantCulture)).Append(';');
                    sb.Append(c.ScoreRaw.ToString("0.0", CultureInfo.InvariantCulture)).Append(';');
                    sb.Append(c.Gated ? 1 : 0).Append(';');
                    sb.Append(c.Grade).Append(';');
                    sb.Append(c.GateFailed).Append(';');
                    sb.Append(c.N1.ToString("0.0", CultureInfo.InvariantCulture)).Append(';');
                    sb.Append(c.N2.ToString("0.0", CultureInfo.InvariantCulture)).Append(';');
                    sb.Append(c.N3.ToString("0.0", CultureInfo.InvariantCulture)).Append(';');
                    sb.Append(c.N4.ToString("0.0", CultureInfo.InvariantCulture)).Append(';');
                    sb.Append(c.Penalty.ToString("0.0", CultureInfo.InvariantCulture)).Append(';');
                    sb.Append(c.Entry.ToString(CultureInfo.InvariantCulture)).Append(';');
                    sb.Append(c.Stop.ToString(CultureInfo.InvariantCulture)).Append(';');
                    sb.Append(c.Target1.ToString(CultureInfo.InvariantCulture)).Append(';');
                    sb.Append(c.Target2.ToString(CultureInfo.InvariantCulture)).Append(';');
                    sb.Append(rr.ToString("0.00", CultureInfo.InvariantCulture)).Append(';');
                    sb.Append(sniperDayType).Append(';');
                    sb.Append(ibExtensionRatio.ToString("0.000", CultureInfo.InvariantCulture)).Append(';');
                    sb.Append(sniperVaOverlap.ToString("0.000", CultureInfo.InvariantCulture)).Append(';');
                    sb.Append(AtrPercentileRank().ToString("0.0", CultureInfo.InvariantCulture)).Append(';');
                    sb.Append(ZDeltaCurrent().ToString("0.00", CultureInfo.InvariantCulture)).Append(';');
                    sb.Append(htfBias).Append(';');
                    sb.Append(c.HtfAligned ? 1 : 0).Append(';');
                    sb.Append(c.Family).Append(';');
                    sb.Append(c.SetupType).Append(';');
                    sb.Append(c.HtfModifier.ToString("0.0", CultureInfo.InvariantCulture)).Append(';');
                    sb.Append(c.M5Modifier.ToString("0.0", CultureInfo.InvariantCulture)).Append(';');
                    sb.Append(string.Join(",", c.EvidenceList)).Append(';');
                    sb.Append(SniperClassName()).Append(';');
                    sb.Append(SniperV3DeltaThreshold()).Append(';');
                    sb.Append(SniperZMadDelta().ToString("0.00", CultureInfo.InvariantCulture)).Append(';');
                    sb.Append(SniperDeltaPercentile().ToString("0.0", CultureInfo.InvariantCulture)).Append(';');
                    sb.Append(string.Join(" | ", c.Detail).Replace(';', ','));
                    sb.Append('\n');

                    if (journalWriter != null)
                        journalWriter.Enqueue(path,
                            // bloquee est inexploitable pour calibrer les gates.
                            "time;instrument;setup;side;score;score_raw;gated;grade;gate_failed;N1;N2;N3;N4;penalty;entry;stop;t1;t2;rr;daytype;ib_ext;va_overlap;atr_pct;z_delta;htf;htf_aligned;family;setup_type;htf_mod;m5_mod;evidence;v3_class;v3_thr;v3_zmad;v3_dpct;detail\n",
                            sb.ToString());
                }
            }
            catch (Exception ex)
            {
                // casse toujours pas l'indicateur, mais l'echec est trace.
                SafePrint("VP_JournalCandidate: ecriture impossible (" + ex.GetType().Name + " - " + ex.Message + ").");
            }
        }

        private void JournalOutcome(TrackedTrade t, string outcome, double rMultiple)
        {
            try
            {
                // (il etait re-execute a chaque trade cloture), ecriture asynchrone.
                string path = SniperOutcomePath();
                if (string.IsNullOrEmpty(path)) return;

                string line = string.Format(CultureInfo.InvariantCulture,
                    "{0};{1};{2};{3};{4};{5:0.0};{6};{7:0.000}\n",
                    t.Time.ToString("yyyy-MM-dd HH:mm:ss"), snTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    t.Name, t.IsBuy ? "LONG" : "SHORT", t.Grade, t.Score, outcome, rMultiple);

                if (journalWriter != null)
                    journalWriter.Enqueue(path,
                        "entry_time;exit_time;setup;side;grade;score;outcome;r_multiple\n", line);
            }
            catch (Exception ex)
            {
                SafePrint("VP_JournalOutcome: ecriture impossible (" + ex.GetType().Name + " - " + ex.Message + ").");
            }
        }

        // Le prix de sortie n'est pas fiable ici (serie potentiellement liberee) :
        // les trades sont journalises en SESSION_END a 0R puis la liste est videe,
        // meme si la journalisation echoue (plus de fuite de compteur).
        private void ClearOpenSniperTrades()
        {
            try
            {
                for (int i = 0; i < openTrades.Count; i++)
                {
                    JournalOutcome(openTrades[i], "SESSION_END", 0.0);
                    RemoveTradeLevels(openTrades[i]);
                }
            }
            catch (Exception ex)
            {
                SafePrint("VP_FlushSniper: " + ex.Message);
            }
            finally
            {
                openTrades.Clear();
            }
        }

        #endregion

    }
}
