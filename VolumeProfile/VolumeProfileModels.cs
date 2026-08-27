#region Using declarations
using System;
using System.Collections.Generic;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.VolumeProfilePro
{
    #region Enums

    /// <summary>Type de période de Volume Profile clôturé.</summary>
    public enum VolumeProfilePeriodType
    {
        Daily = 0,
        Weekly = 1,
        Monthly = 2
    }

    /// <summary>Type de node de profil (HVN = High Volume Node, LVN = Low Volume Node).</summary>
    public enum VolumeProfileNodeType
    {
        HVN = 0,
        LVN = 1
    }

    /// <summary>État du cycle de vie d'une zone ou d'un niveau de référence.</summary>
    public enum VolumeProfileZoneStateEnum
    {
        UNTOUCHED = 0,
        TESTED = 1,
        REJECTED = 2,
        ACCEPTED = 3,
        MITIGATED = 4,
        BROKEN = 5,
        ARCHIVED = 6
    }

    /// <summary>Localisation du prix par rapport à la structure de Value Area et aux nodes.</summary>
    [Flags]
    public enum VolumeProfileLocationType
    {
        None = 0,
        AboveValue = 1 << 0,
        InsideValue = 1 << 1,
        BelowValue = 1 << 2,
        NearPoc = 1 << 3,
        NearVah = 1 << 4,
        NearVal = 1 << 5,
        InsideHvn = 1 << 6,
        InsideLvn = 1 << 7,
        NearHvn = 1 << 8,
        NearLvn = 1 << 9
    }

    #endregion

    #region Data Models

    /// <summary>
    /// Représente un High Volume Node (HVN) ou Low Volume Node (LVN) sous forme de zone de prix.
    /// </summary>
    public sealed class VolumeProfileNode
    {
        public long Id { get; set; }
        public long ProfileId { get; set; }
        public VolumeProfileNodeType NodeType { get; set; }
        public double ZoneLow { get; set; }
        public double ZoneHigh { get; set; }
        public double PeakPrice { get; set; }
        public double RelativeVolume { get; set; }
        public double Prominence { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public VolumeProfileNode()
        {
            CreatedAtUtc = DateTime.UtcNow;
        }

        public VolumeProfileNode(VolumeProfileNodeType type, double zoneLow, double zoneHigh, double peakPrice, double relVol, double prominence)
        {
            NodeType = type;
            ZoneLow = Math.Min(zoneLow, zoneHigh);
            ZoneHigh = Math.Max(zoneLow, zoneHigh);
            PeakPrice = peakPrice;
            RelativeVolume = relVol;
            Prominence = prominence;
            CreatedAtUtc = DateTime.UtcNow;
        }

        public bool Contains(double price)
        {
            return price >= ZoneLow && price <= ZoneHigh;
        }

        public double DistanceTicks(double price, double tickSize)
        {
            if (tickSize <= 0) return 0;
            if (Contains(price)) return 0;
            if (price < ZoneLow) return Math.Abs(ZoneLow - price) / tickSize;
            return Math.Abs(price - ZoneHigh) / tickSize;
        }

        public override string ToString()
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0} [{1:F2} - {2:F2}] Peak:{3:F2} (x{4:F1})",
                NodeType, ZoneLow, ZoneHigh, PeakPrice, RelativeVolume);
        }
    }

    /// <summary>
    /// Profil de volume complet et immuable d'une période clôturée (Jour, Semaine ou Mois).
    /// </summary>
    public sealed class ClosedVolumeProfile
    {
        public long Id { get; set; }
        public string Symbol { get; set; }
        public string Exchange { get; set; }
        public string SessionTemplate { get; set; }
        public VolumeProfilePeriodType ProfileType { get; set; }
        public string PeriodKey { get; set; }

        public DateTime PeriodStartUtc { get; set; }
        public DateTime PeriodEndUtc { get; set; }

        public double Poc { get; set; }
        public double Vah { get; set; }
        public double Val { get; set; }

        // Métriques VWAP Clôturé & Bandes d'Écart-Type (SD)
        public double Vwap { get; set; }
        public double VwapStdDev { get; set; }
        public double VwapSd1Upper { get; set; }
        public double VwapSd1Lower { get; set; }
        public double VwapSd2Upper { get; set; }
        public double VwapSd2Lower { get; set; }
        public double VwapSd3Upper { get; set; }
        public double VwapSd3Lower { get; set; }

        public double TotalVolume { get; set; }
        public int ValueAreaPercent { get; set; }
        public double TickSize { get; set; }
        public string CalculationMethod { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public bool Valid { get; set; }

        public List<VolumeProfileNode> Nodes { get; set; }

        public ClosedVolumeProfile()
        {
            Symbol = "";
            Exchange = "";
            SessionTemplate = "";
            PeriodKey = "";
            ValueAreaPercent = 70;
            TickSize = 0.25;
            CalculationMethod = "AMC_GAUSSIAN_V2";
            CreatedAtUtc = DateTime.UtcNow;
            Nodes = new List<VolumeProfileNode>();
        }

        public ClosedVolumeProfile Clone()
        {
            var clone = new ClosedVolumeProfile
            {
                Id = this.Id,
                Symbol = this.Symbol,
                Exchange = this.Exchange,
                SessionTemplate = this.SessionTemplate,
                ProfileType = this.ProfileType,
                PeriodKey = this.PeriodKey,
                PeriodStartUtc = this.PeriodStartUtc,
                PeriodEndUtc = this.PeriodEndUtc,
                Poc = this.Poc,
                Vah = this.Vah,
                Val = this.Val,
                Vwap = this.Vwap,
                VwapStdDev = this.VwapStdDev,
                VwapSd1Upper = this.VwapSd1Upper,
                VwapSd1Lower = this.VwapSd1Lower,
                VwapSd2Upper = this.VwapSd2Upper,
                VwapSd2Lower = this.VwapSd2Lower,
                VwapSd3Upper = this.VwapSd3Upper,
                VwapSd3Lower = this.VwapSd3Lower,
                TotalVolume = this.TotalVolume,
                ValueAreaPercent = this.ValueAreaPercent,
                TickSize = this.TickSize,
                CalculationMethod = this.CalculationMethod,
                CreatedAtUtc = this.CreatedAtUtc,
                Valid = this.Valid,
                Nodes = new List<VolumeProfileNode>(this.Nodes != null ? this.Nodes.Count : 0)
            };

            if (this.Nodes != null)
            {
                foreach (var n in this.Nodes)
                {
                    clone.Nodes.Add(new VolumeProfileNode(n.NodeType, n.ZoneLow, n.ZoneHigh, n.PeakPrice, n.RelativeVolume, n.Prominence)
                    {
                        Id = n.Id,
                        ProfileId = n.ProfileId,
                        CreatedAtUtc = n.CreatedAtUtc
                    });
                }
            }

            return clone;
        }

        public override string ToString()
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0} {1} | POC:{2:F2} VAH:{3:F2} VAL:{4:F2} | VWAP:{5:F2} (SD2:{6:F2}-{7:F2}) | Nodes:{8} | Valid:{9}",
                ProfileType, PeriodKey, Poc, Vah, Val, Vwap, VwapSd2Lower, VwapSd2Upper, Nodes != null ? Nodes.Count : 0, Valid);
        }
    }

    /// <summary>
    /// État dynamique et historique de réaction du marché sur un niveau ou une zone de profil.
    /// </summary>
    public sealed class VolumeProfileZoneState
    {
        public long Id { get; set; }
        public long ProfileId { get; set; }
        public long? NodeId { get; set; }
        public string LevelType { get; set; } // POC, VAH, VAL, HVN, LVN
        public double LevelPriceLow { get; set; }
        public double LevelPriceHigh { get; set; }
        public double PeakPrice { get; set; }

        public DateTime? FirstTouchUtc { get; set; }
        public DateTime? LastTouchUtc { get; set; }

        public int TouchCount { get; set; }
        public int RejectionCount { get; set; }
        public int AcceptanceCount { get; set; }
        public int BreakCount { get; set; }

        public VolumeProfileZoneStateEnum State { get; set; }
        public double StrengthScore { get; set; }
        public string LastReaction { get; set; }
        public bool Active { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public VolumeProfileZoneState()
        {
            LevelType = "";
            State = VolumeProfileZoneStateEnum.UNTOUCHED;
            StrengthScore = 100.0;
            LastReaction = "NONE";
            Active = true;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        public bool Contains(double price)
        {
            return price >= LevelPriceLow && price <= LevelPriceHigh;
        }
    }

    /// <summary>
    /// Contexte Volume Profile multi-timeframe complet attaché à chaque candidat de trade.
    /// </summary>
    public sealed class VolumeProfileContext
    {
        public ClosedVolumeProfile PrevDay { get; set; }
        public ClosedVolumeProfile PrevWeek { get; set; }
        public ClosedVolumeProfile PrevMonth { get; set; }

        public double DistanceToClosestReference { get; set; }
        public double DistanceToClosestReferenceAtr { get; set; }
        public string ClosestReferenceName { get; set; }
        public double ClosestReferencePrice { get; set; }

        public int ConfluenceCount { get; set; }
        public double ConfluenceZoneLow { get; set; }
        public double ConfluenceZoneHigh { get; set; }
        public string ConfluenceType { get; set; }
        public readonly List<string> ConfluenceDetails;

        public VolumeProfileLocationType Location { get; set; }
        public string LocationSummary { get; set; }

        public VolumeProfileNode ActiveNode { get; set; }
        public VolumeProfileZoneState ActiveZoneState { get; set; }

        public VolumeProfileContext()
        {
            ClosestReferenceName = "";
            ConfluenceType = "";
            ConfluenceDetails = new List<string>(4);
            Location = VolumeProfileLocationType.None;
            LocationSummary = "";
        }

        public bool IsValid
        {
            get
            {
                return (PrevDay != null && PrevDay.Valid) ||
                       (PrevWeek != null && PrevWeek.Valid) ||
                       (PrevMonth != null && PrevMonth.Valid);
            }
        }

        public VolumeProfileContext Clone()
        {
            var ctx = new VolumeProfileContext
            {
                PrevDay = this.PrevDay,
                PrevWeek = this.PrevWeek,
                PrevMonth = this.PrevMonth,
                DistanceToClosestReference = this.DistanceToClosestReference,
                DistanceToClosestReferenceAtr = this.DistanceToClosestReferenceAtr,
                ClosestReferenceName = this.ClosestReferenceName,
                ClosestReferencePrice = this.ClosestReferencePrice,
                ConfluenceCount = this.ConfluenceCount,
                ConfluenceZoneLow = this.ConfluenceZoneLow,
                ConfluenceZoneHigh = this.ConfluenceZoneHigh,
                ConfluenceType = this.ConfluenceType,
                Location = this.Location,
                LocationSummary = this.LocationSummary,
                ActiveNode = this.ActiveNode,
                ActiveZoneState = this.ActiveZoneState
            };
            ctx.ConfluenceDetails.AddRange(this.ConfluenceDetails);
            return ctx;
        }

        public override string ToString()
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "VP: {0} | Closest: {1} ({2:F1}t) | Confluence: {3} ({4})",
                LocationSummary, ClosestReferenceName, DistanceToClosestReference, ConfluenceCount, ConfluenceType);
        }
    }

    #endregion
}
