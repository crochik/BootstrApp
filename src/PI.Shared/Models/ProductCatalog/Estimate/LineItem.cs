using System;
using System.Collections.Generic;
using System.Linq;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models;

namespace PI.ProductCatalog.Models;

public class ChildLineItem
{
    /// <summary>
    /// For subline items it will be the Tag associated with it 
    /// e.g. Tag, Room Name, ....
    /// </summary>
    public string Name { get; set; }
    public string Description { get; set; }
    public Measurement Quantity { get; set; }
    public decimal? WasteFactor { get; set; }
    public Measurement AdjustedQuantity { get; set; }
}

public enum LineItemSource
{
    Unknown,
    Room, // Room Preparation
    ProductType, // Installation Information
    Item,
    Freight,
    Discount,
    Other, // ????
}

public enum TaxCategory
{
    Sales,
    Service,
    Labor,
    Freight,
    Use, // ???
    Other, // ????
}

public class TaxLiability
{
    public TaxCategory Category { get; set; }
    public decimal Amount { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
}

[BsonCollection("TaxRates")]
public class TaxRates
{
    [BsonId] public Guid Id { get; set; }
    public DateTime CreatedOn { get; set; }

    public string PostalCode { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public TaxLiability[] TaxLiabilities { get; set; }

    /// <summary>
    /// Determines whether the tax is based on the seller's location (origin) or the buyer's location (destination).
    /// Useful for proper application of tax rates.
    /// </summary>
    public bool Destination { get; set; }
}

// public class RequiredPayment
// {
//     /// <summary>
//     /// whether value is a percentage (dynamic) or a fixed value
//     /// </summary>
//     public bool IsDynamic { get; set; }
//     
//     /// <summary>
//     /// If percentage, express friendly % (e.g. 20 => 20%)
//     /// if not, dollar (currency) amount
//     /// </summary>
//     public decimal Amount { get; set; }
// }
//
// public class PaymentTerms
// {
//     public decimal? Interest { get; set; }
//     public int? Payments { get; set; }
//     public RequiredPayment RequiredPayment { get; set; }
//     public TimeSpan Interval { get; set; }
// }

public enum LineItemWarning
{
    NoMargin,
    NoCost,
    NoQuantity,
    NoSellingUnit,
    WasteFactorError,
    QuantityError,
    UndefinedColor,
    HardSurfaceFreight,
}

public class LineItem : ChildLineItem, ITaggable, ITaxable
{
    public string SKU { get; set; }
    public Guid ItemId { get; set; }
    public ChildLineItem[] Children { get; set; }
    public LineItemSource Source { get; set; }
    public QuantityCriteria Criteria { get; set; }

    public decimal? UnitCost { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? Margin { get; set; }
    public decimal? TotalCost { get; set; }
    public decimal? TotalPrice { get; set; }

    public ItemCost[] Costs { get; set; }


    /// <summary>
    /// Actual waste factor (after waste factor and selling unit)
    /// </summary>
    public decimal? ActualWasteFactor { get; set; }

    /// <summary>
    /// Selling unit used to calculate
    /// </summary>
    public Measurement SellingUnit { get; set; }

    /// <summary>
    /// Cost used to calculate
    /// </summary>
    public ItemCost Cost { get; set; }

    public HashSet<string> Warnings { get; set; }

    /// <summary>
    /// Tags associated with this item
    /// should be the aggregate of the tags for the children? 
    /// </summary>
    public string[] Tags { get; set; }

    public TaxCategory? TaxCategory { get; set; }

    public LineItem()
    {
        
    }

    /// <summary>
    /// If selling unit is not defined will fall back to cost information
    /// </summary>
    /// <returns></returns>
    public Measurement GuessSellingUnit()
    {
        if (SellingUnit != null) return SellingUnit;

        // selling unit not defined, try to figure out using costs
        // for now assumes all the costs are using the same UOM 
        var minQtty = Costs?.OrderBy(x => x.MinimumQuantity ?? 0).FirstOrDefault();
        if (minQtty != null)
        {
            return new Measurement
            {
                UOM = minQtty.UOM,
                Units = minQtty.MinimumQuantity ?? 0 // ???
            };
        }

        return null;
    }

    private IResult AdjustQuantity()
    {
        SellingUnit ??= GuessSellingUnit();

        SetWarning(LineItemWarning.NoSellingUnit, SellingUnit != null);
        if (SellingUnit == null) return Result.Error("No selling unit found");

        var hasQuantity = Quantity?.Units > 0;
        SetWarning(LineItemWarning.NoQuantity, hasQuantity);
        if (!hasQuantity) return Result.Error("No quantity found");

        var adjQuantity = Quantity.Convert(SellingUnit.UOM);
        SetWarning(LineItemWarning.QuantityError, adjQuantity != null);
        if (adjQuantity == null) return Result.Error("Couldn't calculate adjusted quantity (UOM)");

        // waste factor
        var units = WasteFactor.HasValue ? adjQuantity.Units * (100 + WasteFactor.Value) / 100 : adjQuantity.Units;

        // selling unit
        if (SellingUnit.Units > 0) units = Math.Ceiling(units / SellingUnit.Units) * SellingUnit.Units;

        AdjustedQuantity = new Measurement
        {
            Units = units,
            UOM = adjQuantity.UOM,
        };

        return Result.Success(AdjustedQuantity);
    }

    private bool CalculateActualWasteFactor()
    {
        var baseQuantity = Quantity.Convert(AdjustedQuantity.UOM);
        if (baseQuantity == null)
        {
            // couldn't convert
            return false;
        }

        if (baseQuantity.Units >= AdjustedQuantity.Units)
        {
            // no waste
            // ActualWasteFactor = 0; // ???
            return true;
        }

        ActualWasteFactor = (AdjustedQuantity.Units - baseQuantity.Units) * 100 / baseQuantity.Units;

        return true;
    }

    /// <summary>
    /// Update Price
    /// </summary>
    /// <returns>null if success, or error</returns>
    private IResult UpdatePrice()
    {
        // cost
        Cost = Costs?
            .Where(x =>
                x.UOM == AdjustedQuantity.UOM &&
                (!x.MinimumQuantity.HasValue || x.MinimumQuantity.Value <= AdjustedQuantity.Units)
            )
            .OrderByDescending(x => x.UnitCost)
            .FirstOrDefault();

        SetWarning(LineItemWarning.NoCost, Cost != null);
        if (Cost == null) return Result.Error("Couldn't determine unit cost");

        UnitCost = Cost.UnitCost;
        TotalCost = Cost.UnitCost * AdjustedQuantity.Units;

        var hasMargin = Margin is >= 0 and < 100;
        SetWarning(LineItemWarning.NoMargin, hasMargin);
        if (!hasMargin) return Result.Error("No margin set");

        var unitPrice = 100 * UnitCost.Value / (100 - Margin.Value);
        UnitPrice = Math.Round(unitPrice, 2);
        TotalPrice = Math.Round(UnitPrice.Value * AdjustedQuantity.Units, 2);

        return Result.Success(TotalPrice);
    }

    /// <summary>
    /// Reset calculated fields (before starting)
    /// </summary>
    public void ResetCalculated()
    {
        AdjustedQuantity = null;
        ActualWasteFactor = null;
        UnitCost = null;
        UnitPrice = null;
        TotalCost = null;
        TotalPrice = null;
    }

    public IResult Recalculate()
    {
        ResetCalculated();

        var adjustedQuantity = AdjustQuantity();
        if (!adjustedQuantity.IsSuccess) return adjustedQuantity;

        // actual waste factor
        var calculateWasteFactor = CalculateActualWasteFactor();
        SetWarning(LineItemWarning.WasteFactorError, calculateWasteFactor);

        return UpdatePrice();
    }

    public void SetWarning(LineItemWarning warning, bool clear = false)
    {
        if (!clear)
        {
            Warnings ??= [];
            Warnings.Add(warning.ToString());
        }
        else
        {
            if (Warnings == null) return;
            Warnings.Remove(warning.ToString());
            if (Warnings.IsEmpty()) Warnings = null;
        }
    }

    public bool IsNonTaxable { get; set; }
}

[BsonCollection("fcb2b.WasteFactor")]
public class WasteFactorConfig : FlowObjectModel
{
    public const decimal DefaultWasteFactor = 15L;

    public override string ObjectType => "fcb2b.WasteFactor";

    public string ProductType { get; set; }
    public Guid? PatternTypeId { get; set; }

    public decimal? WasteFactor { get; set; }
    public decimal? MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    public UnitOfMeasurement? UOM { get; set; }

    public string StyleNumber { get; set; }
    public Guid? CatalogFeedId { get; set; }
    public Guid? ItemId { get; set; }

    public Guid CreatedBy { get; set; }
}