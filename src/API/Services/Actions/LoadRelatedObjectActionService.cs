using System.Dynamic;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public class LoadRelatedObjectActionService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;

    public LoadRelatedObjectActionService(
        ILogger<SetObjectStatusActionService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        MongoConnection connection,
        ObjectTypeService objectTypeService
    ) : base(logger, configuration, messageBroker)
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    protected override void Init(IMessageQueue messageQueue, TypeMapper mapper)
    {
        MessageBroker.Bind(messageQueue, ActionIds.GetRoute(ActionIds.LoadRelatedObject));
        mapper.Register<SimpleActionMessage<LoadRelatedObjectActionOptions>>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            var parts = evt.RoutingKey.Split('.');
            var eventId = Guid.Parse(parts[1]);

            switch (evt.Body)
            {
                case SimpleActionMessage<LoadRelatedObjectActionOptions> loadRelatedObject:
                    await LoadRelatedObjectAsync(eventId, loadRelatedObject);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message {id}", evt.RoutingKey);
        }

        evt.Acknowledge();
    }

    private async Task LoadRelatedObjectAsync(Guid eventId, SimpleActionMessage<LoadRelatedObjectActionOptions> action)
    {
        var evt = action.Event;
        var accountContext = new AccountContext(evt.AccountId).With(evt.Actor);

        using var scope = Logger.AddScope(new
        {
            evt.AccountId,
            evt.FlowId,
            evt.TargetId,
            evt.ObjectType,
            evt.StatusId,
            evt.RunId,
            EventTypeId = eventId,
        });

        Logger.LogInformation("Load related Object(s)");

        var flowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.Id, evt.RunId)
            .IncludeField(x => x.Objects)
            .FirstOrDefaultAsync();

        var objectsToBeLoaded = action.Options.RelatedObjects;
        objectsToBeLoaded ??= "{{" + (action.Options.ParentObject ?? evt.ObjectType) + "}}.{{" + action.Options.RelatedObject + "}}";

        var currentRun = flowRun;
        var list = objectsToBeLoaded.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var objectToBeLoaded in list)
        {
            // TODO: could go one step further and support more than two parts
            // e.g. {{Appointment}}.{LeadId}}.{{EntityId}} => {{Appointment}}.{{LeadId}} + {{Appointment|LeadId}}.{{EntityId}}
            // ...

            // TODO: should it be simply Appointment.LeadId ?
            // or {{load Appointment LeadId}} or {{from Appointment LeadId}}
            // or should go even further {{Objects.Appointment}}.{{LeadId}}?
            // ...

            var parts = objectToBeLoaded.Split("}}.{{");
            var parentObjectName = parts.Length == 1 ? evt.ObjectType : parts[0][2..];
            var relatedObject = parts.Length == 1 ? parts[0][2..^2] : parts[^1][..^2];
            var isOptional = relatedObject.EndsWith('?');
            if (isOptional)
            {
                relatedObject = relatedObject[..^1];
            }

            var targetObjectName = $"{parentObjectName}|{relatedObject}";

            if (!currentRun.Objects.TryGetValue(FlowRun.GetObjectAlias(parentObjectName), out var parentObjectWithType))
            {
                Logger.LogError("Didn't find {ParentObject} in this run", parentObjectName);
                await fireErrorEvent($"Didn't find {parentObjectName}");
                return;
            }

            var parentObjectType = await _objectTypeService.GetAsync(accountContext, parentObjectWithType.ObjectType);
            if (parentObjectType == null)
            {
                Logger.LogError("Parent Object Type not found: {ObjectType}", parentObjectWithType.ObjectType);
                await fireErrorEvent($"{parentObjectWithType.ObjectType} type not found");
                return;
            }

            if (parentObjectWithType.Object is not IDictionary<string, object> iDict)
            {
                Logger.LogError("Unexpected object type: {Type}", parentObjectWithType.Object?.GetType().FullName);
                await fireErrorEvent($"Invalid Object");
                return;
            }

            if (iDict.TryGetValue(relatedObject, out var fieldValue))
            {
                // try fields
                if (parentObjectType.Fields.TryGetValue(relatedObject, out var field)
                    && field.Field is ReferenceField referenceField
                    && referenceField.ReferenceFieldOptions != null
                   )
                {
                    Logger.LogInformation("Lookup using {Field}={Value}", referenceField.Name, fieldValue);

                    var found = await LoadRelatedObjectUsingFieldAsync(action.Event.RunId, accountContext, targetObjectName, parentObjectType, referenceField, fieldValue);
                    if (found != null)
                    {
                        currentRun = found;
                        continue;
                    }
                }
            }

            // try related objects (field value doesn't matter, will use relation criteria)
            var relatedObjectType = parentObjectType.RelatedObjectTypes?.FirstOrDefault(x =>
                // TODO: expand for other (implicit) relations?
                // ...
                x.RelationType is RelationType.OneToMany or RelationType.OneToOne &&
                x.Name == relatedObject
                && x.RBAC.CanRead(accountContext)
            );
            
            if (relatedObjectType != null)
            {
                Logger.LogInformation("Lookup using {RelatedObject} to {ObjectType}", relatedObjectType.Name, relatedObjectType.ObjectType);

                var found = await LoadRelatedObjectUsingRelationAsync(action.Event.RunId, accountContext, targetObjectName, parentObjectType, iDict, relatedObjectType);
                if (found != null)
                {
                    currentRun = found;
                    continue;
                }
            }

            if (isOptional)
            {
                Logger.LogInformation("Didn't find optional {Object}, continue", targetObjectName);
                continue;
            }

            // failed!
            Logger.LogInformation("Object not found to load, {Object}", targetObjectName);
            await MessageBroker.DispatchAsync(
                new GenericFlowEvent(action.Event)
                {
                    Action = nameof(ActionIds.LoadRelatedObject),
                    Description = action.GetEventDescription(action.Options.NotFoundEventId,
                        $"Related Object not found: {objectToBeLoaded}"),
                    EventTypeId = action.Options.NotFoundEventId,
                }
            );
            return;
        }

        Logger.LogInformation("Found Related Object(s)");
        await MessageBroker.DispatchAsync(
            new GenericFlowEvent(action.Event)
            {
                Action = nameof(ActionIds.LoadRelatedObject),
                Description = action.GetEventDescription(action.Options.NextEventId, "Loaded Related Object(s)"),
                EventTypeId = action.Options.NextEventId,
            }
        );

        async Task fireErrorEvent(string description)
        {
            await MessageBroker.DispatchAsync(
                new GenericFlowEvent(action.Event)
                {
                    Action = nameof(ActionIds.LoadRelatedObject),
                    Description = description,
                    EventTypeId = action.Options.NextEventId,
                },
                true
            );
        }
    }

    /// <summary>
    /// Load Related Object(s)
    /// For now only supports explicitly defined relations
    /// </summary>
    private async Task<FlowRun> LoadRelatedObjectUsingRelationAsync(Guid flowRunId, IEntityContext context,
        string targetObjectName, ObjectType parentObjectType, IDictionary<string, object> parentObject,
        RelatedObjectType relation)
    {
        using var scope = Logger.AddScope(new
        {
            relation.RelationType,
            relation.ObjectType,
            relation.Name
        });

        Logger.LogInformation("Try to load related object (using relation)");

        var relatedObjectType = await _objectTypeService.GetAsync(context, relation.ObjectType);

        var query = _connection.Filter<ExpandoObject>(relatedObjectType.CollectionName, relatedObjectType.DatabaseName)
                .AddConstraints(context, relatedObjectType)
                .AddConditions(context, relation.Criteria.Conditions, parentObject)
            ;

        return relation.RelationType switch
        {
            RelationType.OneToOne => await loadSingleAsync(),
            RelationType.OneToMany => await loadManyAsync(),
            _ => null
        };

        async Task<FlowRun> loadSingleAsync()
        {
            var oneToOne = await query.FirstOrDefaultAsync();

            // if (oneToOne is not IDictionary<string, object> objectAsDict)
            // {
            //     Logger.LogInformation("Single Object matching criteria not found");
            //     return null;
            // }
            // var flatObject = relatedObjectType.UnsafeFlatten(context, objectAsDict);

            var flatObject = await _objectTypeService.RecursivelyFlattenAsync(context, relatedObjectType, oneToOne);

            Logger.LogInformation("Found Single Related Object");

            return await _connection.Filter<FlowRun>()
                .Eq(x => x.Id, flowRunId)
                .Update
                .Set(
                    x => x.Objects[FlowRun.GetObjectAlias(targetObjectName)],
                    new ObjectWithType
                    {
                        ObjectType = relatedObjectType.FullName,
                        Object = flatObject,
                    }
                )
                .UpdateAndGetOneAsync();
        }

        async Task<FlowRun> loadManyAsync()
        {
            var index = 0;
            var list = await query.FindAsync();

            var result = new Dictionary<string, object>();

            for (var c = 0; c < list.Count; c++)
            {
                // var flatObject = relatedObjectType.UnsafeFlatten(context, list[c] as IDictionary<string, object>);
                var flatObject = await _objectTypeService.RecursivelyFlattenAsync(context, relatedObjectType, list[c]);
                result.Add(c.ToString(), flatObject);
            }

            return await _connection.Filter<FlowRun>()
                .Eq(x => x.Id, flowRunId)
                .Update
                .Set(
                    x => x.Objects[FlowRun.GetObjectAlias(targetObjectName)],
                    new ObjectWithType
                    {
                        ObjectType = $"{relatedObjectType.ObjectType}[]", // ????!
                        Object = result,
                    }
                )
                .UpdateAndGetOneAsync();
        }
    }

    /// <summary>
    /// Load related object into flowRun (for referenceField) 
    /// </summary>
    private async Task<FlowRun> LoadRelatedObjectUsingFieldAsync(Guid flowRunId, IEntityContext context,
        string targetObjectName, ObjectType parentObjectType, ReferenceField referenceField, object fieldValue)
    {
        if (fieldValue == null) return null;

        var relatedObjectType =
            await _objectTypeService.GetAsync(context, referenceField.ReferenceFieldOptions.ObjectType);
        var foreignFieldName = referenceField.ReferenceFieldOptions.ForeignFieldName ??
                               relatedObjectType.LookupFields?.Key ??
                               Model.IdFieldName;

        var query = _connection.Filter<ExpandoObject>(relatedObjectType.CollectionName, relatedObjectType.DatabaseName)
                .Eq(foreignFieldName, fieldValue)
                .AddConstraints(context, relatedObjectType)
                .AddConditions(context, referenceField.ReferenceFieldOptions.Criteria)
            ;

        var relatedObject = await query.FirstOrDefaultAsync();
        if (relatedObject != null)
        {
            // var flatObject = relatedObjectType.UnsafeFlatten(context, relatedObject as IDictionary<string, object>);
            var flatObject = await _objectTypeService.RecursivelyFlattenAsync(context, relatedObjectType, relatedObject);

            Logger.LogInformation("Found Related Object");

            return await _connection.Filter<FlowRun>()
                .Eq(x => x.Id, flowRunId)
                .Update
                .Set(
                    x => x.Objects[FlowRun.GetObjectAlias(targetObjectName)],
                    new ObjectWithType
                    {
                        ObjectType = relatedObjectType.FullName,
                        Object = flatObject,
                    }
                )
                .UpdateAndGetOneAsync();
        }

        Logger.LogInformation(
            "Didn't find {ObjectType} with {Field} = {FieldValue}",
            referenceField.ReferenceFieldOptions.ObjectType,
            foreignFieldName,
            fieldValue
        );

        return null;
    }
}