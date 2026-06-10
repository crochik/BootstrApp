using System;
using System.Collections.Generic;
using System.Linq;

namespace PI.ProductCatalog.Models
{
    public class ItemCost : IPrice
    {
        #region IDates
        public DateTime? EffectiveDate { get; set; }
        public DateTime? PendingDate { get; set; }
        public DateTime? DroppedDate { get; set; }
        public DateTime? PromotionalStart { get; set; }
        public DateTime? PromotionalEnd { get; set; }
        #endregion

        #region IPrice
        public PriceCriteria Criteria { get; set; }
        public UnitOfMeasurement UOM { get; set; }
        public decimal? MinimumQuantity { get; set; }
        public PackagePriceCondition? PackageCondition { get; set; }
        public decimal UnitCost { get; set; }
        public string LocationId { get; set; }
        #endregion

        public string Promotion { get; set; }
        public Allowance[] Allowances { get; set; }

        public override bool Equals(object obj)
        {
            return obj is ItemCost other &&
                EffectiveDate.GetValueOrDefault(DateTime.MinValue) == other.EffectiveDate.GetValueOrDefault(DateTime.MinValue) &&
                PendingDate.GetValueOrDefault(DateTime.MinValue) == other.PendingDate.GetValueOrDefault(DateTime.MinValue) &&
                DroppedDate.GetValueOrDefault(DateTime.MinValue) == other.DroppedDate.GetValueOrDefault(DateTime.MinValue) &&
                PromotionalStart.GetValueOrDefault(DateTime.MinValue) == other.PromotionalStart.GetValueOrDefault(DateTime.MinValue) &&
                PromotionalEnd.GetValueOrDefault(DateTime.MinValue) == other.PromotionalEnd.GetValueOrDefault(DateTime.MinValue) &&
                Criteria == other.Criteria &&
                UOM == other.UOM &&
                MinimumQuantity.GetValueOrDefault(-1) == other.MinimumQuantity.GetValueOrDefault(-1) &&
                PackageCondition.GetValueOrDefault(PackagePriceCondition.Undefined) == other.PackageCondition.GetValueOrDefault(PackagePriceCondition.Undefined) &&
                UnitCost == other.UnitCost &&
                string.Equals(LocationId, other.LocationId) &&
                string.Equals(Promotion, other.Promotion) &&
                Allowances.IsEqualTo(other.Allowances)
                ;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            var lines = getLines().ToArray();
            return lines.Length == 1 ? lines[0] : string.Join("; ", lines);

            IEnumerable<string> getLines()
            {
                yield return $"${UnitCost:.##} / {UOM.GetAbbreviation()}";

                if (Criteria != PriceCriteria.List)
                {
                    if (Criteria==PriceCriteria.Promotional && PromotionalStart.HasValue && PromotionalEnd.HasValue)
                    {
                        yield return $"Promotion {PromotionalStart:MM/dd} - {PromotionalEnd:MM/dd}";
                    }
                    else
                    {
                        yield return Criteria.ToString();
                    }
                }

                if (!string.IsNullOrEmpty(Promotion)) yield return Promotion;

                switch (PackageCondition)
                {
                    case PackagePriceCondition.Cut:
                        yield return "Cut";
                        break;
                    case PackagePriceCondition.RollAtCut:
                        yield return "Roll at Cut";
                        break;
                    case PackagePriceCondition.Pallet:
                        yield return "Pallet";
                        break;
                }

                if (MinimumQuantity.GetValueOrDefault(0) != 0 && MinimumQuantity.Value != 1) yield return $"Minimum {UOM.Format(MinimumQuantity.Value)}";
            }
        }

        public decimal? CalculatePrice(decimal? margin) => margin.HasValue ? Math.Round(100 * UnitCost / (100 - margin.Value), 2) : null;
        // public string FormatPrice(decimal? margin) => margin.HasValue ? $"${CalculatePrice(margin):.##}" : null;
    }

    public static class ItemCostExtensions 
    {
        public static bool IsValid(this ItemCost x)
        {
            if (x == null) return false;
            if (x.UnitCost <= 0) return false;
            if (x.DroppedDate.HasValue && x.DroppedDate.Value < DateTime.UtcNow) return false;
            if (x.Criteria == PriceCriteria.Promotional && x.PromotionalEnd.HasValue && x.PromotionalEnd.Value < DateTime.UtcNow) return false;

            return true;
        }
    }

    public static class ArrayExtensions
    {
        public static bool IsEqualTo<T>(this T[] array, T[] other)
        {
            if (array == null) return other == null;
            if (array.Length != other.Length) return false;
            for (var i = 0; i < array.Length; i++)
            {
                if (array[i] == null)
                {
                    if (other[i] != null) return false;
                    continue;
                }

                if (!array[i].Equals(other[i])) return false;
            }

            return true;
        }
    }
}