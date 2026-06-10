using System.Diagnostics.CodeAnalysis;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using MongoDB.Bson;
using Newtonsoft.Json;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Models;

namespace Services;

public class FlowLogService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;

    public FlowLogService(
        ILogger<FlowLogService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        MongoConnection connection
    )
        : base(logger, configuration, messageBroker)
    {
        _connection = connection;
    }

    protected override void Init(IMessageQueue messageQueue, TypeMapper mapper)
    {
        MessageBroker.Bind(messageQueue, EventIds.AllRoute);
        MessageBroker.Bind(messageQueue, EventIds.ErrorRoute);
        mapper.RegisterAll<FlowEvent>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            switch (evt.Body)
            {
                case FlowEvent flowEvent:
                    await LogEventAsync(evt.RoutingKey, flowEvent);
                    evt.Acknowledge();
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to log event: {Body}", evt.Body);
        }
    }

    private async Task LogEventAsync(string routingKey, FlowEvent evt)
    {
        var parts = routingKey.Split('.');
        var eventId = Guid.Parse(parts[1]);
        var error = string.Equals(parts[2], "error");

        var refs = evt.Refs?
            .Where(x => x.Value != null)
            .Select(x => new KeyValue(x.Key, x.Value));

        refs ??= Enumerable.Empty<KeyValue>();

        var meta = evt.Meta ?? Enumerable.Empty<KeyValuePair<string, object>>();

        switch (evt.Actor)
        {
            case AbstractAPIActor api when api.UserId.HasValue:
                refs = refs.Append(new KeyValue("UserId", api.UserId.AsSerializedId()));
                break;

            case SingerSyncActor singer:
                refs = refs.Append(new KeyValue("SingerJobId", singer.JobId.AsSerializedId()));
                break;
        }

        // todo: resolve actor (user name?)
        // ...

        // add target to refs (if missing)
        var objectKey = $"{evt.ObjectType}Id";
        if (!refs.Any(isTargetReferenced))
        {
            refs = refs.Append(new KeyValue(objectKey, evt.TargetId.AsSerializedId()));
        }
        
        var log = new FlowEventLog
        {
            AccountId = evt.AccountId,
            StatusId = evt.StatusId,
            ObjectType = evt.ObjectType,
            FlowId = evt.FlowId,
            ObjectId = evt.TargetId,

            Type = evt.GetType().Name,
            Description = evt.Description,
            EventId = eventId,
            RunId = evt.RunId,

            Action = evt.Action,
            Actor = evt.Actor,
            Refs = refs.DistinctBy(x => HashCode.Combine(x.Key, x.Value)).ToArray(),
            Meta = ToDictionary(meta),
            Failed = error,
            
            Event = evt,
        };

        try
        {
            await _connection.InsertAsync(log);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to insert record: {Json}", JsonConvert.SerializeObject(log));
        }

        bool isTargetReferenced(KeyValue x)
        {
            if (x.Key != objectKey) return false;
            return x.Value switch
            {
                Guid uuid => uuid == evt.TargetId,
                string str => Guid.TryParse(str, out var uuid) && uuid == evt.TargetId,
                _ => false,
            };
        }
    }

    private Dictionary<string, object> ToDictionary(IEnumerable<KeyValuePair<string, object>> refs)
        => new Dictionary<string, object>(refs.Where(x => x.Value != null).Distinct(new Comparer()));

    private class Comparer : IEqualityComparer<KeyValuePair<string, object>>
    {
        public bool Equals([AllowNull] KeyValuePair<string, object> x, [AllowNull] KeyValuePair<string, object> y) => string.Equals(x.Key, y.Key);
        public int GetHashCode([DisallowNull] KeyValuePair<string, object> obj) => obj.Key.GetHashCode();
    }
}