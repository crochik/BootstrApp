using System;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Models;

namespace PI.Shared.Services;

public class LeadConversionIntegrationMonitorService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly ILeadConversionIntegrationService _service;

    public LeadConversionIntegrationMonitorService(
        ILogger<LeadConversionIntegrationMonitorService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        MongoConnection connection,
        ILeadConversionIntegrationService service
        ) : base(logger, configuration, messageBroker)
    {
        this._connection = connection;
        this._service = service;
    }

    protected override void Init(IMessageQueue messageQueue, TypeMapper mapper)
    {
        MessageBroker.Bind(messageQueue, IntegrationIds.GetActionRouteForAllAccounts(_service.IntegrationId));
        mapper.Register<Messages.Flow.ExportToIntegrationAction.Message>();
    }

    protected async override Task OnMessageAsync(IMessage evt)
    {
        try
        {
            var parts = evt.RoutingKey.Split('.');
            Logger.LogInformation("Received message: {Route}", evt.RoutingKey);

            switch (evt.Body)
            {
                case Messages.Flow.ExportToIntegrationAction.Message flow:
                    await ExportAsync(flow);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message {Id}", evt.RoutingKey);

        }

        evt.Acknowledge();
    }

    private async Task ExportAsync(ExportToIntegrationAction.Message msg)
    {
        if (msg.Options.IntegrationId != _service.IntegrationId)
        {
            // not for this integration, skip
            return;
        }

        // using var apm = ApmService.StartTransaction("FlowAction", $"Export {msg.Event.ObjectType}");
        // apm.Context = new
        // {
        //     msg.Event.ObjectType,
        //     msg.Event.TargetId,
        //     msg.Options.IntegrationId,
        // };

        if (msg.Event.ObjectType != nameof(Lead))
        {
            await sendEventAsync(false, "Unsupported Object Type");
            return;
        }

        var lead = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, msg.Event.AccountId)
            .Eq(x => x.Id, msg.Event.TargetId)
            .FirstOrDefaultAsync();

        if (lead == null)
        {
            Logger.LogError("{LeadId} not found", msg.Event.TargetId);
            return;
        }

        using var scope = Logger.AddScope(new
        {
            msg.Event.ObjectType,
            msg.Options.IntegrationId,
            msg.Event.TargetId,
            LeadId = lead.Id,
            lead.AccountId,
            lead.EntityId,
        });

        Logger.LogInformation("Export Lead");

        var existingIntegration = lead.Integrations?.FirstOrDefault(x => x.IntegrationId == msg.Options.IntegrationId);
        var proceed = msg.Options.UpdateOperation switch
        {
            UpdateOperation.Create => existingIntegration == null,
            UpdateOperation.Update => existingIntegration != null,
            _ => true,
        };
        if (!proceed)
        {
            Logger.LogInformation("Skip update: {UpdateOperation}", msg.Options.UpdateOperation);
            return;
        }

        var result = await _service.ConditionallyPostLeadAsync(lead);
        if (result.IsUnknown)
        {
            return;
        }

        await sendEventAsync(result.IsSuccess, result.Status);
        return;

        async Task sendEventAsync(bool success, string status)
        {
            if (success)
            {
                var evt = new GenericFlowEvent(msg.Event)
                {
                    Action = nameof(ActionIds.ExportToIntegration),
                    Description = status,
                    EventTypeId = msg.Options.NextEventId,
                };

                evt.SetRefValue(nameof(Integration), msg.Options.IntegrationId);
                evt.SetMetaValue(nameof(Integration), IntegrationIds.GetName(msg.Options.IntegrationId.Value));
                
                await MessageBroker.DispatchAsync(evt);
                return;
            }

            var errorEvent = new GenericFlowEvent(msg.Event)
            {
                Action = nameof(ActionIds.ExportToIntegration),
                Description = status,
                EventTypeId = msg.Options.ErrorEventId ?? msg.Options.NextEventId,
            };

            errorEvent.SetRefValue(nameof(Integration), msg.Options.IntegrationId);
            errorEvent.SetMetaValue(nameof(Integration), IntegrationIds.GetName(msg.Options.IntegrationId.Value));

            await MessageBroker.DispatchAsync(errorEvent, !msg.Options.ErrorEventId.HasValue);
        }
    }
}
