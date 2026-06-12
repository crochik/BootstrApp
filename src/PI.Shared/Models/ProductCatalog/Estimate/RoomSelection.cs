using System;
using System.Collections.Generic;
using System.Linq;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Extensions;
using PI.Shared.Models;

namespace PI.ProductCatalog.Models;

[BsonCollection("fcb2b.RoomSelection")]
public class RoomSelection : FlowObjectModel, ITaggable, ITaxable
{
    public const string ObjectTypeFullName = "otg.RoomSelection";

    public override string ObjectType => ObjectTypeFullName;

    /// <summary>
    /// Estimate session key (e.g. Appointment Id, ...) 
    /// </summary>
    public string SessionKey { get; set; }

    /// <summary>
    /// Bin Id (is it necessary since we use the hash for comparison?)
    /// </summary>
    public Guid BinId { get; set; }

    /// <summary>
    ///  Calculated hash to identify selections for the "same" bin during an estimate session
    /// </summary>
    [BsonElement]
    public string Hash => CalculateHash(sessionKey: SessionKey, roomIds: RoomIds);

    public string ProjectExternalId { get; set; }
    public string ProductType { get; set; }

    public Guid[] RoomIds { get; set; }
    public Guid CreatedBy { get; set; }

    /// <summary>
    /// Main Item id
    /// </summary>
    public Guid? ItemId { get; set; }

    /// <summary>
    /// Rule Set used to create this estimate 
    /// </summary>
    public Guid? RuleSetId { get; set; }

    /// <summary>
    /// Template 
    /// </summary>
    public Guid? TemplateId { get; set; }

    /// <summary>
    /// Style Number for Main Item   
    /// </summary>
    public string StyleNumber { get; set; }

    /// <summary>
    /// Catalog Feed for the Main Item
    /// </summary>
    public Guid? CatalogFeedId { get; set; }

    /// <summary>
    /// Bin name (used?)
    /// </summary>
    public string Bin { get; set; }

    public Guid? InstallationTypeId { get; set; }
    public Guid? PatternTypeId { get; set; }
    public Guid? TrimWorkId { get; set; }
    public Guid? UnderlaymentId { get; set; }
    
    public Guid? SubfloorPrepId { get; set; }
    public Guid? StairsRiserFinishId { get; set; }

    public Measurement Area { get; set; }
    public Measurement Perimeter { get; set; }
    public LineItem[] LineItems { get; set; }

    /// <summary>
    /// Waste factor, initially defined by area of room/rules 
    /// </summary>
    public decimal? WasteFactor { get; set; }

    /// <summary>
    /// Main Product Quantity
    /// </summary>
    public Measurement MainProductQuantity { get; set; }

    public decimal? TotalCost { get; set; }
    public decimal? TotalPrice { get; set; }
    public decimal? TotalTax { get; set; }
    public decimal? BlendedMargin { get; set; }
    public bool HasWarnings { get; set; }

    public string[] Tags { get; set; }

    public IEnumerable<Guid> EstimateOptionIds
    {
        get
        {
            if (InstallationTypeId.HasValue) yield return InstallationTypeId.Value;
            if (PatternTypeId.HasValue) yield return PatternTypeId.Value;
            if (TrimWorkId.HasValue) yield return TrimWorkId.Value;
            if (UnderlaymentId.HasValue) yield return UnderlaymentId.Value;
            if (SubfloorPrepId.HasValue) yield return SubfloorPrepId.Value;
            if (StairsRiserFinishId.HasValue) yield return StairsRiserFinishId.Value;
        }
    }

    public static string CalculateHash(string sessionKey, Guid[] roomIds)
    {
        return string.Join("_", new[]
                {
                    sessionKey,
                }
                .Concat(roomIds.Select(x => x.ToString()).Order())
            )
            .CalculateMD5Hash()
            .ToString();
    }

    public bool IsNonTaxable { get; set; }
}

public static class RoomSelectionExtensions
{
    public static void RecalculateAll(this RoomSelection selection, List<LineItem> lineItems, Dictionary<Guid, CatalogItem> items)
    {
        if (selection.ItemId.HasValue && items != null)
        {
            // recalculate main product quantity in case the waste factor changed
            if (!items.TryGetValue(selection.ItemId.Value, out var mainItem))
            {
                throw new Exception("Couldn't find main item");
            }

            var mainLineItem = lineItems.FirstOrDefault(x => x.ItemId == mainItem.Id);
            if (mainLineItem == null)
            {
                throw new Exception("Couldn't find main item");
            }

            mainLineItem.ResetCalculated();
            if (mainLineItem.Criteria == QuantityCriteria.RoomArea)
            {
                mainLineItem.Quantity = selection.Area;
            }

            mainLineItem.Recalculate();
            selection.MainProductQuantity = mainLineItem.AdjustedQuantity;
        }

        selection.Recalculate(lineItems, true);
    }

    public static void Recalculate(this RoomSelection selection, List<LineItem> lineItems, bool resetQuantities = false)
    {
        selection.LineItems = lineItems.ToArray();
        selection.UpdateQuantities(resetQuantities);
        selection.RecalculateTotals();
        selection.HasWarnings = selection.LineItems.Any(x => x.Warnings?.Count > 0);
    }

    public static void RecalculateTotals(this RoomSelection selection)
    {
        selection.TotalCost = 0;
        selection.TotalPrice = 0;
        selection.BlendedMargin = null;

        // TODO:...
        selection.TotalTax = null;
        foreach (var item in selection.LineItems)
        {
            selection.TotalCost += item.TotalCost ?? 0;
            selection.TotalPrice += item.TotalPrice ?? 0;

            // TODO:...
            // selection.TotalTax += item.TotalTax ?? 0;
        }

        selection.BlendedMargin = selection.TotalPrice > 0 ? Math.Round(100 * (selection.TotalPrice.Value - (selection.TotalCost ?? 0)) / selection.TotalPrice.Value, 2) : null;
    }

    private static void UpdateQuantities(this RoomSelection selection, bool resetQuantities = false)
    {
        foreach (var lineItem in selection.LineItems)
        {
            lineItem.ResetCalculated();

            // if (!items.TryGetValue(lineItem.ItemId, out var item))
            // {
            //     lineItem.AddWarning("Item no longer available");
            //     continue;
            // }

            if (resetQuantities)
            {
                // quantity 
                lineItem.Quantity = lineItem.Criteria switch
                {
                    QuantityCriteria.Arbitrary or QuantityCriteria.Custom => lineItem.Quantity,
                    QuantityCriteria.RoomArea => selection.Area,
                    QuantityCriteria.MainProductArea => selection.MainProductQuantity,
                    QuantityCriteria.Perimeter => selection.Perimeter,
                    _ => lineItem.Quantity,
                };
            }

            var hasQuantity = lineItem.Quantity?.Units > 0;
            lineItem.SetWarning(LineItemWarning.NoQuantity, hasQuantity);
            if (!hasQuantity) continue;

            lineItem.Recalculate();
        }
    }
}