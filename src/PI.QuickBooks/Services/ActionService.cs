using System;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Messaging;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PI.QuickBooks.Models;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Salesforce.Models;

namespace PI.QuickBooks.Services;

public class ActionService : AbstractMessageQueueService, ILifetimeService
{
    private readonly IServiceProvider _serviceProvider;

    public ActionService(
        ILogger<ActionService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        IServiceProvider _serviceProvider
    ) : base(logger, configuration, messageBroker)
    {
        this._serviceProvider = _serviceProvider;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.ExportToQuickbooks));
        mapper.Register<SimpleActionMessage<GenericActionOptions>>();
        mapper.Register<SimpleActionMessage<ExportToQuickbooksActionOptions>>(); // should never happen but... 
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        var flowEvent = (evt.Body as IActionMessage)?.GetEvent();

        using var scope = Logger.AddScope(new
        {
            flowEvent?.FlowId,
            flowEvent?.RunId,
            flowEvent?.EventTypeId,
            flowEvent?.Action,
            flowEvent?.TargetId,
        });

        Logger.LogInformation("Process {Event}", flowEvent?.GetType().Name);

        try
        {
            switch (evt.Body)
            {
                case SimpleActionMessage<GenericActionOptions> msg:
                    await ProcessMessageAsync(msg);
                    break;

                case SimpleActionMessage<ExportToQuickbooksActionOptions> msg:
                    await ProcessMessageAsync(msg);
                    break;
            }
        }
        finally
        {
            evt.Acknowledge();
        }
    }

    private async Task ProcessMessageAsync(SimpleActionMessage<GenericActionOptions> action)
    {
        if (action.Options is not GenericActionOptions genericActionOptions)
        {
            Logger.LogError("Unexpected Options");
            return;
        }

        var options = genericActionOptions.ConvertTo<ExportToQuickbooksActionOptions>();
        options.Output = genericActionOptions.Output;

        await ProcessMessageAsync(action.Event, options);
    }

    private async Task ProcessMessageAsync(SimpleActionMessage<ExportToQuickbooksActionOptions> action)
        => await ProcessMessageAsync(action.Event, action.Options);

    private async Task ProcessMessageAsync(FlowEvent evt, ExportToQuickbooksActionOptions options)
    {
        using var scope = Logger.AddScope(new
        {
            evt.ObjectType,
            evt.TargetId,
        });

        Logger.LogInformation("Export to Quickbooks");

        var result = await ExportInvoiceAsync(evt);

        if (result.IsError)
        {
            Logger.LogError("Failed to export to Quickbooks: {Status}", result.Status);

            var errorOutput = options.Output.FirstOrDefault(x => x.Name == ExportToQuickbooksActionOptions.OnErrorEvent);
            if (errorOutput?.EventId.HasValue ?? false)
            {
                var errorEvt = new GenericFlowEvent(evt)
                {
                    Action = nameof(ActionIds.ExportToQuickbooks),
                    Description = result.Status, //  errorOutput.Description,
                    EventTypeId = errorOutput.EventId,
                };

                errorEvt.SetMetaValue("Action|Output|Errors", result.Status);

                await MessageBroker.DispatchAsync(errorEvt);
            }

            return;
        } 
        
        if (result.IsUnknown)
        {
            Logger.LogError("Skipped export to Quickbooks: {Status}", result.Status);

            var duplicateOutput = options.Output.FirstOrDefault(x => x.Name == ExportToQuickbooksActionOptions.Duplicate);
            if (duplicateOutput?.EventId.HasValue ?? false)
            {
                var errorEvt = new GenericFlowEvent(evt)
                {
                    Action = nameof(ActionIds.ExportToQuickbooks),
                    Description = result.Status, 
                    EventTypeId = duplicateOutput.EventId,
                };

                errorEvt.SetMetaValue("Action|Output|Errors", result.Status);

                await MessageBroker.DispatchAsync(errorEvt);
            }

            return;
        }

        // success
        if (result.Value.Warnings?.Length > 0)
        {
            Logger.LogInformation("Exported Invoice {QuickbooksId}: {Warnings}", result.Value.Invoice.ExternalId, string.Join("; ", result.Value.Warnings));
        }
        else
        {
            Logger.LogInformation("Exported Invoice {QuickbooksId}", result.Value.Invoice.ExternalId);
        }

        var successOutput = options.Output.FirstOrDefault(x => x.Name == ExportToQuickbooksActionOptions.OnSuccessEvent);
        if (successOutput?.EventId.HasValue ?? false)
        {
            var successEvt = new GenericFlowEvent(evt)
            {
                Action = nameof(ActionIds.ExportToQuickbooks),
                Description = successOutput.Description,
                EventTypeId = successOutput.EventId,
            };

            successEvt.AddRefValue("QuickbooksInvoice", result.Value.Invoice.ExternalId);
            if (result.Value.Warnings?.Length > 0)
            {
                successEvt.SetMetaValue("Action|Output|Errors", string.Join('\n', result.Value.Warnings));
            }

            successEvt.SetMetaValue("Action|Output|QuickbooksInvoiceId", result.Value.Invoice.ExternalId);

            await MessageBroker.DispatchAsync(successEvt);
        }
    }

    private async Task<Result<ExportInvoiceResult>> ExportInvoiceAsync(FlowEvent evt)
    {
        try
        {
            using var serviceScope = _serviceProvider.CreateScope();

            Result<QbEntity> result;
            var warnings = default(string[]);
            switch (evt.ObjectType)
            {
                case SfOption.ObjectTypeName:
                {
                    var builder = serviceScope.ServiceProvider.GetRequiredService<QbTransactionExporter>();
                    result = await builder.ExportInvoiceAsync(new AccountContext(evt.AccountId), evt.TargetId);
                    warnings = builder.Errors?.ToArray();
                    break;
                }

                case ProductCatalog.Models.Estimate.ObjectTypeFullName:
                {
                    var builder = serviceScope.ServiceProvider.GetRequiredService<QbInvoiceFromEstimateExporter>();
                    result = await builder.ExportInvoiceAsync(new AccountContext(evt.AccountId), evt.TargetId);
                    warnings = builder.Errors?.ToArray();
                    break;
                }

                default:
                    result = Result.Error<QbEntity>($"Unexpected Object Type: {evt.ObjectType}");
                    break;
            }

            if (!result.IsSuccess) return result.ConvertTo<ExportInvoiceResult>();

            return Result.Success(new ExportInvoiceResult
            {
                Warnings = warnings,
                Invoice = result.Value,
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception while exporting to Quickbooks");
            return Result.Error<ExportInvoiceResult>($"Exception creating invoice: {ex.Message}");
        }
    }

    public class ExportInvoiceResult
    {
        public string[] Warnings { get; init; }
        public QbEntity Invoice { get; init; }
    }
}