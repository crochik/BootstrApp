using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Adapters;
using Crochik.Dipper;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Data.Adapters;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Models.Billing;
using PI.Shared.Salesforce;
using PI.Shared.Services;

namespace Services;

public class SingerService : AbstractMessageQueueService, ILifetimeService
{
    private readonly ILogger<SingerService> _logger;
    private readonly MongoConnection _connection;
    private readonly IMessageBroker _messageBroker;
    private readonly ExtractService _extractService;
    private readonly ITransferService _transfer;
    // private readonly IAPMService _apmService;
    private readonly SalesforceService _salesforceService;
    private readonly ISingerConfigAdapter _adapter;
    private readonly IAccountAdapter _accountAdapter;
    private readonly SalesforceConfig _configuration;

    public SingerService(
        ILogger<SingerService> logger,
        IConfiguration configuration,
        MongoConnection connection,
        IMessageBroker messageBroker,
        ExtractService extractService,
        ITransferService transferService,
        // IAPMService apmService,
        SalesforceService salesforceService,
        ISingerConfigAdapter adapter,
        IAccountAdapter accountAdapter
    ) : base(logger, configuration, messageBroker)
    {
        _logger = logger;
        _connection = connection;
        _messageBroker = messageBroker;
        _extractService = extractService;
        _transfer = transferService;
        // _apmService = apmService;
        _salesforceService = salesforceService;
        _adapter = adapter;
        _accountAdapter = accountAdapter;
        _configuration = SalesforceConfig.Get(configuration);
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.RunSingerSync));
        mapper.Register<RunSingerSyncAction.Message>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            switch (evt.Body)
            {
                case RunSingerSyncAction.Message post:
                    await ProcessAsync(post);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message {id}", evt.RoutingKey);
        }

        evt.Acknowledge();
    }

    private async Task ProcessAsync(RunSingerSyncAction.Message post)
    {
        try
        {
            await ProcessMessageAsync(post);

            var evt = new GenericFlowEvent(post.Event)
            {
                Action = nameof(ActionIds.RunSingerSync),
                Description = "Successfully completed sync",
                EventTypeId = post.Options.SyncedEventId,
            };
            
            await MessageBroker.DispatchAsync(evt);
        }
        catch (Exception ex)
        {
            var evt = new GenericFlowEvent(post.Event)
            {
                Action = nameof(ActionIds.RunSingerSync),
                Description = ex.Message,
                EventTypeId = post.Options.SyncedEventId,
            };

            await MessageBroker.DispatchAsync(evt, true);
        }
    }

    private async Task ProcessMessageAsync(RunSingerSyncAction.Message post)
    {
        if (!post.Options.ConfigurationId.HasValue)
        {
            await ProcessAccountAsync(new AccountContext(post.Event.AccountId));
        }

        var config = await _adapter.GetByIdAsync(post.Options.ConfigurationId.Value);
        if (config == null)
        {
            _logger.LogError("Couldn't find configuration {configId}", post.Options.ConfigurationId.Value);
            throw new BadRequestException("Couldn't find the configuration");
        }

        if (config.AccountId != post.Event.AccountId)
        {
            _logger.LogError("Account mismatch for configuration {configId}", post.Options.ConfigurationId.Value);
            throw new BadRequestException("Account mismatch for configuration");
        }

        var account = await _accountAdapter.GetByIdAsync(config.AccountId);
        if (account == null)
        {
            _logger.LogError("Couldn't load {accountId}", config.AccountId);
            throw new NotFoundException(nameof(Account), config.AccountId);
        }

        await ProcessAsync(account.Context, config);
    }

    public async Task ProcessAccountAsync(IEntityContext context)
    {
        var config = await _adapter.GetDefaultForAccountAsync(context.AccountId.Value);
        if (config == null)
        {
            _logger.LogError("Couldn't determine default configuration {accountId}", context.AccountId.Value);
            throw new BadRequestException("Couldn't find configuration");
        }

        await ProcessAsync(context, config);
    }

    private async Task ValidateAndUpdateAsync(IEntityContext context, SingerImportConfig config)
    {
        // using var apm = _apmService.StartTransaction("Validate", "Validate Tap Config");

        switch (config.TapConfig)
        {
            case SalesforceTapConfig salesforce:
                await UpdateTapConfigAsync(config, salesforce);
                break;

            default:
                throw new Exception("Tap not implemented yet");
        }
    }

    private async Task UpdateTapConfigAsync(SingerImportConfig config, SalesforceTapConfig tap)
    {
        var context = new AccountContext(config.EntityId);
        var (token, error) = await _salesforceService.GetTokenAsync(context, true);
        if (token == null)
        {
            _logger.LogCritical("Couldn't determine token: {Error}", error);
            throw new Exception("Failed to renew Token");
        }

        _logger.LogDebug("Refreshed Salesforce token successfully");
        tap.ClientId = _configuration.ClientId;
        tap.ClientSecret = _configuration.ClientSecret;
        tap.RefreshToken = token.RefreshToken;
    }

    public async Task ProcessAsync(IEntityContext context, SingerImportConfig config)
    {
        await ValidateAndUpdateAsync(context, config);

        SingerJob job = null;
        string error;
        try
        {
            // Extract
            _logger.LogInformation("Starting Extract: {configId}", config.Id);
            (job, error) = await _extractService.ExtractAsync(config);
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogError("Extract failed for {configId}: {error}", config.Id, error);
                throw new Exception("Extract Failed");
            }

            using var scope = _logger.AddScope(new
            {
                config.AccountId,
                config.CurrentTag,
                job.ConfigId,
                job.Tag,
                JobId = job.Id,
            });

            Actor.Current = new SingerSyncActor(job.Id);

            // Load
            _logger.LogInformation("Starting Load");
            var tmpFolder = _extractService.GetTmpFolder(job);
            await _transfer.LoadAsync(tmpFolder, job);
            _logger.LogInformation("Finished Load");

            // Transform
            await TransformAsync(job);

            // Fire 
            await DispatchEventsAsync(context, job);
        }
        finally
        {
            _extractService.RemoveTmpFolder(job);
        }
    }

    public async Task DispatchEventsAsync(IEntityContext context, SingerJob job)
    {
        await DispatchInvoicesAsync(context, job);

        // lead imported events
        var leadCursor = _connection.Filter<Lead>()
            .OfTypeBuilder<Lead, Actor, SingerSyncActor>(x => x.LastActor, q => q.Eq(x => x.JobId, job.Id))
            .ToCursor();

        while (await leadCursor.MoveNextAsync())
        {
            foreach (var lead in leadCursor.Current)
            {
                using var scope = _logger.AddScope(new
                {
                    LeadId = lead.Id,
                });

                if (!lead.FlowId.HasValue)
                {
                    _logger.LogInformation("{LeadId} not part of a flow", lead.Id);
                    continue;
                }

                var evt = new GenericFlowEvent(lead)
                {
                    Actor = context.Actor(),
                    Description = "Processed Update from Salesforce (via Singer)",
                    EventTypeId = EventIds.OnLeadImported,
                };

                await _messageBroker.DispatchAsync(evt);

                // TODO: dispatch lead changed event
                // ...
            }
        }

        // // appt imported events
        // var apptCursor = _connection.Filter<Appointment>()
        //     .OfTypeBuilder<Appointment, Actor, SingerSyncActor>(x => x.LastActor, q => q.Eq(x => x.JobId, job.Id))
        //     .ToCursor();
        //
        // while (await apptCursor.MoveNextAsync())
        // {
        //     foreach (var appt in apptCursor.Current)
        //     {
        //         using var scope = _logger.AddScope(new
        //         {
        //             appt.LeadId,                        
        //             AppointmentId = appt.Id,
        //         });
        //
        //         if (!appt.FlowId.HasValue) continue;
        //
        //         // var evt = new GenericFlowEvent(appt)
        //         // {
        //         //     Actor = context.Actor(),
        //         //     Description = "Processed Update from Salesforce (via Singer)",
        //         //     EventTypeId = EventIds.OnAppointmentImported,
        //         // };
        //         //
        //         // await _messageBroker.DispatchAsync(evt);
        //
        //         // TODO: dispatch appointment changed event
        //         // ...
        //     }
        // }
    }

    public async Task DispatchInvoicesAsync(IEntityContext context, SingerJob job)
    {
        // using var apm = _apmService.StartTransaction("DispatchInvoices", "Dispatch Invoices");

        var parameters = new
        {
            JobId = job.Id.ToString(),
        };

        // TODO: get namespace from account settings
        // ...
        var result = await _connection.DipperAggregateAsync<Invoice>("GetInvoiceTransactions", "fci", parameters);
        var entities = new Dictionary<Guid, Entity>();
        foreach (var invoice in result)
        {
            var entity = await getEntityName(invoice.EntityId);

            var evt = new CreateInvoiceEvent(entity)
            {
                Invoice = invoice,
                Actor = context.Actor(),
                Description = $"New Billable: {invoice.Name}",
                EventTypeId = EventIds.OnCreateInvoice,
            };

            await _messageBroker.DispatchAsync(evt);
        }

        async Task<Entity> getEntityName(Guid? id)
        {
            if (!id.HasValue) return null;
            if (entities.TryGetValue(id.Value, out var name)) return name;

            var entity = await _connection.Filter<Entity>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.Id, id.Value)
                // .IncludeField(x => x.Name)
                // .IncludeField("_t")
                .FirstOrDefaultAsync();

            if (entity == null) return null;
            entities.Add(id.Value, entity);
            return entity;
        }
    }

    public async Task ExtractAsync(IEntityContext context, SingerImportConfig config)
    {
        await ValidateAndUpdateAsync(context, config);

        string error;
        _logger.LogInformation("Starting Extract: {configId}", config.Id);
        (_, error) = await _extractService.ExtractAsync(config);
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogError("Extract failed for {configId}: {error}", config.Id, error);
            throw new Exception("Extract Failed");
        }
    }

    public async Task LoadAsync(IEntityContext context, SingerJob job)
    {
        try
        {
            _logger.LogInformation("Starting Load: {jobId}", job.Id);
            var tmpFolder = _extractService.GetTmpFolder(job);
            await _transfer.LoadAsync(tmpFolder, job);
            _logger.LogInformation("Finish Load: {jobId}", job.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed during replay of {jobId}", job.Id);
            throw;
        }
        finally
        {
            _extractService.RemoveTmpFolder(job);
        }
    }

    /// <summary>
    /// Load local file (for debugging)
    /// </summary>
    public async Task LoadFileAsync(IEntityContext context, SingerJob job, string filename)
    {
        var tmpFolder = _extractService.GetTmpFolder(job);
        await _transfer.ProcessFileAsync(job, filename);
    }

    public async Task<object> TransformAsync(SingerJob job)
    {
        // using var apm = _apmService.StartTransaction("Transform", "AfterSync");

        var parameters = new
        {
            JobId = job.Id.ToString(),
        };

        var result = await _connection.DipperAsync("AfterSync", "fci", parameters);

        return result;
    }
}