using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.ProductCatalog.Models;
using PI.ProductCatalog.Models.MeasureSquare;
using PI.Shared.Models;
using Services;

namespace PI.ProductCatalog.Services;

public class MeasureSquareService(
    ILogger<MeasureSquareService> logger,
    MongoConnection connection,
    IHttpClientFactory factory
)
{
    private JsonSerializerOptions JsonSerializerOptions { get; } = new JsonSerializerOptions
    {
        Converters = { new JsonStringEnumConverter() }
    };

    HttpClient Client
    {
        get
        {
            var client = factory.CreateClient(nameof(MeasureSquareService));
            client.BaseAddress = new Uri("https://calculator.measuresquare.com/");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", "Rmxvb3IgQ292ZXJpbmdzIEludGVybmF0aW9uYWw6UGFzc3dvcmQ=");
            return client;
        }
    }

    public async Task<Result<EstimateSeams>> CalculateAsync(IEntityContext context, Estimate estimate, SeamOptions options)
    {
        var mainItems = estimate.LineItems
            .Where(x => x.Criteria == QuantityCriteria.RoomArea && x.Source == LineItemSource.Item && x.Children?.Length > 0)
            .ToArray();

        var items = (await connection.Filter<CatalogItem>()
                .Eq(x => x.AccountId, estimate.AccountId)
                .Eq(x => x.EntityId, estimate.EntityId)
                .In(x => x.Id, mainItems.Select(x => x.ItemId))
                .Ne(x => x.IsActive, false)
                // .Eq(x => x.Material.IsRollGoods, true)
                .In(x => x.Material.Type, [MaterialType.Carpet, MaterialType.Vinyl])
                .FindAsync()
            )
            .Where(x => x.Material.IsRollGoods) // not all items will have it
            .ToArray();

        if (items.Length != 1)
        {
            logger.LogError("Couldn't pick line item to estimate: Found {ItemsCount}", items.Length);
            return Result.Error<EstimateSeams>($"Couldn't determine what item to estimate: found {items.Length} items");
        }

        var rooms = await connection.Filter<AbstractRoom>()
            .Eq(x => x.AccountId, estimate.AccountId)
            .Eq(x => x.Parent.ObjectId, estimate.ProjectExternalId)
            .Eq(x => x.Parent.ObjectType, "sf_WorkOrder")
            .Ne(x => x.IsActive, false)
            .FindAsync();

        var roomDict = rooms.DistinctBy(x => x.Name).ToDictionary(x => x.Name);

        var item = items[0];
        var polygons = new List<PolygonRoom>();
        var lineItems = estimate.LineItems.Where(x => x.ItemId == item.Id).ToArray();
        foreach (var lineItem in lineItems)
        {
            if (lineItem.Children?.Length > 0)
            {
                foreach (var childItem in lineItem.Children)
                {
                    if (!roomDict.TryGetValue(childItem.Name, out var room))
                    {
                        logger.LogError("Couldn't find {Room} for {SKU}", childItem.Name, lineItem.SKU);
                        return Result.Error<EstimateSeams>($"Couldn't find Room: {childItem.Name}");
                    }

                    if (room is RegularRoom regularRoom)
                    {
                        polygons.Add(ToPolygonRoom(regularRoom));    
                    }
                    else
                    {
                        // TODO: will have to track stairs independently
                        // ...
                    }
                }
            }
            else
            {
                logger.LogError("Line Item doesn't have any children: {SKU}", lineItem.SKU);
                return Result.Error<EstimateSeams>($"Invalid Line Item: {lineItem.SKU}");
            }
        }

        var rollWidth = options.RollWidthInches ?? (item.RollWidth.HasValue ? (int)(item.RollWidth.Value * 12) : null);
        var rollLength = options.RollLengthInches ?? (item.RollLength.HasValue ? (int)(item.RollLength.Value * 12) : null);
        if (!rollWidth.HasValue || !rollLength.HasValue || rollLength <= 0 || rollWidth <= 0)
        {
            return Result.Error<EstimateSeams>($"Roll Width and/or Length not available for {item.SKU}");
        }

        var horiRepeat = options.HorizRepeatInches;
        if (!horiRepeat.HasValue && item.PatternWidth != null)
        {
            if (!item.PatternWidth.ConvertTo(UnitOfMeasurement.Inches, out var value))
            {
                return Result.Error<EstimateSeams>($"Couldn't convert pattern width: {item.PatternWidth.Units} {item.PatternWidth.UOM} for {item.SKU}");
            }

            horiRepeat = (int)value.Units;
        }

        var vertRepeat = options.VertRepeatInches;
        if (!vertRepeat.HasValue && item.PatternWidth != null)
        {
            // ...
        }

        var horiDrop = options.HorizDropInches;
        if (!horiDrop.HasValue && item.PatternDrop != null)
        {
            // ...
        }

        var vertDrop = options.VertDropInches;
        if (!horiRepeat.HasValue && item.PatternWidth != null)
        {
            // ....
        }

        var modelItems = new List<object>
        {
            new FloorProduct
            {
                Type = item.Material.Type switch
                {
                    MaterialType.Carpet => FloorProductType.Carpet,
                    MaterialType.Vinyl => FloorProductType.Vinyl,
                    _ => throw new NotImplementedException("Unexpected material type"),
                },
                ID = item.Name,
                Width = InchesToString(rollWidth.Value),
                Length = InchesToString(rollLength.Value),
                HoriRepeat = InchesToString(horiRepeat),
                VertRepeat = InchesToString(vertRepeat),
                HoriDrop = InchesToString(horiDrop),
                VertDrop = InchesToString(vertDrop),
            }
        };

        modelItems.AddRange(polygons);

        var model = new M2Script
        {
            Direction = options.Direction,
            CutMargin = $"{options.CutMarginInches}\"",
            MaxTSeamCount = options.MaxTSeamCount,
            // GroutWidth = "0.25\"",
            Items = modelItems,
        };

        var result = await RequestAsync(model);
        if (!result.IsSuccess) return result.ConvertTo<EstimateSeams>();

        var qtty = result.Value.ProductEstimates.FirstOrDefault(x => x.ID == item.Name);
        if (qtty == null) return Result.Error<EstimateSeams>($"Couldn't find Adjusted Quantity");
        if (!decimal.TryParse(qtty.Usage, out var usage) || !decimal.TryParse(qtty.ShapeQty, out var roomArea)) return Result.Error<EstimateSeams>($"Invalid Result: {qtty.Usage} {qtty.ShapeQty}");
        var waste = 100 * (usage - roomArea) / roomArea;
        foreach (var lineItem in lineItems)
        {
            lineItem.WasteFactor = waste;
            lineItem.Recalculate();
        }

        var totals = EstimateService.CalculateTotals(estimate.LineItems, estimate.TaxRates, estimate.IsNonTaxable);
        estimate.TotalCost = totals.TotalCost;
        estimate.TotalPrice = totals.TotalPrice;
        estimate.BlendedMargin = totals.BlendedMargin;
        estimate.TaxLiabilities = totals.TaxLiabilities;
        estimate.TotalTax = totals.TotalTax;

        return Result.Success(new EstimateSeams
        {
            Item = item,
            LineItems = lineItems,
            WasteFactor = waste,
            CalculatedArea = roomArea,
            CalculatedQuantity = usage,
            Response = result.Value,
        });
    }

    // public async Task<Result<Response>> CalculateAsync(IEntityContext context, RoomSelection roomSelection, SeamOptions options)
    // {
    //     var rooms = await connection.Filter<AbstractRoom>()
    //         .Eq(x => x.AccountId, roomSelection.AccountId)
    //         .In(x => x.Id, roomSelection.RoomIds)
    //         .FindAsync();
    //
    //     var pRooms = rooms
    //         .Where(x => x.Surfaces.Length == 1)
    //         .Select(ToPolygonRoom);
    //
    //     var items = new List<object>
    //     {
    //         new FloorProduct
    //         {
    //             Type = FloorProductType.Carpet,
    //             ID = "carpet",
    //             Width = InchesToString(options.RollWidthInches),
    //             Length = InchesToString(options.RollLengthInches),
    //             HoriRepeat = InchesToString(options.HorizRepeatInches),
    //             VertRepeat = InchesToString(options.VertRepeatInches),
    //             HoriDrop = InchesToString(options.HorizDropInches),
    //             VertDrop = InchesToString(options.VertDropInches),
    //         }
    //     };
    //
    //     items.AddRange(pRooms);
    //
    //     var model = new M2Script
    //     {
    //         Direction = options.Direction,
    //         CutMargin = $"{options.CutMarginInches}\"",
    //         MaxTSeamCount = options.MaxTSeamCount,
    //         // GroutWidth = "0.25\"",
    //         Items = items,
    //     };
    //
    //     return await RequestAsync(model);
    // }

    private PolygonRoom ToPolygonRoom(RegularRoom room)
    {
        var matrix = room.Transformation;
        var surface = room.Surfaces[0];
        var points = string.Join("|",
            surface.Shape
                .Select(p => new ShapePoint
                {
                    X = p.X * matrix[0] + p.Y * matrix[1] + 1 * matrix[2],
                    Y = p.X * matrix[3] + p.Y * matrix[4] + 1 * matrix[5],
                })
                .Select(p => $"{p.X * 10},{p.Y * 10}")
        );

        return new PolygonRoom
        {
            RoomName = room.Name,
            Points = points,
        };
    }

    private string InchesToString(int? inches)
    {
        if (inches == null) return "0'0\"";

        var onlyIn = inches % 12;
        var ft = (inches - onlyIn) / 12;
        return $"{ft}'{onlyIn}\"";
    }

    public async Task<Result<Response>> RequestAsync(M2Script script)
    {
        return await RequestAsync(script.ToXml());
    }

    public async Task<Result<Response>> RequestAsync(string modelScript)
    {
        return await RequestAsync(new Request
        {
            MeasureSystem = MeasurementSystem.Imperial,
            ImageWidth = 1024,
            ModelScript = modelScript,
        });
    }

    public async Task<Result<Response>> RequestAsync(Request request)
    {
        try
        {
            var response = await Client.PostAsJsonAsync("public/calculator", request, JsonSerializerOptions);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return Result.Error<Response>($"Status {response.StatusCode}: {body}");
            }

            var result = JsonConvert.DeserializeObject<Response>(body);
            return Result.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Request failed");
            return Result.Error<Response>(ex.Message);
        }
    }

    public class EstimateSeams
    {
        public LineItem[] LineItems { get; set; }
        public CatalogItem Item { get; set; }
        public decimal WasteFactor { get; set; }
        public decimal CalculatedArea { get; set; }
        public decimal CalculatedQuantity { get; set; }
        public Response Response { get; set; }
    }


    public class SeamOptions
    {
        /// <summary>
        /// install direction
        /// </summary>
        public Direction Direction { get; set; }

        /// <summary>
        /// cut margin in inches 
        /// </summary>
        public int CutMarginInches { get; set; }

        /// <summary>
        /// Max number of t-seams
        /// </summary>
        public int MaxTSeamCount { get; set; }

        public int? RollWidthInches { get; set; }
        public int? RollLengthInches { get; set; }
        public int? HorizRepeatInches { get; set; }
        public int? VertRepeatInches { get; set; }
        public int? HorizDropInches { get; set; }
        public int? VertDropInches { get; set; }
    }
}