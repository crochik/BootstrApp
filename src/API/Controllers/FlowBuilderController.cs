using Crochik.Mongo;
using Messages.Flow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using Newtonsoft.Json;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Requests;
using PI.Shared.Services;
using Services;
using Flow = PI.Shared.Models.Flow;

namespace Controllers;

[Authorize("admin")]
[Route("/api/v1/[controller]")]
public class FlowBuilderController(
    MongoConnection connection,
    FlowService flowService
    ) : APIController
{
    // [HttpPost("Init")]
    // public async Task<IActionResult> InitAsync([FromServices] IEnumerable<IFlowActionBuilder> builders)
    // {
    //     var genericBuilders = new IGenericActionBuilder[]
    //     {
    //         new CreateObjectActionOptions(),
    //         new UpdateObjectActionOptions(),
    //         new LookupObjectActionOptions(),
    //         new LeadTypeServiceUsageActionOptions(),
    //         new TrustedFormCertActionOptions(),
    //     };
    //
    //     var objectTypes = genericBuilders.Select(x => x.BuildActionOptionsObjectType(Context));
    //     await _connection.InsertAsync(objectTypes);
    //     await _connection.InsertAsync(genericBuilders.Select(x => x.BuildGenericAction(Context)));
    //
    //     return Ok();
    // }

    [HttpGet("/api/v1/[controller]({id})")]
    public async Task<FlowTree> GetFlowAsync([FromRoute] Guid id, [FromServices] FlowTreeBuilder builder)
    {
        var flow = await connection.Filter<Flow>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (flow == null) throw NotFoundException.New<Flow>(id);

        return await builder.BuildAsync(Context, flow);
    }

    [HttpGet("/api/v1/[controller]({id})/ObjectStatus")]
    public async Task<IEnumerable<ReferenceValue>> GetObjectStatusAsync([FromRoute] Guid id)
    {
        var flow = await connection.Filter<Flow>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .IncludeField(x => x.ObjectType)
            .FirstOrDefaultAsync();

        if (flow == null) throw NotFoundException.New<Flow>(id);

        var objectStatuses = await connection.Filter<PI.Shared.Models.ObjectStatus>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.ObjectType, flow.ObjectType)
            .SortAsc(x => x.Name)
            .FindAsync();

        return objectStatuses.Select(x => new ReferenceValue
        {
            Id = x.Id.ToString(),
            Value = x.Name,
            Description = x.Description,
        });
    }

    /// <summary>
    /// Add Action
    /// Step 1: Get action form
    /// TODO: include objectStatus 
    /// ...  
    /// </summary>
    [HttpGet("/api/v1/[controller]({flowId})/EventType({eventTypeId})/Action/DataForm")]
    public async Task<Form> GetFlowActionsForEventTypeAsync(
        [FromRoute] Guid flowId,
        [FromRoute] Guid eventTypeId,
        [FromQuery] Guid? objectStatusId)
    {
        var builders = await flowService.GetActionBuilderNamesAsync(Context, flowId, eventTypeId);

        var form = new Form
        {
            Name = "Actions",
            Title = "Add Reaction",
            Fields = new FormField[]
            {
                new SelectField
                {
                    Name = "FlowActionId",
                    Label = "Action",
                    SelectFieldOptions = new SelectFieldOptions
                    {
                        Items = builders
                            .OrderBy(x => x.Value)
                            .ToDictionary(x => x.Key.ToString(), x => x.Value),
                    },
                }
            },
            Actions = new[]
            {
                new FormAction
                {
                    Name = "Next",
                    Action = "Next",
                    // Action = $"dataForm://api/v1/FlowBuilder({flowId})/EventType({eventTypeId})/Action",
                }
            }
        };

        return form;
    }

    /// <summary>
    /// Add Action
    /// Step 1: redirects to action form 
    /// </summary>
    [HttpPost("/api/v1/[controller]({flowId})/EventType({eventTypeId})/Action/DataForm")]
    public DataFormActionResponse GetFlowActionAsync(
        [FromRoute] Guid flowId,
        [FromRoute] Guid eventTypeId,
        [FromQuery] Guid? objectStatusId,
        [FromBody] DataFormActionRequest request)
    {
        if (!(request.Parameters?.TryGetGuidParam("FlowActionId", out var actionId) ?? false))
        {
            throw new BadRequestException("Missing required Action");
        }

        return new DataFormActionResponse(request)
        {
            Success = true,
            NextUrl =
                $"dataForm://api/v1/FlowBuilder({flowId})/EventType({eventTypeId})/Action({actionId})?objectStatusId={objectStatusId}",
        };
    }

    /// <summary>
    /// Add Action
    /// Step 2: action form
    /// </summary>
    [HttpGet("/api/v1/[controller]({flowId})/EventType({eventTypeId})/Action({actionId})/DataForm")]
    public async Task<Form> GetAddActionAsync(
        [FromRoute] Guid flowId,
        [FromRoute] Guid eventTypeId,
        [FromRoute] Guid actionId,
        [FromQuery] Guid? objectStatusId)
    {
        if (objectStatusId == Guid.Empty) objectStatusId = null;
        return await flowService.GetAddStepFormAsync(Context, flowId, eventTypeId, actionId, objectStatusId);
    }

    /// <summary>
    /// Add Action
    /// Step 2: action form
    /// </summary>
    [HttpPost("/api/v1/[controller]({flowId})/EventType({eventTypeId})/Action({actionId})/DataForm")]
    public async Task<DataFormActionResponse> AddActionToFlowAsync(
        [FromRoute] Guid flowId,
        [FromRoute] Guid eventTypeId,
        [FromRoute] Guid actionId,
        [FromBody] DataFormActionRequest request)
    {
        var flow = await flowService.AddStepAsync(Context, flowId, eventTypeId, actionId, request.Parameters);

        return new DataFormActionResponse(request)
        {
            Success = true,
            NextUrl = FormAction.Client_Cancel,
        };
    }

    /// <summary>
    /// Edit Action
    /// Step 2: action form
    /// </summary>
    [HttpGet("/api/v1/[controller]({flowId})/Step({stepId})/DataForm")]
    public async Task<Form> GetStepFormAsync(
        [FromRoute] Guid flowId,
        [FromRoute] Guid stepId)
    {
        return await flowService.GetEditStepFormAsync(Context, flowId, stepId);
    }

    [HttpPost("/api/v1/[controller]({flowId})/Step({stepId})/DataForm")]
    public async Task<DataFormActionResponse> UpdateStepAsync(
        [FromRoute] Guid flowId,
        [FromRoute] Guid stepId,
        [FromBody] DataFormActionRequest request)
    {
        var flow = request.Action switch
        {
            FormAction.Delete => await flowService.DeleteStepAsync(Context, flowId, stepId),
            FormAction.Update => await flowService.UpdateStepAsync(Context, flowId, stepId, request.Parameters),
            _ => throw new BadRequestException("Invalid action"),
        };

        return new DataFormActionResponse(request)
        {
            Success = true,
            NextUrl = FormAction.Client_Cancel,
        };
    }

    [HttpGet("/api/v1/[controller]({flowId})/Step({stepId})/Placeholders/DataForm")]
    public async Task<Form> PlaceholdersFormAsync(
        [FromRoute] Guid flowId,
        [FromRoute] Guid stepId)
    {
        var flow = await connection.Filter<Flow>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, flowId)
            .FirstOrDefaultAsync();
        
        var placeholders = await flowService.GetPlaceholdersAsync(Context, flow, stepId);

        return new Form
        {
            Name = "Placeholders",
            Fields = new FormField[]
            {
                new SelectField
                {
                    Name = "Placeholder",
                    SelectFieldOptions =new SelectFieldOptions
                    {
                        Items = placeholders
                            .Reverse()
                            .DistinctBy(x=>x.Name)
                            .OrderBy(x=>x.Name)
                            .ToDictionary(x=>x.Name, x=> $"{x.Name} - {x.Description} ({x.Type}: {x.ObjectType})")
                    }
                }
            },
        };
    }

    [HttpGet("/api/v1/[controller]({flowId})/Step({stepId})/Copy/DataForm")]
    public async Task<Form> CopyStepFormAsync(
        [FromRoute] Guid flowId,
        [FromRoute] Guid stepId)
    {
        return await CopyStepFormAsync(flowId, stepId, false);
    }

    [HttpGet("/api/v1/[controller]({flowId})/Step({stepId})/Cut/DataForm")]
    public async Task<Form> CutStepFormAsync(
        [FromRoute] Guid flowId,
        [FromRoute] Guid stepId)
    {
        return await CopyStepFormAsync(flowId, stepId, true);
    }

    private async Task<Form> CopyStepFormAsync(Guid flowId, Guid stepId, bool cut)
    {
        var operation = cut ? "Cut" : "Copy";

        var clipboard = await flowService.CopyToClipboardAsync(Context, flowId, stepId, cut);
        if (!clipboard) return Form.BuildErrorForm(clipboard.Status, $"Failed to {operation}");

        return new Form
        {
            Name = operation,
            Title = $"{operation} Reaction Chain",
            Fields = new FormField[]
            {
                new LabelField
                {
                    Name = "Success",
                    Label = clipboard.Value.Steps.Length == 1
                        ? "Reaction copied to clipboard."
                        : $"Chain Reaction with {clipboard.Value.Steps.Length} Steps copied into clipboard",
                    LabelFieldOptions = new LabelFieldOptions
                    {
                        Color = PalletColor.Primary,
                    }
                },
                new CheckboxField
                {
                    Name = nameof(Clipboard.IsShared),
                    Label = "Add to Library",
                    DefaultValue = false,
                    Visible = cut ? new[] { "false" } : null,
                },
                new TextField
                {
                    Name = nameof(Clipboard.Name),
                    DefaultValue = clipboard.Value.Name,
                    Visible = new[] { nameof(Clipboard.IsShared) }
                },
                new TextField
                {
                    Name = nameof(Clipboard.Description),
                    DefaultValue = clipboard.Value.Description,
                    Visible = new[] { nameof(Clipboard.IsShared) }
                },
            },
            Actions = new[]
            {
                new FormAction
                {
                    Name = "Save to Library",
                    Action = FormAction.Client_Save,
                    Visible = new[] { nameof(Clipboard.IsShared) }
                },
                new FormAction
                {
                    Name = "OK",
                    Action = FormAction.Client_Save,
                    Visible = new[] { $"!{nameof(Clipboard.IsShared)}" }
                }
            }
        };
    }

    /// <summary>
    /// Paste into a top level event (e.g. system, user, timer trigger)
    /// </summary>
    [HttpGet("/api/v1/[controller]({flowId})/EventType({eventTypeId})/Paste/DataForm")]
    public async Task<Form> GetPasteStepFormForEventAsync(
        [FromRoute] Guid flowId,
        [FromRoute] Guid eventTypeId,
        [FromQuery] Guid? objectStatusId)
    {
        if (objectStatusId == Guid.Empty) objectStatusId = null;
        return await flowService.GetPasteStepFormAsync(Context, flowId, objectStatusId, eventTypeId);
    }

    /// <summary>
    /// Paste into a top level event (e.g. system, user, timer trigger)
    /// </summary>
    [HttpPost("/api/v1/[controller]({flowId})/EventType({eventTypeId})/Paste/DataForm")]
    public async Task<DataFormActionResponse> PasteStepForEventFormAsync(
        [FromRoute] Guid flowId,
        [FromRoute] Guid eventTypeId,
        [FromQuery] Guid? objectStatusId,
        [FromBody] DataFormActionRequest request)
    {
        if (objectStatusId == Guid.Empty) objectStatusId = null;

        if (!request.TryGetGuidParam(nameof(Clipboard), out var clipboardId))
        {
            throw new BadRequestException("Missing required clipboard parameter");
        }

        var clipboard = await connection.Filter<Clipboard, FlowStepsClipboard>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.EntityId, Context.EntityId.Value)
            .Eq(x => x.Id, clipboardId)
            .FirstOrDefaultAsync();

        var result = await flowService.PasteStepAsync(Context, flowId, eventTypeId, objectStatusId, clipboard,
            request.Parameters);
        if (!result)
        {
            return new DataFormActionResponse(request, result.Status);
        }

        return new DataFormActionResponse(request, "Pasted", true);
    }

    /// <summary>
    /// Paste into a step output
    /// </summary>
    [HttpGet("/api/v1/[controller]({flowId})/Step({stepId})/Paste/DataForm")]
    public async Task<Form> GetPasteStepFormAsync(
        [FromRoute] Guid flowId,
        [FromRoute] Guid stepId)
    {
        return await flowService.GetPasteStepFormAsync(Context, flowId, stepId);
    }

    /// <summary>
    /// Paste into a step output
    /// </summary>
    [HttpPost("/api/v1/[controller]({flowId})/Step({stepId})/Paste/DataForm")]
    public async Task<DataFormActionResponse> PasteStepFormAsync(
        [FromRoute] Guid flowId,
        [FromRoute] Guid stepId,
        [FromBody] DataFormActionRequest request)
    {
        if (!request.TryGetGuidParam(nameof(Clipboard), out var clipboardId))
        {
            throw new BadRequestException("Missing required clipboard parameter");
        }

        var clipboard = await connection.Filter<Clipboard, FlowStepsClipboard>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.EntityId, Context.EntityId.Value)
            .Eq(x => x.Id, clipboardId)
            .FirstOrDefaultAsync();

        var result = await flowService.PasteStepAsync(Context, flowId, stepId, clipboard, request.Parameters);
        if (!result)
        {
            return new DataFormActionResponse(request, result.Status);
        }

        return new DataFormActionResponse(request, "Pasted", true);
    }

    [HttpGet("/api/v1/[controller]({flowId})/Step({stepId})/MoveUp/DataForm")]
    [HttpGet("/api/v1/[controller]({flowId})/Step({stepId})/MoveDown/DataForm")]
    public async Task<Form> MoveStepFormAsync(
        [FromRoute] Guid flowId,
        [FromRoute] Guid stepId)
    {
        // TODO: IMPLEMENT ME
        // ...

        return new Form
        {
            Name = "Move",
            Title = "Move",
            Fields = new FormField[]
            {
            },
            Actions = new[]
            {
                new FormAction
                {
                    Name = "Update",
                }
            }
        };
    }

    /// <summary>
    /// Edit Output
    /// </summary>
    [HttpGet("/api/v1/[controller]({flowId})/Step({stepId})/EventType({eventTypeId})/Output/DataForm")]
    public async Task<Form> GetOutputActionAsync(
        [FromRoute] Guid flowId,
        [FromRoute] Guid stepId,
        [FromRoute] Guid eventTypeId
    )
    {
        var (flow, step) = await flowService.GetStepOrThrowAsync(Context, flowId, stepId);
        var output = step.Options.Output?.FirstOrDefault(x => x.EventId == eventTypeId);
        if (output == null) throw NotFoundException.New("Output");

        return new Form
        {
            Name = "Output",
            Title = $"Output",
            Fields = new FormField[]
            {
                new TextField
                {
                    Name = "Step",
                    DefaultValue = step.Description,
                    Enable = new[] { "false" },
                },
                new TextField
                {
                    Name = nameof(ActionOutput.Description),
                    Label = "Event Description",
                    DefaultValue = output.Description,
                    IsRequired = true,
                },
                // TODO: replace with some kind of color picker
                // even if it is a selectfield with standard color names
                // ..
                new TextField
                {
                    Name = nameof(ActionOutput.Color),
                    Label = "Color",
                    DefaultValue = output.Color,
                    IsRequired = false,
                }
            },
            Actions = new[]
            {
                new FormAction
                {
                    Name = "Update",
                    Enable = new[] { Form.RequiredFieldsName }
                }
            }
        };
    }

    [HttpPost("/api/v1/[controller]({flowId})/Step({stepId})/EventType({eventTypeId})/Output/DataForm")]
    public async Task<DataFormActionResponse> UpdateStepOutputAsync(
        [FromRoute] Guid flowId,
        [FromRoute] Guid stepId,
        [FromRoute] Guid eventTypeId,
        [FromBody] DataFormActionRequest request)
    {
        var (flow, step) = await flowService.GetStepOrThrowAsync(Context, flowId, stepId);
        var output = step.Options.Output?.FirstOrDefault(x => x.EventId == eventTypeId);
        if (output == null) throw NotFoundException.New("Output");

        if (!request.Parameters.TryGetStrParam(nameof(ActionOutput.Description), out var description) ||
            string.IsNullOrWhiteSpace(description))
        {
            return new DataFormActionResponse(request, "Event Description is required");
        }

        if (!request.Parameters.TryGetStrParam(nameof(ActionOutput.Color), out var color) ||
            string.IsNullOrWhiteSpace(color))
        {
            color = null;
        }

        var other = step.Options.Output?.FirstOrDefault(x => x.EventId != eventTypeId && x.Description == description);
        if (other != null)
        {
            return new DataFormActionResponse(request, "There is already another Event with this Description");
        }

        flow = await connection.Filter<Flow>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, flowId)
            .ElemMatchBuilder(
                x => x.Steps, q =>
                    q.Eq(x => x.Id, step.Id)
                        .ElemMatchBuilder(x => x.Options.Output, q => q.Eq(x => x.EventId, eventTypeId))
            )
            .Update
            .Set(
                $"{nameof(Flow.Steps)}.$[step].{nameof(FlowStep.Options)}.{nameof(ActionOptions.Output)}.$[output].{nameof(ActionOutput.Description)}",
                description)
            .SetOrUnset(
                $"{nameof(Flow.Steps)}.$[step].{nameof(FlowStep.Options)}.{nameof(ActionOptions.Output)}.$[output].{nameof(ActionOutput.Color)}",
                color)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, Context.Actor())
            .ArrayFilter(new BsonDocument("step._id", step.Id.ToString()))
            .ArrayFilter(new BsonDocument("output.EventId", eventTypeId.ToString()))
            .UpdateAndGetOneAsync();

        // TODO: fire event
        // ...

        return new DataFormActionResponse(request, "Event Description Updated", true);
    }
}

public class FlowTree // Flow
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string ObjectType { get; set; }
    public FlowObjectStatus[] ObjectStatuses { get; set; }
}

public class FlowObjectStatus
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public EventTypeFlowNode[] Triggers { get; set; }
}

public abstract class FlowNode
{
    [JsonProperty("_t")] 
    // ReSharper disable once InconsistentNaming
    public abstract string _t { get; }

    public Guid? EventTypeId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
}

public class EventTypeFlowNode : FlowNode
{
    public override string _t => "EventType";

    /// <summary>
    /// Step id
    /// </summary>
    public Guid? Id { get; set; }

    public ActionFlowNode[] Actions { get; set; }
    public TriggerType TriggerType { get; set; }
    public string Color { get; set; }
}

public class ActionFlowNode : FlowNode
{
    public override string _t => "Action";

    /// <summary>
    /// Step id
    /// </summary>
    public Guid Id { get; set; }

    public EventTypeFlowNode[] Outputs { get; set; }
    public Guid ActionId { get; set; }
    public string Icon { get; set; }

    [JsonIgnore] public Guid? ObjectStatusId { get; set; }
}