using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Extensions;
using PI.Shared.Models;

namespace PI.Shared.Services;

public class JobStatusService
{
    private readonly ILogger<JobStatusService> _logger;
    private readonly MongoConnection _connection;
    private readonly IMessageBroker _messageBroker;

    public JobStatusService(
        ILogger<JobStatusService> logger,
        MongoConnection connection,
        IMessageBroker messageBroker
    )
    {
        _logger = logger;
        _connection = connection;
        _messageBroker = messageBroker;
    }

    public async Task<BackgroundServiceConfig> StartAsync(IEntityContext context, string serviceName, Guid transactionId)
    {
        using var scope = _logger.AddScope(new
        {
            context.AccountId,
            Service = serviceName,
            TransactionId = transactionId,
        });

        var service = await _connection.Filter<BackgroundServiceConfig>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.ExternalId, serviceName)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (service == null)
        {
            service = await _connection.Filter<BackgroundServiceConfig>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.ExternalId, serviceName)
                .FirstOrDefaultAsync();

            if (service != null)
            {
                _logger.LogError("Service not found");
                return null;
            }

            _logger.LogInformation("{BackgroundServiceConfig} not defined. Auto provision", serviceName);
            service = new BackgroundServiceConfig
            {
                AccountId = context.AccountId.Value,
                Name = serviceName,
                ExternalId = serviceName,
                IsActive = true,
                CreatedOn = DateTime.UtcNow,
                LastActor = context.Actor(),
                MaxConcurrentInstances = 1,
            };

            await _connection.InsertAsync(service);
        }

        var query = _connection.Filter<BackgroundServiceConfig>()
            .Eq(x => x.Id, service.Id);

        var maxOccurrences = service.MaxConcurrentInstances.GetValueOrDefault();
        if (maxOccurrences > 0)
        {
            if (!service.ConcurrentInstances.HasValue)
            {
                _logger.LogInformation("Limit {maxConcurrentInstances}, first run.", maxOccurrences);

                await _connection.Filter<BackgroundServiceConfig>()
                    .Eq(x => x.Id, service.Id)
                    .Exists(x => x.AvailableInstances, false)
                    .Update
                    .Set(x => x.AvailableInstances, maxOccurrences)
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    .UpdateAndGetOneAsync();
            }

            query.Gt(x => x.AvailableInstances, 0);
        }

        service = await query
            .Update
            .Inc(x => x.AvailableInstances, -1)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastTransactionId, transactionId)
            .Set(x => x.StartedOn, DateTime.UtcNow)
            .Unset(x => x.EndedOn)
            .Unset(x => x.LastError)
            .UpdateAndGetOneAsync();

        if (service == null)
        {
            _logger.LogWarning("Failed to increment instances, reached max instances?");
            return null;
        }

        await FireSyncStartedAsync(context, service, $"{service.Name} started", new
        {
            service.Name,
            ServiceId = service.Id,
            TransactionId = transactionId,
        });

        return service;
    }

    private async Task FinishAsync(IEntityContext context, BackgroundServiceConfig service, string error = null)
    {
        var query = _connection.Filter<BackgroundServiceConfig>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, service.Id)
            .Update
            .Inc(x => x.AvailableInstances, 1)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.EndedOn, DateTime.UtcNow);

        if (!string.IsNullOrWhiteSpace(error)) query.Set(x => x.LastError, error);

        await query.UpdateOneAsync();
    }

    public async Task FailedAsync(IEntityContext context, BackgroundServiceConfig service, Exception ex, object metaValues = null)
    {
        var dict = metaValues.GetPropertiesAsDictionary();
        dict.TryAdd("Service", service.Name);
        dict.TryAdd("ServiceId", service.Id);
        dict.TryAdd("AccountId", context.AccountId);

        _logger.LogError(ex, "{Service} Crashed", service.Name);

        // todo: log to db somewhere?
        // ...

        await FinishAsync(context, service, ex.Message);

        await FireSyncFailedAsync(context, service, ex.Message, metaValues);
    }

    // public async Task SucceededAsync(IEntityContext context, CustomObject service, object metaValues)
    // {
    //     var dict = metaValues.GetPropertiesAsDictionary();
    //     await SucceededAsync(context, service, $"{service.Name} Fisnished successfully", dict);
    // }

    public async Task SucceededAsync(IEntityContext context, BackgroundServiceConfig service, string description, Dictionary<string, object> meta, Guid transactionId, TimeSpan elapsed)
    {
        meta ??= new Dictionary<string, object>();
        meta.TryAdd("Name", service.Name);
        meta.TryAdd("Elapsed", elapsed);

        using var scope = _logger.BeginScope(meta);

        _logger.LogInformation("{Service} finished successfully: {message}", service.Name, description);

        await FinishAsync(context, service);
        await FireSyncFinsihedAsync(context, service, description, meta, new
        {
            TransactionId = transactionId,
        });
    }

    public async Task FireSyncStartedAsync<T>(IEntityContext context, T obj, string description, object metaValues = null, object refValues = null) where T : IFlowObject
        => await FireEventAsync(context, EventIds.OnSyncStarted, obj, description, metaValues, refValues);

    public async Task FireSyncFailedAsync<T>(IEntityContext context, T obj, string description, object metaValues = null, object refValues = null) where T : IFlowObject
        => await FireEventAsync(context, EventIds.OnSyncFailed, obj, description, metaValues, refValues);

    // public async Task FireSyncFinsihedAsync<T>(IEntityContext context, T obj, string description, object metaValues = null, object refValues = default) where T : IFlowObject
    //     => await FireEventAsync(context, EventIds.OnSyncFinished, obj, description, metaValues, refValues);

    public async Task FireSyncFinsihedAsync<T>(IEntityContext context, T obj, string description, Dictionary<string, object> metaValues, object refValues = default) where T : IFlowObject
        => await FireEventAsync(context, EventIds.OnSyncFinished, obj, description, metaValues, refValues);

    public async Task FireEventAsync<T>(IEntityContext context, Guid eventId, T obj, string description, object metaValues = null, object refValues = null)
        where T : IFlowObject
    {
        var metaDict = metaValues.GetPropertiesAsDictionary();
        await FireEventAsync(context, eventId, obj, description, metaDict, refValues);
    }

    public async Task FireEventAsync<T>(IEntityContext context, Guid eventId, T obj, string description, Dictionary<string, object> metaValues = null, object refValues = null)
        where T : IFlowObject
    {
        if (!obj.FlowId.HasValue) return;

        metaValues ??= new Dictionary<string, object>();
        metaValues.TryAdd(obj.ObjectType, obj.Name);

        var evt = new GenericFlowEvent(obj)
        {
            Actor = context.Actor(),
            Description = description,
            MetaValues = metaValues,
            RefValues = refValues?.GetPropertiesAsDictionary().ToList(),
            EventTypeId = eventId,
        };

        await _messageBroker.DispatchAsync(evt);
    }
}