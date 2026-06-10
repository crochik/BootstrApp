using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace FlowActions;

public abstract class AbstractFlowActionBuilder<T, TMessage> : IFlowActionBuilder
    where T : ActionOptions, new()
    where TMessage : ActionMessage<T>, new()
{
    protected const string DescriptionFieldName = "stepDescription";
    protected const string ObjectStatusIdFieldName = "stepObjectStatusId";
    protected const string NextEventName = "next";
    protected const string ErrorEventName = "error";

    protected readonly MongoConnection _connection;

    public abstract Guid Id { get; }
    public abstract string Name { get; }
    public abstract string[] InputObjectTypes { get; }
    public virtual string IconName { get; }
    public virtual string Description { get; }

    protected abstract IActionMessage Build<T2>(T2 context, IActionOptions options) where T2 : FlowEvent;

    protected abstract ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, T opts = null);

    protected abstract Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, T options);

    protected AbstractFlowActionBuilder(MongoConnection connection)
    {
        _connection = connection;
    }

    public virtual bool IsValidTrigger(string objectType)
    {
        return InputObjectTypes == null || InputObjectTypes.Length == 0 || InputObjectTypes.Any(t => objectType != null && string.Equals(t, objectType));
    }

    public virtual (IActionMessage Message, string Route) Build<T2>(IEntityContext context, T2 evt, IActionOptions options) where T2 : FlowEvent
    {
        var message = Build<T2>(evt, options);
        var route = ActionIds.GetRoute(Id);
        return (message, route);
    }

    private async Task<FlowStep> CreateStepAsync(IEntityContext context, Flow flow, Guid eventTypeId, Guid? objectStatusId, Dictionary<string, object> requestParameters, T options)
    {
        var step = new FlowStep
        {
            Id = Guid.NewGuid(),
            EventIdTrigger = eventTypeId,
            CurrentStatusId = objectStatusId,
            Description = Description,
            IconName = IconName,
            ActionId = Id,
            Options = options,
        };

        await AddOutputsAsync(context, flow, requestParameters, step, options);

        return step;
    }

    protected virtual ValueTask<FlowStep> UpdateStepAsync(IEntityContext context, Flow flow, FlowStep step, Dictionary<string, object> requestParameters, T options)
    {
        // preserve outputs
        options.Output = step.Options.Output;

        // hack to start with to preserve event ids!!!!?!!?!?!?
        var eventProps = options.GetType().GetProperties().Where(x => x.Name.EndsWith("EventId"));
        foreach (var prop in eventProps)
        {
            var current = prop.GetValue(step.Options);
            prop.SetValue(options, current);
        }

        // update options
        step.Options = options;

        // update step description
        if (requestParameters.TryGetStrParam(DescriptionFieldName, out var stepDescription))
        {
            step.Description = stepDescription;
        }

        return ValueTask.FromResult(step);
    }

    public ValueTask SwapAsync(IEntityContext context, FlowStep step, Dictionary<Guid, Guid?> swap)
    {
        var options = step.Options;

        // hack update EventIds 
        var eventProps = options.GetType().GetProperties().Where(x => x.Name.EndsWith("EventId"));
        foreach (var prop in eventProps)
        {
            var current = prop.GetValue(options) as Guid?;
            if (current.HasValue && swap.TryGetValue(current.Value, out var newEventId))
            {
                prop.SetValue(options, newEventId);    
            }
        }

        return ValueTask.CompletedTask;
    }

    public virtual ValueTask<IEnumerable<Placeholder>> GetPlaceholdersForOutputAsync(IEntityContext context, Flow flow, ActionOptions triggerOptions, IEnumerable<Placeholder> placeholders, Guid stepEventIdTrigger)
    {
        // remove generated from previous event
        var list = placeholders.Where(x=>!x.Name.StartsWith("{{Event."));

        // default event fields
        list = list.Concat(FlowEvent.GetDefaultEventPlaceHolders());
        
        return ValueTask.FromResult(list);
    }

    public virtual async Task<FlowStep> AddOrUpdateStepAsync(IEntityContext context, Flow flow, Guid eventTypeId, Dictionary<string, object> requestParameters, FlowStep step = null)
    {
        var objectStatusId = default(Guid?);
        if (requestParameters.TryGetGuidParam(ObjectStatusIdFieldName, out var selObjectStatusId) && selObjectStatusId != Guid.Empty)
        {
            objectStatusId = selObjectStatusId;
        }

        var options = JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(requestParameters));

        if (step == null)
        {
            step = await CreateStepAsync(context, flow, eventTypeId, objectStatusId, requestParameters, options);

            // TODO: fire event 
            // ...
            return step;
        }

        step = await UpdateStepAsync(context, flow, step, requestParameters, options);

        // TODO: fire event 
        // ...
        return step;
    }


    public virtual async Task<Form> GetFormAsync(FlowActionContext flowActionContext, Guid? objectStatusId, FlowStep step = null)
    {
        var fields = await getFieldsAsync();

        return new Form
        {
            Name = GetType().Name.ToCamelCase(),
            Title = Description ?? Name,
            Fields = fields.ToArray(),
            Actions = actions().ToArray(),
        };

        IEnumerable<FormAction> actions()
        {
            yield return new FormAction
            {
                Name = "Cancel",
                Action = FormAction.Client_Cancel,
            };

            if (step != null)
            {
                yield return new FormAction
                {
                    Name = "Delete",
                    Action = FormAction.Delete,
                };
            }

            yield return new FormAction
            {
                Name = step == null ? "Add" : "Update",
                Action = step == null ? FormAction.Add : FormAction.Update,
            };
        }

        async Task<IEnumerable<FormField>> getFieldsAsync()
        {
            if (step != null)
            {
                if (step.Options is not T opts) throw new BadRequestException("Invalid options");
                var fields = await GetFieldsAsync(flowActionContext, step, opts);

                // editing, always add "Description" field
                return fields.Prepend(new TextField
                {
                    Name = DescriptionFieldName,
                    Label = "Step Description",
                    DefaultValue = step.Description,
                });
            }
            else
            {
                var fields = await GetFieldsAsync(flowActionContext);

                // add hidden field with current status
                return fields.Append(
                    new HiddenField
                    {
                        Name = ObjectStatusIdFieldName,
                        DefaultValue = objectStatusId,
                    }
                );

                //     // adding, always add current status field 
                //     fields = fields.Append(new ReferenceField
                //     {
                //         Name = ObjectStatusIdFieldName,
                //         Label = "When the Status is...",
                //         IsRequired = true,
                //         ReferenceFieldOptions = new ReferenceFieldOptions
                //         {
                //             ObjectType = nameof(ObjectStatus),
                //             Criteria = new[]
                //             {
                //                 Condition.Eq(nameof(ObjectStatus.ObjectType), flowActionContext.ObjectType),
                //             },
                //             Items = new Dictionary<string, object>
                //             {
                //                 { Guid.Empty.ToString(), "[ANY]" }
                //             }
                //         },
                //         DefaultValue = objectStatusId ?? Guid.Empty,
                //     });
            }
        }
    }

    /// <summary>
    /// Create event and return output for it
    /// </summary>
    protected Task<(EventType, ActionOutput)> AddEventAsync(IEntityContext context, string objectType, Guid? objectStatusId, string name, string description, string outputName)
    {
        var eventType = new EventType
        {
            AccountId = context.AccountId.Value,
            EntityId = context.AccountId.Value, // ????
            Id = Guid.NewGuid(),
            ObjectType = objectType,
            CreatedOn = DateTime.UtcNow,
            LastActor = context.Actor(),
            Name = name,
            Description = description,
            Trigger = new Trigger
            {
                Name = outputName,
                ActionId = Id,
                ObjectStatusId = objectStatusId,
            }
        };

        // do not add events to database
        // await _connection.InsertAsync(eventType);

        return Task.FromResult((eventType, new ActionOutput
        {
            Name = outputName,
            EventId = eventType.Id,
            Description = description,
        }));
    }
}