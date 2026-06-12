using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.ProductCatalog.Models;
using PI.ProductCatalog.Services;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;
using PI.Shared.Salesforce.Models;
using Measurement = PI.ProductCatalog.Models.Measurement;

namespace Services;

public class EstimateService(ILogger<EstimateService> logger, MongoConnection connection, ObjectTypeService objectTypeService, TaxService taxService)
{
    public async Task<Result<Estimate>> GetEstimateAsync(IEntityContext context, Guid? estimateId)
    {
        var estimate = estimateId.HasValue
            ? await connection.Filter<Estimate>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.EntityId, context.OrganizationId.Value)
                .Eq(x => x.Id, estimateId)
                .FirstOrDefaultAsync()
            : null;

        if (estimate == null) return Result.Error<Estimate>("Estimate not found");

        // TODO: check whether can still be edited?
        // ...

        return Result.Success(estimate);
    }

    public async Task<Result<RoomSelection>> CreateRoomSelectionAsync(IEntityContext context, Guid templateId, Guid itemId)
    {
        var roomSelection = await connection.Filter<RoomSelection>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.OrganizationId.Value)
            .Eq(x => x.Id, templateId)
            .SortDesc(x => x.CreatedOn)
            .FirstOrDefaultAsync();

        if (roomSelection == null)
        {
            return Result.Error<RoomSelection>("Couldn't find template.");
        }

        var rooms = await connection.Filter<AbstractRoom>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .In(x => x.Id, roomSelection.RoomIds)
            .FindAsync();

        // try to find one for the same product type
        var item = await connection.Filter<CatalogItem>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.OrganizationId.Value)
            .Eq(x => x.Id, itemId)
            .FirstOrDefaultAsync();

        if (item == null || !ProductTypeResolver.TryResolve(item, out var productType) || !productType.HasValue)
        {
            return Result.Error<RoomSelection>("Catalog Item doesn't exist or couldn't determine product type");
        }

        roomSelection.CreatedOn = DateTime.UtcNow;
        roomSelection.LastActor = context.Actor();
        roomSelection.LastModifiedOn = DateTime.UtcNow;

        roomSelection.Name = $"{item.StyleName ?? item.StyleNumber ?? item.Name} - {roomSelection.Name}";
        // roomSelection.Description

        roomSelection.TemplateId = roomSelection.Id;
        roomSelection.Id = Guid.NewGuid();

        roomSelection.ItemId = item.Id;
        roomSelection.CatalogFeedId = item.CatalogFeedId;
        roomSelection.StyleNumber = item.StyleNumber;

        var productTypeCode = Enum.TryParse<ProductType>(roomSelection.ProductType, out var code) ? code : default(ProductType?);
        
        decimal? totalSqft = 0;
        var children = new List<ChildLineItem>();
        foreach (var room in rooms)
        {
            var area = room.GetAreaForProductType(productTypeCode);
            var wasteFactorConfig = await GetWasteFactorConfig(context, roomSelection, item, area);
            var child = new ChildLineItem
            {
                Name = room.Name,
                Description = room.Description,
                Quantity = area,
                WasteFactor = wasteFactorConfig?.WasteFactor ?? WasteFactorConfig.DefaultWasteFactor,
            };

            if (area.ConvertTo(UnitOfMeasurement.SqFt, out var sqFtArea))
            {
                if (totalSqft != null) totalSqft += sqFtArea.Units;

                var units = child.WasteFactor.HasValue ? sqFtArea.Units * (100 + child.WasteFactor.Value) / 100 : sqFtArea.Units;
                child.AdjustedQuantity = new Measurement
                {
                    Units = units,
                    UOM = UnitOfMeasurement.SqFt
                };
            }
            else
            {
                totalSqft = null;
            }

            children.Add(child);
        }

        // calculate average waste factor 
        var adjustedQuantity = totalSqft.HasValue
            ? new Measurement
            {
                Units = children.Sum(x => x.AdjustedQuantity.Units),
                UOM = UnitOfMeasurement.SqFt,
            }
            : null;

        // Add main (line) item 
        var mainLineItem = new LineItem
        {
            ItemId = item.Id,
            Name = item.Name,
            SKU = item.SKU,
            Description = item.Description,
            Source = LineItemSource.Item,
            Costs = item.Costs,
            SellingUnit = item.SellingUnit,
            Margin = item.Margin,
            Criteria = QuantityCriteria.RoomArea,
            Children = children.ToArray(),
            TaxCategory = item.GetTaxCategory(),
            Quantity = totalSqft.HasValue
                ? new Measurement
                {
                    Units = totalSqft.Value,
                    UOM = UnitOfMeasurement.SqFt,
                }
                : null,
            AdjustedQuantity = adjustedQuantity, // will be overriden on recalculate anyway
            WasteFactor = totalSqft.HasValue ? 100 * (adjustedQuantity.Units - totalSqft.Value) / totalSqft.Value : null,
        };

        mainLineItem.Recalculate();

        // apply main quantity to line items
        roomSelection.MainProductQuantity = mainLineItem.AdjustedQuantity;
        foreach (var lineItem in roomSelection.LineItems)
        {
            if (lineItem.Criteria != QuantityCriteria.MainProductArea) continue;
            if (lineItem.Children?.Length > 0)
            {
                // TODO: adjust children
                // ...
            }

            lineItem.Quantity = roomSelection.MainProductQuantity;
        }

        roomSelection.Recalculate(roomSelection.LineItems.Append(mainLineItem).ToList(), false);

        roomSelection = await objectTypeService.InsertAsync(context, roomSelection, e =>
        {
            e.Description = "Cloned from selection for same product type";
            e.Action = "ObjectCloned";
        });

        return Result.Success(roomSelection);
    }

    private async Task<WasteFactorConfig> GetWasteFactorConfig(IEntityContext context, RoomSelection selection, CatalogItem mainItem, Measurement roomArea)
    {
        var sqftArea = roomArea.Convert(UnitOfMeasurement.SqFt).Units;

        var wasteFactorConfigs = await connection.Filter<WasteFactorConfig>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .In(x => x.EntityId, [context.AccountId.Value, context.EntityId.Value])
            .Eq(x => x.ProductType, selection.ProductType)
            .In(x => x.PatternTypeId, [selection.PatternTypeId, null])
            .In(x => x.UOM, [null, UnitOfMeasurement.SqFt])
            .In(x => x.CatalogFeedId, [null, mainItem.CatalogFeedId])
            .In(x => x.StyleNumber, [null, mainItem.StyleNumber])
            .In(x => x.ItemId, [null, mainItem.Id])
            .Where(x => x.MinQuantity == null || x.MinQuantity <= sqftArea)
            .Where(x => x.MaxQuantity == null || x.MaxQuantity >= sqftArea)
            .FindAsync();

        wasteFactorConfigs.Sort(compare);
        return wasteFactorConfigs.FirstOrDefault();

        int compare(WasteFactorConfig l, WasteFactorConfig r)
        {
            if (l.EntityId != l.AccountId)
            {
                // left is org level, right no 
                if (r.EntityId == r.AccountId) return -1;
            }
            else
            {
                // right is org level, left no
                if (r.EntityId != r.AccountId) return +1;
            }

            var lscore = (l.MinQuantity.HasValue ? 1 : 0) + (l.MaxQuantity.HasValue ? 1 : 0);
            var rscore = (r.MinQuantity.HasValue ? 1 : 0) + (r.MaxQuantity.HasValue ? 1 : 0);
            if (lscore == rscore) return 0;

            return rscore - lscore;
        }
    }

    public async Task<Result<RoomSelection>> GetRoomSelectionAsync(IEntityContext context, Guid? selectionId)
    {
        var roomSelection = selectionId.HasValue
            ? await connection.Filter<RoomSelection>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.EntityId, context.OrganizationId.Value)
                .Eq(x => x.Id, selectionId)
                .FirstOrDefaultAsync()
            : null;

        if (roomSelection == null) return Result.Error<RoomSelection>("Selection not found");

        // TODO: check whether can still be edited?
        // ...

        return Result.Success(roomSelection);
    }

    public async Task<IResult> RemoveItemAsync(IEntityContext context, RoomSelection roomSelection, RemoveItemRequest request)
    {
        if (roomSelection.ItemId == request.ItemId && request.Source == LineItemSource.Item)
        {
            return Result.Error("Can't remove the main item on this estimate.");
        }

        var result = RemoveItem(request, roomSelection.LineItems);
        if (result.IsError || result.IsUnknown) return result;

        return await UpdateLineItemsAsync(context, roomSelection, result.Value);
    }

    public async Task<IResult> AddItemAsync(IEntityContext context, RoomSelection roomSelection, AddItemRequest request)
    {
        var loadRooms = request.QuantityCriteria switch
        {
            QuantityCriteria.Arbitrary or QuantityCriteria.Custom => false,
            _ => true,
        };

        var rooms = loadRooms
            ? await connection.Filter<AbstractRoom>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .In(x => x.Id, roomSelection.RoomIds)
                // Parent ... 
                // Org ... 
                .FindAsync()
            : null;

        var children = rooms?
            .Select(x => new ChildLineItem
            {
                Name = x.Name,
                Quantity = request.QuantityCriteria switch
                {
                    QuantityCriteria.RoomArea or QuantityCriteria.MainProductArea => x.AdjustedArea ?? x.TotalArea,
                    QuantityCriteria.Perimeter => x switch
                    {
                        RegularRoom room => room.Perimeter,
                        _ => null,
                    },
                },
                // WasteFactor =
                // AdjustedQuantity = 
                // Description = 
            })
            .Where(x => x.Quantity != null) // exclude rooms w/o qtty
            .ToArray();

        if (request.QuantityCriteria == QuantityCriteria.MainProductArea && roomSelection.ItemId.HasValue)
        {
            var mainLineItem = roomSelection.LineItems.FirstOrDefault(x => x.ItemId == roomSelection.ItemId);
            if (mainLineItem == null)
            {
                return Result.Error("Couldn't find Main Line Item");
            }

            // copy children and quantity
            children = mainLineItem.Children.Select(x => new ChildLineItem
            {
                Name = x.Name,
                Description = x.Description,
                Quantity = x.Quantity,
                AdjustedQuantity = x.AdjustedQuantity,
                WasteFactor = x.WasteFactor,
            }).ToArray();
        }

        // arbitrary
        var result = await CreateItemAsync(context, request, children);
        if (result.IsError) return result;

        var lineItems = (roomSelection.LineItems ?? []).Append(result.Value).ToArray();

        return await UpdateLineItemsAsync(context, roomSelection, lineItems);
    }

    public async Task<IResult> UpdateBlendedMarginAsync(IEntityContext context, RoomSelection roomSelection, BlendedMarginRequest request)
    {
        var result = UpdateMargin(context, roomSelection.LineItems, request);
        if (result.IsError) return result;

        return await UpdateLineItemsAsync(context, roomSelection, result.Value);
    }

    public async Task<IResult> UpdateBlendedMarginAsync(IEntityContext context, Estimate estimate, BlendedMarginRequest request)
    {
        var result = UpdateMargin(context, estimate.LineItems, request);
        if (result.IsError) return result;

        return await UpdateLineItemsAsync(context, estimate, result.Value);
    }

    private Result<LineItem[]> UpdateMargin(IEntityContext context, LineItem[] lineItems, BlendedMarginRequest request)
    {
        if (!request.BlendedMargin.HasValue || request.BlendedMargin.Value < 0 || request.BlendedMargin.Value >= 100)
        {
            return Result.Error<LineItem[]>("Blended Margin has to be between 0 and 99.99%");
        }

        var margin = request.BlendedMargin.Value;
        foreach (var lineItem in lineItems)
        {
            lineItem.Margin = margin;
            lineItem.Recalculate();
        }

        return Result.Success(lineItems);
    }

    public async Task<IResult> AddItemAsync(IEntityContext context, RoomSelection roomSelection, AddFreightRequest request)
    {
        var result = await CreateItemAsync(context, roomSelection, request);
        if (result.IsError) return result;

        var lineItems = (roomSelection.LineItems ?? []).Append(result.Value).ToArray();

        return await UpdateLineItemsAsync(context, roomSelection, lineItems);
    }

    public async Task<IResult> AddItemAsync(IEntityContext context, Estimate estimate, AddFreightRequest request)
    {
        var result = await CreateItemAsync(context, estimate, request);
        if (result.IsError) return result;

        var lineItems = (estimate.LineItems ?? []).Append(result.Value).ToArray();

        return await UpdateLineItemsAsync(context, estimate, lineItems);
    }

    public async Task<IResult> SetDiscountAsync(IEntityContext context, Estimate estimate, SetDiscountRequest request)
    {
        var totals = CalculateTotals(estimate.LineItems, estimate.TaxRates, estimate.IsNonTaxable);

        // TODO: other categories?
        // services vs labor
        // sales vs use
        // ...
        var list = new[]
        {
            // products
            new DiscountParms { Percentage = request.Sales, Amount = request.SalesAmount, Category = TaxCategory.Sales },
            new DiscountParms { Percentage = request.Sales, Category = TaxCategory.Freight },
            // new DiscountParms { Percentage = request.Sales, Amount = request.SalesAmount, Category = TaxCategory.Use },
            // service
            new DiscountParms { Percentage = request.Service, Amount = request.ServiceAmount, Category = TaxCategory.Service },
            // new DiscountParms { Percentage = request.Service, Category = TaxCategory.Labor },
        };

        var discount = new List<DiscountRate>();
        foreach (var p in list)
        {
            var discountRate = calculateRate(p.Percentage, p.Amount, p.Category);
            if (discountRate.IsError) return discountRate;
            if (discountRate.IsSuccess) discount.Add(discountRate.Value);
        }

        if (discount.IsEmpty())
        {
            estimate.Discounts = null;
            estimate.DiscountPrice = null;
            estimate.DiscountTax = null;
        }
        else
        {
            decimal priceDiscount = 0;
            decimal taxDiscount = 0;

            foreach (var x in discount)
            {
                var category = x.Category;

                // discount on category
                var categoryPriceDiscount = (totals.TotalsPerCategory[category] * x.Rate) / 100;

                // how the portion that is taxable is affected by the discount
                var taxLiability = estimate.TaxRates.TaxLiabilities.Where(t => t.Category == category).ToArray();
                var categoryTaxableDiscount = (totals.TaxableTotals[category] * x.Rate) / 100;
                var categoryTaxDiscount = taxLiability.Sum(t => t.Amount * categoryTaxableDiscount);

                x.Amount = categoryPriceDiscount;
                priceDiscount += categoryPriceDiscount;
                taxDiscount += categoryTaxDiscount;
            }

            priceDiscount = Math.Round(priceDiscount, 2);
            taxDiscount = Math.Round(taxDiscount, 2);

            estimate.Discounts =
            [
                new Discount
                {
                    CreatedOn = DateTime.UtcNow,
                    LastActor = context.Actor(),
                    Name = "Discount",
                    // Description = "Appreciation",
                    DiscountsRates = discount.ToArray(),
                    PriceDiscount = priceDiscount,
                    TaxDiscount = taxDiscount,
                }
            ];

            estimate.DiscountPrice = priceDiscount;
            estimate.DiscountTax = taxDiscount;
        }

        var query = connection.Filter<Estimate>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.OrganizationId.Value)
            .Eq(x => x.Id, estimate.Id)
            .Eq(x => x.LastModifiedOn, estimate.LastModifiedOn)
            .Ne(x => x.IsActive, false)
            .Update
            .Set(x => x.Discounts, estimate.Discounts)
            .Set(x => x.DiscountPrice, estimate.DiscountPrice)
            .Set(x => x.DiscountTax, estimate.DiscountTax)
            .SetOrUnset(x => x.GrandTotal, estimate.GrandTotal)
            .SetOrUnset(x => x.GrandTax, estimate.GrandTax)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor());

        estimate = await query.UpdateAndGetOneAsync();

        // TODO: fire event(s)
        // - fire update
        // - should it start flow ?
        // ...

        // estimate = await objectTypeService.InsertAsync(context, estimate, e =>
        // {
        //     e.Action = "ObjectCreated";
        //     e.Description = "Estimated Created";
        // });

        return Result.Success(estimate);

        Result<DiscountRate> calculateRate(decimal? percentage, decimal? amount, TaxCategory category)
        {
            if (percentage is <= 0) percentage = null;
            if (amount is <= 0) amount = null;
            if (percentage.HasValue && amount.HasValue) return Result.Error<DiscountRate>("Only specify percentage or amount");

            if (percentage.HasValue)
            {
                if (percentage > 100) return Result.Error<DiscountRate>("Invalid Discount Rate");
                return Result.Success(new DiscountRate
                {
                    Category = category,
                    Rate = percentage.Value,
                });
            }

            if (amount.HasValue && totals.TotalsPerCategory.TryGetValue(category, out var categoryTotal))
            {
                if (amount.Value > categoryTotal)
                {
                    return Result.Error<DiscountRate>($"Discount Amount must be less than {categoryTotal:C}");
                }

                return Result.Success(new DiscountRate
                {
                    Category = category,
                    Rate = 100 * amount.Value / categoryTotal,
                });
            }

            return Result.Unknown<DiscountRate>("ignore");
        }
    }

    public async Task<IResult> AddSectionAsync(IEntityContext context, Estimate estimate, AddSectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return Result.Error("Missing Name");
        if (string.IsNullOrWhiteSpace(request.Content)) return Result.Error("Missing Content");

        request.Name = request.Name.Trim();
        var existing = estimate.Sections?.FirstOrDefault(x => x.Name == request.Name);
        if (existing != null) return Result.Error("Can't add two sections with the same name.");

        estimate.Sections = (estimate.Sections ?? Enumerable.Empty<EstimateSection>())
            .Append(new EstimateSection
            {
                Name = request.Name.Trim(),
                Content = request.Content.Trim(),
                ContentType = request.ContentType ?? "text/plain",
                Position = request.Position ?? SectionPosition.After,
            })
            .ToArray();

        return await SaveSectionsAsync(context, estimate);
    }

    public async Task<IResult> EditSectionAsync(IEntityContext context, Estimate estimate, EditSectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return Result.Error("Missing Name");
        if (string.IsNullOrWhiteSpace(request.Content)) return Result.Error("Missing Content");

        request.Name = request.Name.Trim();
        var existing = estimate.Sections?.FirstOrDefault(x => x.Name == request.Name);
        if (existing == null) return Result.Error("Section not found.");

        existing.Content = request.Content.Trim();
        existing.ContentType = request.ContentType ?? "text/plain";

        return await SaveSectionsAsync(context, estimate);
    }

    public async Task<IResult> RemoveSectionAsync(IEntityContext context, Estimate estimate, RemoveSectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return Result.Error("Missing Name");

        request.Name = request.Name.Trim();
        var existing = estimate.Sections?.FirstOrDefault(x => x.Name == request.Name);
        if (existing == null) return Result.Error("Section not found.");

        estimate.Sections = estimate.Sections.Where(x => x != existing).ToArray();

        return await SaveSectionsAsync(context, estimate);
    }

    private async Task<IResult> SaveSectionsAsync(IEntityContext context, Estimate estimate)
    {
        if (estimate.Sections?.IsEmpty() ?? false)
        {
            estimate.Sections = null;
        }

        var query = connection.Filter<Estimate>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.OrganizationId.Value)
            .Eq(x => x.Id, estimate.Id)
            .Eq(x => x.LastModifiedOn, estimate.LastModifiedOn)
            .Ne(x => x.IsActive, false)
            .Update
            .SetOrUnset(x => x.Sections, estimate.Sections)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor());

        estimate = await query.UpdateAndGetOneAsync();

        // TODO: fire event(s)
        // - fire update
        // - should it start flow ?
        // ...

        // estimate = await objectTypeService.InsertAsync(context, estimate, e =>
        // {
        //     e.Action = "ObjectCreated";
        //     e.Description = "Estimated Created";
        // });

        return Result.Success(estimate);
    }

    private async Task<Result<LineItem>> CreateItemAsync(IEntityContext context, RoomSelection roomSelection, AddFreightRequest request)
    {
        var item = await connection.Filter<CatalogItem>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.OrganizationId.Value)
            .Eq(x => x.Id, request.ItemId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (item?.Costs == null) return Result.Error<LineItem>("No Price information");

        var uom = item.SellingUnit?.UOM ?? item.Costs.FirstOrDefault()?.UOM;
        if (uom == null) return Result.Error<LineItem>("Missing UOM");

        var qtty = uom switch
        {
            UnitOfMeasurement.SqFt or UnitOfMeasurement.SqYd => roomSelection.MainProductQuantity,
            UnitOfMeasurement.Each => new Measurement
            {
                Units = 1,
                UOM = UnitOfMeasurement.Each,
            },
            _ => null,
        };

        var lineItem = new LineItem
        {
            ItemId = item.Id,
            Name = item.Name,
            SKU = item.SKU,
            Description = item.Description,
            Source = LineItemSource.Freight,
            Costs = item.Costs,
            SellingUnit = item.SellingUnit,
            Quantity = qtty,
            WasteFactor = null,
            Margin = item.Margin,
            Criteria = QuantityCriteria.Custom,
            TaxCategory = TaxCategory.Freight,
        };

        var recalc = lineItem.Recalculate();
        return recalc.IsError ? Result.Error<LineItem>(recalc.Status) : Result.Success(lineItem);
    }

    private async Task<Result<LineItem>> CreateItemAsync(IEntityContext context, Estimate estimate, AddFreightRequest request)
    {
        var item = await connection.Filter<CatalogItem>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.OrganizationId.Value)
            .Eq(x => x.Id, request.ItemId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (item?.Costs == null) return Result.Error<LineItem>("No Price information");

        var uom = item.SellingUnit?.UOM ?? item.Costs.FirstOrDefault()?.UOM;
        if (uom == null) return Result.Error<LineItem>("Missing UOM");

        var qtty = uom switch
        {
            UnitOfMeasurement.SqFt or UnitOfMeasurement.SqYd => new Measurement
            {
                Units = estimate.LineItems
                    .Where(x => x.Source == LineItemSource.Item)
                    .Select(x => ((x.AdjustedQuantity ?? x.Quantity)?.ConvertTo(UnitOfMeasurement.SqFt, out var dst) ?? false) ? dst : null)
                    .Sum(x => x?.Units ?? 0),
                UOM = UnitOfMeasurement.SqFt,
            },
            UnitOfMeasurement.Each => new Measurement
            {
                Units = 1,
                UOM = UnitOfMeasurement.Each,
            },
            _ => null,
        };

        var lineItem = new LineItem
        {
            ItemId = item.Id,
            Name = item.Name,
            SKU = item.SKU,
            Description = item.Description,
            Source = LineItemSource.Freight,
            Costs = item.Costs,
            SellingUnit = item.SellingUnit,
            Quantity = qtty,
            WasteFactor = null,
            Margin = item.Margin,
            Criteria = QuantityCriteria.Custom,
            TaxCategory = TaxCategory.Freight,
        };

        var recalc = lineItem.Recalculate();
        return recalc.IsError ? Result.Error<LineItem>(recalc.Status) : Result.Success(lineItem);
    }

    private async Task<Result<LineItem>> CreateItemAsync(IEntityContext context, AddItemRequest request, ChildLineItem[] children = null)
    {
        var item = await connection.Filter<CatalogItem>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.OrganizationId.Value)
            .Eq(x => x.Id, request.ItemId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (item?.Costs == null) return Result.Error<LineItem>("No Price information");

        var quantity = request.Quantity.HasValue
            ? new Measurement
            {
                Units = request.Quantity.Value,
                UOM = request.UOM ?? item.SellingUnit.UOM,
            }
            : null;

        decimal? wasteFactor = null;

        if (children?.Length > 0)
        {
            var uom = request.QuantityCriteria switch
            {
                QuantityCriteria.MainProductArea or QuantityCriteria.RoomArea => UnitOfMeasurement.SqFt,
                QuantityCriteria.Perimeter => UnitOfMeasurement.Feet,
                _ => default(UnitOfMeasurement?),
            };
            
            decimal? quantityTotal = uom.HasValue ? 0 : null;
            decimal adjustedQuantity = 0;

            if (quantityTotal != null)
            {
                foreach (var child in children)
                {
                    if (child.Quantity == null || !child.Quantity.ConvertTo(uom.Value, out var qtty))
                    {
                        quantityTotal = null;
                        break;
                    }

                    quantityTotal += qtty.Units;
                    var aQtty = qtty.Units;
                    if (child.AdjustedQuantity != null && child.AdjustedQuantity.ConvertTo(uom.Value, out var t))
                    {
                        aQtty = t.Units;
                    }

                    adjustedQuantity += aQtty;
                }

                if (quantityTotal.HasValue)
                {
                    quantity = new Measurement
                    {
                        Units = adjustedQuantity,
                        UOM = uom.Value,
                    };

                    wasteFactor = adjustedQuantity > 0 && quantityTotal.Value != 0 ? 100 * (adjustedQuantity - quantityTotal.Value) / quantityTotal.Value : 0;
                }
            }
        }

        var lineItem = new LineItem
        {
            ItemId = item.Id,
            Name = item.Name,
            SKU = item.SKU,
            Description = item.Description,
            Source = request.Source,
            Costs = item.Costs,
            SellingUnit = item.SellingUnit,
            Quantity = quantity,
            WasteFactor = wasteFactor,
            Margin = item.Margin,
            Criteria = request.QuantityCriteria,
            Children = children,
            TaxCategory = item.GetTaxCategory(),
        };

        var recalc = lineItem.Recalculate();
        return recalc.IsError ? Result.Error<LineItem>(recalc.Status) : Result.Success(lineItem);
    }

    public async Task<IResult> SwitchItemAsync(IEntityContext context, RoomSelection roomSelection, SwitchItemRequest request)
    {
        var result = await ReplaceItemAsync(context, roomSelection.LineItems, request);
        if (result.IsError) return result;

        var item = result.Value.Item;
        var updateMainItem = roomSelection.ItemId == request.CurrentItemId && request.Source == LineItemSource.Item;
        if (!updateMainItem) return await UpdateLineItemsAsync(context, roomSelection, result.Value.LineItems);

        // update main item
        roomSelection.ItemId = item.Id;
        roomSelection.Name = item.Name;
        roomSelection.Description ??= item.Description == item.Name ? null : item.Description;

        return await UpdateLineItemsAsync(context, roomSelection, result.Value.LineItems, true);
    }

    private async Task<Result<(LineItem[] LineItems, CatalogItem Item)>> ReplaceItemAsync(IEntityContext context, LineItem[] currLineItems, SwitchItemRequest request)
    {
        var item = await connection.Filter<CatalogItem>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.OrganizationId.Value)
            .Eq(x => x.Id, request.ItemId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        var lineItems = new List<LineItem>();
        foreach (var x in currLineItems ?? [])
        {
            if (x.Source != request.Source || x.ItemId != request.CurrentItemId)
            {
                lineItems.Add(x);
                continue;
            }

            var lineItem = new LineItem
            {
                ItemId = item.Id,
                Name = item.Name,
                SKU = item.SKU,
                Description = item.Description,
                Source = request.Source,
                Costs = item.Costs,
                SellingUnit = item.SellingUnit,
                Quantity = x.Quantity,
                WasteFactor = x.WasteFactor,
                Margin = x.Margin,
                Criteria = x.Criteria,
                Tags = x.Tags,
                TaxCategory = x.TaxCategory,
                Children = x.Children,
                Warnings = x.Warnings, // copy warnings?

                // these will be recalculated 
                AdjustedQuantity = null,
                ActualWasteFactor = null,
                UnitCost = null,
                UnitPrice = null,
                TotalCost = null,
                TotalPrice = null,
                Cost = null,
            };

            var recalc = lineItem.Recalculate();

            if (recalc.IsError)
            {
                return Result.Error<(LineItem[], CatalogItem)>(lineItem.Warnings != null ? string.Join("; ", lineItem.Warnings) : recalc.Status);
            }

            // assume this was the user explicitly selecting a color
            lineItem.SetWarning(LineItemWarning.UndefinedColor, true);

            lineItems.Add(lineItem);
        }

        return Result.Success((lineItems.ToArray(), item));
    }

    public async Task<IResult> EditItemAsync(IEntityContext context, RoomSelection roomSelection, EditItemRequest request)
    {
        var result = EditItem(roomSelection.LineItems, request);
        if (result.IsError) return result;

        return await UpdateLineItemsAsync(context, roomSelection, result.Value);
    }

    private Result<LineItem[]> EditItem(LineItem[] currLineItems, EditItemRequest request)
    {
        var lineItem = currLineItems.FirstOrDefault(x => x.ItemId == request.ItemId && x.Source == request.Source);
        if (lineItem == null) return Result.Error<LineItem[]>("Item not found");

        lineItem.Description = string.IsNullOrEmpty(request.Description) ? lineItem.Name : request.Description;

        if (request.Quantity.HasValue && request.Quantity.Value != lineItem.Quantity?.Units)
        {
            var uom = request.UOM ?? lineItem.Quantity?.UOM ?? lineItem.SellingUnit?.UOM ?? lineItem.Costs?.FirstOrDefault()?.UOM;
            if (!uom.HasValue) return Result.Error<LineItem[]>("Couldn't determine UOM");

            if (request.Quantity.Value <= 0) return Result.Error<LineItem[]>("Invalid Quantity");

            lineItem.Quantity = new Measurement
            {
                Units = request.Quantity.Value,
                UOM = uom.Value,
            };
        }

        if (request.WasteFactor.HasValue && request.WasteFactor.Value != lineItem.WasteFactor)
        {
            var wasteFactor = request.WasteFactor.Value;
            if (wasteFactor < 0 || wasteFactor >= 100) return Result.Error<LineItem[]>("Invalid Waste Factor");

            lineItem.WasteFactor = wasteFactor == 0 ? null : wasteFactor;
        }

        if (request.UnitCost.HasValue && request.UnitCost.Value != lineItem.UnitCost)
        {
            var unitCost = request.UnitCost.Value;
            if (unitCost < 0) return Result.Error<LineItem[]>("Invalid Unit Cost");

            lineItem.UnitCost = unitCost;
            lineItem.Cost = new ItemCost
            {
                Criteria = PriceCriteria.List,
                UOM = lineItem.Cost.UOM,
                MinimumQuantity = null,
                PackageCondition = null,
                UnitCost = unitCost,
                LocationId = null,
                Promotion = null,
                Allowances = null,
            };

            lineItem.Costs =
            [
                lineItem.Cost,
            ];
        }

        if (request.Margin.HasValue && request.Margin.Value != lineItem.Margin)
        {
            var margin = request.Margin.Value;
            if (margin <= 0 || margin >= 100) return Result.Error<LineItem[]>("Invalid Margin");

            lineItem.Margin = margin;
        }

        if (request.IsNonTaxable.HasValue && request.IsNonTaxable.Value != lineItem.IsNonTaxable)
        {
            lineItem.IsNonTaxable = request.IsNonTaxable.Value;
        }

        var recalc = lineItem.Recalculate();

        return recalc.IsError ? Result.Error<LineItem[]>(lineItem.Warnings != null ? string.Join("; ", lineItem.Warnings) : recalc.Status) : Result.Success(currLineItems);
    }

    public async Task<IResult> RemoveItemAsync(IEntityContext context, Estimate estimate, RemoveItemRequest request)
    {
        var result = RemoveItem(request, estimate.LineItems);
        if (result.IsError || result.IsUnknown) return result;

        return await UpdateLineItemsAsync(context, estimate, result.Value);
    }

    public async Task<IResult> AddItemAsync(IEntityContext context, Estimate estimate, AddItemRequest request)
    {
        var result = await CreateItemAsync(context, request);
        if (result.IsError) return result;

        var lineItems = (estimate.LineItems ?? []).Append(result.Value).ToArray();

        return await UpdateLineItemsAsync(context, estimate, lineItems);
    }

    public async Task<IResult> SwitchItemAsync(IEntityContext context, Estimate estimate, SwitchItemRequest request)
    {
        var result = await ReplaceItemAsync(context, estimate.LineItems, request);
        if (result.IsError) return result;

        return await UpdateLineItemsAsync(context, estimate, result.Value.LineItems);
    }

    public async Task<IResult> EditItemAsync(IEntityContext context, Estimate estimate, EditItemRequest request)
    {
        var result = EditItem(estimate.LineItems, request);
        if (result.IsError) return result;

        return await UpdateLineItemsAsync(context, estimate, result.Value);
    }

    private async Task<IResult> UpdateLineItemsAsync(IEntityContext context, Estimate estimate, LineItem[] lineItems)
    {
        estimate.LineItems = lineItems;

        var updateTaxRates = estimate.TaxRates == null;
        if (updateTaxRates)
        {
            var resolvedTaxRates = await taxService.ResolveForProjectAsync(context, estimate.ProjectExternalId);
            if (!resolvedTaxRates.IsSuccess) return resolvedTaxRates;

            estimate.TaxRates = resolvedTaxRates.Value; // may be null
        }

        var totals = CalculateTotals(lineItems, estimate.TaxRates, estimate.IsNonTaxable);
        estimate.TotalCost = totals.TotalCost;
        estimate.TotalPrice = totals.TotalPrice;
        estimate.BlendedMargin = totals.BlendedMargin;
        estimate.TaxLiabilities = totals.TaxLiabilities;
        estimate.TotalTax = totals.TotalTax;

        var query = connection.Filter<Estimate>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.OrganizationId.Value)
            .Eq(x => x.Id, estimate.Id)
            .Eq(x => x.LastModifiedOn, estimate.LastModifiedOn)
            .Ne(x => x.IsActive, false)
            .Update
            .Set(x => x.LineItems, estimate.LineItems)
            .Set(x => x.TotalCost, estimate.TotalCost)
            .Set(x => x.TotalPrice, estimate.TotalPrice)
            .Set(x => x.BlendedMargin, estimate.BlendedMargin)
            .Set(x => x.TaxLiabilities, estimate.TaxLiabilities)
            .Set(x => x.TotalTax, estimate.TotalTax)
            .Set(x => x.IsNonTaxable, estimate.IsNonTaxable)
            .SetOrUnset(x => x.GrandTotal, estimate.GrandTotal)
            .SetOrUnset(x => x.GrandTax, estimate.GrandTax)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor());

        if (updateTaxRates)
        {
            query.Set(x => x.TaxRates, estimate.TaxRates);
        }

        estimate = await query.UpdateAndGetOneAsync();

        // TODO: fire event(s)
        // - fire update
        // - should it start flow ?
        // ...

        // estimate = await objectTypeService.InsertAsync(context, estimate, e =>
        // {
        //     e.Action = "ObjectCreated";
        //     e.Description = "Estimated Created";
        // });

        return Result.Success(estimate);
    }

    private async Task<Result<RoomSelection>> UpdateLineItemsAsync(IEntityContext context, RoomSelection roomSelection, LineItem[] lineItems, bool updatedMainItem = false)
    {
        roomSelection.LineItems = lineItems;
        roomSelection.RecalculateTotals();

        var query = connection.Filter<RoomSelection>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.OrganizationId.Value)
            .Eq(x => x.Id, roomSelection.Id)
            .Eq(x => x.LastModifiedOn, roomSelection.LastModifiedOn)
            .Ne(x => x.IsActive, false)
            .Update
            .Set(x => x.LineItems, roomSelection.LineItems)
            .Set(x => x.TotalCost, roomSelection.TotalCost)
            .Set(x => x.TotalTax, roomSelection.TotalTax)
            .Set(x => x.TotalPrice, roomSelection.TotalPrice)
            .Set(x => x.BlendedMargin, roomSelection.BlendedMargin)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor());

        if (updatedMainItem)
        {
            query.Set(x => x.Name, roomSelection.Name)
                .Set(x => x.ItemId, roomSelection.ItemId)
                .SetOrUnset(x => x.Description, roomSelection.Description);
        }

        roomSelection = await query.UpdateAndGetOneAsync();

        if (roomSelection == null) return Result.Error<RoomSelection>("Failed to update estimate");

        // TODO: fire event(s)
        // - fire update
        // - should it start flow ?
        // ...

        return Result.Success(roomSelection);
    }

    private Result<LineItem[]> RemoveItem(RemoveItemRequest request, LineItem[] currLineItems)
    {
        var lineItems = currLineItems.Where(x => x.Source != request.Source || x.ItemId != request.ItemId).ToArray();
        var removed = currLineItems.Length - lineItems.Length;
        if (removed == 0)
        {
            // not found 
            return Result.Unknown<LineItem[]>("Nothing to remove");
        }

        return Result.Success(lineItems);
    }

    /// <summary>
    /// duplicate estimate ("Proposal")
    /// </summary>
    public async Task<Result<Estimate>> DuplicateAsync(IEntityContext context, Estimate original, string removeTag = null)
    {
        var objectType = await objectTypeService.GetAsync(context, Estimate.ObjectTypeFullName);
        if (objectType == null) return Result.Error<Estimate>("Object Type not found");

        if (!objectType.CanCreate(context)) return Result.Error<Estimate>("Access Forbidden");

        var counter = await IncrementEntityCounterAsync(context);
        original.EstimateNumber = $"{counter.Count}";
        original.Version = 1;
        original.Id = Guid.NewGuid();
        original.CreatedOn = DateTime.UtcNow;
        original.CreatedBy = context.UserId.Value;
        original.LastActor = context.Actor();
        original.LastModifiedOn = null;
        if (removeTag == null) original.Name = $"{original.Name} (Duplicate)";
        original.FlowId = objectType.InitialFlowId;
        original.ObjectStatusId = objectType.InitialObjectStatusId;
        original.RelatedObjects?.Clear();
        original.Attachments?.Clear();

        if (removeTag != null)
        {
            // TODO: filter line items, remove subitems and recalculate quantities/prices 
            var result = FilterOutLineItems(original.LineItems, removeTag);
            if (!result.IsSuccess)
            {
                return result.ConvertTo<Estimate>();
            }

            original.LineItems = result.Value;

            // recalculate totals? 
            var totals = CalculateTotals(original.LineItems, original.TaxRates, original.IsNonTaxable);
            original.TotalCost = totals.TotalCost;
            original.TotalPrice = totals.TotalPrice;
            original.BlendedMargin = totals.BlendedMargin;
            original.TaxLiabilities = totals.TaxLiabilities;
            original.TotalTax = totals.TotalTax;
        }

        var added = await objectTypeService.InsertAsync(context, original, e =>
        {
            e.Action = "Duplicate";
            e.Description = "Duplicated";
        });

        return Result.Success(added);
    }

    public async Task<Result<Estimate>> CreateEstimateAsync(IEntityContext context, CreateEstimateRequest request)
    {
        var lineItems =default(List<LineItem>);
        if (request?.RoomSelectionIds?.Length > 0)
        {
            var selections = await connection.Filter<RoomSelection>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.EntityId, context.OrganizationId.Value)
                .Ne(x => x.IsActive, false)
                .In(x => x.Id, request.RoomSelectionIds)
                .FindAsync();

            if (selections.Count != request.RoomSelectionIds.Length) return Result.Error<Estimate>("Couldn't find room selections");

            request.ProjectExternalId ??= selections.First().ProjectExternalId;

            if (selections.Any(x => x.ProjectExternalId != request.ProjectExternalId)) return Result.Error<Estimate>("Project mismatch in selections");
            if (selections.GroupBy(x => x.BinId).Any(x => x.Count() > 1)) return Result.Error<Estimate>("More than one selection for the same bin");
            
            // TODO: group, optimize line items
            // ...
            lineItems = ConsolidateLineItems(selections);
        }
        else
        {
            lineItems = [];
        }

        if (string.IsNullOrEmpty(request.ProjectExternalId))
        {
            return Result.Error<Estimate>("Project required");
        }
        
        var project = await connection.Filter<SalesforceWorkOrderObject>("salesforce.WorkOrder")
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.ExternalId, request.ProjectExternalId)
            .Ne(x => x.Properties.IsDeleted, true)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (project == null) return Result.Error<Estimate>("Project is no longer available");
        if (!project.LeadId.HasValue)
        {
            return Result.Error<Estimate>("Unknown Lead");
        }
        
        var objectType = await objectTypeService.GetAsync(context, Estimate.ObjectTypeFullName);
        if (objectType == null) return Result.Error<Estimate>("Object Type not found");
        
        var counter = await IncrementEntityCounterAsync(context);

        var estimate = new Estimate
        {
            Id = Guid.NewGuid(),
            AccountId = context.AccountId.Value,
            EntityId = context.OrganizationId.Value,
            CreatedBy = context.UserId.Value,
            EstimateNumber = $"{counter.Count}",
            Name = request.Name,
            Description = request.Description,
            RoomSelectionIds = request.RoomSelectionIds,
            ProjectExternalId = request.ProjectExternalId,
            ProjectId = project.Id,
            LeadId = project.LeadId.Value,
            CreatedOn = DateTime.UtcNow,
            LastActor = context.Actor(),
            FlowId = objectType.InitialFlowId, // request.FlowId ?? ... 
            ObjectStatusId = objectType.InitialObjectStatusId, // request.ObjectStatusId ?? ...
            LineItems = lineItems.ToArray(),
            HasWarnings = lineItems.ToArray().Any(x => x.Warnings?.Count > 0),
            IsNonTaxable = project.IsNonTaxable,
        };

        // resolve tax for project 
        var resolvedTaxRates = await taxService.ResolveForProjectAsync(context, request.ProjectExternalId);
        var taxRates = resolvedTaxRates.Value; // may be null

        var totals = CalculateTotals(lineItems, taxRates, estimate.IsNonTaxable);
        estimate.TotalCost = totals.TotalCost;
        estimate.TotalPrice = totals.TotalPrice;
        estimate.BlendedMargin = totals.BlendedMargin;
        estimate.TaxRates = taxRates;
        estimate.TaxLiabilities = totals.TaxLiabilities;
        estimate.TotalTax = totals.TotalTax;

        // custom sections 
        var customSections = await connection.Filter<CustomSection>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .In(x => x.EntityId, [context.AccountId, context.OrganizationId, context.UserId])
            .Ne(x => x.AlwaysInclude, false)
            .SortAsc(x => x.Order)
            .FindAsync();

        estimate.Sections = customSections.Select(x => new EstimateSection
        {
            Name = x.Name,
            Content = x.Content,
            ContentType = "text/plain",
            Position = x.Position,
        }).ToArray();

        estimate = await objectTypeService.InsertAsync(context, estimate, e =>
        {
            e.Action = "ObjectCreated";
            e.Description = "Estimated Created";
        });

        return Result.Success(estimate);
    }

    private async Task<EntityCounter> IncrementEntityCounterAsync(IEntityContext context)
    {
        var counter = await connection.Filter<EntityCounter>()
            .Eq(x => x.EntityId, context.OrganizationId.Value)
            .Eq(x => x.Name, "Estimate")
            .Update
            .Inc(x => x.Count, 1)
            .UpdateAndGetOneAsync(true);
        return counter;
    }

    /// <summary>
    /// Create a copy of the room selection
    /// - will set the creator id
    /// - will reset status/flow
    /// </summary>
    public async Task<Result<RoomSelection>> DuplicateAsync(IEntityContext context, RoomSelection original, string removeTag = null)
    {
        var objectType = await objectTypeService.GetAsync(context, RoomSelection.ObjectTypeFullName);
        if (objectType == null) return Result.Error<RoomSelection>("Object Type not found");

        if (!objectType.CanCreate(context)) return Result.Error<RoomSelection>("Access Forbidden");

        original.Id = Guid.NewGuid();
        if (removeTag == null) original.Name = $"{original.Name} (Duplicate)";
        original.CreatedOn = DateTime.UtcNow;
        if (context.UserId.HasValue) original.CreatedBy = context.UserId.Value;
        original.LastActor = context.Actor();
        original.LastModifiedOn = null;
        original.FlowId = objectType.InitialFlowId;
        original.ObjectStatusId = objectType.InitialObjectStatusId;

        if (removeTag != null)
        {
            // TODO: filter line items, remove subitems and recalculate quantities/prices 
            var result = FilterOutLineItems(original.LineItems, removeTag);
            if (!result.IsSuccess)
            {
                return result.ConvertTo<RoomSelection>();
            }

            original.LineItems = result.Value;
            original.RecalculateTotals();
            original.HasWarnings = original.LineItems.Any(x => x.Warnings?.Count > 0);
        }

        var inserted = await objectTypeService.InsertAsync(context, original, e =>
        {
            e.Action = "Duplicate";
            e.Description = "Duplicated";
        });

        return Result.Success(inserted);
    }

    private Result<LineItem[]> FilterOutLineItems(LineItem[] src, string removeTag)
    {
        if (src == null) return Result.Unknown<LineItem[]>("No line items to change");

        var changed = false;
        var lineItems = new List<LineItem>();
        foreach (var line in src)
        {
            if (line.Children?.IsEmpty() ?? true)
            {
                lineItems.Add(line);
                continue;
            }

            var filteredOut = line.Children.Where(x => x.Name != removeTag).ToArray();
            if (filteredOut.Length == line.Children.Length)
            {
                lineItems.Add(line);
                continue;
            }

            changed = true;
            if (filteredOut.IsEmpty())
            {
                // nothing left
                continue;
            }

            line.Children = filteredOut;

            // recalculate quantity
            decimal qtty = 0;
            foreach (var child in filteredOut)
            {
                if (!child.Quantity.ConvertTo(line.Quantity.UOM, out var qttt))
                {
                    logger.LogError("Failed to convert {Quantity} from {SrcUOM} to {DstUOM} for {SKU}", child.Quantity.Units, child.Quantity.UOM, line.Quantity.UOM, line.SKU);
                    return Result.Error<LineItem[]>($"Can't recalculate quantity for {line.Name}, incompatible UOMs");
                }

                qtty += qttt.Units;
            }

            line.Quantity = new Measurement
            {
                Units = qtty,
                UOM = line.Quantity.UOM
            };

            // recalculate (use previous waste factor if any
            line.Recalculate();

            lineItems.Add(line);
        }

        return !changed ? Result.Unknown<LineItem[]>("No line items with tag") : Result.Success(lineItems.ToArray());
    }

    public async Task<Result<RoomSelection>> RecalculateAsync(IEntityContext context, RoomSelection selection)
    {
        if (selection.RoomIds == null || selection.RoomIds.IsEmpty()) return Result.Error<RoomSelection>("Selection does not include any rooms");

        EstimateRuleSet ruleSet = null;
        if (selection.RuleSetId.HasValue)
        {
            ruleSet = await connection.Filter<EstimateRuleSet>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.Id, selection.RuleSetId.Value)
                .Ne(x => x.IsActive, false)
                .FirstOrDefaultAsync();
        }

        if (ruleSet != null && !selection.ItemId.HasValue)
        {
            // update selection
            if (ruleSet.ProductType.HasValue) selection.ProductType = ruleSet.ProductType.Value.ToString();
            selection.Name = ruleSet.Name;
            selection.Description = ruleSet.Description;
        }

        var rooms = await connection.Filter<AbstractRoom>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .In(x => x.Id, selection.RoomIds)
            .FindAsync();

        var malFeed = await connection.Filter<CatalogFeed, MALCatalogFeed>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.OrganizationId.Value)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (malFeed == null) return Result.Error<RoomSelection>("MOB feed not found");

        CatalogItem mainItem = null;
        if (selection.ItemId.HasValue)
        {
            mainItem = await connection.Filter<CatalogItem>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.Id, selection.ItemId.Value)
                .Ne(x => x.IsActive, false)
                .FirstOrDefaultAsync();
        }

        var estimateRules = await connection.Filter<EstimateRule>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            // TODO: check entity
            // ...
            .Ne(x => x.Conditions, null) // ignore the ones we can't check
            .In(x => x.EstimateRuleSetId, [null, selection.RuleSetId]) // exclude rules for different rulesets
            .FindAsync();

        var estimateOptionIds = rooms.SelectMany(x => x.GetEstimateOptionIds())
            .Concat(selection.EstimateOptionIds)
            .ToHashSet();

        var estimateOptions = (
            await connection.Filter<EstimateOption>()
                .In(x => x.Id, estimateOptionIds)
                .IncludeField(x => x.Name)
                .FindAsync()
        ).ToDictionary(x => x.Id, x => x.Name);

        var lineItems = new List<LineItem>();
        decimal sqftArea = 0;
        decimal ftPerimeter = 0;

        foreach (var room in rooms)
        {
            // accumulate area/2p
            sqftArea += (room.AdjustedArea ?? room.TotalArea)?.Convert(UnitOfMeasurement.SqFt)?.Units ?? 0;
            ftPerimeter += (room as RegularRoom)?.Perimeter?.Convert(UnitOfMeasurement.Feet)?.Units ?? 0;

            // calculate inputs 
            var calculatedInput = CalculateInputs(selection, room, estimateOptions);
            if (calculatedInput.IsError) return calculatedInput.ConvertTo<RoomSelection>();
            var input = calculatedInput.Value;

            if (mainItem != null)
            {
                var mainLineItem = await CalculateMainLineItem(context, selection, mainItem, room, input);
                lineItems.Add(mainLineItem);
            }

            var roomLineItems = CalculateLineItems(estimateRules, room, input).ToArray();
            lineItems.AddRange(roomLineItems);
        }

        // what to do?
        selection.WasteFactor = null;
        selection.MainProductQuantity = null;

        await LoadItemsAsync(context, malFeed, lineItems);

        lineItems = ConsolidateLineItems(lineItems);
        selection.Recalculate(lineItems);

        var result = await connection.Filter<RoomSelection>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, selection.Id)
            .Update
            .Set(x => x.Name, selection.Name)
            .SetOrUnset(x => x.Description, selection.Description)
            .SetOrUnset(x => x.ProductType, selection.ProductType)
            .Set(x => x.Hash, selection.Hash)
            .Set(x => x.LineItems, lineItems.ToArray())
            .Set(x => x.MainProductQuantity, selection.MainProductQuantity)
            .SetOrUnset(x => x.WasteFactor, selection.WasteFactor)
            .SetOrUnset(x => x.TotalCost, selection.TotalCost)
            .SetOrUnset(x => x.TotalPrice, selection.TotalPrice)
            .SetOrUnset(x => x.BlendedMargin, selection.BlendedMargin)
            .SetOrUnset(x => x.TotalTax, selection.TotalTax)
            .Set(x => x.HasWarnings, selection.HasWarnings)
            .Set(x => x.Area, new Measurement
            {
                Units = sqftArea,
                UOM = UnitOfMeasurement.SqFt,
            })
            .Set(x => x.Perimeter, new Measurement
            {
                Units = ftPerimeter,
                UOM = UnitOfMeasurement.Feet,
            })
            .Set(x => x.LastActor, context.Actor())
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.Tags, rooms.Select(x => x.Name))
            .UpdateAndGetOneAsync();

        return Result.Success(result);
    }

    private async Task<LineItem> CalculateMainLineItem(IEntityContext context, RoomSelection selection, CatalogItem mainItem, AbstractRoom room, Dictionary<string, object> input)
    {
        var mainLineItem = CreateLineItem(mainItem, LineItemSource.Item);
        mainLineItem.Tags = [room.Name];

        var sellingUnit = mainLineItem.GuessSellingUnit();
        if (sellingUnit?.UOM.GetMeasurementOf() == UnitOfMeasurementExtensions.MeasurementOf.Area)
        {
            mainLineItem.Criteria = QuantityCriteria.RoomArea;

            if (input.TryGetValue(nameof(EstimateInput.SurfaceArea), out var surfaceAreaObj) && surfaceAreaObj is decimal surfaceArea)
            {
                // use area for surface
                mainLineItem.Quantity = new Measurement
                {
                    Units = surfaceArea,
                    UOM = UnitOfMeasurement.SqFt,
                };
                mainLineItem.SetWarning(LineItemWarning.UndefinedColor);

                // set initial waste factor
                var wasteFactorConfig = await GetWasteFactorConfig(context, selection, mainItem, room.AdjustedArea ?? room.TotalArea);
                mainLineItem.WasteFactor = wasteFactorConfig?.WasteFactor ?? WasteFactorConfig.DefaultWasteFactor;

                var recalc = mainLineItem.Recalculate();
                if (recalc.IsSuccess)
                {
                    var area = mainLineItem.AdjustedQuantity ?? mainLineItem.Quantity;
                    if (area.ConvertTo(UnitOfMeasurement.SqFt, out var qtty))
                    {
                        // update the main qtty
                        input[nameof(EstimateInput.MainProductArea)] = qtty.Units;
                    }
                }
            }
            else
            {
                // no surface area defined
                mainLineItem.Quantity = null;
                mainLineItem.SetWarning(LineItemWarning.QuantityError);
                input.Remove(nameof(EstimateInput.MainProductArea));
            }
        }
        else
        {
            // can't calculate main item because it is not sold by area 
            mainLineItem.Criteria = QuantityCriteria.Arbitrary;
            mainLineItem.Quantity = null;
            mainLineItem.SetWarning(sellingUnit == null ? LineItemWarning.NoSellingUnit : LineItemWarning.QuantityError);
            input.Remove(nameof(EstimateInput.MainProductArea));
        }

        mainLineItem.Children =
        [
            new ChildLineItem
            {
                Name = room.Name,
                // Description =
                Quantity = mainLineItem.Quantity,
                AdjustedQuantity = mainLineItem.AdjustedQuantity,
                WasteFactor = mainLineItem.WasteFactor,
            }
        ];

        return mainLineItem;
    }

    private async Task LoadItemsAsync(IEntityContext context, MALCatalogFeed malFeed, List<LineItem> lineItems)
    {
        var itemIds = lineItems
            .Where(x => x.ItemId != Guid.Empty)
            .Select(x => x.ItemId)
            .ToHashSet();

        if (itemIds.IsEmpty())
        {
            var loaded = (
                await connection.Filter<CatalogItem>()
                    .Eq(x => x.AccountId, context.AccountId.Value)
                    .Ne(x => x.IsActive, false)
                    .In(x => x.Id, itemIds)
                    .FindAsync()
            ).ToDictionary(x => x.Id);

            foreach (var lineItem in lineItems)
            {
                if (!loaded.TryGetValue(lineItem.ItemId, out var item)) continue;

                lineItem.SKU = item.SKU;
                lineItem.Name = item.Name;
                lineItem.Description = item.Description;
                lineItem.Margin = item.Margin;
                lineItem.Costs = item.GetValidCosts();
                lineItem.SellingUnit = item.SellingUnit;
                // TaxCategory = item.GetTaxCategory(),                    
            }
        }

        var skus = lineItems
            .Where(x => x.ItemId == Guid.Empty)
            .Select(x => x.SKU)
            .ToHashSet();

        if (!skus.IsEmpty())
        {
            var loaded = (
                await connection.Filter<CatalogItem>()
                    .Eq(x => x.AccountId, context.AccountId.Value)
                    .Eq(x => x.CatalogFeedId, malFeed.Id)
                    .Ne(x => x.IsActive, false)
                    .In(x => x.SKU, skus)
                    .FindAsync()
            ).ToDictionary(x => x.SKU);

            foreach (var lineItem in lineItems)
            {
                if (lineItem.ItemId != Guid.Empty) continue;
                if (!loaded.TryGetValue(lineItem.SKU, out var item)) continue;

                lineItem.ItemId = item.Id;
                lineItem.SKU = item.SKU;
                lineItem.Name = item.Name;
                lineItem.Description = item.Description;
                lineItem.Margin = item.Margin;
                lineItem.Costs = item.GetValidCosts();
                lineItem.SellingUnit = item.SellingUnit;
                // TaxCategory = item.GetTaxCategory(),                    
            }
        }
    }

    private IEnumerable<LineItem> CalculateLineItems(List<EstimateRule> estimateRules, AbstractRoom room, Dictionary<string, object> input)
    {
        var rules = estimateRules.Where(rule => rule.IsMatch(input)).ToArray();
        foreach (var rule in rules)
        {
            decimal? numValue;

            if (rule.Input == EstimateInput.Arbitrary)
            {
                // nothing to do 
                numValue = 1;
            }
            else if (!input.TryResolvePathValue(rule.Input.ToString(), out object value))
            {
                logger.LogError("Failed to find {Input} for {Rule}", rule.Input, rule.Name);
                continue;
            }
            else
            {
                numValue = value switch
                {
                    decimal d => d,
                    int i => i,
                    string str => decimal.TryParse(str, out var d) ? d : null,
                    _ => null,
                };
            }

            if (!numValue.HasValue)
            {
                logger.LogError("Invalid {Input} for {Rule}", rule.Input, rule.Name);
                continue;
            }

            if (rule.Factor.HasValue)
            {
                numValue = rule.Factor.Value * numValue;
            }

            if (rule.Offset.HasValue)
            {
                numValue += rule.Offset.Value;
            }

            if (rule.Min.HasValue && numValue < rule.Min.Value)
            {
                numValue = rule.Min.Value;
            }

            if (rule.Max.HasValue && numValue > rule.Max.Value)
            {
                numValue = rule.Max.Value;
            }

            if (rule.Modulus is > 0 && numValue is > 0)
            {
                var m = (int)(numValue.Value / rule.Modulus.Value);
                if (m * rule.Modulus.Value < numValue.Value) m++;
                numValue = m * rule.Modulus.Value;
            }

            var qtty = new Measurement
            {
                Units = numValue.Value,
                UOM = rule.UOM,
            };

            var lineItem = new LineItem
            {
                SKU = rule.SKU,
                Name = rule.Name,
                Description = rule.Description ?? rule.Name,
                ItemId = rule.ItemId ?? Guid.Empty,
                Source = rule.Section ?? LineItemSource.Unknown,
                Criteria = rule.QuantityCriteria,
                TaxCategory = rule.TaxCategory,
                WasteFactor = rule.WasteFactor,
                Quantity = qtty,
                Tags = [room.Name],
                Children =
                [
                    new ChildLineItem
                    {
                        Name = room.Name,
                        Quantity = qtty,
                        // Description =
                        // AdjustedQuantity =
                        // WasteFactor = 
                    },
                ]
            };

            // add result to input list
            input[lineItem.SKU] = numValue;

            yield return lineItem;
        }
    }

    private Result<Dictionary<string, object>> CalculateInputs(RoomSelection selection, AbstractRoom room, Dictionary<Guid, string> estimateOptions)
    {
        var input = new Dictionary<EstimateInput, object>(room.GetEstimateInputs())
        {
            { EstimateInput.ProductType, selection.ProductType }, // product type names will not include spaces as they are from enum ProductType.ToString
            { EstimateInput.InstallationType, selection.InstallationTypeId },
            { EstimateInput.PatternType, selection.PatternTypeId },
            { EstimateInput.TrimWork, selection.TrimWorkId },
            { EstimateInput.Underlayment, selection.UnderlaymentId },
            { EstimateInput.SubfloorPrep, selection.SubfloorPrepId },
            { EstimateInput.StairsRiserFinish, selection.StairsRiserFinishId },
        };

        if (!Enum.TryParse<ProductType>(selection.ProductType, out var productType)) return Result.Error<Dictionary<string, object>>($"Unexpected value for Product Type: {selection.ProductType}");

        var surfaceArea = room.GetAreaForProductType(productType)?.Units ?? 0;
        input[EstimateInput.SurfaceArea] = surfaceArea;

        if (surfaceArea > 0 && (!input.TryGetValue(EstimateInput.RoomArea, out var roomAreaObj) || roomAreaObj is not decimal roomArea || roomArea == 0))
        {
            // hack so we can start the estimate for flooring on stairs using normal rules
            input[EstimateInput.RoomArea] = surfaceArea;
        }

        // default to surface area (in case there is no main product yet - "template")
        input[EstimateInput.MainProductArea] = surfaceArea;

        return Result.Success(input.ToDictionary(
                x => x.Key.ToString(),
                x => x.Value switch
                {
                    // try to convert to option name
                    Guid optionId => estimateOptions.TryGetValue(optionId, out var name) ? name : optionId,
                    _ => x.Value,
                }
            )
        );
    }

    private static LineItem CreateLineItem(CatalogItem item, LineItemSource source)
    {
        return new LineItem
        {
            ItemId = item.Id,
            SKU = item.SKU,
            Criteria = item.GetQuantityCriteria(),
            Name = item.Name,
            Description = item.Description,
            Source = source,
            Margin = item.Margin,
            Costs = item.GetValidCosts(),
            SellingUnit = item.SellingUnit,
            TaxCategory = item.GetTaxCategory(),
        };
    }

    public static List<LineItem> ConsolidateLineItems(List<LineItem> src)
    {
        var lineItems = src.Select(l => new TaggedLineItem(string.Join(", ", l.Tags), l)).ToArray();
        return Consolidate(lineItems);
    }

    private static List<LineItem> ConsolidateLineItems(List<RoomSelection> selections)
    {
        var lineItems = selections.SelectMany(x => x.LineItems.Select(l =>
            {
                // if room selection is non-taxable, all items are 
                l.IsNonTaxable = l.IsNonTaxable || x.IsNonTaxable;
                return new TaggedLineItem(x.Bin, l);
            })
        ).ToArray();
        return Consolidate(lineItems);
    }

    private static List<LineItem> Consolidate(TaggedLineItem[] lineItems)
    {
        var consolidatedItems = new List<LineItem>(
            lineItems
                .Where(x => x.LineItem.Quantity == null)
                .Select(x => x.LineItem)
        );
        var groupedItems = lineItems
            .Where(x => x.LineItem.Quantity != null)
            .GroupBy(x => x.LineItem.ItemId)
            .ToDictionary(x => x.Key, x => x.ToArray());

        foreach (var g in groupedItems)
        {
            var gItems = g.Value;
            var first = gItems.First();
            var sampleLineItem = first.LineItem;
            if (gItems.Length == 1)
            {
                // only one, add as it is
                consolidatedItems.Add(sampleLineItem);
                continue;
            }

            // when merging, if anywhere an item is non-taxable, the grouped is non-taxable
            var isNonTaxable = gItems.Any(x => x.LineItem.IsNonTaxable);

            var total = gItems.Sum(x => x.LineItem.WasteFactor is > 0 ? x.LineItem.Quantity.Units * (100 + x.LineItem.WasteFactor.Value) / 100M : x.LineItem.Quantity.Units);
            var qtty = gItems.Sum(x => x.LineItem.Quantity.Units);
            var criterion = gItems.Select(x => x.LineItem.Criteria).Distinct().ToArray();
            var criteria = criterion.Length != 1
                ? QuantityCriteria.Custom
                : criterion[0] switch
                {
                    QuantityCriteria.MainProductArea => QuantityCriteria.MainProductArea,
                    QuantityCriteria.RoomArea => QuantityCriteria.RoomArea,
                    QuantityCriteria.Perimeter => QuantityCriteria.Perimeter,
                    QuantityCriteria.Custom => QuantityCriteria.Custom,
                    // QuantityCriteria.Arbitrary or QuantityCriteria.RoomArea or QuantityCriteria.Perimeter => QuantityCriteria.Arbitrary,
                    _ => QuantityCriteria.Arbitrary,
                };

            var gItem = new LineItem
            {
                SKU = sampleLineItem.SKU,
                ItemId = sampleLineItem.ItemId,
                Source = sampleLineItem.Source, // use first for no good reason
                Margin = sampleLineItem.Margin, // use first for no good reason
                Name = sampleLineItem.Name, // use first for no good reason
                Description = sampleLineItem.Description, // use first for no good reason
                SellingUnit = sampleLineItem.SellingUnit, // use first for no good reason
                Costs = sampleLineItem.Costs, // use first for no good reason
                TaxCategory = sampleLineItem.TaxCategory,
                Criteria = criteria,
                Quantity = new Measurement
                {
                    UOM = sampleLineItem.Quantity.UOM,
                    Units = qtty,
                },
                WasteFactor = total == qtty ? null : 100 * (total - qtty) / qtty, // blended waste factor
                Warnings = gItems.SelectMany(x => x.LineItem.Warnings ?? []).ToHashSet(), // copy all warnings

                // TODO: should copy all children from all items instead
                // ...
                Children = gItems.SelectMany(x => x.LineItem.Children?.Length > 0
                    ? x.LineItem.Children
                    :
                    [
                        new ChildLineItem
                        {
                            Name = x.Tag,
                            // Description = $"{x.Item1.Bin}: {x.Item2.Description}",
                            Quantity = x.LineItem.Quantity,
                            // AdjustedQuantity =
                            // WasteFactor = 
                        }
                    ]
                ).ToArray(),
                Tags = gItems.SelectMany(x => x.LineItem.Tags == null || x.LineItem.Tags.Length == 0 ? [x.Tag] : x.LineItem.Tags).Distinct().ToArray(),
                IsNonTaxable = isNonTaxable,
            };

            gItem.Recalculate();

            consolidatedItems.Add(gItem);
        }

        return consolidatedItems;
    }

    public static LineItemsTotal CalculateTotals(IEnumerable<LineItem> lineItems, TaxRates taxRates, bool isNonTaxable)
    {
        decimal totalCost = 0;
        decimal totalPrice = 0;
        var taxableTotals = new Dictionary<TaxCategory, decimal>
        {
            { TaxCategory.Sales, 0 },
            { TaxCategory.Service, 0 },
            { TaxCategory.Labor, 0 },
            { TaxCategory.Freight, 0 },
            { TaxCategory.Use, 0 },
        };

        var totalsPerCategory = new Dictionary<TaxCategory, decimal>
        {
            { TaxCategory.Sales, 0 },
            { TaxCategory.Service, 0 },
            { TaxCategory.Labor, 0 },
            { TaxCategory.Freight, 0 },
            { TaxCategory.Use, 0 },
            { TaxCategory.Other, 0 },
        };

        foreach (var item in lineItems)
        {
            totalCost += item.TotalCost ?? 0;
            totalPrice += item.TotalPrice ?? 0;

            totalsPerCategory[item.TaxCategory ?? TaxCategory.Other] += item.TotalPrice ?? 0;

            if (!item.TaxCategory.HasValue)
            {
                // TODO: add warning?
                // ...

                continue;
            }

            if (isNonTaxable || item.IsNonTaxable)
            {
                // not taxable
                continue;
            }

            // TODO: should use tax use cost instead of price?
            // ...

            taxableTotals[item.TaxCategory.Value] += item.TotalPrice ?? 0;
        }

        var totals = new LineItemsTotal
        {
            TotalCost = totalCost,
            TotalPrice = totalPrice,
            TaxableTotals = taxableTotals,
            TotalsPerCategory = totalsPerCategory,
        };

        UpdateTaxes(totals, taxRates);

        return totals;
    }

    private static bool UpdateTaxes(LineItemsTotal totals, TaxRates taxRates)
    {
        decimal totalTax = 0;
        var taxLiabilities = new List<TaxLiability>();
        if (taxRates == null)
        {
            totals.TotalTax = null;
            totals.TaxLiabilities = null;
            return false;
        }

        foreach (var taxable in totals.TaxableTotals)
        {
            if (taxable.Value == 0) continue;

            var liabilities = taxRates.TaxLiabilities
                .Where(x => x.Category == taxable.Key)
                .Select(x => new TaxLiability
                {
                    Category = x.Category,
                    Amount = Math.Round(x.Amount * taxable.Value, 2),
                    Name = x.Name,
                    Description = x.Description,
                })
                .ToArray();

            taxLiabilities.AddRange(liabilities);
            totalTax += liabilities.Sum(x => x.Amount);
        }

        // tax
        totals.TotalTax = totalTax;
        totals.TaxLiabilities = taxLiabilities.IsEmpty() ? null : taxLiabilities.ToArray();

        return true;
    }

    public class AddItemRequest
    {
        public LineItemSource Source { get; set; }
        public Guid ItemId { get; set; }

        [Obsolete] public AppliesTo AppliesTo { get; set; }
        public decimal? Quantity { get; set; }
        public UnitOfMeasurement? UOM { get; set; }

        public QuantityCriteria QuantityCriteria { get; set; }
    }

    public class AddFreightRequest
    {
        public Guid ItemId { get; set; }
    }

    public class BlendedMarginRequest
    {
        public decimal? BlendedMargin { get; set; }
    }

    public class RemoveItemRequest
    {
        public LineItemSource Source { get; set; }
        public Guid ItemId { get; set; }

        [Obsolete] public AppliesTo AppliesTo { get; set; }
    }

    public class RemoveTaggedItemsRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Tag { get; set; }
    }

    public class SwitchItemRequest
    {
        public LineItemSource Source { get; set; }
        public Guid CurrentItemId { get; set; }
        public Guid ItemId { get; set; }
    }

    public enum AppliesTo
    {
        All,
        Estimate,
        ProductType
    }

    public class EditItemRequest
    {
        public string Description { get; set; }
        public LineItemSource Source { get; set; }
        public Guid ItemId { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? WasteFactor { get; set; }
        public decimal? UnitCost { get; set; }
        public decimal? Margin { get; set; }
        public UnitOfMeasurement? UOM { get; set; }
        public bool? IsNonTaxable { get; set; }

        [Obsolete] public AppliesTo AppliesTo { get; set; }
    }

    public class LineItemsTotal
    {
        public Dictionary<TaxCategory, decimal> TaxableTotals { get; set; }
        public decimal? TotalCost { get; set; }
        public decimal? TotalPrice { get; set; }
        public decimal? TotalTax { get; set; }
        public decimal? BlendedMargin => TotalPrice > 0 ? Math.Round(100 * (TotalPrice.Value - (TotalCost ?? 0)) / TotalPrice.Value, 2) : null;
        public TaxLiability[] TaxLiabilities { get; set; }

        /// <summary>
        /// Totals per category
        /// </summary>
        public Dictionary<TaxCategory, decimal> TotalsPerCategory { get; set; }
    }

    public class CreateEstimateRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Guid? FlowId { get; set; }
        public Guid? ObjectStatusId { get; set; }
        
        /// <summary>
        /// Optional, list of selection ids to seed proposal with
        /// </summary>
        public Guid[] RoomSelectionIds { get; set; }
        
        /// <summary>
        /// Project External Id, can't be empty if roomselectionids are previded
        /// </summary>
        public string ProjectExternalId { get; set; }
    }

    public class DiscountParms
    {
        public decimal? Percentage { get; init; }
        public decimal? Amount { get; init; }
        public TaxCategory Category { get; init; }
    }

    public class SetDiscountRequest
    {
        public decimal? Sales { get; set; }
        public decimal? SalesAmount { get; set; }
        public decimal? Service { get; set; }
        public decimal? ServiceAmount { get; set; }
    }

    public class AddSectionRequest
    {
        public string Name { get; set; }
        public string Content { get; set; }
        public string ContentType { get; set; }
        public SectionPosition? Position { get; set; }
    }

    public class EditSectionRequest
    {
        public string Name { get; set; }
        public string Content { get; set; }
        public string ContentType { get; set; }
    }

    public class RemoveSectionRequest
    {
        public string Name { get; set; }
    }

    public class EditInfoRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>
        /// Only for Estimate
        /// </summary>
        public string Summary { get; set; }

        public bool IsNonTaxable { get; set; }
    }

    private class TaggedLineItem
    {
        public string Tag { get; init; }
        public LineItem LineItem { get; init; }

        public TaggedLineItem(string tag, LineItem lineItem)
        {
            Tag = tag;
            LineItem = lineItem;
        }
    }

    public async Task<Result<RoomSelection>> UpdateInfoAsync(IEntityContext context, RoomSelection roomSelection, EditInfoRequest request)
    {
        var changed = false;

        var query = connection.Filter<RoomSelection>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.Id, roomSelection.Id)
                .Eq(x => x.LastModifiedOn, roomSelection.LastModifiedOn)
                .Update
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, context.Actor())
            ;

        if (!string.IsNullOrWhiteSpace(request.Name) && roomSelection.Name != request.Name.Trim())
        {
            changed = true;
            roomSelection.Name = request.Name.Trim();
            query.Set(x => x.Name, roomSelection.Name);
        }

        if (!string.IsNullOrWhiteSpace(request.Description) && roomSelection.Description != request.Description.Trim())
        {
            changed = true;
            roomSelection.Description = request.Description.Trim();
            query.Set(x => x.Description, roomSelection.Description);
        }

        if (request.IsNonTaxable != roomSelection.IsNonTaxable)
        {
            changed = true;

            roomSelection.IsNonTaxable = request.IsNonTaxable;
            query.Set(x => x.IsNonTaxable, roomSelection.IsNonTaxable);
        }

        if (!changed) return Result.Unknown<RoomSelection>("Nothing changed");

        roomSelection = await query.UpdateAndGetOneAsync();

        return roomSelection == null ? Result.Error<RoomSelection>("Error updating object") : Result.Success(roomSelection);
    }

    public async Task<Result<Estimate>> UpdateInfoAsync(IEntityContext context, Estimate estimate, EditInfoRequest request)
    {
        var changed = false;

        var query = connection.Filter<Estimate>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.Id, estimate.Id)
                .Eq(x => x.LastModifiedOn, estimate.LastModifiedOn)
                .Update
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, context.Actor())
            ;

        if (!string.IsNullOrWhiteSpace(request.Name) && estimate.Name != request.Name.Trim())
        {
            changed = true;
            estimate.Name = request.Name.Trim();
            query.Set(x => x.Name, estimate.Name);
        }

        if (!string.IsNullOrWhiteSpace(request.Description) && estimate.Description != request.Description.Trim())
        {
            changed = true;
            estimate.Description = request.Description.Trim();
            query.Set(x => x.Description, estimate.Description);
        }

        if (!string.IsNullOrWhiteSpace(request.Summary) && estimate.Summary != request.Summary.Trim())
        {
            changed = true;
            estimate.Summary = request.Summary.Trim();
            query.Set(x => x.Name, estimate.Summary);
        }

        if (request.IsNonTaxable != estimate.IsNonTaxable)
        {
            changed = true;

            estimate.IsNonTaxable = request.IsNonTaxable;
            query.Set(x => x.IsNonTaxable, estimate.IsNonTaxable);

            // recalculate totals
            var totals = CalculateTotals(estimate.LineItems, estimate.TaxRates, estimate.IsNonTaxable);
            estimate.TotalCost = totals.TotalCost;
            estimate.TotalPrice = totals.TotalPrice;
            estimate.BlendedMargin = totals.BlendedMargin;
            estimate.TaxLiabilities = totals.TaxLiabilities;
            estimate.TotalTax = totals.TotalTax;

            query.Set(x => x.TotalCost, estimate.TotalCost)
                .Set(x => x.TotalPrice, estimate.TotalPrice)
                .Set(x => x.BlendedMargin, estimate.BlendedMargin)
                .Set(x => x.TaxLiabilities, estimate.TaxLiabilities)
                .Set(x => x.TotalTax, estimate.TotalTax)
                .Set(x => x.IsNonTaxable, estimate.IsNonTaxable)
                .SetOrUnset(x => x.GrandTotal, estimate.GrandTotal)
                .SetOrUnset(x => x.GrandTax, estimate.GrandTax)
                ;
        }

        if (!changed) return Result.Unknown<Estimate>("Nothing changed");

        estimate = await query.UpdateAndGetOneAsync();

        return estimate == null ? Result.Error<Estimate>("Error updating object") : Result.Success(estimate);
    }
}

[BsonCollection("fcb2b.CustomSection")]
public class CustomSection : EntityOwnedModel
{
    public const string ObjectTypeFullName = "fcb2b.CustomSection";

    public string Content { get; set; }
    public int Order { get; set; }
    public SectionPosition Position { get; set; }
    public bool AlwaysInclude { get; set; }
}