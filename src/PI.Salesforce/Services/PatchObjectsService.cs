using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Dipper;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PI.Shared.App;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public class PatchObjectsService : AbstractMessageQueueService, ILifetimeService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly JobStatusService _jobStatusService;

    public PatchObjectsService(
        ILogger<PatchObjectsService> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        IMessageBroker messageBroker,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        JobStatusService jobStatusService) :
        base(logger, configuration, messageBroker)
    {
        _serviceProvider = serviceProvider;
        _connection = connection;
        _objectTypeService = objectTypeService;
        _jobStatusService = jobStatusService;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, "object.sf_Lead.loaded");
        MessageBroker.Bind(queue, "object.sf_ServiceAppointment.loaded");
        MessageBroker.Bind(queue, "object.sf_Account.loaded");
        MessageBroker.Bind(queue, "object.sf_WorkOrder.loaded");
        
        // without processor, just fire events
        MessageBroker.Bind(queue, "object.sf_INET_ExternalLink__c.loaded");
        MessageBroker.Bind(queue, "object.sf_INET_Option__c.loaded");

        MessageBroker.Bind(queue, "object.sf_Lead.bayeux");
        MessageBroker.Bind(queue, "object.sf_ServiceAppointment.bayeux");
        MessageBroker.Bind(queue, "object.sf_Account.bayeux");
        MessageBroker.Bind(queue, "object.sf_WorkOrder.bayeux");

        mapper.Register<GenericFlowEvent>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        var start = DateTime.UtcNow;
        var parts = evt.RoutingKey.Split(".");
        try
        {
            switch (evt.Body)
            {
                case GenericFlowEvent msg:
                    switch (parts[2])
                    {
                        case "loaded":
                            await ProcessLoadedMessageAsync(msg);
                            break;

                        case "bayeux":
                            await ProcessBayeuxMessageAsync(msg);
                            break;

                        // case "batchLoaded":
                        //     await OnBatchLoadedAsync(msg);
                        //     break;
                        //
                        default:
                            Logger.LogError("Message ignored");
                            break;
                    }

                    break;

                default:
                    Logger.LogError("Message ignored");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message");
        }
        finally
        {
            Logger.LogInformation("Finished in {ms}", (DateTime.UtcNow - start).TotalMilliseconds);
            evt.Acknowledge();
        }
    }

    private async Task ProcessBayeuxMessageAsync(GenericFlowEvent action)
    {
        using var scope = Logger.AddScope(new
        {
            ObjectId = action.TargetId,
            action.ObjectType,
        });

        Logger.LogInformation("Received notification from bayeux");

        var processor = GetProcessor(action);
        if (processor == null)
        {
            Logger.LogError("No registered processor for this object type");
            return;
        }

        if (!action.MetaValues.TryGetValue("Id", out var externalIdObj) || externalIdObj is not string externalId)
        {
            Logger.LogError("Couldn't find ExternalId");
            return;
        }

        var timeStamp = action.MetaValues.TryGetValue("SystemModstamp", out var systemModstamp) && DateTime.TryParse(systemModstamp.ToString(), out var dateTime) ? dateTime : default(DateTime?);
        // var isDeleted = action.MetaValues.TryGetValue("IsDeleted", out var isDeletedObj) && isDeletedObj is bool value ? value : false;
        await processor.ProcessChangeAsync(action.AccountId, externalId, timeStamp);
    }

    private IObjectChangeProcessor GetProcessor(GenericFlowEvent action)
    {
        using IServiceScope serviceScope = _serviceProvider.CreateScope();
        var processor = action.ObjectType switch
        {
            "sf_Lead" => serviceScope.ServiceProvider.GetRequiredService<IOnLeadChangeProcessor>(),
            "sf_Account" => serviceScope.ServiceProvider.GetRequiredService<IOnAccountChangeProcessor>(),
            "sf_ServiceAppointment" => serviceScope.ServiceProvider.GetRequiredService<IOnServiceAppointmentChangeProcessor>(),
            "sf_WorkOrder" => serviceScope.ServiceProvider.GetRequiredService<IOnWorkOrderChangeProcessor>(),
            _ => default(IObjectChangeProcessor),
        };
        return processor;
    }

    private async Task ProcessLoadedMessageAsync(GenericFlowEvent action)
    {
        var externalIdObj = action.RefValues?.FirstOrDefault(x => x.Key == nameof(CustomObject.ExternalId)).Value;
        if (externalIdObj is not string externalId)
        {
            Logger.LogError("Missing required externalId");
            return;
        }

        using var scope = Logger.AddScope(new
        {
            action.ObjectType,
            ExternalId = externalId,
        });

        Logger.LogInformation("Processing event");

        // TODO: set actor 
        // ...
        var context = new AccountContext(action.AccountId);
        var objectType = await _objectTypeService.GetAsync<SalesforceObjectType>(context, action.ObjectType);
        var processor = GetProcessor(action);
        if (processor == null)
        {
            Logger.LogInformation("Didn't find processor for this object type");

            var source = await _connection.Filter<SalesforceCustomObject>(objectType.CollectionName, objectType.DatabaseName)
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.ExternalId, externalId)
                .FirstOrDefaultAsync();

            if (source == null)
            {
                Logger.LogError("Didn't find object in the database");
                return;
            }

            if (source.FlowId.HasValue)
            {
                Logger.LogInformation("Fire Events");

                if (!source.LastModifiedOn.HasValue || source.CreatedOn == source.LastModifiedOn.Value)
                {
                    await _objectTypeService.FireCreateEventAsync(context, source, e => { e.Description = $"{objectType.Description ?? objectType.Name} Loaded"; });
                }
                else
                {
                    await _objectTypeService.FireObjectUpdatedAsync(context, source, new Dictionary<string, object>
                    {
                        { nameof(SalesforceCustomObject.Properties), "*" },
                    }, e => { e.Description = $"{objectType.Description ?? objectType.Name} Loaded"; });
                }
            }

            return;
        }

        var result = await processor.ImportObjectAsync(context, objectType, externalId);

        if (result.Source != null && result.Source.FlowId.HasValue)
        {
            Logger.LogInformation("Fire Update Event for loaded {ObjectId}", result.Source.Id);
            await _objectTypeService.FireObjectUpdatedAsync(context, result.Source, new Dictionary<string, object>
            {
                { nameof(SalesforceCustomObject.Properties), "*" },
            }, e => { e.Description = $"{objectType.Description ?? objectType.Name} Loaded"; });
        }
    }

    /// <summary>
    /// process (old) message when the go-salesforce loaded the object
    /// </summary>
    /// <param name="action"></param>
    [Obsolete]
    private async Task ProcessMessageAsync(GenericFlowEvent action)
    {
        using var scope = Logger.AddScope(new
        {
            ObjectId = action.TargetId,
            action.ObjectType,
        });

        if (action.TargetId == Guid.Empty)
        {
            Logger.LogError("Missing object id");
            return;
        }

        Logger.LogInformation("Processing event");

        var context = new AccountContext(action.AccountId);

        // TODO: set actor 
        // ...

        var id = action.TargetId;
        var externalIdObj = action.RefValues?.FirstOrDefault(x => x.Key == nameof(CustomObject.ExternalId)).Value;
        if (externalIdObj is not string externalId)
        {
            // TODO: should load object 
            // ...
            Logger.LogError("Missing required externalId");
            return;
        }

        var objectType = await _objectTypeService.GetAsync<SalesforceObjectType>(context, action.ObjectType);
        var source = await _connection.Filter<SalesforceCustomObject>(objectType.CollectionName, objectType.DatabaseName)
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.ExternalId, externalId)
            .FirstOrDefaultAsync();

        if (source == null)
        {
            Logger.LogError("{ObjectType} not found with {ExternalId}", action.ObjectType, externalId);
            return;
        }

        Logger.LogInformation("{ObjectType} with {ExternalId}: {Id}", action.ObjectType, externalId, source.Id);

        var processor = GetProcessor(action);
        if (processor == null)
        {
            Logger.LogError("Didn't find processor for this object type");
            return;
        }

        await processor.ImportObjectAsync(context, objectType, source);
    }

    /// <summary>
    /// attempt at "converting" objects in batch entirely in the database
    /// </summary>
    /// <param name="action"></param>
    [Obsolete]
    private async Task OnBatchLoadedAsync(GenericFlowEvent action)
    {
        using var scope = Logger.AddScope(new
        {
            ObjectId = action.TargetId,
            action.ObjectType,
        });

        Logger.LogInformation("Batch Loaded, process");

        // end of batch load
        await Task.Run(() => ProcessBatchLoadedAsync(action));
    }

    [Obsolete]
    private async Task ProcessBatchLoadedAsync(GenericFlowEvent action)
    {
        IEntityContext context = new AccountContext(action.AccountId);
        var objectTypeName = action.ObjectType;

        var start = DateTime.UtcNow;
        var transactionId = Guid.NewGuid();
        var serviceName = $"{objectTypeName}.OnBatchLoaded";
        var service = await _jobStatusService.StartAsync(context, serviceName, transactionId);
        if (service == null)
        {
            Logger.LogError("Failed to initialize Job");
            return;
        }

        context = context.With(new JobActor
        {
            ServiceId = service.Id,
            TransactionId = transactionId.ToString()
        });

        using var scope = Logger.AddScope(new
        {
            context.AccountId,
            Service = service.Name,
            ServiceId = service.Id,
            TransactionId = transactionId
        });

        Logger.LogInformation("Start Execution");

        try
        {
            var sp = await _connection.Filter<StoredProcedure>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.Namespace, "trigger.onLoadBatch")
                .Eq(x => x.Name, objectTypeName)
                .Ne(x => x.IsActive, false)
                .FirstOrDefaultAsync();

            if (sp == null)
            {
                throw new NotFoundException("Trigger for object not found");
            }

            Logger.LogInformation("Execute {StoredProcedure}", sp.Id);
            await sp.ExecuteAsync(_connection, new Dictionary<string, object>());

            var elapsed = DateTime.UtcNow - start;

            Logger.LogInformation("Finished Execution in {Elapsed} seconds", elapsed.TotalSeconds);

            await _jobStatusService.SucceededAsync(
                context,
                service,
                "Executed trigger for batch loaded",
                new Dictionary<string, object>
                {
                    { "ObjectType", objectTypeName },
                },
                transactionId,
                elapsed
            );
        }
        catch (Exception ex)
        {
            Logger.LogError("Job Execution Failed", ex);

            await _jobStatusService.FailedAsync(context, service, ex, new
            {
                service.Name,
                ServiceId = service.Id,
                TransactionId = transactionId,
            });
        }
    }
}