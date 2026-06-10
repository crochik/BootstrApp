using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using MongoDB.Bson;
using MongoDB.Driver;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Data.Adapters;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Models.Billing;
using PI.Shared.Models.Expressions;
using Crochik.Extensions;

namespace Services;

public class BillingContext
{
    public BillEntity Billing { get; set; }
    public Account Account { get; set; }

    // TODO: this probably should be entity
    public Organization Entity { get; set; }
}

public class StatementService : AbstractMessageQueueService, ILifetimeService
{
    private readonly ILogger<StatementService> _logger;
    private readonly MongoConnection _connection;
    private readonly IOrganizationAdapter _organizationAdapter;

    public StatementService(
        ILogger<StatementService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        MongoConnection connection,
        IOrganizationAdapter organizationAdapter
    ) : base(logger, configuration, messageBroker)
    {
        _logger = logger;
        _connection = connection;
        _organizationAdapter = organizationAdapter;
    }

    protected override void Init(IMessageQueue messageQueue, TypeMapper mapper)
    {
        MessageBroker.Bind(messageQueue, EventIds.GetRoute(EventIds.OnPendingPayment));
        MessageBroker.Bind(messageQueue, EventIds.GetRoute(EventIds.OnPayment));
        MessageBroker.Bind(messageQueue, EventIds.GetRoute(EventIds.OnFailedPayment));
        mapper.Register<PaymentStatusUpdateEvent>();

        MessageBroker.Bind(messageQueue, EventIds.GetRoute(EventIds.OnCreateInvoice));
        mapper.Register<CreateInvoiceEvent>();

        MessageBroker.Bind(messageQueue, ActionIds.GetRoute(ActionIds.CreateInvoice));
        mapper.Register<SimpleActionMessage<CreateInvoiceActionOptions>>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        var parts = evt.RoutingKey.Split('.');
        var eventId = Guid.Parse(parts[1]);

        try
        {
            switch (evt.Body)
            {
                case PaymentStatusUpdateEvent payment:
                    await UpdatePaymentAsync(payment);
                    break;

                case CreateInvoiceEvent invoice:
                    await CreateInvoiceAsync(invoice);
                    break;

                case SimpleActionMessage<CreateInvoiceActionOptions> invoice:
                    await CreateInvoiceAsync(eventId, invoice);
                    break;
            }

            evt.Acknowledge();

            // TODO: flag last "run" for service with timestamp?
            // ...
        }
        catch (Exception ex)
        {
            // TODO: flag "service" as not working and notify mother ship?
            // ... 

            _logger.LogCritical(ex, "Failed to process transaction, time bomb initiated!");
            throw;
        }
    }

    public async Task<BillEntity> UpdateAutoRefillAsync(IEntityContext context, Guid id, bool autoRefill, decimal? minBalance, decimal? maxBalance)
    {
        var entity = await GetOrganizationOrThrowAsync(context, id);
        if (entity.Billing == null) return null;

        entity.Billing = await _connection.Filter<BillEntity>()
            .Eq(x => x.Id, entity.Billing.Id)
            .Update
            .Set(x => x.MinBalance, minBalance)
            .Set(x => x.MaxBalance, maxBalance)
            .Set(x => x.AutoRefill, autoRefill)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .UpdateAndGetOneAsync();

        if (entity.Billing == null) return null;

        // var actor = !context.EntityId.HasValue ?
        //     null :
        //     await _connection.Filter<Entity>()
        //         .Eq(x => x.AccountId, context.AccountId.Value)
        //         .Eq(x => x.Id, context.EntityId.Value)
        //         .IncludeField(x => x.Name)
        //         .FirstOrDefaultAsync();

        var evt = new AutoRefillSettingsUpdated(entity.Entity, entity.Billing, context.Actor())
        {
            EventTypeId = EventIds.OnAutoRefillSettingsUpdated,
        };
        
        await MessageBroker.DispatchAsync(evt);

        return entity.Billing;
    }

    private async Task CreateInvoiceAsync(Guid eventId, SimpleActionMessage<CreateInvoiceActionOptions> action)
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

        Logger.LogInformation("Create Invoice");

        if (action.Event.ObjectType != nameof(Lead))
        {
            // do not fire event as is simply not supported
            throw new BadRequestException("Only leads");
        }

        var result = await CreateInvoiceAsync(accountContext, action);
        if (result.IsUnknown) return;


        if (result.IsError)
        {
            // error event`
            Logger.LogError("Failed to create Invoice: {Status}", result.Status);

            var newEvent = new GenericFlowEvent(evt)
            {
                Action = nameof(ActionIds.CreateInvoice),
                EventTypeId = action.Options.NextEventId,
                Description = result.Status ?? "Failed to create invoice"
            };

            await MessageBroker.DispatchAsync(newEvent, true);
            return;
        }

        if (result.IsSuccess)
        {
            Logger.LogInformation("Invoice Created Successfully: {InvoiceId}", result.Value.Id);

            var newEvent = new GenericFlowEvent(evt)
            {
                Action = nameof(ActionIds.CreateInvoice),
                EventTypeId = action.Options.NextEventId,
                Description = result.Status ?? "Invoice Created",
            };

            newEvent.AddRefValue(nameof(Invoice), result.Value.Id);
            await MessageBroker.DispatchAsync(newEvent);
            return;
        }

        // unknown = no item
        Logger.LogInformation("Skipped creating invoice (no invoice item for scenario)");

        var skipEvent = new GenericFlowEvent(evt)
        {
            Action = nameof(ActionIds.CreateInvoice),
            EventTypeId = action.Options.SkipEventId,
            Description = result.Status ?? "Skip Invoice Creation",
        };
        
        await MessageBroker.DispatchAsync(skipEvent);
    }

    /// <summary>
    /// Create Invoice Action
    /// </summary>
    private async Task<Result<Invoice>> CreateInvoiceAsync(IEntityContext context, SimpleActionMessage<CreateInvoiceActionOptions> action)
    {
        var currentRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.Id, action.Event.RunId)
            .ExcludeField(x => x.Steps)
            .FirstOrDefaultAsync();

        var objectContext = currentRun.BuildHandlebarsContext();

        var name = ResolveValue<string>(action.Options.Name);
        var description = ResolveValue<string>(action.Options.Description) ?? ResolveValue<string>("Object.Description") ?? ResolveValue<string>("Object.Name");
        var entityIdStr = ResolveValue<string>(action.Options.EntityId);

        if (string.IsNullOrEmpty(entityIdStr) || !Guid.TryParse(entityIdStr, out var entityId))
        {
            // error
            return Result.Error<Invoice>("Invalid or missing EntityId");
        }

        if (string.IsNullOrWhiteSpace(name)) return Result.Error<Invoice>("Invalid or missing Name");

        var entity = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, entityId)
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            // error
            return Result.Error<Invoice>("Organization not found");
        }

        var suffix = ResolveValue<string>(action.Options.ExternalIdSuffix);
        var externalId = string.IsNullOrWhiteSpace(suffix) ? action.Event.TargetId.ToString() : $"{action.Event.TargetId}-{suffix}";

        decimal total = 0;
        var items = new List<Invoice.Item>();

        // dynamic value from run
        if (!string.IsNullOrWhiteSpace(action.Options.Item))
        {
            var itemIdStr = ResolveValue<string>(action.Options.Item, "_id");
            if (string.IsNullOrEmpty(itemIdStr) || !Guid.TryParse(itemIdStr, out var itemId))
            {
                // assumes this means that there is not a charge for this "scenario"
                return Result.Unknown<Invoice>("Invalid or missing ItemId");
            }

            var itemName = ResolveValue<string>(action.Options.Item, nameof(Invoice.Item.Name));
            var itemDescription = ResolveValue<string>(action.Options.Item, nameof(Invoice.Item.Description));

            if (string.IsNullOrWhiteSpace(itemName))
            {
                // error
                return Result.Error<Invoice>("Invalid or missing Item Name");
            }

            if (!TryToResolveDecimal(action.Options.Item, nameof(Invoice.Item.Value), out var itemValue))
            {
                // error
                return Result.Error<Invoice>("Invalid or missing value");
            }

            items.Add(new Invoice.Item
            {
                Id = itemId,
                Name = itemName ?? name,
                Description = itemDescription,
                Value = itemValue,
            });
            
            total += itemValue;
        }

        if (action.Options.AdditionalItems?.Length > 0)
        {
            var additionalItems = await _connection.Filter<BillableItem>()
                .Eq(x => x.AccountId, action.Event.AccountId)
                .In(x => x.Id, action.Options.AdditionalItems)
                .Ne(x => x.IsActive, false)
                .FindAsync();

            if (additionalItems.Count != action.Options.AdditionalItems.Length) return Result.Error<Invoice>("Couldn't load all additional items");
            var dict = additionalItems.ToDictionary(x => x.Id);

            var invoiceContext = new Dictionary<string, object>()
            {
                { "Total", total }
            };
            ((IDictionary<string,object>)objectContext)["Invoice"] = invoiceContext;

            foreach (var additionalItemId in action.Options.AdditionalItems)
            {
                var ai = dict[additionalItemId];
                invoiceContext["Total"] = total;
                
                var value = ai.Value;
                if (!string.IsNullOrEmpty(ai.Formula))
                {
                    if (!TryToResolveDecimal(ai.Formula, null, out var v1))
                    {
                        _logger.LogError("Failed to resolve {Formula} for {AdditionalItem}", ai.Formula, ai.Id);
                        return Result.Error<Invoice>("Couldn't calculate additional item");
                    }

                    value += v1;
                }

                if (ai.Factor.HasValue && ai.Factor > 0)
                {
                    value *= ai.Factor.Value;
                }
                
                var item = new Invoice.Item
                {
                    Id = ai.Id,
                    Name = ai.Name,
                    Description = ai.Description,
                    Value = value,
                };
                items.Add(item);
                total += item.Value;
            }
        }

        if (items.IsEmpty())
        {
            // error
            return Result.Error<Invoice>("Empty invoice");
        }

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            ExternalId = externalId,
            AccountId = action.Event.AccountId,
            EntityId = entityId,
            OrganizationId = entityId,
            CreatedOn = DateTime.UtcNow,
            LastActor = context.Actor(),
            ReferenceDate = DateTime.UtcNow,
            Total = -total,
            Name = name,
            Description = description,
            Items = items.ToArray(),
            Refs = new[]
            {
                new Reference
                {
                    Type = action.Event.ObjectType,
                    Id = action.Event.TargetId,
                    Tag = suffix,
                }
            }
        };

        return await AddInvoiceAndUpdateAsync(context, invoice, action.Event.RunId);

        bool TryToResolveDecimal(string pathOrValue, string property, out decimal output)
        {
            var objValue = ResolveValue<object>(pathOrValue, property);
            var itemValue = objValue switch
            {
                string str => decimal.TryParse(str, out var decValue) ? decValue : default(decimal?),
                decimal dec => dec,
                Decimal128 dec => (decimal)dec,
                int i => i,
                long i => i,
                float f => (decimal)f,
                _ => default
            };

            if (itemValue.HasValue)
            {
                output = itemValue.Value;
                return true;
            }

            output = default;
            return false;
        }
        
        T ResolveValue<T>(string pathOrValue, string property = null)
        {
            if (pathOrValue.StartsWith("{{") && pathOrValue.EndsWith("}}"))
            {
                var path = pathOrValue.Substring(2, pathOrValue.Length - 4).Split(".");
                if (property != null) path = path.Append(property).ToArray();
                if (!objectContext.TryResolveValue(path, out var obj) || obj is not T str)
                {
                    str = default(T);
                }

                return str;
            }

            return pathOrValue is T t ? t : default(T);
        }
    }

    /// <summary>
    /// Create Invoice from event fired by singer
    /// </summary>
    private async Task CreateInvoiceAsync(CreateInvoiceEvent evt)
    {
        var accountContext = new AccountContext(evt.Invoice.AccountId);

        evt.Invoice.ExternalId ??= evt.Invoice.Id.ToString();

        await AddInvoiceAndUpdateAsync(accountContext, evt.Invoice, evt.RunId);
    }

    /// <summary>
    /// Add Invoice
    /// </summary>
    private async Task<Result<Invoice>> AddInvoiceAndUpdateAsync(IEntityContext context, Invoice invoice, Guid runId, bool retry = true)
    {
        using var scope = _logger.AddScope(new
        {
            InvoiceId = invoice.Id,
            invoice.EntityId,
            invoice.OrganizationId,
            invoice.ExternalId,
        });

        Logger.LogInformation("Create Invoice");

        try
        {
            var existing = await _connection.Filter<BillTransaction>()
                .OfType<BillTransaction, Invoice>()
                .Eq(x => x.AccountId, invoice.AccountId)
                .Eq(x => x.ExternalId, invoice.ExternalId)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                _logger.LogDebug("There is already an invoice: {id}", existing.Id);
                return Result.Error<Invoice>("There is already an invoice");
            }

            var billingContext = await GetOrganizationAsync(context, invoice.OrganizationId.Value, invoice.ReferenceDate);
            if (!billingContext)
            {
                _logger.LogError(
                    "Can't create {invoiceId} for {organizationId}: {error}",
                    invoice.ExternalId,
                    invoice.OrganizationId.Value,
                    billingContext.Status
                );

                return Result.Error<Invoice>("Organization not configured for billing");
            }

            invoice = await AddTransactionAsync(billingContext.Value, invoice, runId);

            return Result.Success(invoice);
        }
        catch (AddTransactionException ex)
        {
            if (retry)
            {
                _logger.LogError(ex, "Failed to add invoice, retry");
                return await AddInvoiceAndUpdateAsync(context, invoice, runId, false);
            }

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create invoice");
            throw;
        }
    }

    private async Task<T> AddTransactionAsync<T>(BillingContext context, T transaction, Guid? runId = null) where T : BillTransaction
    {
        transaction.CreatedOn = DateTime.UtcNow;
        transaction.Number = context.Billing.TransactionNumber + 1;
        transaction.Balance = context.Billing.Balance + transaction.Total.GetValueOrDefault(0);

        using var session = await _connection.StartSessionAsync();
        try
        {
            session.StartTransaction();
            await _connection.InsertAsync(session, transaction);

            var result = await _connection.Filter<BillEntity>(session)
                .Eq(x => x.Id, context.Billing.Id)
                .Eq(x => x.TransactionNumber, context.Billing.TransactionNumber)
                .Eq(x => x.Balance, context.Billing.Balance)
                .Update
                .Set(x => x.TransactionNumber, transaction.Number)
                .Set(x => x.Balance, transaction.Balance)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, Actor.Current) // ????
                .UpdateAndGetOneAsync();

            if (result == null) throw new Exception("Failed to update entity");
            await session.CommitTransactionAsync();

            _logger.LogInformation(
                "Added {transactionNumber} of {total}, changed balance {from} {to}",
                transaction.Number,
                transaction.Total,
                context.Billing.Balance,
                result.Balance
            );

            await FireEventAsync(context, result, transaction, runId);

            // update context with current billing status
            context.Billing = result;

            return transaction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add {transactionId}", transaction.ExternalId);
            await session.AbortTransactionAsync();
            throw new AddTransactionException(ex);
        }
    }

    private async Task FireEventAsync<T>(BillingContext before, BillEntity after, T transaction, Guid? runId) where T : BillTransaction
    {
        if (before.Billing.Balance == after.Balance) return;
        if (!before.Account.FlowId.HasValue)
        {
            _logger.LogInformation("Won't fire event: undefined flow for {accountId}", before.Account.Id);
            return;
        }

        var evt = new BalanceUpdatedEvent(before.Entity)
        {
            // FlowId = FlowIds.Billing,
            // StatusId = null,
            // AccountId = after.AccountId,
            // TargetId = after.Id,
            // Entity = after.Name,
            TransactionId = transaction.Id,
            TransactionType = transaction.GetType().Name,
            TransactionNumber = transaction.Number,
            Description = $"Account balance after {transaction.GetType().Name} #{transaction.Number} is {after.Balance.FormatCurrency()}",
            PreviousBalance = before.Billing.Balance,
            Balance = after.Balance,
            EventTypeId = EventIds.OnBalanceUpdated,
        };

        if (runId.HasValue) evt.RunId = runId.Value;

        await MessageBroker.DispatchAsync(evt);
    }

    public async Task<Dispute> ResolveDisputeAsync(IEntityContext context, BillingContext billingContext, User user, Dispute dispute, DisputeResolution resolution, Guid adjustmentId)
    {
        var result = await _connection.Filter<BillTransaction, Dispute>()
            .Eq(x => x.Id, dispute.Id)
            .Eq(x => x.ResolvedOn, null)
            .Update
            .Set(x => x.ResolvedOn, DateTime.UtcNow)
            .Set(x => x.ResolvedBy, user.Name)
            .Set(x => x.ResolvedByEntityId, user.Id)
            .Set(x => x.Resolution, resolution)
            .Set(x => x.AdjustmentId, adjustmentId)
            .UpdateAndGetOneAsync();

        if (result != null)
        {
            var evt = new DisputeEvent(billingContext.Entity)
            {
                Actor = context.Actor(),
                Description = resolution switch
                {
                    DisputeResolution.Approve => $"Dispute approved by {result.ResolvedBy}",
                    DisputeResolution.Reject => $"Dispute rejected by {result.ResolvedBy}",
                    _ => $"Dispute resolved by {result.ResolvedBy}",
                },
                Dispute = result,
                EventTypeId = result.Resolution == DisputeResolution.Reject ? EventIds.OnDisputeRejected : EventIds.OnDisputeApproved,
            };

            await MessageBroker.DispatchAsync(evt);
        }

        return result;
    }

    public async Task<Dispute> AddDisputeAsync(IEntityContext context, BillingContext billingContext, Dispute dispute)
    {
        var result = await AddTransactionAsync(billingContext, dispute);

        var evt = new DisputeEvent(billingContext.Entity)
        {
            Actor = context.Actor(),
            Description = $"Dispute initiated for {result.Name} by {result.InitiatedBy}",
            Dispute = result,
            EventTypeId = EventIds.OnDisputeCreated,
        };

        await MessageBroker.DispatchAsync(evt);

        return result;
    }

    public async Task<Adjustment> AddAdjustmentAsync(IEntityContext context, BillingContext billingContext, Adjustment adjustment)
    {
        var result = await AddTransactionAsync(billingContext, adjustment);

        var evt = new AdjustmentEvent(billingContext.Entity)
        {
            Actor = context.Actor(),
            Description = $"{result.AdjustedBy} adjusted balance by {result.Total.FormatCurrency()}",
            Adjustment = result,
            EventTypeId = EventIds.OnBalanceAdjusted,
        };

        await MessageBroker.DispatchAsync(evt);

        return result;
    }

    public Task<BillingContext> GetOrganizationOrThrowAsync(IEntityContext context, Guid id)
    {
        return GetOrganizationOrThrowAsync(context, id, DateTime.UtcNow);
    }

    private async Task<BillingContext> GetOrganizationOrThrowAsync(IEntityContext context, Guid organizationId, DateTime referenceDate)
    {
        var billingContext = await GetOrganizationAsync(context, organizationId, referenceDate);
        if (billingContext) return billingContext.Value;

        _logger.LogError($"Failed to get billing context for {organizationId}: {billingContext.Status}");
        throw new Exception(billingContext.Status);
    }

    private async Task<Result<BillingContext>> GetOrganizationAsync(IEntityContext context, Guid organizationId, DateTime referenceDate)
    {
        var org = await _organizationAdapter.GetByIdAsync(context, organizationId);
        if (org == null) return Result<BillingContext>.Error($"Organization not found: {organizationId}");

        var account = await _connection.Filter<Entity, Account>()
            .Eq(x => x.Id, org.AccountId)
            .FirstOrDefaultAsync();

        if (account == null) return Result<BillingContext>.Error($"Account for {org.Name} not found: {org.AccountId}");

        var entity = await _connection.Filter<BillEntity>()
            .Eq(x => x.Id, organizationId)
            .FirstOrDefaultAsync();

        if (entity != null)
        {
            return Result.Success(new BillingContext
            {
                Billing = entity,
                Entity = org,
                Account = account,
            });
        }

        _logger.LogInformation("Initializing billing for {organizationId}", organizationId);

        entity = await InitializeAsync(referenceDate, org);
        if (entity == null) return Result<BillingContext>.Error($"Failed to initialize billing for {org.Id}");

        return Result.Success(new BillingContext
        {
            Billing = entity,
            Entity = org,
            Account = account
        });
    }

    private async Task<Payment> UpdatePaymentAsync(PaymentStatusUpdateEvent evt)
    {
        using var scope = _logger.AddScope(new
        {
            evt.Payment.Id,
            evt.Payment.EntityId,
            evt.Payment.OrganizationId,
            evt.Payment.ExternalId,
            evt.Status,
        });

        try
        {
            // ????
            if (!evt.Payment.OrganizationId.HasValue)
            {
                if (!evt.Payment.EntityId.HasValue)
                {
                    _logger.LogError("No Organization or Entity available to add {paymentId}: {externalId}", evt.Payment.Id, evt.Payment.ExternalId);
                    return null;
                }

                evt.Payment.OrganizationId = evt.Payment.EntityId;
            }

            var accountContext = new AccountContext(evt.Payment.AccountId);
            var billingContext = await GetOrganizationOrThrowAsync(accountContext, evt.Payment.OrganizationId.Value, evt.Payment.ReferenceDate);
            await AdjustPendingTotalAsync(evt, billingContext);

            return await AddPaymentAsync(billingContext, evt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to Update Payment {externalId}", evt.Payment.ExternalId);
            throw;
        }
    }

    private async Task AdjustPendingTotalAsync(PaymentStatusUpdateEvent evt, BillingContext context)
    {
        switch (evt.Status)
        {
            case PaymentStatus.Succeeded:
            case PaymentStatus.Failed:
                await RemovePendingTransactionAsync(context, evt);
                break;

            case PaymentStatus.Pending:
                await AddPendingTransactionAsync(context, evt);
                break;
        }
    }

    private async Task AddPendingTransactionAsync(BillingContext context, PaymentStatusUpdateEvent evt)
    {
        var modified = await _connection.Filter<BillEntity>()
            .Eq(x => x.Id, context.Billing.Id)
            .AnyNe(x => x.PendingTransactions, evt.Payment.ExternalId)
            .Update
            .AddToSet(x => x.PendingTransactions, evt.Payment.ExternalId)
            .Inc(x => x.PendingTotal, evt.Payment.Total.Value)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, null) // TODO: ...
            .UpdateAndGetOneAsync();

        _logger.LogInformation(
            "Added Pending transaction from {before} to {after}: {modified}",
            context.Billing.PendingTotal,
            modified?.PendingTotal,
            modified != null
        );

        if (modified != null) context.Billing = modified;
    }

    private async Task RemovePendingTransactionAsync(BillingContext context, PaymentStatusUpdateEvent evt)
    {
        var modified = await _connection.Filter<BillEntity>()
            .Eq(x => x.Id, context.Billing.Id)
            .AnyEq(x => x.PendingTransactions, evt.Payment.ExternalId)
            .Update
            .Pull(x => x.PendingTransactions, evt.Payment.ExternalId)
            .Inc(x => x.PendingTotal, -evt.Payment.Total.Value)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, null) // TODO: ...
            .UpdateAndGetOneAsync();

        _logger.LogInformation(
            "Removed Pending transaction from {before} to {after}: {modified}",
            context.Billing.PendingTotal,
            modified?.PendingTotal,
            modified != null
        );

        if (modified != null) context.Billing = modified;
    }

    private async Task<Payment> AddPaymentAsync(BillingContext context, PaymentStatusUpdateEvent evt, bool retry = true)
    {
        try
        {
            switch (evt.Status)
            {
                case PaymentStatus.Succeeded:
                    break;

                case PaymentStatus.Failed:
                    evt.Payment.Name = $"Charge of {evt.Payment.Total.FormatCurrency()} failed";
                    evt.Payment.Total = null;
                    evt.Payment.ExternalId = $"{evt.Payment.ExternalId}:failed";
                    break;

                case PaymentStatus.Pending:
                    evt.Payment.Name = $"Pending Payment of {evt.Payment.Total.FormatCurrency()}";
                    evt.Payment.Total = null;
                    evt.Payment.ExternalId = $"{evt.Payment.ExternalId}:pending";
                    break;
            }

            var existing = await _connection.Filter<BillTransaction>()
                .OfType<BillTransaction, Payment>()
                .Eq(x => x.AccountId, evt.Payment.AccountId)
                .Eq(x => x.ExternalId, evt.Payment.ExternalId)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                _logger.LogDebug("There is already a transaction: {id}", existing.Id);
                return null;
            }

            return await AddTransactionAsync(context, evt.Payment, evt.RunId);
        }
        catch (AddTransactionException ex)
        {
            if (retry)
            {
                _logger.LogError(ex, "Failed to add payment transaction, retry");
                return await AddPaymentAsync(context, evt, false);
            }

            throw ex;
        }
    }

    private async Task ResetAsync(IEntityContext context)
    {
        await _connection.Filter<BillTransaction>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Update.Unset(x => x.Balance)
            .UpdateManyAsync();
    }

    public async Task ResetAsync(IEntityContext context, Guid id)
    {
        await _connection.Filter<BillTransaction>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.OrganizationId, id)
            .Update.Unset(x => x.Balance)
            .UpdateManyAsync();
    }

    public async Task InitAsync(IEntityContext context)
    {
        await ResetAsync(context);
    }

    public async Task<bool> CalculateAsync(IEntityContext context, Guid id, DateTime? start = null, decimal? startBalance = null)
    {
        var organization = await _organizationAdapter.GetByIdAsync(context, id);
        if (organization == null) return false;

        var first = await GetStartAsync(context, start, organization);
        if (first == null)
        {
            _logger.LogInformation("Nothing to do for: {organization}", organization.Name);
            return true;
        }

        var balance = startBalance.GetValueOrDefault(0);
        if (!startBalance.HasValue && start.HasValue) balance -= first.Total.GetValueOrDefault(0);

        var cursor = _connection.Filter<BillTransaction>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.OrganizationId, organization.Id)
            .SortAsc(x => x.CreatedOn)
            .Gte(x => x.CreatedOn, first.CreatedOn)
            .ToCursor();

        while (await cursor.MoveNextAsync())
        {
            var list = new List<UpdateOneModel<BillTransaction>>();
            foreach (var item in cursor.Current)
            {
                balance += item.Total.GetValueOrDefault(0);

                list.Add(_connection.Filter<BillTransaction>()
                    .Eq(x => x.Id, item.Id)
                    .Update.Set(x => x.Balance, balance)
                    .UpdateOneModel()
                );
            }

            var result = await _connection.BulkWriteAsync(list);
        }

        return true;
    }

    private async Task<BillTransaction> GetStartAsync(IEntityContext context, DateTime? start, Organization organization)
    {
        if (start.HasValue)
        {
            // start from 
            return await _connection.Filter<BillTransaction>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.OrganizationId, organization.Id)
                .SortAsc(x => x.CreatedOn)
                .Gte(x => x.CreatedOn, start.Value)
                .Limit(1)
                .FirstOrDefaultAsync();
        }

        // find last balance
        var first = await _connection.Filter<BillTransaction>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.OrganizationId, organization.Id)
            .SortDesc(x => x.CreatedOn)
            .Limit(1)
            .Exists(x => x.Balance)
            .FirstOrDefaultAsync();

        if (first != null) return first;

        // oldest
        first = await _connection.Filter<BillTransaction>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.OrganizationId, organization.Id)
            .SortAsc(x => x.CreatedOn)
            .Limit(1)
            .FirstOrDefaultAsync();

        return first;
    }

    public async Task RecalculateAllAsync(IEntityContext context, DateTime start)
    {
        var orgs = await _organizationAdapter.GetByAccountAsync(context.AccountId.Value);
        foreach (var org in orgs)
        {
            var first = await _connection.Filter<BillTransaction>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.OrganizationId, org.Id)
                .SortAsc(x => x.CreatedOn)
                .Gte(x => x.CreatedOn, start)
                .Limit(1)
                .FirstOrDefaultAsync();

            if (first == null)
            {
                _logger.LogInformation("No transactions for {organizationId}", org.Id);
                continue;
            }

            var entity = await InitializeAsync(start, org);
            await ReCalculateAsync(context, entity);
        }
    }

    private async Task<BillEntity> InitializeAsync(DateTime start, Organization org)
    {
        return await _connection.Filter<BillEntity>()
            .Eq(x => x.Id, org.Id)
            .Update
            .SetOnInsert(x => x.AccountId, org.AccountId)
            .SetOnInsert(x => x.Name, org.Name)
            .Set(x => x.CreatedOn, start)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.Balance, 0)
            .Set(x => x.TransactionNumber, 10000)
            .UpdateAndGetOneAsync(true);
    }

    private async Task<bool> ReCalculateAsync(IEntityContext context, BillEntity entity)
    {
        var cursor = _connection.Filter<BillTransaction>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.OrganizationId, entity.Id)
            .SortAsc(x => x.CreatedOn)
            .Gte(x => x.CreatedOn, entity.CreatedOn)
            .ToCursor();

        var balance = entity.Balance;
        var number = entity.TransactionNumber;

        while (await cursor.MoveNextAsync())
        {
            var list = new List<UpdateOneModel<BillTransaction>>();
            foreach (var item in cursor.Current)
            {
                balance += item.Total.GetValueOrDefault(0);
                number++;

                list.Add(_connection.Filter<BillTransaction>()
                    .Eq(x => x.Id, item.Id)
                    .Update
                    .Set(x => x.Balance, balance)
                    .Set(x => x.Number, number)
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    .UpdateOneModel()
                );
            }

            using var session = await _connection.StartSessionAsync();
            try
            {
                session.StartTransaction();

                var result = await _connection.BulkWriteAsync(session, list);
                if (result.ModifiedCount != list.Count) throw new Exception("Failed to update transactions");

                var result2 = await _connection.Filter<BillEntity>(session)
                    .Eq(x => x.Id, entity.Id)
                    .Eq(x => x.TransactionNumber, entity.TransactionNumber)
                    .Eq(x => x.Balance, entity.Balance)
                    .Update
                    .Set(x => x.TransactionNumber, number)
                    .Set(x => x.Balance, balance)
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    .Set(x => x.LastActor, null) // TODO: ...
                    .UpdateAndGetOneAsync();

                if (result2 == null) throw new Exception("Failed to update entity");

                await session.CommitTransactionAsync();
                entity = result2;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update balance for {organizationId}", entity.Id);
                await session.AbortTransactionAsync();
            }
        }

        return true;
    }
}

public class AddTransactionException : Exception
{
    public AddTransactionException(Exception ex) : base("Failed to add transaction", ex)
    {
    }
}