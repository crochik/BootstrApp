using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models;

namespace PI.ProductCatalog.Models;

/// <summary>
/// Properties marked with this attribute will unset existing values during merge
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class MergeNullAttribute : Attribute
{
}

/// <summary>
/// Can be used to calculate description
/// </summary>
public interface IDescribableCatalogItem
{
    string Name { get; }
    DateTime? DroppedDate { get; }
    DateTime? PromotionalStart { get; }
    DateTime? PromotionalEnd { get; }
    ItemCost StandardCost { get; }
    ItemCost CutCost { get; }
    ItemCost PalletCost { get; }
    bool IsActive { get; }
}

[BsonCollection("fcb2b.Item")]
public partial class CatalogItem : AbstractCatalogEntity, ICatalogItem, IExternalId, IDescribableCatalogItem
{
    public static readonly TimeSpan ElapsedBeforeRemoval = TimeSpan.FromDays(180);

    private static readonly Dictionary<UnitOfMeasurement, int> PreferredUOM = new[]
    {
        UnitOfMeasurement.SqFt,
        UnitOfMeasurement.Piece,
        UnitOfMeasurement.Each,
        UnitOfMeasurement.Feet,
        UnitOfMeasurement.FeetAndInches
    }.ToSortDict();

    public string Manufacturer { get; set; }
    public string StyleNumber { get; set; }
    public string PricingGroup { get; set; }
    public string BuyingGroup { get; set; }
    public string Contract { get; set; }
    public string SellingCompany { get; set; }
    public string Gauge { get; set; }
    [MergeNull]
    public DateTime? EffectiveDate { get; set; }
    [MergeNull]
    public DateTime? PendingDate { get; set; }
    [MergeNull]
    public DateTime? DroppedDate { get; set; }
    [MergeNull]
    public DateTime? PromotionalStart { get; set; }
    [MergeNull]
    public DateTime? PromotionalEnd { get; set; }
    public MaterialClassification Material { get; set; }
    public string ProductType { get; set; }
    public string Composition { get; set; }
    public string CollectionName { get; set; }
    public string PrimaryComponent { get; set; }
    public Warranty Warranty { get; set; }
    public BuilderProgram BuilderProgram { get; set; }
    public UOMRate[] Packaging { get; set; }
    public string UniqueStyleCode { get; set; }
    public string StyleName { get; set; }
    public string ManufacturerStyleNumber { get; set; }
    public string ManufacturerStyleName { get; set; }
    public string Backing { get; set; }
    public string SizeCode { get; set; }
    public string AbbreviatedProductName { get; set; }
    public Measurement PatternRepeat { get; set; }
    public Measurement PatternDrop { get; set; }
    public Measurement PatternLength { get; set; }
    public Measurement PatternWidth { get; set; }
    public Measurement NominalLength { get; set; }
    public Measurement ActualLength { get; set; }
    public Measurement NominalWidth { get; set; }
    public Measurement ActualWidth { get; set; }
    public Measurement Height { get; set; }
    public UOMRate ShippingWeight { get; set; }
    public Measurement FaceWeight { get; set; }
    public Measurement SellingUnit { get; set; }
    public string[] AssociatedStyleNumbers { get; set; }
    public string SKU { get; set; }
    public string UniqueColorCode { get; set; }
    public string UPCCode { get; set; }
    public string ManufacturerSKU { get; set; }
    public string ColorName { get; set; }
    public string ColorNumber { get; set; }
    public string[] ImageUrls { get; set; }
    public string ProductSpecification { get; set; }
    public string[] AssociatedSKUs { get; set; }
    public BaseUnit BaseUnit { get; set; }

    private ItemCost[] _costs;

    public ItemCost[] Costs
    {
        get => _costs ?? [];
        set => _costs = value?.Where(x => x != null).ToArray();
    }

    /// <summary>
    /// Salesforce Product Id 
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// Information about last sync to salesforce 
    /// </summary>
    public SalesforceSync Salesforce { get; set; }

    public bool IsActive { get; set; } = true;
        
    /// <summary>
    /// copy of product selector properties?
    /// </summary>
    public Dictionary<string, object> Properties { get; set; }

    [BsonElement] public DateTime[] KeyDates => GetKeyDates();

    [BsonElement] public ItemCost StandardCost => GetStandardCost() ?? CutCost;

    [BsonElement] public ItemCost CutCost => GetCutCost();

    [BsonElement] public ItemCost PalletCost => GetPalletCost();

    [BsonElement]
    public decimal? RollLength
        => IsRollGoods && (NominalLength ?? ActualLength).ConvertTo(UnitOfMeasurement.Feet, out var value) ? value.Units : default(decimal?);

    [BsonElement]
    public decimal? RollWidth
        => IsRollGoods && (NominalWidth ?? ActualWidth).ConvertTo(UnitOfMeasurement.Feet, out var value) ? value.Units : default(decimal?);

    [BsonElement] public UOMRate Package => GetDefaultPackage();

    [BsonElement] public UOMRate Pallet => GetPalletPackages().FirstOrDefault();

    [BsonElement] public decimal? PackagesPerPallet => GetPackagesPerPallet().FirstOrDefault()?.Measurement.Units;

    [BsonElement] public decimal? StandardPrice => StandardCost?.CalculatePrice(Margin);

    [BsonElement] public decimal? CutPrice => CutCost?.CalculatePrice(Margin);

    [BsonElement] public decimal? PalletPrice => PalletCost?.CalculatePrice(Margin);

    [BsonElement]
    public IEnumerable<ItemCost> Prices => Margin.HasValue
        ? Costs
            .Where(x => x.IsValid())
            .Select(x => new ItemCost
            {
                EffectiveDate = x.EffectiveDate,
                PendingDate = x.PendingDate,
                DroppedDate = x.DroppedDate,
                PromotionalStart = x.PromotionalStart,
                PromotionalEnd = x.PromotionalEnd,
                Criteria = x.Criteria,
                UnitCost = x.CalculatePrice(Margin).Value,
                UOM = x.UOM,
                MinimumQuantity = x.MinimumQuantity,
                PackageCondition = x.PackageCondition,
                LocationId = x.LocationId,
                Promotion = x.Promotion,
                Allowances = x.Allowances,
            })
        : [];

    [BsonElement] bool IsRollGoods => Material?.IsRollGoods ?? false;

    public DateTime[] GetKeyDates()
    {
        var threshold = DateTime.UtcNow;

        var list = AllKeyDates()
            .Where(x => x > threshold)
            .Select(x => x.Date)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

        return list;
    }

    private IEnumerable<DateTime> AllKeyDates()
    {
        if (DroppedDate.HasValue)
        {
            yield return DroppedDate.Value;
            yield return DroppedDate.Value.Add(ElapsedBeforeRemoval);
        }

        if (EffectiveDate.HasValue) yield return EffectiveDate.Value;
        if (PendingDate.HasValue) yield return PendingDate.Value;
        if (PromotionalStart.HasValue) yield return PromotionalStart.Value;
        if (PromotionalEnd.HasValue) yield return PromotionalEnd.Value;

        var costs = Costs;
        if (costs == null) yield break;

        foreach (var cost in costs)
        {
            if (cost.DroppedDate.HasValue)
            {
                yield return cost.DroppedDate.Value;
                yield return cost.DroppedDate.Value.Add(ElapsedBeforeRemoval);
            }

            if (cost.EffectiveDate.HasValue) yield return cost.EffectiveDate.Value;
            if (cost.PendingDate.HasValue) yield return cost.PendingDate.Value;
            if (cost.PromotionalStart.HasValue) yield return cost.PromotionalStart.Value;
            if (cost.PromotionalEnd.HasValue) yield return cost.PromotionalEnd.Value;
        }
    }

    public void Update()
    {
        if (DroppedDate.HasValue && DroppedDate.Value < DateTime.UtcNow.Subtract(ElapsedBeforeRemoval))
        {
            IsActive = false;
        }
        // else
        // {
        //     IsActive = true;
        // }

        RemoveInvalidCosts();
        Description = this.GetDescription();
    }

    public ItemCost[] GetValidCosts()
    {
        return Costs?.Where(x => x.IsValid()).ToArray() ?? [];
    }
        
    public void RemoveInvalidCosts()
    {
        _costs = GetValidCosts();
    }

    public IEnumerable<UOMRate> GetAllPackaging()
    {
        if (Packaging != null)
        {
            foreach (var item in Packaging.Where(x => x.Measurement?.Units > 0))
            {
                yield return item;
            }
        }

        if (!IsRollGoods && SellingUnit != null && SellingUnit.Units > 0)
        {
            // non-roll, assume selling unit is package
            yield return new UOMRate
            {
                UOM = UnitOfMeasurement.Package,
                Measurement = SellingUnit,
            };
        }
    }

    private IEnumerable<UOMRate> GetPackages()
        => GetAllPackaging()
            .Where(x => x.UOM switch
            {
                UnitOfMeasurement.Carton => true,
                UnitOfMeasurement.Package => true,
                UnitOfMeasurement.Bundle => true,
                UnitOfMeasurement.Box => true,
                _ => false
            })
            .SortByUOM(PreferredUOM);

    private UOMRate GetDefaultPackage()
    {
        if (SellingUnit?.Units > 0)
        {
            return new UOMRate
            {
                UOM = UnitOfMeasurement.Package,
                Measurement = SellingUnit,
            };
        }

        return GetPackages().FirstOrDefault();
    }

    private IEnumerable<UOMRate> GetPalletPackages()
        => GetAllPackaging()
            .Where(x => x.UOM switch
            {
                UnitOfMeasurement.Pallet => true,
                _ => false
            })
            .SortByUOM(PreferredUOM);

    private IEnumerable<UOMRate> GetPackagesPerPallet()
    {
        foreach (var pallet in GetPalletPackages())
        {
            switch (pallet.UOM)
            {
                case UnitOfMeasurement.Package:
                case UnitOfMeasurement.Box:
                case UnitOfMeasurement.Carton:
                    yield return pallet;
                    break;

                default:
                {
                    var package = GetPackages().FirstOrDefault(x => x.Measurement.UOM == pallet.Measurement.UOM && x.Measurement.Units > 0);
                    if (package != null)
                    {
                        // auto convert
                        yield return new UOMRate
                        {
                            UOM = UnitOfMeasurement.Pallet,
                            Measurement = new Measurement
                            {
                                UOM = UnitOfMeasurement.Package,
                                Units = pallet.Measurement.Units / package.Measurement.Units,
                            }
                        };
                    }
                }
                    break;
            }
        }
    }

    private ItemCost GetStandardCost()
    {
        if (Costs == null) return null;

        return PickCost(Costs.Where(x => x.PackageCondition.GetValueOrDefault() switch
        {
            PackagePriceCondition.StandardRollLength => true,
            PackagePriceCondition.Undefined => true,
            _ => false,
        }));
    }

    private ItemCost GetPalletCost()
    {
        if (Costs == null) return null;

        return PickCost(Costs.Where(x => x.PackageCondition.GetValueOrDefault() switch
        {
            PackagePriceCondition.Pallet => true,
            _ => false,
        }));
    }

    private ItemCost GetCutCost()
    {
        if (Costs == null) return null;

        return PickCost(Costs.Where(x => x.PackageCondition.GetValueOrDefault() switch
        {
            PackagePriceCondition.Cut => true,
            PackagePriceCondition.RollAtCut => true,
            _ => false,
        }));
    }

    private ItemCost PickCost(IEnumerable<ItemCost> ecosts)
    {
        if (ecosts == null) return null;

        var costs = ecosts
            .Where(x => !x.DroppedDate.HasValue || x.DroppedDate.Value > DateTime.UtcNow)
            .Where(x =>
                x.Criteria != PriceCriteria.Promotional || (
                    (!x.PromotionalStart.HasValue || x.PromotionalStart.Value < DateTime.UtcNow) &&
                    (!x.PromotionalEnd.HasValue || x.PromotionalEnd.Value > DateTime.UtcNow)
                )
            )
            // .Where(x => !x.EffectiveDate.HasValue && x.EffectiveDate.Value<DateTime.UtcNow)
            .OrderByDescending(x => x.UnitCost)
            .ToArray();

        switch (costs.Length)
        {
            case 0: return null;
            case 1: return costs[0];
        }

        return costs.FirstOrDefault(x => x.Criteria == PriceCriteria.Promotional) ?? costs.First();
    }
        
    /// <summary>
    /// Guess criteria to calculate quantity (until we have an explicit property) 
    /// </summary>
    public QuantityCriteria GetQuantityCriteria()
    {
        // if (Criteria != null) return Criteria.Value;
        return (SellingUnit?.UOM ?? Costs?.FirstOrDefault()?.UOM) switch
        {
            UnitOfMeasurement.SqFt or UnitOfMeasurement.SqYd => QuantityCriteria.MainProductArea,
            UnitOfMeasurement.Feet or UnitOfMeasurement.FeetAndInches => QuantityCriteria.Perimeter,
            _ => QuantityCriteria.Arbitrary,
        };
    }

    public TaxCategory? GetTaxCategory()
    {
        // TODO: far from complete
        return Material.Type switch
        {
            MaterialType.Labor => TaxCategory.Service,
            _ => TaxCategory.Sales,
        };
    }
}

public static class ICatalogItemExtensions
{
    public static string GetStyleNumber(this ICatalogItem item)
        => !string.IsNullOrWhiteSpace(item.StyleNumber) ? item.StyleNumber : item.ManufacturerStyleNumber;

    public static string GetStyleName(this ICatalogItem item)
        => !string.IsNullOrWhiteSpace(item.StyleName) ? item.StyleName : (!string.IsNullOrWhiteSpace(item.ManufacturerStyleName) ? item.ManufacturerStyleName : item.GetStyleNumber());

    public static string GetSKU(this ICatalogItem item)
        => !string.IsNullOrWhiteSpace(item.SKU) ? item.SKU : item.ManufacturerSKU;

    public static string GetColorName(this ICatalogItem item)
        => !string.IsNullOrWhiteSpace(item.ColorName) ? item.ColorName : (!string.IsNullOrWhiteSpace(item.ColorNumber) ? item.ColorNumber : item.GetSKU());

    public static string GetMaterialTypeLookupValue(this ICatalogItem item)
    {
        var str = new StringBuilder(item?.Material?.Type != null ? item.Material.Type.ToString() : nameof(MaterialType.Unclassified));
        str.Append(":");
        str.Append(item?.Material?.SubType != null ? item.Material.SubType.ToString() : nameof(MaterialSubType.Unclassified));
        return str.ToString();
    }
}

public static class IDescribableCatalogItemExtensions
{
    public static string GetDescription(this IDescribableCatalogItem item)
    {
        var desc = item.Name;

        if (!item.IsActive)
        {
            desc += " (INACTIVE)";
        }
        else if (item.DroppedDate.HasValue)
        {
            desc += $" (DROP {item.DroppedDate:MM/dd})";
        }
        else if (item.PromotionalStart.HasValue && item.PromotionalEnd.HasValue)
        {
            desc += $" (PROMOTION {item.PromotionalStart:MM/dd} - {item.PromotionalEnd:MM/dd})";
        }
        else
        {
            var calcCosts = costs().ToArray();
            if (calcCosts.Length > 0 && calcCosts.Count(x => x.Criteria == PriceCriteria.Promotional) == calcCosts.Length)
            {
                var start = calcCosts.GroupBy(x => x.PromotionalStart).Count() == 1 ? calcCosts[0].PromotionalStart : null;
                var end = calcCosts.GroupBy(x => x.PromotionalEnd).Count() == 1 ? calcCosts[0].PromotionalEnd : null;
                desc += (start.HasValue && end.HasValue) ? $" (PROMOTION {start:MM/dd} - {end:MM/dd})" : " (PROMOTION)";
            }
        }

        return desc;

        IEnumerable<ItemCost> costs()
        {
            if (item.CutCost != null) yield return item.CutCost;
            if (item.StandardCost != null) yield return item.StandardCost;
            if (item.PalletCost != null) yield return item.PalletCost;
        }
    }
}