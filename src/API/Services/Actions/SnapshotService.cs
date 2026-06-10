using System.Dynamic;
using Crochik.Dipper;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using MongoDB.Bson;
using MongoDB.Driver;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public class SnapshotService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;

    public SnapshotService(
        ILogger<SnapshotService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        MongoConnection connection,
        ObjectTypeService objectTypeService
    ) : base(logger, configuration, messageBroker)
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    protected override void Init(IMessageQueue messageQueue, TypeMapper mapper)
    {
        MessageBroker.Bind(messageQueue, ActionIds.GetRoute(ActionIds.TakeSnapshot));
        mapper.Register<SimpleActionMessage<TakeSnapshotActionOptions>>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            switch (evt.Body)
            {
                case SimpleActionMessage<TakeSnapshotActionOptions> action:
                    evt.Acknowledge();
                    await ProcessAsync(action);
                    break;

                default:
                    Logger.LogError("Unexpected {Body}", evt.Body.GetType().FullName);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message");
        }
        finally
        {
            evt.Acknowledge();
        }
    }

    private async Task ProcessAsync(SimpleActionMessage<TakeSnapshotActionOptions> action)
    {
        var snapshot = await TakeSnapshotAsync(action);

        await MessageBroker.DispatchAsync(new GenericFlowEvent(action.Event)
            {
                Action = nameof(ActionIds.TakeSnapshot),
                Description = snapshot.Status,
                EventTypeId = action.Options.NextEventId, 
            },
            snapshot.IsError
        );
    }

    private async Task<Result<Snapshot>> TakeSnapshotAsync(SimpleActionMessage<TakeSnapshotActionOptions> action)
    {
        var start = DateTime.UtcNow;
        var snapshot = await _connection.Filter<Snapshot>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, action.Event.TargetId)
            .Eq(x => x.Start, null)
            .Update
            .Set(x => x.Start, start)
            .Set(x => x.LastModifiedOn, start)
            .UpdateAndGetOneAsync();

        if (snapshot == null)
        {
            Logger.LogError("{Snapshot} does not exist or has already started", action.Event.TargetId);
            return Result<Snapshot>.Error("Couldn't set Start");
        }

        var result = await TakeSnapshotAsync(snapshot);

        return result.IsSuccess ?
            Result.Success(snapshot, "Successfully took snapshot of data") :
            result.ConvertTo<Snapshot>();
    }

    private async Task<Result<Snapshot>> TakeSnapshotAsync(Snapshot snapshot)
    {
        var creator = await _connection.Filter<Entity>()
            .Eq(x => x.AccountId, snapshot.AccountId)
            .Eq(x => x.Id, snapshot.CreatedById)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (creator == null)
        {
            Logger.LogError("{Entity} not found or inactive", snapshot.CreatedById);
            return await errorAsync("Creator not found or inactive");
        }

        var appDataView = await _connection.Filter<AppDataView>()
            .Eq(x => x.AccountId, snapshot.AccountId)
            .Eq(x => x.Id, snapshot.AppDataViewId)
            .FirstOrDefaultAsync();

        var objectType = await _objectTypeService.GetAsync(new AccountContext(snapshot.AccountId), appDataView.ObjectType);
        if (objectType == null)
        {
            return await errorAsync($"{appDataView.ObjectType} not found");
        }

        var pipeline = PipelineDefinition<ExpandoObject, ExpandoObject>.Create(await getStagesAsync());

        await _connection.Database
            .GetCollection<ExpandoObject>(objectType.CollectionName)
            .Aggregate(pipeline)
            .AnyAsync();

        var count = await _connection.Filter<SnapshotData>()
            .Eq(x => x.AccountId, snapshot.AccountId)
            .Eq(x => x.SnapshotId, snapshot.Id)
            .CountDocumentsAsync();

        var fieldNames = appDataView.Fields.ToHashSet();
        snapshot.DataView = new DataView
        {
            Name = snapshot.Name ?? appDataView.Name,
            Title = snapshot.Description ?? appDataView.Description,
            DefaultSort = appDataView.OrderBy,
            KeyField = Model.IdFieldName,
            // IsSelectable = false,
            // Searchable = false,
            // IsFilterableLocally = false,
            PageSize = 100,
            // TODO: add _id, account id, snapshotid fields?
            // ... 
            Fields = objectType.Fields
                // .Where(x => x.Value.RBAC.CanRead(context))
                .Where(x => fieldNames.Contains(x.Key))
                .Select(x => x.Value.Field)
                .Select(x =>
                {
                    x.Name = $"Properties|{x.Name}";
                    return x;
                })
                .ToArray(),
            FilterForm = new Form
            {
                Fields = Array.Empty<FormField>(),
            }
        };

        snapshot.StoredProcedure = new AggregateStoredProcedure
        {
            Collection = snapshot.CollectionName,
        };

        snapshot.Options = appDataView.Options;
        snapshot.Count = (int)count;
        
        var updated = await updateEndAsync();
        if (updated == null)
        {
            Logger.LogError("Failed to mark {Snapshot} as complete", snapshot.Id);
            return Result<Snapshot>.Error("Couldn't set End");
        }

        return Result.Success(updated);

        async Task<Result<Snapshot>> errorAsync(string error)
        {
            await updateEndAsync(error);
            return Result.Error<Snapshot>(error);
        }
        
        async Task<Snapshot> updateEndAsync(string error = null)
        {
            var end = DateTime.UtcNow;
            var update = _connection.Filter<Snapshot>()
                .Eq(x => x.AccountId, snapshot.AccountId)
                .Eq(x => x.Id, snapshot.Id)
                .Eq(x => x.Start, snapshot.Start)
                .Eq(x => x.End, null)
                .Update
                .Set(x => x.End, end)
                .Set(x => x.LastModifiedOn, end);

            if (!string.IsNullOrWhiteSpace(error))
            {
                update.Set(x => x.Error, error);
            }
            else
            {
                update.Set(x => x.DataView, snapshot.DataView);
                update.Set(x => x.StoredProcedure, snapshot.StoredProcedure);
                update.Set(x => x.Options, snapshot.Options);
                update.Set(x => x.Count, snapshot.Count);
            }

            return await update.UpdateAndGetOneAsync();
        }

        async Task<IEnumerable<BsonDocument>> getStagesAsync()
        {
            var context = creator.Context;

            // match
            var builder = AppDataViewPipelineBuilder.New(
                _connection,
                context,
                appDataView,
                objectType
            );

            var stages = builder.BuildPipeline();

            var str = $"{{'$project': {{'AccountId': '{snapshot.AccountId}', 'SnapshotId': '{snapshot.Id}', 'SnapshotObjectType': '{snapshot.ObjectType}', 'Properties': '$$ROOT'}} }}";
            stages = stages.AppendStage(str);
            
            stages = stages.AppendStage("{ '$project': {'_id': 0} }");

            str = $"{{'$merge': {{'into': '{snapshot.CollectionName}'}} }}";
            stages = stages.AppendStage(str);

            return stages;
        }
    }
}