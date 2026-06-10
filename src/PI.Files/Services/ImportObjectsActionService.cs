using System;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Models;

namespace PI.Files.Services;

public class ImportObjectsActionService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly IServiceProvider _serviceProvider;

    public ImportObjectsActionService(
        ILogger<ImportObjectsActionService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        MongoConnection connection,
        IServiceProvider serviceProvider
    ) : base(logger, configuration, messageBroker)
    {
        _connection = connection;
        _serviceProvider = serviceProvider;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.ImportObjects));
        mapper.Register<SimpleActionMessage<ImportObjectsActionOptions>>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            switch (evt.Body)
            {
                case SimpleActionMessage<ImportObjectsActionOptions> action:
                    await ProcessMessageAsync(action);
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

    private async Task ProcessMessageAsync(SimpleActionMessage<ImportObjectsActionOptions> action)
    {
        using var scope = Logger.AddScope(new
        {
            action.Event.ObjectType,
            action.Event.TargetId,
        });

        if (action.Event.ObjectType != nameof(ImportObjectsJob))
        {
            Logger.LogError("{ObjectType} not supported", action.Event.ObjectType);
            return;
        }

        Logger.LogInformation("Import Objects");

        var importObjectsJob = await _connection.Filter<ImportObjectsJob>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, action.Event.TargetId)
            .Eq(x => x.StartedOn, null)
            .FirstOrDefaultAsync();

        if (importObjectsJob == null) throw NotFoundException.New<ImportObjectsJob>(action.Event.TargetId);

        // create a better actor?
        var context = new AccountContext(importObjectsJob.AccountId)
            .With(new JobActor
            {
                ServiceId = ActionIds.ImportObjects,
                TransactionId = action.Event.RunId.ToString(),
            });

        // fire and forget
        var _ = Task.Run(async () =>
        {
            try
            {
                var job = _serviceProvider.GetRequiredService<Jobs.ImportObjectsJob>();
                var result = await job.ExecuteAsync(context, importObjectsJob, CancellationToken.None);

                var evt = new GenericFlowEvent(action.Event)
                {
                    Action = nameof(ActionIds.ImportObjects),
                    EventTypeId = action.Options.NextEventId,
                    Description = result.Message
                };

                foreach (var kvp in result.Result)
                {
                    evt.TryAddMetaValue(kvp.Key, kvp.Value);
                }

                await MessageBroker.DispatchAsync(evt);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to import objects");
                
                var evt = new GenericFlowEvent(action.Event)
                {
                    Action = nameof(ActionIds.ImportObjects),
                    EventTypeId = action.Options.NextEventId,
                    Description = ex.Message,
                };                
               
                await MessageBroker.DispatchAsync(evt, true);
            }
        });
    }
}