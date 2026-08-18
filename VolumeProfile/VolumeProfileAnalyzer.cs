#region Using declarations
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.VolumeProfilePro
{
    /// <summary>
    /// Moteur d'analyse spatiale, de localisation du prix, de détection des confluences
    /// multi-timeframes et de suivi du cycle de vie des zones historiques.
    /// </summary>
    public sealed class VolumeProfileAnalyzer
    {
        #region Paramètres

        public int LevelToleranceTicks { get; set; }
        public int NodeToleranceTicks { get; set; }
        public int ConfluenceToleranceTicks { get; set; }
        public int MinConfluenceLevels { get; set; }

        public VolumeProfileAnalyzer()
        {
            LevelToleranceTicks = 3;
            NodeToleranceTicks = 4;
            ConfluenceToleranceTicks = 4;
            MinConfluenceLevels = 2;
        }

        #endregion

        #region Analyse & Assemblage du Contexte

        /// <summary>
        /// Construit un VolumeProfileContext complet pour la barre et le prix actuels.
        /// </summary>
        public VolumeProfileContext Analyze(
            double currentPrice,
            double barHigh,
            double barLow,
            double barClose,
            double barDelta,
            double atr,
            double tickSize,
            DateTime barTimeUtc,
            ClosedVolumeProfile prevDay,
            ClosedVolumeProfile prevWeek,
            ClosedVolumeProfile prevMonth)
        {
            var ctx = new VolumeProfileContext
            {
                PrevDay = prevDay,
                PrevWeek = prevWeek,
                PrevMonth = prevMonth
            };

            if (tickSize <= 0 || !ctx.IsValid)
            {
                ctx.LocationSummary = "VP INACTIF / HISTORIQUE INSUFFISANT";
                return ctx;
            }

            // 1. Liste de toutes les références actives pour la distance et la confluence
            var refLevels = CollectReferenceLevels(prevDay, prevWeek, prevMonth);

            // 2. Recherche de la référence la plus proche
            FindClosestReference(currentPrice, refLevels, tickSize, atr, ctx);

            // 3. Localisation structurelle du prix (Above/Inside/Below Value, Near POC/VAH/VAL/Nodes)
            AnalyzeLocation(currentPrice, prevDay, prevWeek, prevMonth, tickSize, ctx);

            // 4. Détection des confluences multi-timeframes
            DetectConfluences(currentPrice, refLevels, tickSize, ctx);

            // 5. Suivi des nodes actifs
            FindActiveNode(currentPrice, prevWeek, prevMonth, tickSize, ctx);

            return ctx;
        }

        #endregion

        #region Collecte des Niveaux & Recherche de Proximité

        private sealed class RefLevel
        {
            public string Name;
            public double Price;
            public double ZoneLow;
            public double ZoneHigh;
            public string PeriodType;
            public bool IsZone;

            public double DistanceTicks(double price, double tickSize)
            {
                if (tickSize <= 0) return double.MaxValue;
                if (IsZone)
                {
                    if (price >= ZoneLow && price <= ZoneHigh) return 0;
                    if (price < ZoneLow) return Math.Abs(ZoneLow - price) / tickSize;
                    return Math.Abs(price - ZoneHigh) / tickSize;
                }
                return Math.Abs(price - Price) / tickSize;
            }
        }

        private List<RefLevel> CollectReferenceLevels(ClosedVolumeProfile day, ClosedVolumeProfile week, ClosedVolumeProfile month)
        {
            var list = new List<RefLevel>(16);

            if (day != null && day.Valid)
            {
                list.Add(new RefLevel { Name = "PrevDay POC", Price = day.Poc, PeriodType = "DAY" });
                list.Add(new RefLevel { Name = "PrevDay VAH", Price = day.Vah, PeriodType = "DAY" });
                list.Add(new RefLevel { Name = "PrevDay VAL", Price = day.Val, PeriodType = "DAY" });
            }

            if (week != null && week.Valid)
            {
                list.Add(new RefLevel { Name = "PrevWeek POC", Price = week.Poc, PeriodType = "WEEK" });
                list.Add(new RefLevel { Name = "PrevWeek VAH", Price = week.Vah, PeriodType = "WEEK" });
                list.Add(new RefLevel { Name = "PrevWeek VAL", Price = week.Val, PeriodType = "WEEK" });

                if (week.Nodes != null)
                {
                    for (int i = 0; i < week.Nodes.Count; i++)
                    {
                        var n = week.Nodes[i];
                        list.Add(new RefLevel
                        {
                            Name = string.Format("PrevWeek {0} #{1}", n.NodeType, i + 1),
                            Price = n.PeakPrice,
                            ZoneLow = n.ZoneLow,
                            ZoneHigh = n.ZoneHigh,
                            PeriodType = "WEEK",
                            IsZone = true
                        });
                    }
                }
            }

            if (month != null && month.Valid)
            {
                list.Add(new RefLevel { Name = "PrevMonth POC", Price = month.Poc, PeriodType = "MONTH" });
                list.Add(new RefLevel { Name = "PrevMonth VAH", Price = month.Vah, PeriodType = "MONTH" });
                list.Add(new RefLevel { Name = "PrevMonth VAL", Price = month.Val, PeriodType = "MONTH" });

                if (month.Nodes != null)
                {
                    for (int i = 0; i < month.Nodes.Count; i++)
                    {
                        var n = month.Nodes[i];
                        list.Add(new RefLevel
                        {
                            Name = string.Format("PrevMonth {0} #{1}", n.NodeType, i + 1),
                            Price = n.PeakPrice,
                            ZoneLow = n.ZoneLow,
                            ZoneHigh = n.ZoneHigh,
                            PeriodType = "MONTH",
                            IsZone = true
                        });
                    }
                }
            }

            return list;
        }

        private void FindClosestReference(double price, List<RefLevel> levels, double tickSize, double atr, VolumeProfileContext ctx)
        {
            double minTicks = double.MaxValue;
            RefLevel best = null;

            foreach (var lvl in levels)
            {
                double d = lvl.DistanceTicks(price, tickSize);
                if (d < minTicks)
                {
                    minTicks = d;
                    best = lvl;
                }
            }

            if (best != null)
            {
                ctx.DistanceToClosestReference = minTicks;
                ctx.ClosestReferenceName = best.Name;
                ctx.ClosestReferencePrice = best.Price;

                double atrTicks = atr > 0 ? atr / tickSize : 1.0;
                ctx.DistanceToClosestReferenceAtr = atrTicks > 0 ? minTicks / atrTicks : 0.0;
            }
        }

        #endregion

        #region Analyse de Localisation

        private void AnalyzeLocation(
            double price,
            ClosedVolumeProfile day,
            ClosedVolumeProfile week,
            ClosedVolumeProfile month,
            double tickSize,
            VolumeProfileContext ctx)
        {
            var loc = VolumeProfileLocationType.None;
            var sb = new StringBuilder(64);

            // 1. Analyse par rapport au jour précédent
            if (day != null && day.Valid)
            {
                if (price > day.Vah)
                {
                    loc |= VolumeProfileLocationType.AboveValue;
                    sb.Append("ABOVE PREV DAY VA");
                }
                else if (price < day.Val)
                {
                    loc |= VolumeProfileLocationType.BelowValue;
                    sb.Append("BELOW PREV DAY VA");
                }
                else
                {
                    loc |= VolumeProfileLocationType.InsideValue;
                    sb.Append("INSIDE PREV DAY VA");
                }

                if (Math.Abs(price - day.Poc) / tickSize <= LevelToleranceTicks)
                {
                    loc |= VolumeProfileLocationType.NearPoc;
                    sb.Append(" [NEAR DAY POC]");
                }
                else if (Math.Abs(price - day.Vah) / tickSize <= LevelToleranceTicks)
                {
                    loc |= VolumeProfileLocationType.NearVah;
                    sb.Append(" [NEAR DAY VAH]");
                }
                else if (Math.Abs(price - day.Val) / tickSize <= LevelToleranceTicks)
                {
                    loc |= VolumeProfileLocationType.NearVal;
                    sb.Append(" [NEAR DAY VAL]");
                }
            }

            // 2. Analyse par rapport à la semaine précédente
            if (week != null && week.Valid)
            {
                if (Math.Abs(price - week.Poc) / tickSize <= LevelToleranceTicks)
                {
                    loc |= VolumeProfileLocationType.NearPoc;
                    sb.Append(" [NEAR WEEK POC]");
                }
            }

            // 3. Analyse par rapport aux nodes de la semaine et du mois
            CheckNodesLocation(price, week, tickSize, ref loc, sb);
            CheckNodesLocation(price, month, tickSize, ref loc, sb);

            ctx.Location = loc;
            ctx.LocationSummary = sb.Length > 0 ? sb.ToString() : "NEUTRAL";
        }

        private void CheckNodesLocation(double price, ClosedVolumeProfile profile, double tickSize, ref VolumeProfileLocationType loc, StringBuilder sb)
        {
            if (profile == null || !profile.Valid || profile.Nodes == null) return;

            foreach (var n in profile.Nodes)
            {
                if (n.Contains(price))
                {
                    if (n.NodeType == VolumeProfileNodeType.HVN)
                    {
                        loc |= VolumeProfileLocationType.InsideHvn;
                        sb.Append(string.Format(" [IN {0} HVN]", profile.ProfileType));
                    }
                    else
                    {
                        loc |= VolumeProfileLocationType.InsideLvn;
                        sb.Append(string.Format(" [IN {0} LVN]", profile.ProfileType));
                    }
                }
                else
                {
                    double d = n.DistanceTicks(price, tickSize);
                    if (d <= NodeToleranceTicks)
                    {
                        if (n.NodeType == VolumeProfileNodeType.HVN)
                        {
                            loc |= VolumeProfileLocationType.NearHvn;
                            sb.Append(string.Format(" [NEAR {0} HVN]", profile.ProfileType));
                        }
                        else
                        {
                            loc |= VolumeProfileLocationType.NearLvn;
                            sb.Append(string.Format(" [NEAR {0} LVN]", profile.ProfileType));
                        }
                    }
                }
            }
        }

        #endregion

        #region Détection des Confluences Multi-Timeframes

        private void DetectConfluences(double currentPrice, List<RefLevel> levels, double tickSize, VolumeProfileContext ctx)
        {
            if (levels.Count < MinConfluenceLevels || tickSize <= 0) return;

            int bestCount = 0;
            double bestLow = 0, bestHigh = 0;
            var bestMembers = new List<RefLevel>();

            for (int i = 0; i < levels.Count; i++)
            {
                var pivot = levels[i];
                var cluster = new List<RefLevel> { pivot };
                double clusterLow = pivot.IsZone ? pivot.ZoneLow : pivot.Price;
                double clusterHigh = pivot.IsZone ? pivot.ZoneHigh : pivot.Price;
                var periodTypesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { pivot.PeriodType };

                for (int j = 0; j < levels.Count; j++)
                {
                    if (i == j) continue;
                    var other = levels[j];
                    double otherLow = other.IsZone ? other.ZoneLow : other.Price;
                    double otherHigh = other.IsZone ? other.ZoneHigh : other.Price;

                    // Distance entre les zones ou points
                    double distTicks;
                    if (otherHigh < clusterLow) distTicks = (clusterLow - otherHigh) / tickSize;
                    else if (otherLow > clusterHigh) distTicks = (otherLow - clusterHigh) / tickSize;
                    else distTicks = 0; // Chevauchement

                    if (distTicks <= ConfluenceToleranceTicks)
                    {
                        cluster.Add(other);
                        periodTypesSeen.Add(other.PeriodType);
                        clusterLow = Math.Min(clusterLow, otherLow);
                        clusterHigh = Math.Max(clusterHigh, otherHigh);
                    }
                }

                // Une confluence est valide si elle regroupe au moins 2 sources distinctes ou 2+ niveaux majeurs
                if (cluster.Count >= MinConfluenceLevels && periodTypesSeen.Count >= 2 && cluster.Count > bestCount)
                {
                    bestCount = cluster.Count;
                    bestLow = clusterLow;
                    bestHigh = clusterHigh;
                    bestMembers = cluster;
                }
            }

            if (bestCount >= MinConfluenceLevels)
            {
                ctx.ConfluenceCount = bestCount;
                ctx.ConfluenceZoneLow = bestLow;
                ctx.ConfluenceZoneHigh = bestHigh;

                var sb = new StringBuilder(64);
                sb.Append(string.Format("VP_CONFLUENCE x{0} [", bestCount));
                for (int m = 0; m < bestMembers.Count; m++)
                {
                    if (m > 0) sb.Append(" + ");
                    sb.Append(bestMembers[m].Name);
                    ctx.ConfluenceDetails.Add(bestMembers[m].Name);
                }
                sb.Append("]");
                ctx.ConfluenceType = sb.ToString();
            }
        }

        private void FindActiveNode(double price, ClosedVolumeProfile week, ClosedVolumeProfile month, double tickSize, VolumeProfileContext ctx)
        {
            if (week != null && week.Nodes != null)
            {
                foreach (var n in week.Nodes)
                {
                    if (n.Contains(price) || n.DistanceTicks(price, tickSize) <= NodeToleranceTicks)
                    {
                        ctx.ActiveNode = n;
                        return;
                    }
                }
            }

            if (month != null && month.Nodes != null)
            {
                foreach (var n in month.Nodes)
                {
                    if (n.Contains(price) || n.DistanceTicks(price, tickSize) <= NodeToleranceTicks)
                    {
                        ctx.ActiveNode = n;
                        return;
                    }
                }
            }
        }

        #endregion

        #region Machine à États de Réaction des Zones

        /// <summary>
        /// Évalue l'interaction du prix avec une zone et met à jour son état de réaction.
        /// </summary>
        public void EvaluateZoneReaction(
            VolumeProfileZoneState state,
            double barHigh,
            double barLow,
            double barClose,
            double barOpen,
            double barDelta,
            double tickSize,
            DateTime barTimeUtc)
        {
            if (state == null || !state.Active) return;

            bool penetrated = (barHigh >= state.LevelPriceLow && barLow <= state.LevelPriceHigh);
            double distTicks = state.Contains(barClose) ? 0 :
                (barClose < state.LevelPriceLow ? (state.LevelPriceLow - barClose) / tickSize : (barClose - state.LevelPriceHigh) / tickSize);

            if (penetrated || distTicks <= LevelToleranceTicks)
            {
                if (!state.FirstTouchUtc.HasValue)
                    state.FirstTouchUtc = barTimeUtc;

                state.LastTouchUtc = barTimeUtc;
                state.TouchCount++;

                if (state.State == VolumeProfileZoneStateEnum.UNTOUCHED)
                    state.State = VolumeProfileZoneStateEnum.TESTED;

                // Détection de Rejet (Wick + Close opposé + Delta de rejet)
                bool isBullishRejection = (barLow <= state.LevelPriceLow && barClose > state.LevelPriceHigh && barDelta > 0);
                bool isBearishRejection = (barHigh >= state.LevelPriceHigh && barClose < state.LevelPriceLow && barDelta < 0);

                if (isBullishRejection || isBearishRejection)
                {
                    state.RejectionCount++;
                    state.State = VolumeProfileZoneStateEnum.REJECTED;
                    state.LastReaction = isBullishRejection ? "BULLISH_REJECTION" : "BEARISH_REJECTION";
                    state.StrengthScore = Math.Min(100.0, state.StrengthScore + 10.0);
                }
                // Détection d'Acceptation (Clôture nette et répétée à l'intérieur)
                else if (barClose >= state.LevelPriceLow && barClose <= state.LevelPriceHigh)
                {
                    state.AcceptanceCount++;
                    if (state.AcceptanceCount >= 2)
                    {
                        state.State = VolumeProfileZoneStateEnum.ACCEPTED;
                        state.LastReaction = "ACCEPTED_IN_VALUE";
                        state.StrengthScore = Math.Max(20.0, state.StrengthScore - 15.0);
                    }
                }
                // Détection de Cassure franche
                else if (distTicks > LevelToleranceTicks * 2 && ((barClose > state.LevelPriceHigh && barOpen > state.LevelPriceHigh) || (barClose < state.LevelPriceLow && barOpen < state.LevelPriceLow)))
                {
                    state.BreakCount++;
                    state.State = VolumeProfileZoneStateEnum.BROKEN;
                    state.LastReaction = "BROKEN_THROUGH";
                    state.StrengthScore = Math.Max(0.0, state.StrengthScore - 30.0);
                }

                state.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        #endregion
    }
}
