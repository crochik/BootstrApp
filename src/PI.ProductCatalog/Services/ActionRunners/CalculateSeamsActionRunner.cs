using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Logging;
using PI.ProductCatalog.Models;
using PI.ProductCatalog.Models.MeasureSquare;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Files;
using PI.Shared.Models.Interfaces;
using PI.Shared.Services;
using PI.Shared.Services.ActionRunners;

namespace PI.ProductCatalog.Services.ActionRunners;

public class CalculateSeamsActionRunner(
    ILogger<CalculateSeamsActionRunner> logger,
    MongoConnection connection,
    ObjectTypeService objectTypeService,
    MeasureSquareService measureSquareService,
    RemoteFileService remoteFileService
) : AbstractRunner<CalculateSeamsActionOptions>
{
    public override Guid ActionId => ActionIds.CalculateSeams;

    protected override async ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, CalculateSeamsActionOptions options)
    {
        Result<FlowEvent> result;
        try
        {
            result = await _RunAsync(context, options);
            logger.LogInformation("{RunId}: Calculated Seams successfully", context.Event.RunId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception calculating seams");
            result = Result.Error<FlowEvent>(ex.Message);
        }

        if (result?.IsError ?? true)
        {
            logger.LogError("{RunId}: Action Failed: {Error}", context.Event.RunId, result?.Status);
            var output = options.Output.FirstOrDefault(x => x.Name == CalculateSeamsActionOptions.Failed);
            if (output?.EventId.HasValue ?? false)
            {
                var evt = new GenericFlowEvent(context.Event)
                {
                    Action = nameof(ActionIds.CalculateSeams),
                    Description = result?.Status ?? output.Description ?? "Failed to calculate seams",
                    EventTypeId = output.EventId,
                };

                return [evt];
            }

            return [];
        }

        return result.IsUnknown ? [] : [result.Value];
    }

    private async Task<Result<FlowEvent>> _RunAsync(ActionRunnerContext context, CalculateSeamsActionOptions options)
    {
        var userId = context.Event.GetUserId();
        var user = userId.HasValue
            ? await connection.Filter<Entity, User>()
                .Eq(x => x.AccountId, context.EntityContext.AccountId.Value)
                .Eq(x => x.Id, userId.Value)
                .Ne(x => x.IsActive, false)
                .FirstOrDefaultAsync()
            : null;
        if (user == null) return Result.Error<FlowEvent>("Can't determine UserId from actor");
        var userContext = user.Context;

        var runContext = context.Run.BuildHandlebarsContext(context.Event);

        var direction = ParseDirection(resolve(options.Direction));
        if (!direction.HasValue) return Result.Error<FlowEvent>($"Couldn't resolve Direction: {options.Direction}");

        var maxTCount = resolveInt(options.MaxTSeamCount);
        if (!maxTCount.HasValue) return Result.Error<FlowEvent>($"Couldn't resolve Max T Seam Count: {options.MaxTSeamCount}");

        // will try to fall back to product info
        if (!resolveOptionalInt(options.RollLength, out var rollLength)) return Result.Error<FlowEvent>($"Couldn't resolve Roll Length: {options.RollLength}");
        if (!resolveOptionalInt(options.RollWidth, out var rollWidth)) return Result.Error<FlowEvent>($"Couldn't resolve Roll Width: {options.RollWidth}");

        var cutMarginInches = resolveInt(options.CutMarginInches);
        if (!cutMarginInches.HasValue) return Result.Error<FlowEvent>($"Couldn't resolve Cut Margin (Inches): {options.CutMarginInches}");

        // TODO: validate?
        // options.UploadFileOptions

        var seamOptions = new MeasureSquareService.SeamOptions
        {
            Direction = direction.Value,
            MaxTSeamCount = maxTCount.Value,
            CutMarginInches = cutMarginInches.Value,
            RollLengthInches = rollLength * 12, // ft => in
            RollWidthInches = rollWidth * 12, // ft => in
            // HorizRepeatInches = 
            // VertRepeatInches = 
            // HorizDropInches = 
            // VertDropInches =             
        };

        var result = context.Event.ObjectType switch
        {
            Estimate.ObjectTypeFullName => await CalculateForEstimateAsync(context, userContext, options, seamOptions, runContext),
            _ => Result.Error<FlowEvent>($"Action not implemented for {context.Event.ObjectType}"),
        };

        return result;

        bool resolveOptionalInt(object input, out int? output)
        {
            var outputValue = resolve(input);
            if (outputValue == null)
            {
                output = null;
                return true;
            }

            output = ParseInt(outputValue);
            return output.HasValue;
        }

        object resolve(object input) => ExpressionEvaluatorService.TryResolve(userContext, runContext, input, out var outValue) ? outValue : null;
        int? resolveInt(object input) => ExpressionEvaluatorService.TryResolve(userContext, runContext, input, out var outValue) ? ParseInt(outValue) : null;
    }

    private async Task<Result<FlowEvent>> CalculateForEstimateAsync(ActionRunnerContext context, IEntityContext userContext, CalculateSeamsActionOptions options, MeasureSquareService.SeamOptions seamOptions, ExpandoObject runContext)
    {
        var estimate = await connection.Filter<Estimate>()
            .Eq(x => x.AccountId, userContext.AccountId.Value)
            // .Eq(x => x.EntityId, userContext.OrganizationId.Value)
            .Eq(x => x.Id, context.Event.TargetId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (estimate == null)
        {
            return Result.Error<FlowEvent>($"Object Not found: {context.Event.ObjectType} {context.Event.TargetId}");
        }

        var result = await measureSquareService.CalculateAsync(userContext, estimate, seamOptions);

        if (!result.IsSuccess) return result.ConvertTo<FlowEvent>();

        RemoteFile remoteFile = null;
        var uploadOptions = options.UploadFileOptions;
        if (uploadOptions != null && !options.DryRun)
        {
            using var stream = new MemoryStream(result.Value.Response.ImageBytes);
            remoteFile = await remoteFileService.UploadAsync(userContext, stream, "image/png", $"{Guid.CreateVersion7()}.png", uploadOptions, runContext);
            remoteFile.Parent = new ReferencedObject
            {
                ObjectId = context.Event.TargetId,
                ObjectType = context.Event.ObjectType,
            };
            // remoteFile.PublicUrl
            remoteFile.AllowAnonymousDownload = true;
            remoteFile.Tags = (remoteFile.Tags ?? []).Append("Seaming Diagram").ToArray();

            var refs = remoteFile.Refs ?? Enumerable.Empty<KeyValuePair<string, object>>();
            if (estimate.RelatedObjects?.Count > 0) refs = refs.Concat(estimate.RelatedObjects.Select(x => x));
            refs = refs.Append(new KeyValuePair<string, object>("sf_WorkOrder", estimate.ProjectExternalId));
            remoteFile.Refs = refs.DistinctBy(x => x.Key).ToList();

            await connection.InsertAsync(remoteFile);
            await objectTypeService.FireCreateEventAsync(userContext, remoteFile);
        }

        if (remoteFile != null)
        {
            estimate.Attachments ??= new Dictionary<string, EstimateAttachment>();
            
            // TODO: bad, as it assumes only one per proposal but.... 
            estimate.Attachments["M2"] = new EstimateAttachment
            {
                Tag = result.Value.Item.SKU,
                Name = "Carpet Seaming Diagram",
                Description = $"Measure Square Seaming Diagram for {result.Value.Item.Name}",
                RemoteFileId = remoteFile.Id,
            };
        }

        if (!options.DryRun)
        {
            // update estimate
            var updated = await connection.Filter<Estimate>()
                .Eq(x => x.AccountId, estimate.AccountId)
                .Eq(x => x.Id, estimate.Id)
                .Ne(x => x.IsActive, false)
                .Eq(x => x.LastModifiedOn, estimate.LastModifiedOn)
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
                .Set(x => x.Attachments, estimate.Attachments)
                .Set(x => x.LastActor, userContext.Actor())
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .UpdateAndGetOneAsync();

            if (updated == null) Result.Error<FlowEvent>("Failed to Update Estimate");

            await objectTypeService.FireObjectUpdatedAsync(userContext, estimate, new Dictionary<string, object>
            {
                { "LineItems", "*" },
                { "TotalCost", estimate.TotalCost },
                { "TotalPrice", estimate.TotalPrice },
                { "BlendedMargin", estimate.BlendedMargin },
                { "TaxLiabilities", "*" },
                { "TotalTax", estimate.TotalTax },
                { "IsNonTaxable", estimate.IsNonTaxable },
                { "GrandTotal", estimate.GrandTotal },
                { "GrandTax", estimate.GrandTax },
                { "Attachments", "*" },
            }, e => { e.Description = $"Updated Estimate for {result.Value.Item.SKU}"; });
        }

        var output = options.Output.FirstOrDefault(x => x.Name == CalculateSeamsActionOptions.Success);
        if (output?.EventId.HasValue ?? false)
        {
            var evt = new GenericFlowEvent(context.Event)
            {
                Action = nameof(ActionIds.CalculateSeams),
                Description = output.Description ?? "Calculated Seams using M2",
                EventTypeId = output.EventId,
            };
            if (remoteFile != null)
            {
                evt.AddRefValue("RemoteFile", remoteFile.Id);
                evt.SetMetaValue($"Action|Output|RemoteFile", remoteFile.Id);
            }

            return Result.Success<FlowEvent>(evt);
        }

        return Result.Unknown<FlowEvent>("No Output");
    }

    private static Direction? ParseDirection(object value) => value switch
    {
        nameof(Direction.Auto) => Direction.Auto,
        nameof(Direction.Horizontal) => Direction.Horizontal,
        nameof(Direction.Vertical) => Direction.Vertical,
        _ => null,
    };

    private static int? ParseInt(object value) => value switch
    {
        int i => i,
        short i => i,
        long l => (int)l,
        decimal d => (int)d,
        string str => int.TryParse(str, out var i) ? i : null,
        _ => null,
    };
}

public class CalculateSeamsActionOptions : ActionOptions
{
    public const string Success = nameof(Success);
    public const string Failed = nameof(Failed);

    /// template, resolve to in
    public string CutMarginInches { get; set; }

    public string Direction { get; set; }

    public string MaxTSeamCount { get; set; }

    /// <summary>
    /// template, resolve to ft 
    /// </summary>
    public string RollLength { get; set; }

    /// <summary>
    /// template, resolve to ft
    /// </summary>
    public string RollWidth { get; set; }

    public bool DryRun { get; set; }

    public UploadFileOptions UploadFileOptions { get; set; }
}