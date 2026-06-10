using Crochik.Mongo;
using FlowActions;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Controllers;

public class FlowTreeBuilder(
    ILogger<FlowTreeBuilder> logger,
    MongoConnection connection,
    ObjectTypeService objectTypeService,
    IEnumerable<IFlowActionBuilder> builders
    )
{
    public async Task<FlowTree> BuildAsync(IEntityContext context, Flow flow)
    {
        var actionBuilders = builders.ToDictionary(x => x.Id);
        
        // TODO: limit query results to actions in the existing steps?
        // ...
        var genericActions = await connection.GetProfileElementsAsync<GenericAction, Guid>(context, action => action.ActionId);
        foreach (var genericAction in genericActions)
        {
            actionBuilders.TryAdd(genericAction.ActionId, new GenericActionBuilder(genericAction, objectTypeService));
        }

        var flowTree = new FlowTree
        {
            Id = flow.Id,
            Name = flow.Name,
            ObjectType = flow.ObjectType
        };

        var flowContext = new FlowActionContext(context, flow);
       
        var objectStatuses = await connection.Filter<PI.Shared.Models.ObjectStatus>()
            .Eq(x => x.AccountId, flow.AccountId)
            .Eq(x => x.ObjectType, flow.ObjectType)
            // .In(x => x.Id, swimmingLanes.Keys)
            .SortAsc(x => x.Name)
            .FindAsync();

        var objectStatusDict = objectStatuses
            .ToDictionary(
                x => x.Id,
                x => new FlowObjectStatus
                {
                    Id = x.Id,
                    Name = x.Name,
                }
            );

        var nodes = flowContext.FlowSteps
            .Select(stepToNode)
            .ToArray();

        var groups = nodes.GroupBy(x => x.ObjectStatusId);
        var includesAny = false;
        foreach (var group in groups)
        {
            var id = group.Key ?? Guid.Empty;
            if (!objectStatusDict.TryGetValue(id, out var objectStatus))
            {
                includesAny |= id == Guid.Empty;
                objectStatus = new FlowObjectStatus
                {
                    Id = id,
                    Name = id == Guid.Empty ? "[Any]" : "[Error]",
                };
                objectStatusDict.Add(id, objectStatus);
            }

            objectStatus.Triggers = await AggregateAsync(flowContext, id, group);
        }

        if (!includesAny)
        {
            var objectStatus = new FlowObjectStatus
            {
                Id = Guid.Empty,
                Name = "[Any]",
            };
            objectStatus.Triggers = await AggregateAsync(flowContext, objectStatus.Id, Enumerable.Empty<ActionFlowNode>());
            objectStatusDict.Add(objectStatus.Id, objectStatus);
        }

        foreach (var objectStatus in objectStatusDict.Values)
        {
            objectStatus.Triggers ??= await AggregateAsync(flowContext, objectStatus.Id, Enumerable.Empty<ActionFlowNode>());
        }

        flowTree.ObjectStatuses = objectStatusDict.Values.ToArray();

        return flowTree;

        ActionFlowNode stepToNode(FlowStep step)
        {
            if (!actionBuilders.TryGetValue(step.ActionId, out var builder))
            {
                throw new Exception($"Couldn't find {step.ActionId}");
            }

            var node = new ActionFlowNode
            {
                Id = step.Id,
                EventTypeId = step.EventIdTrigger,
                ActionId = step.ActionId,
                Icon = step.IconName ?? builder.IconName,
                ObjectStatusId = step.CurrentStatusId,
                Name = builder.Name,
                Description = step.Description ?? builder.Description,
                // EventDescription = step.Options.EventDescription,
                Outputs = (step.Options.Output ?? Enumerable.Empty<ActionOutput>()).Select(outputToNode).ToArray(),
            };

            return node;

            EventTypeFlowNode outputToNode(ActionOutput output)
            {
                return new EventTypeFlowNode
                {
                    Id = step.Id,
                    EventTypeId = output.EventId,
                    Name = output.Name,
                    Description = output.Description,
                    Color = output.Color,
                };
            }
        }
    }

    private async Task<EventTypeFlowNode[]> AggregateAsync(FlowActionContext flowContext, Guid objectStatusId, IEnumerable<ActionFlowNode> nodes)
    {
        var values = nodes.ToArray();

        var eventTypes = (await connection.Filter<EventType>()
            .In(x => x.AccountId, [flowContext.Flow.AccountId, AccountIds.CSS]) // allow loading for "generic" account so we can use the system events 
            .OrBuilder(
                // system
                q => q
                    .In(x => x.ObjectType, [null, flowContext.ObjectType])
                    .Eq($"{nameof(EventType.Trigger)}._t", nameof(TriggerType.System)),
                // user
                q => q
                    .Eq(x => x.ObjectType, flowContext.ObjectType)
                    .Eq($"{nameof(EventType.Trigger)}._t", nameof(TriggerType.User)),
                // scheduled
                q => q
                    .In(x => x.ObjectType, [flowContext.ObjectType])
                    .In($"{nameof(EventType.Trigger)}.{nameof(ScheduledTrigger.FlowId)}", new[] { default(Guid?), flowContext.Flow.Id })
                    .Eq($"{nameof(EventType.Trigger)}._t", nameof(TriggerType.Scheduled)),
                // side effects
                // TODO: remove auto-generated so we will only have sideeffects for events fired by other flows
                q => q
                    .In(x => x.ObjectType, [flowContext.ObjectType])
                    .In($"{nameof(EventType.Trigger)}._t", [null, nameof(TriggerType.SideEffect)]))
            .FindAsync()).ToDictionary(x => x.Id);

        var dict = values
            .GroupBy(x => x.EventTypeId)
            .ToDictionary(
                x => x.Key, x =>
                {
                    var node = new EventTypeFlowNode
                    {
                        EventTypeId = x.Key,
                        Actions = x.ToArray(),
                    };

                    if (!x.Key.HasValue || !eventTypes.TryGetValue(x.Key.Value, out var eventType)) return node;

                    node.Name = eventType.Name;
                    node.Description = eventType.Description;
                    node.TriggerType = eventType.Trigger?.Type ?? TriggerType.SideEffect;

                    return node;
                }
            );

        foreach (var node in values)
        {
            if (node.Outputs == null)
            {
                logger.LogInformation("Action has no events/outputs defined");
                continue;
            }

            foreach (var output in node.Outputs)
            {
                if (!output.EventTypeId.HasValue) continue;
                if (!dict.Remove(output.EventTypeId, out var eventType)) continue;

                // merge
                if (output.Actions != null)
                {
                    throw new BadRequestException("something bad!");
                }

                output.Actions = eventType.Actions;
                // output.Name ??= eventType.Name;
                output.Description = output.Description; // eventType.Description ?? eventType.Name ??
            }
        }

        var defaultTriggers = eventTypes.Values
            .Where(x => x.Trigger switch
            {
                SystemTrigger s => !s.ObjectStatusId.HasValue || s.ObjectStatusId == objectStatusId,
                ScheduledTrigger s => !s.ObjectStatusId.HasValue || s.ObjectStatusId == objectStatusId,
                UserTrigger u => !u.ObjectStatusId.HasValue || u.ObjectStatusId == objectStatusId,
                _ => false
            })
            .Where(x => !dict.ContainsKey(x.Id))
            .Select(x => new EventTypeFlowNode
            {
                EventTypeId = x.Id,
                Name = (x.Trigger as UserTrigger)?.Name ?? x.Name,
                Description = x.Description,
                TriggerType = x.Trigger?.Type ?? TriggerType.User,
            });

        return dict.Values.Concat(defaultTriggers).ToArray();
    }
}