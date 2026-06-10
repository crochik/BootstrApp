using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using PI.ProductCatalog.Models;
using PI.ProductCatalog.Services;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

[Route("/productcatalog/v1/CatalogFeed/mal")]
public class MALCatalogFeedController : AbstractCatalogFeedController<MALCatalogFeed>
{
    private readonly ILogger<MALCatalogFeedController> _logger;
    private readonly CatalogService _catalogService;
    private readonly MongoConnection _connection;

    public MALCatalogFeedController(
        ILogger<MALCatalogFeedController> logger,
        ObjectTypeService objectTypeService,
        CatalogService catalogService,
        MongoConnection connection
    ) : base(objectTypeService)
    {
        _logger = logger;
        _catalogService = catalogService;
        _connection = connection;
    }

    [Authorize("default")]
    [HttpPost("DataForm")]
    public Task<DataFormActionResponse> EditFormOnActionAsync([FromBody] DataFormActionRequest request)
        => OnActionAsync(request);

    [Authorize("managerplus")]
    [HttpPost("items/DataView")]
    [Produces("text/csv", "application/json")]
    public async Task<IDataViewResponse> ItemsDataViewAsync([FromBody] DataViewRequest request)
    {
        Prepare(request);

        var catalog = await _catalogService.GetOrCreateMALCatalogAsync(Context);

        var result = await _connection.Filter<CustomObject>("fcb2b.MAL")
            .Eq(x => x.AccountId, Context.AccountId)
            .DipperAsync<object>("productCatalog.MAL.getItems.v2", new Dictionary<string, object>
            {
                {"CatalogFeedId", catalog.Id.AsSerializedId()},
            });

        var response = new DataViewResponse
        {
            Result = result,
            Request = request,
            View = new DataView
            {
                Name = "Catalog Feed",
                Title = catalog.Name,
                KeyField = nameof(CatalogItem.SKU),
                DefaultSort = nameof(CatalogItem.Name),
                IsSelectable = false,
                Detail = new DataViewDetail
                {
                    Page = $"dataForm://productcatalog/v1/CatalogFeed/mal({catalog.Id})",
                },
                Fields = new FormField[]
                {
                    new TextField
                    {
                        Name = nameof(CatalogItem.SKU)
                    },
                    new TextField
                    {
                        Name = nameof(CatalogItem.Name)
                    },
                    new TextField
                    {
                        Name = nameof(CatalogItem.SellingUnit.UOM),
                        Label = "UOM"
                    },
                    new NumberField
                    {
                        Name = "Cost",
                        Label = "Cost",
                        NumberFieldOptions = {
                            Style = NumberFieldOptionsStyle.Currency,
                        }
                    },
                    new NumberField
                    {
                        Name = nameof(CatalogItem.Margin),
                        Label = "Margin (%)"
                    },
                    // new NumberField
                    // {
                    //     Name = "Price",
                    //     Label = "Price",
                    //     NumberFieldOptions = {
                    //         Style = NumberFieldOptionsStyle.Currency,
                    //     }
                    // },
                    new TextField
                    {
                        Name = "MaterialType",
                        Label = "Type"
                    },
                    // new TextField
                    // {
                    //     Name = "MaterialSubType",
                    //     Label = "Sub Type"
                    // },
                    // new TextField
                    // {
                    //     Name = "ExternalId",
                    //     Label = "SF Product"
                    // },
                },
                Menu = new Menu
                {
                    Name = "EditMenu",
                    Items = 
                    [
                        new ActionMenuItem {
                            Name = "Download",
                            Icon =  nameof(Icons.Download),
                            Action = "#csv"
                        },
                        new ActionMenuItem {
                            Name = "Upload",
                            Icon =  nameof(Icons.Upload),
                            Action = "#upload"
                        },
                        new ActionMenuItem {
                            Name = "Edit",
                            Visible =  new [] {
                                "selectedCount=='1'"
                            },
                            Icon =  nameof(Icons.Edit),
                            Action = $"dataForm://productcatalog/v1/MALCatalogFeed({catalog.Id})"
                        },
                    ],

                    Collapsible = false,
                }
            },
        };

        return response.UpdateFields();
    }

    [Authorize("managerplus")]
    [HttpPost("items/Import")]
    [Consumes("application/octet-stream", "multipart/form-data")]
    public virtual async Task<DataFormActionResponse> DataViewImportByIdAsync(IFormFile file)
    {
        try
        {
            var catalog = await _catalogService.GetOrCreateMALCatalogAsync(Context);
            using var csv = SpreadsheetFileParser.Create(file);
            if (csv == null)
            {
                return error("Unsupported file type");
            }

            var skuColumn = -1;
            var costColumn = -1;
            var marginColumn = -1;
            var marginFactor = 1;

            if (csv.ColumnNames.Length >= 8 &&
                csv.ColumnNames[2] == "STYLE or SKU" &&
                csv.ColumnNames[5] == "UNIT COST: CUT or CARTON" &&
                csv.ColumnNames[7] == "GROSS MARGIN")
            {
                skuColumn = 2;
                costColumn = 5;
                marginColumn = 7;
                marginFactor = 100;
            }
            else if (
                !csv.Columns.TryGetValue(nameof(CatalogItem.SKU), out skuColumn) ||
                !csv.Columns.TryGetValue("Cost", out costColumn) ||
                !csv.Columns.TryGetValue("Margin (%)", out marginColumn)
            )
            {
                _logger.LogError("Couldn't detect columns");
                return error("Invalid spreadsheet format");
            }

            var updates = new List<WriteModel<CatalogItem>>();
            var rows = await csv.GetRowsAsync(new[] { skuColumn, costColumn, marginColumn });
            var errors = new List<string>();
            var index = 0;
            await foreach (var record in rows.ReadAllAsync())
            {
                index++;

                try
                {
                    var sku = record[0]?.ToString();
                    if (string.IsNullOrEmpty(sku))
                    {
                        errors.Add($"{index} Missing required SKU");
                        continue;
                    }

                    var costValue = Convert.ToDecimal(record[1]);
                    var marginValue = Convert.ToDecimal(record[2]);
                    var (mal, item) = await GetItemAsync(catalog, sku);
                    if (mal == null) continue;

                    var result = await CreateWriteModelAsync(catalog, mal, item, costValue, marginValue * marginFactor);
                    if (result) updates.Add(result.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse {row}", index);
                    errors.Add($"{index} {ex.Message}");
                }
            }

            var message = default(string);
            if (updates.Count > 0)
            {
                var result = await _connection.BulkWriteAsync(updates);
                message = $"{result.InsertedCount} item(s) created, {result.ModifiedCount} price(s) modified";

                await _catalogService.SetLastUpdatedOnAsync(Context, catalog);
            }

            return new DataFormActionResponse
            {
                Message = message,
                Success = message != null,
                Action = "Import"
            };
        }
        catch (Exception ex)
        {
            return error($"Failed to load CSV file: {ex.Message}");
        }

        DataFormActionResponse error(string message)
        {
            return new DataFormActionResponse
            {
                Message = message,
                Success = false,
                Action = "Import"
            };
        }
    }

    [Authorize("managerplus")]
    [HttpPost("/productcatalog/v1/CatalogFeed/mal({catalogFeedId})/DataForm")]
    public async Task<DataFormActionResponse> EditFormOnActionAsync([FromRoute] Guid catalogFeedId, [FromBody] DataFormActionRequest request)
    {
        switch (request.Action)
        {
            case "Update":
                break;

            default:
                return new DataFormActionResponse(request, "Action not implemented");
        }

        if (!request.TryGetStrParam(nameof(CatalogItem.SKU), out var sku))
        {
            return new DataFormActionResponse(request, "Missing required SKU");
        }

        var catalogFeed = await _connection.Filter<CatalogFeed, MALCatalogFeed>(Context)
            .Eq(x => x.Id, catalogFeedId)
            .FirstOrDefaultAsync();

        if (catalogFeed == null) return new DataFormActionResponse(request, "Catalog feed not found");

        var cost = getDecimal("Cost");
        if (cost < 0) return new DataFormActionResponse(request, "Cost can't be negative");
        var margin = getDecimal("Margin");
        if (margin <= 0) return new DataFormActionResponse(request, "Invalid range for Margin");

        var (mal, item) = await GetItemAsync(catalogFeed, sku);
        if (mal == null) return new DataFormActionResponse(request, "Item not found");

        var result = await AddOrUpdateAsync(catalogFeed, mal, item, cost, margin);
        if (!result)
        {
            return new DataFormActionResponse(request, result.Status);
        }

        // in theory it should update breadcrumbs and mark catalog as modified
        // ...

        var message = item == null ? "Item added" : "Item Updated";
        return new DataFormActionResponse(request, message, true)
        {
            Ids = new[] { result.Value.Id }
        };

        decimal getDecimal(string name)
        {
            if (!request.Parameters.TryGetValue(name, out var costObj)) throw new BadRequestException($"Invalid/Missing {name}");
            if (costObj is decimal cost) return cost;
            if (decimal.TryParse(costObj.ToString(), out cost)) return cost;
            throw new BadRequestException($"Invalid/Missing {name}");
        }
    }

    private async Task<Result<WriteModel<CatalogItem>>> CreateWriteModelAsync(CatalogFeed catalogFeed, CustomObject mal, CatalogItem item, decimal cost, decimal margin)
    {
        if (item == null)
        {
            var result = await CreateItemAsync(catalogFeed, mal, cost, margin);
            if (result)
            {
                var model = _connection.Filter<CatalogItem>().InsertOneModel(result.Value);
                return Result<WriteModel<CatalogItem>>.Success(model);
            }

            return Result<WriteModel<CatalogItem>>.Error(result.Status);
        }

        var update = GetUpdateModelQuery(mal, item, cost, margin);
        if (update)
        {
            return Result<WriteModel<CatalogItem>>.Success(update.Value.UpdateOneModel());
        }

        return Result<WriteModel<CatalogItem>>.Error(update.Status);
    }

    private async Task<Result<CatalogItem>> AddOrUpdateAsync(CatalogFeed catalogFeed, CustomObject mal, CatalogItem item, decimal cost, decimal margin)
    {
        if (item == null)
        {
            var result = await CreateItemAsync(catalogFeed, mal, cost, margin);
            if (result) await _connection.InsertAsync(result.Value);
            return result;
        }

        return await UpdateItemAsync(mal, item, cost, margin);
    }

    private async Task<Result<CatalogItem>> CreateItemAsync(CatalogFeed catalogFeed, CustomObject mal, decimal cost, decimal margin)
    {
        if (!Enum.TryParse<UnitOfMeasurement>(getRequiredParam("UOM"), true, out var uom)) return Result<CatalogItem>.Error("Invalid/Missing UOM");

        var item = await _objectTypeService.CreateObjectAsync<CatalogItem>(Context);
        item.CatalogFeedId = catalogFeed.Id;
        item.EntityId = catalogFeed.EntityId;
        item.SKU = mal.ExternalId;
        item.Name = mal.Name;
        // item.Description = mal.Description;
        item.Margin = margin;
        item.Manufacturer = "Mobile Accessories and Labor";

        item.SellingUnit = new Measurement
        {
            Units = 1,
            UOM = uom,
        };

        item.Costs = new[]
        {
            new ItemCost
            {
                UnitCost = cost,
                UOM = uom
            }
        };

        item.Salesforce = new CatalogItem.SalesforceSync
        {
            Product2 = getRequiredParam("SfProductId"),
            ExternalId = getRequiredParam("SfExternalId"),
        };

        if (!Enum.TryParse<MaterialType>(getRequiredParam("MaterialType"), true, out var materialType)) return Result<CatalogItem>.Error("Invalid/Missing Material Type");
        if (!Enum.TryParse<MaterialSubType>(getRequiredParam("MaterialSubType"), true, out var materialSubType)) return Result<CatalogItem>.Error("Invalid/Missing Material Sub Type");
        item.Material = new MaterialClassification
        {
            Type = materialType,
            SubType = materialSubType
        };

        return Result.Success(item);

        string getRequiredParam(string name)
        {
            if (!mal.Properties.TryGetValue(name, out var value)) throw new BadRequestException($"Invalid or missing {name}");
            if (value is string str) return str;
            throw new BadRequestException($"Invalid type for {name}");
        }
    }

    private Result<UpdateQuery<CatalogItem>> GetUpdateModelQuery(CustomObject mal, CatalogItem item, decimal cost, decimal margin)
    {
        if (!Enum.TryParse<UnitOfMeasurement>(getRequiredParam("UOM"), true, out var uom)) return Result<UpdateQuery<CatalogItem>>.Error("Invalid/Missing UOM");

        // UPDATE
        item.Margin = margin;
        item.Costs = new[]
        {
            new ItemCost
            {
                UnitCost = cost,
                UOM = uom
            }
        };

        var update = _connection.Filter<CatalogItem>()
            .Eq(x => x.Id, item.Id)
            .Update
            .Set(x => x.Costs, item.Costs)
            .Set(x => x.Margin, item.Margin)
            .SetOrUnset(x => x.StandardCost, item.StandardCost)
            // .SetOrUnset(x => x.StandardPrice, item.StandardPrice)
            .SetOrUnset(x => x.CutCost, item.CutCost)
            // .SetOrUnset(x => x.CutPrice, item.CutPrice)
            .SetOrUnset(x => x.PalletCost, item.PalletCost)
            // .SetOrUnset(x => x.PalletPrice, item.PalletPrice)
            .Set(x => x.LastActor, Context.Actor())
            .Set(x => x.LastModifiedOn, DateTime.UtcNow);

        return Result.Success(update);

        string getRequiredParam(string name)
        {
            if (!mal.Properties.TryGetValue(name, out var value)) throw new BadRequestException($"Invalid or missing {name}");
            if (value is string str) return str;
            throw new BadRequestException($"Invalid type for {name}");
        }
    }

    private async Task<Result<CatalogItem>> UpdateItemAsync(CustomObject mal, CatalogItem item, decimal cost, decimal margin)
    {
        var update = GetUpdateModelQuery(mal, item, cost, margin);
        if (update)
        {
            var result = await update.Value.UpdateAndGetOneAsync();
            return result == null ? Result<CatalogItem>.Error("Item not found") : Result.Success(result);
        }

        return Result<CatalogItem>.Error(update.Status);
    }

    private async Task<(CustomObject MAL, CatalogItem Item)> GetItemAsync(CatalogFeed catalogFeed, string id)
    {
        var mal = await _connection.Filter<CustomObject>("fcb2b.MAL")
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.ExternalId, id)
            .FirstOrDefaultAsync();

        if (mal == null) return (null, null);

        var item = await _connection.Filter<CatalogItem>(Context)
            .Eq(x => x.AccountId, catalogFeed.AccountId)
            .Eq(x => x.EntityId, catalogFeed.EntityId)
            .Eq(x => x.CatalogFeedId, catalogFeed.Id)
            .Eq(x => x.SKU, id)
            .FirstOrDefaultAsync();

        return (mal, item);
    }

    [Authorize("managerplus")]
    [HttpGet("/productcatalog/v1/CatalogFeed/mal({catalogFeedId})/DataForm")]
    public async Task<Form> GetEditFormAsync([FromRoute] Guid catalogFeedId, [FromQuery] string id)
    {
        var catalogFeed = await _connection.Filter<CatalogFeed, MALCatalogFeed>(Context)
            .Eq(x => x.Id, catalogFeedId)
            .FirstOrDefaultAsync();

        var (mal, item) = await GetItemAsync(catalogFeed, id);
        if (mal == null) throw new NotFoundException();

        return new Form
        {
            Name = "Edit",
            Title = mal.Name,
            Fields = new FormField[] {
                new TextField
                {
                    Name = "UOM",
                    Label = "UOM",
                    DefaultValue = mal.Properties.TryGetValue("UOM", out var uom) ? uom.ToString() : null,
                    Enable = new[] {"false"},
                },
                new NumberField
                {
                    Name = "Cost",
                    Label = "Cost",
                    DefaultValue = item?.StandardCost?.UnitCost,
                    NumberFieldOptions = {
                        Style = NumberFieldOptionsStyle.Currency,
                    }
                },
                new NumberField
                {
                    Name = nameof(CatalogItem.Margin),
                    Label = "Margin (%)",
                    DefaultValue = item?.Margin ?? 50,
                },
                new TextField
                {
                    Name = nameof(CatalogItem.SKU),
                    DefaultValue = mal.ExternalId,
                    Enable = new[] {"false"},
                },
                // new TextField
                // {
                //     Name = nameof(CatalogItem.Name),
                //     DefaultValue = mal.Name,
                //     Enable = new string[] {"false"},
                // },
                new TextField
                {
                    Name = "MaterialType",
                    Label = "Type",
                    DefaultValue = mal.Properties.TryGetValue("MaterialType", out var type) ? type.ToString() : null,
                    Enable = new[] {"false"},
                },
                new TextField
                {
                    Name = "MaterialSubType",
                    Label = "Sub Type",
                    DefaultValue = mal.Properties.TryGetValue("MaterialSubType", out var subType) ? subType.ToString() : null,
                    Enable = new[] {"false"},
                },
                // new TextField
                // {
                //     Name = "ExternalId",
                //     Label = "SF Product",
                //     DefaultValue = mal.Properties.TryGetValue("SfProductId", out var productId) ? productId.ToString() : null,
                //     Enable = new string[] {"false"},
                // },
            },
            Actions = new[] {
                new FormAction
                {
                    Name = "Update"
                },
                new FormAction
                {
                    Name = "#cancel",
                    Label = "Cancel"
                },
            }
        };
    }
}