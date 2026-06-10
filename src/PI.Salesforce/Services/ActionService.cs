using System;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Messaging;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Models;

namespace Services;

public class ActionService : AbstractMessageQueueService, ILifetimeService
{
    private readonly SalesforceLeadService _salesforceService;

    public ActionService(
        ILogger<ActionService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        SalesforceLeadService salesforceService) :
        base(logger, configuration, messageBroker)
    {
        _salesforceService = salesforceService;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.ExportToSalesforce));
        mapper.Register<ExportToSalesforceAction.Message>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            switch (evt.Body)
            {
                case ExportToSalesforceAction.Message msg:
                    await ProcessMessageAsync(msg);
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

    private async Task ProcessMessageAsync(ExportToSalesforceAction.Message msg)
    {
        using var scope = Logger.AddScope(new
        {
            msg.Event.ObjectType,
            msg.Event.TargetId,
        });

        Logger.LogInformation("Export to Salesforce");

        string id, error;

        try
        {
            // TODO: ACTOR?
            // ...
            var accountContext = new AccountContext(msg.Event.AccountId);

            (id, error) = msg.Event.ObjectType switch
            {
                nameof(Lead) => await _salesforceService.ExportLeadAsync(accountContext, msg),
                nameof(Appointment) => await _salesforceService.ExportAppointmentAsync(accountContext, msg),
                _ => throw new NotImplementedException($"Can't export {msg.Event.ObjectType}"),
            };

            if (!string.IsNullOrEmpty(error))
            {
                Logger.LogInformation(
                    "export of {ObjectType} {ObjectId} failed: {Error}",
                    msg.Event.ObjectType,
                    msg.Event.TargetId,
                    error
                );
            }
            else if (id == null)
            {
                Logger.LogInformation(
                    "{ObjectType} {ObjectId} update skipped",
                    msg.Event.ObjectType,
                    msg.Event.TargetId
                );

                return;
            }
            else
            {
                Logger.LogInformation(
                    "{ObjectType} {ObjectId} exported to salesforce as {ExternalId}",
                    msg.Event.ObjectType,
                    msg.Event.TargetId,
                    id
                );
            }
        }
        catch (Exception ex)
        {
            Logger.LogCritical(ex, "Failed to export {ObjectType} {ObjectId}", msg.Event.ObjectType, msg.Event.TargetId);
            error = $"Exception: {ex.Message}";
        }

        if (!string.IsNullOrEmpty(error) && msg.Options.ErrorEventId.HasValue)
        {
            var evt = new GenericFlowEvent(msg.Event)
            {
                Action = nameof(ActionIds.ExportToSalesforce),
                Description = error,
                EventTypeId = msg.Options.ErrorEventId,
            };

            await MessageBroker.DispatchAsync(evt);
        }
        else
        {
            var evt = new GenericFlowEvent(msg.Event)
            {
                Action = nameof(ActionIds.ExportToSalesforce),
                Description = error ?? $"{msg.Event.ObjectType} exported to Salesforce",
                EventTypeId = msg.Options.NextEventId,
            };

            await MessageBroker.DispatchAsync(evt, error != null);
        }
    }
}