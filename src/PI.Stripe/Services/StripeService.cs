using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
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
using PI.Shared.Models;
using PI.Shared.Models.Billing;
using Stripe;
using Stripe.BillingPortal;
using Invoice = Stripe.Invoice;
using Crochik.Extensions;
using PI.Shared.Exceptions;

namespace Services;

public class StripeService : AbstractMessageQueueService, ILifetimeService
{
    private readonly IMapper _mapper;
    private readonly MongoConnection _connection;
    private readonly IOrganizationAdapter _organizationAdapter;

    public StripeService(
        ILogger<StripeService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        IMapper mapper,
        MongoConnection connection,
        IOrganizationAdapter organizationAdapter
    ) : base(logger, configuration, messageBroker)
    {
        _mapper = mapper;
        _connection = connection;
        _organizationAdapter = organizationAdapter;
    }

    protected override void Init(IMessageQueue messageQueue, TypeMapper mapper)
    {
        MessageBroker.Bind(messageQueue, ActionIds.GetRoute(ActionIds.AutoRefillBalance));
        mapper.Register<AutoRefillBalanceAction.Message>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        switch (evt.Body)
        {
            case AutoRefillBalanceAction.Message ar:
                await ProcessAutoRefillBalanceAsync(ar);
                break;
        }

        evt.Acknowledge();
    }

    private async Task ProcessAutoRefillBalanceAsync(AutoRefillBalanceAction.Message action)
    {
        using var scope = Logger.AddScope(new
        {
            action.Event.AccountId,
            EntityId = action.Event.TargetId
        });

        var result = await AutoRefillBalanceAsync(action);

        action.Event.Description = result.Status;
        action.Event.Action = nameof(ActionIds.AutoRefillBalance);

        if (result.IsUnknown)
        {
            Logger.LogInformation("Unknown result: {Status}", result.Status);
            await MessageBroker.DispatchAsync(action.Event, action.Options.DisabledEventId);
            return;
        }

        if (result.IsSuccess)
        {
            Logger.LogInformation("Successfully (auto) refilled account: {PaymentIntentId}", result.Value.Id);
        }
        else if (result.IsError)
        {
            Logger.LogError("Failed to refill account: {Message}", result.Status);

            if (action.Options.ErrorEventId.HasValue)
            {
                await MessageBroker.DispatchAsync(action.Event, action.Options.ErrorEventId);
                return;
            }
        }

        await MessageBroker.DispatchAsync(action.Event, action.Options.RefilledEventId, result.IsError);
    }

    private async Task<Result<PaymentIntent>> AutoRefillBalanceAsync(AutoRefillBalanceAction.Message action)
    {
        var accountConfig = await _connection.Filter<StripeSync>()
            .Eq(x => x.Id, action.Event.AccountId)
            .FirstOrDefaultAsync();
        if (accountConfig == null)
        {
            return Result<PaymentIntent>.Error("Account is not configured for Stripe");
        }

        var settings = await _connection.Filter<BillEntity>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, action.Event.TargetId)
            .FirstOrDefaultAsync();

        if (settings == null)
        {
            return Result<PaymentIntent>.Error("Entity is not configured for billing");
        }

        if (!settings.AutoRefill || !settings.MinBalance.HasValue)
        {
            return Result<PaymentIntent>.Unknown("Auto Refill is disabled for Entity");
        }

        if (settings.LastFailedAttemptOn.HasValue && settings.LastFailedAttemptOn.Value > DateTime.UtcNow.AddHours(-11))
        {
            return Result<PaymentIntent>.Unknown("Do not retry for 11 hours after a failed attempt.");
        }

        // look for stripe identity to get customerId
        var stripeProvider = ExternalProvider.Stripe.ToString();
        var entity = await _connection.Filter<Entity>()
            .Eq(x => x.AccountId, settings.AccountId)
            .Eq(x => x.Id, settings.Id)
            .ElemMatchBuilder(
                x => x.Identities,
                q => q.Eq(x => x.IdentityProviderId, stripeProvider)
            )
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            return Result<PaymentIntent>.Error("Couldn't find stripe identity");
        }

        var projectedBalance = settings.Balance + settings.PendingTotal.GetValueOrDefault(0);
        if (projectedBalance >= settings.MinBalance.Value)
        {
            return Result<PaymentIntent>.Unknown($"No need to refill, {projectedBalance.FormatCurrency()} >= {settings.MinBalance.Value.FormatCurrency()}");
        }

        var desiredBalance = !settings.MaxBalance.HasValue || settings.MaxBalance.Value < settings.MinBalance.Value ?
            settings.MinBalance.Value :
            settings.MaxBalance.Value;

        var payment = desiredBalance - projectedBalance;
        var description = $"Auto Refill Balance to {desiredBalance.FormatCurrency()}";
        var paymentIntent = await AddChargeUsingPaymentAsync(new AccountContext(settings.AccountId), entity, description, payment);

        return paymentIntent;
    }

    private async Task<StripeClient> GetClientAsync(IEntityContext context)
    {
        // use api key for account
        var config = await GetSyncConfigAsync(context);
        return new StripeClient(config.ApiKey);
    }

    public async Task<EntityIdentity> AddCardToOrganizationAsync(IEntityContext context, Organization organization, User user, string tokenId)
    {
        var client = await GetClientAsync(context);
        var customerService = new CustomerService(client);

        var stripeProviderId = ExternalProvider.Stripe.ToString();
        var stripe = organization.GetIdentities().FirstOrDefault(x => string.Equals(x.IdentityProviderId, stripeProviderId));
        var newCustomer = false;

        if (stripe == null)
        {
            var customer = await customerService.CreateAsync(new CustomerCreateOptions
            {
                Name = user.Name,
                Email = user.Email,
                Source = tokenId,
                Metadata = new Dictionary<string, string>
                {
                    { "AccountId", organization.AccountId.ToString() },
                    { "OrganizationId", organization.Id.ToString() },
                    { "UserId", user.Id.ToString() },
                    { "Organization", organization.Name }
                }
            });

            var result = await _organizationAdapter.AddAsync(context, organization.Id, new EntityIdentity
            {
                Id = Guid.NewGuid(),
                IdentityProviderId = stripeProviderId,
                ExternalId = customer.Id,
                Name = customer.Name,
                Data = new Dictionary<string, object>
                {
                    { "Email", customer.Email },
                    // {"Test", "true"} 
                }
            });

            newCustomer = true;
            stripe = result.Identity;
        }
        else
        {
            var service = new CardService(client);
            var card = await service.CreateAsync(stripe.ExternalId, new CardCreateOptions
            {
                Source = tokenId,
            });

            var updateResult = await customerService.UpdateAsync(stripe.ExternalId, new CustomerUpdateOptions
            {
                DefaultSource = card.Id,
            });
        }

        await MessageBroker.DispatchAsync(new EntityEvent(organization)
        {
            Actor = context.Actor(),
            Description = $"{user.Name} added Stripe credit card information to {organization.Name}",
            RefValues = new[]
            {
                new KeyValuePair<string, object>("EntityId", organization.Id),
                new KeyValuePair<string, object>("EntityId", user.Id),
            },
            MetaValues = new Dictionary<string, object>
            {
                { "StripeCustomerId", stripe.ExternalId },
                { "StripeIsNewCustomer", newCustomer },
            },
            EventTypeId =EventIds.OnPaymentMethodAdded, 
        });

        return stripe;
    }

    private Task<StripeSync> GetSyncConfigAsync(IEntityContext context)
    {
        // limit access?
        // ...
        return GetSyncConfigAsync(context.AccountId.Value);
    }

    public async Task<StripeSync> GetSyncConfigAsync(Guid id)
    {
        var row = await _connection.Filter<StripeSync>()
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (row == null)
        {
            Logger.LogCritical("Couldn't find Stripe config for {accountId}", id);
            throw new Exception("Couldn't find config for account");
        }

        return row;
    }

    [Obsolete("Use AddChargeUsingPayment... ")]
    private async Task<Result<Charge>> AddChargeAsync(IEntityContext context, IEntity entity, string description, decimal value)
    {
        using var scope = Logger.AddScope(new
        {
            Total = value,
            EntityId = entity.Id,
            context.AccountId,
        });

        Logger.LogInformation("Add charge to account");

        try
        {
            var identity = entity.FirstIdentity(ExternalProvider.Stripe.ToString());
            if (identity == null) return Result<Charge>.Error($"No Stripe Identity found for {entity.Id}");

            // limit to one attempt every 10 minutes
            var lastChargeOn = DateTime.UtcNow;
            var settings = await _connection.Filter<BillEntity>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.Id, entity.Id)
                .OrBuilder(
                    q => q.Eq(x => x.LastChargeOn, null),
                    q => q.Lt(x => x.LastChargeOn, lastChargeOn.AddMinutes(-10))
                )
                .Update
                .Set(x => x.LastChargeOn, lastChargeOn)
                .Unset(x => x.LastFailedAttemptOn)
                .UpdateAndGetOneAsync();

            if (settings == null) return Result<Charge>.Error("Couldn't put hold on account");

            var chargeOptions = new ChargeCreateOptions
            {
                Amount = (long)(value * 100),
                Currency = "usd",
                Customer = identity.ExternalId,
                Description = description,
                Metadata = new Dictionary<string, string>
                {
                    { "EntityId", entity.Id.ToString() },
                    { "Name", entity.Name },
                    { "AccountId", entity.AccountId.ToString() }
                }
            };

            var client = await GetClientAsync(context);
            var chargeService = new ChargeService(client);
            var charge = await chargeService.CreateAsync(chargeOptions);

            Logger.LogInformation("Payment of {value} added to {customerId}: {chargeId}", value, identity.ExternalId, charge.Id);

            return Result.Success(charge, $"Initiated Stripe Charge of {value.FormatCurrency()} to refill balance: {charge.Id}");
        }
        catch (StripeException ex)
        {
            switch (ex.StripeError.Code)
            {
                case "card_declined":
                    Logger.LogError("Credit card declined: {errorMessage} {declinedCode}", ex.StripeError.Message, ex.StripeError.DeclineCode);
                    await _connection.Filter<BillEntity>()
                        .Eq(x => x.AccountId, context.AccountId)
                        .Eq(x => x.Id, entity.Id)
                        .Update.Set(x => x.LastFailedAttemptOn, DateTime.UtcNow)
                        .UpdateOneAsync();
                    break;

                default:
                    Logger.LogError(ex, "Failed to add charge: {errorMessage} {errorCode}", ex.StripeError.Message, ex.StripeError.Code);
                    break;
            }

            return Result<Charge>.Error(ex.StripeError.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to add charge");
            return Result<Charge>.Error("Unexpected");
        }
    }

    public async Task<bool> SyncCustomers(IEntityContext context)
    {
        var config = await GetSyncConfigAsync(context);
        var job = await _connection.Filter<StripeSync>()
            .Eq(x => x.Id, context.AccountId.Value)
            .Update
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .Set(x => x.Customer.LastStart, DateTime.UtcNow)
            .Set(x => x.Customer.LastError, null)
            .Set(x => x.Customer.LastSuccessEnd, null)
            .UpdateAndGetOneAsync();

        var startingAfter = job.Customer.LastSyncedId;

        while (true)
        {
            var page = await GetCustomersAsync(job.ApiKey, startingAfter);
            await StoreAsync(context, page.Items, page.LastId);

            startingAfter = page.LastId;
            if (!page.HasMore) break;
        }

        await _connection.Filter<StripeSync>()
            .Eq(x => x.Id, context.AccountId.Value)
            .Update
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .Set(x => x.Customer.LastSyncedId, startingAfter)
            .Set(x => x.Customer.LastSuccessEnd, DateTime.UtcNow)
            .UpdateOneAsync();

        Logger.LogInformation("Finished Customer sync with {customerId}", startingAfter);

        return true;
    }

    public async Task<string> GetPortalUrlAsync(IEntityContext context, string returnUrl)
    {
        switch (context.Role)
        {
            case EntityRoleId.Organization:
            case EntityRoleId.Manager:
                break;
            default:
                return null;
        }

        var organization = await _organizationAdapter.GetByIdAsync(context, context.OrganizationId.Value);
        var identity = organization?.FirstIdentity(ExternalProvider.Stripe.ToString());
        if (identity == null) return null;

        var client = await GetClientAsync(context);
        var service = new SessionService(client);
        var session = await service.CreateAsync(new SessionCreateOptions
        {
            Customer = identity.ExternalId,
            ReturnUrl = returnUrl,
        });

        return session.Url;
    }

    private async Task<(IEnumerable<EntityIdentity> Items, bool HasMore, string LastId)> GetCustomersAsync(
        string apiKey,
        string startingAfter = null,
        int pageSize = 100)
    {
        Logger.LogInformation("Getting next page starting with {CustomerId}", startingAfter);

        var service = new CustomerService(new StripeClient(apiKey));
        var customers = await service.ListAsync(new CustomerListOptions
        {
            Limit = pageSize,
            StartingAfter = startingAfter
        });

        var result = customers.Select(x => Map(x));
        var lastId = customers.Data.Count > 0 ? customers.Data[^1].Id : startingAfter;
        return (result, customers.HasMore, lastId);
    }

    internal async Task<bool> UpsertAsync(IEntityContext context, Charge charge)
    {
        var model = Map(context, charge);

        var result = await _connection.Filter<StripeCharge>()
            .Eq(x => x.ExternalId, charge.Id)
            .Update
            .SetOnInsert(x => x.Id, model.Id)
            .SetOnInsert(x => x.AccountId, model.AccountId)
            .SetOnInsert(x => x.Name, model.Name)
            .SetOnInsert(x => x.CreatedOn, model.CreatedOn)
            .Set(x => x.Details, model.Details)
            .Set(x => x.LastModifiedOn, model.LastModifiedOn)
            .Set(x => x.LastActor, model.LastActor)
            .UpdateAndGetOneAsync(true);

        if (result == null)
        {
            Logger.LogError("Failed to upsert {StripeId}", charge.Id);
            return false;
        }

        await DispatchTransactionAsync(context, result);

        return true;
    }

    private async Task DispatchTransactionAsync(IEntityContext context, StripeCharge charge)
    {
        var status = Enum.TryParse(typeof(PaymentStatus), charge.Details.Status, true, out var objStatus) && objStatus != null ?
            (PaymentStatus)objStatus :
            PaymentStatus.Unknown;

        // TODO: should look for any entity (not just organization)
        // but as now the same customerId is associated with user and organization
        var organization = await _organizationAdapter.FindAsync(context, ExternalProvider.Stripe, charge.Details.CustomerId);
        if (organization.Entity == null)
        {
            Logger.LogWarning("Didn't find organization to add payment to for {StripeCustomerId}: {StripeChargeId}", charge.Details.CustomerId, charge.Id);
            return;
        }

        var eventId = status switch
        {
            PaymentStatus.Failed => EventIds.OnFailedPayment,
            PaymentStatus.Pending => EventIds.OnPendingPayment,
            PaymentStatus.Succeeded => EventIds.OnPayment,
            _ => throw new Exception($"Failed to parse type: {charge.Details.Status}")
        };

        var evt = new PaymentStatusUpdateEvent(organization.Entity)
        {
            Description = $"Stripe charge status updated: {status}",
            Status = status,
            Actor = context.Actor(),
            Payment = new Payment
            {
                Id = Guid.NewGuid(),
                AccountId = context.AccountId.Value,
                OrganizationId = organization.Entity.Id,
                EntityId = organization.Entity.Id,
                Name = charge.Details.Outcome?.SellerMessage,
                Description = charge.Details.Description,
                Total = charge.Details.Amount != 0 ? (decimal)charge.Details.Amount / 100 : null,
                CreatedOn = DateTime.UtcNow,
                ReferenceDate = charge.Details.Created,
                LastActor = context.Actor(),
                Balance = null,
                Source = "Stripe",
                ExternalId = charge.Details.Id,
                ExternalUrl = charge.Details.ReceiptUrl,
            },
            EventTypeId = eventId,
        };
        
        await MessageBroker.DispatchAsync(evt);
    }

    public async Task<bool> UpsertAsync(IEntityContext context, Customer customer)
    {
        var model = Map(customer);

        var result = await _connection.Filter<StripeCustomer>()
            .Eq(x => x.Id, model.ExternalId)
            .Update
            .SetOnInsert(x => x.AccountId, context.AccountId.Value)
            .SetOnInsert(x => x.Identity, model)
            .SetOnInsert(x => x.Name, model.Name)
            .SetOnInsert(x => x.CreatedOn, DateTime.UtcNow)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .UpdateOneAsync(true);

        if (result.MatchedCount == 1)
        {
            Logger.LogInformation("Updated {stripeCustomerId}", customer.Id);
        }
        else if (result.UpsertedId != null)
        {
            Logger.LogInformation("Upserted {stripeCustomerId} with {customerId}", customer.Id, model.Id);
        }
        else
        {
            Logger.LogError("Failed to upsert {stripeCustomerId}", customer.Id);
            return false;
        }

        return true;
    }

    private async Task<(IEnumerable<Invoice> Items, bool HasMore)> GetInvoicesAsync(string startingAfter = null, int pageSize = 100)
    {
        var service = new InvoiceService();
        var invoices = await service.ListAsync(new InvoiceListOptions
        {
            Limit = pageSize,
            StartingAfter = startingAfter
        });

        return (invoices.Data, invoices.HasMore);
    }

    public async Task<bool> SyncCharges(IEntityContext context)
    {
        var config = await GetSyncConfigAsync(context);
        var job = await _connection.Filter<StripeSync>()
            .Eq(x => x.Id, context.AccountId.Value)
            .Update
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .Set(x => x.Charges.LastStart, DateTime.UtcNow)
            .Set(x => x.Charges.LastError, null)
            .Set(x => x.Charges.LastSuccessEnd, null)
            .UpdateAndGetOneAsync();

        var startingAfter = job.Charges.LastSyncedId;

        while (true)
        {
            var page = await GetChargesAsync(config.ApiKey, startingAfter);

            await StoreAsync(context, page.Items, page.lastId);
            startingAfter = page.lastId;

            if (!page.HasMore) break;
        }

        await _connection.Filter<StripeSync>()
            .Eq(x => x.Id, context.AccountId.Value)
            .Update
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .Set(x => x.Charges.LastSyncedId, startingAfter)
            .Set(x => x.Charges.LastSuccessEnd, DateTime.UtcNow)
            .UpdateOneAsync();

        Logger.LogInformation("Finished Charges sync with {chargeId}", startingAfter);

        return true;
    }

    private async Task<(List<Charge> Items, bool HasMore, string lastId)> GetChargesAsync(
        string apiKey,
        string startingAfter = null,
        int pageSize = 100)
    {
        Logger.LogInformation("Getting next page starting with {chargeId}", startingAfter);

        var service = new ChargeService(new StripeClient(apiKey));
        var result = await service.ListAsync(new ChargeListOptions
        {
            Limit = pageSize,
            StartingAfter = startingAfter
        });

        var lastId = result.Data.Count > 0 ? result.Data[result.Data.Count - 1].Id : startingAfter;

        return (result.Data, result.HasMore, lastId);
    }

    private async Task StoreAsync(IEntityContext context, IEnumerable<Charge> items, string lastId)
    {
        Logger.LogInformation("Store page ending with {chargeId}", lastId);

        // var rows = items.Select(x => _connection.Filter<StripeCharge>()
        //     .Eq(x => x.ExternalId, x.Id)
        //     .ReplaceOneModel(Map(context, x), true)
        // );

        var updates = items
            .Select(x => Map(context, x))
            .Select(x => _connection.Filter<StripeCharge>()
                .Eq(x => x.ExternalId, x.ExternalId)
                .Update
                .Set(x => x.Details, x.Details)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, context.Actor())
                .SetOnInsert(x => x.Id, x.Id)
                .SetOnInsert(x => x.AccountId, context.AccountId.Value)
                .SetOnInsert(x => x.Name, x.Name)
                .SetOnInsert(x => x.CreatedOn, x.CreatedOn)
                .UpdateOneModel(true)
            );

        var result = await _connection.BulkWriteAsync(updates);

        Logger.LogInformation("Upserted charges: {Upserts} + {ModifiedCount} = {Total}", result.Upserts.Count, result.ModifiedCount, result.ProcessedRequests.Count);

        await _connection.Filter<StripeSync>()
            .Eq(x => x.Id, context.AccountId.Value)
            .Update
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .Set(x => x.Charges.LastSyncedId, lastId)
            .UpdateOneAsync();
    }

    public async Task<bool> SyncInvoices(IEntityContext context, string startingAfter = null)
    {
        for (var c = 0; c < 10; c++)
        {
            var page = await GetInvoicesAsync(startingAfter);
            var items = page.Items.ToArray();

            await StoreAsync(context, items);
            if (!page.HasMore) return false;

            startingAfter = items[items.Length - 1].Id;
        }

        return true;
    }

    private async Task StoreAsync(IEntityContext context, IEnumerable<Invoice> items)
    {
        var rows = items.Select(x => _connection.Filter<StripeInvoice>().InsertOneModel(Map(context, x)));
        var result = await _connection.BulkWriteAsync(rows);
    }

    private async Task StoreAsync(IEntityContext context, IEnumerable<EntityIdentity> identities, string lastId)
    {
        Logger.LogInformation("Store page ending with {customerId}", lastId);

        var updates = identities.Select(s => _connection.Filter<StripeCustomer>()
            .Eq(x => x.Id, s.ExternalId)
            .Update
            .SetOnInsert(x => x.AccountId, context.AccountId.Value)
            .SetOnInsert(x => x.Identity, s)
            .SetOnInsert(x => x.Name, s.Name)
            .SetOnInsert(x => x.CreatedOn, DateTime.UtcNow)
            // avoid updating
            .SetOnInsert(x => x.LastModifiedOn, DateTime.UtcNow)
            .SetOnInsert(x => x.LastActor, context.Actor())
            .UpdateOneModel(true)
        );

        var result = await _connection.BulkWriteAsync(updates);
        Logger.LogInformation("Upserted customers: {InsertedCount} + {ModifiedCount}", result.ModifiedCount, result.InsertedCount);

        if (result.MatchedCount + result.InsertedCount != updates.Count())
        {
            Logger.LogError("couldn't upsert all");
        }

        await _connection.Filter<StripeSync>()
            .Eq(x => x.Id, context.AccountId.Value)
            .Update
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .Set(x => x.Customer.LastSyncedId, lastId)
            .UpdateOneAsync();
    }

    // https://stripe.com/docs/payments/payment-intents/migration?charges-cards-migration=saving-cards&client=javascript_esnext#web
    public async Task<string> InitiateSetupAsync(IEntityContext context)
    {
        var client = await GetClientAsync(context);
        var service = new SetupIntentService(client);
        var setupIntent = await service.CreateAsync(null);

        Logger.LogInformation("Created intent: {Id}", setupIntent.Id);
        return setupIntent.ClientSecret;
    }

    private async Task<StripeList<PaymentMethod>> GetPaymentMethodsAsync(Entity organization)
    {
        Logger.LogInformation("Get Payment Methods for {EntityId}", organization.Id);

        var identity = organization.FirstIdentity(ExternalProvider.Stripe.ToString());
        if (identity == null) throw NotFoundException.New($"No Stripe Identity found for {organization.Id}");

        var client = await GetClientAsync(organization.Context);
        var service = new PaymentMethodService(client);
        var options = new PaymentMethodListOptions
        {
            Customer = identity.ExternalId,
            // Limit = 3,
            Type = "card"
        };

        var paymentMethods = await service.ListAsync(options);
        return paymentMethods;
    }

    /// <summary>
    /// Use new payment intents api to add charge
    /// </summary>
    public async Task<Result<PaymentIntent>> AddChargeUsingPaymentAsync(IEntityContext context, Entity entity, string description, decimal value)
    {
        using var scope = Logger.AddScope(new
        {
            Total = value,
            EntityId = entity.Id,
            context.AccountId,
        });

        Logger.LogInformation("Add charge to account");

        try
        {
            var payments = await GetPaymentMethodsAsync(entity);
            var payment = payments
                .Where(x => x.Card != null && new DateTime((int)x.Card.ExpYear, (int)x.Card.ExpMonth, 1) > DateTime.UtcNow)
                .MaxBy(x => x.Created);

            if (payment == null) return Result<PaymentIntent>.Error("No payment method found");

            var identity = entity.FirstIdentity(ExternalProvider.Stripe.ToString());

            // limit to one attempt every 10 minutes
            var lastChargeOn = DateTime.UtcNow;
            var settings = await _connection.Filter<BillEntity>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.Id, entity.Id)
                .OrBuilder(
                    q => q.Eq(x => x.LastChargeOn, null),
                    q => q.Lt(x => x.LastChargeOn, lastChargeOn.AddMinutes(-10))
                )
                .Update
                .Set(x => x.LastChargeOn, lastChargeOn)
                .Unset(x => x.LastFailedAttemptOn)
                .UpdateAndGetOneAsync();

            if (settings == null) return Result<PaymentIntent>.Unknown("Couldn't put hold on account");

            var client = await GetClientAsync(context);

            var service = new PaymentIntentService(client);
            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(value * 100),
                Currency = "usd",
                Customer = identity.ExternalId,
                Description = description,
                PaymentMethod = payment.Id,
                Confirm = true,
                OffSession = true,
                Metadata = new Dictionary<string, string>
                {
                    { "EntityId", entity.Id.ToString() },
                    { "Name", entity.Name },
                    { "AccountId", entity.AccountId.ToString() }
                }
            };

            var intent = await service.CreateAsync(options);

            Logger.LogInformation("Payment of {Value} added to {CustomerId}: {PaymentIntentId}", value, identity.ExternalId, intent.Id);

            return Result.Success(intent, $"Initiated Stripe Charge of {value.FormatCurrency()} to refill balance: {intent.Id}");
        }
        catch (StripeException ex)
        {
            switch (ex.StripeError.Code)
            {
                case "card_declined":
                    Logger.LogError("Credit card declined: {ErrorMessage} {DeclinedCode}", ex.StripeError.Message, ex.StripeError.DeclineCode);
                    await _connection.Filter<BillEntity>()
                        .Eq(x => x.AccountId, context.AccountId)
                        .Eq(x => x.Id, entity.Id)
                        .Update.Set(x => x.LastFailedAttemptOn, DateTime.UtcNow)
                        .UpdateOneAsync();
                    break;

                default:
                    Logger.LogError(ex, "Failed to add charge: {ErrorMessage} {ErrorCode}", ex.StripeError.Message, ex.StripeError.Code);
                    break;
            }

            return Result<PaymentIntent>.Error(ex.StripeError.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to add charge");
            return Result<PaymentIntent>.Error("Unexpected");
        }
    }

    private StripeCharge Map(IEntityContext context, Charge source)
    {
        var target = new StripeCharge
        {
            AccountId = context.AccountId.Value,
            Id = Guid.NewGuid(),
            Details = _mapper.Map<StripeChargeDetails>(source),
            CreatedOn = DateTime.UtcNow,

            LastActor = context.Actor(),
            LastModifiedOn = DateTime.UtcNow,
        };

        return target;
    }

    private StripeInvoice Map(IEntityContext context, Invoice source)
    {
        var target = _mapper.Map<StripeInvoice>(source);
        target.AccountId = context.AccountId.Value;
        target.LastActor = context.Actor();

        return target;
    }

    private EntityIdentity Map(Customer source)
    {
        var identity = new EntityIdentity
        {
            Id = Guid.NewGuid(),
            ExternalId = source.Id,
            Name = source.Name,
            IdentityProviderId = ExternalProvider.Stripe.ToString(),
            ExternalIdentity = null, // ????
            Data = getProperties()
        };

        return identity;

        Dictionary<string, object> getProperties()
        {
            var properties = new Dictionary<string, object>
            {
                { nameof(Customer.Phone), source.Phone },
                { nameof(Customer.Email), source.Email },
                { nameof(Customer.Description), source.Description },
                { nameof(Customer.Currency), source.Currency },
                { nameof(Address.Line1), source.Address?.Line1 },
                { nameof(Address.Line2), source.Address?.Line2 },
                { nameof(Address.City), source.Address?.City },
                { nameof(Address.State), source.Address?.State },
                { nameof(Address.PostalCode), source.Address?.PostalCode },
                { nameof(Address.Country), source.Address?.Country },
            };

            return new Dictionary<string, object>(properties.Where(x => x.Value != null));
        }
    }
}

public class InvoiceProfile : Profile
{
    public InvoiceProfile()
    {
        CreateMap<Invoice, StripeInvoice>(MemberList.Destination)
            .ForMember(x => x.AccountId, o => o.Ignore())
            .ForMember(x => x.LastActor, o => o.Ignore())
            .ForMember(x => x.LastModifiedOn, o => o.Ignore())
            .ForMember(x => x.CreatedOn, o => o.MapFrom(s => s.Created))
            .ForMember(x => x.Name, o => o.MapFrom(s => s.CustomerName));
    }
}

public class ChargeProfile : Profile
{
    public ChargeProfile()
    {
        CreateMap<ChargeOutcome, StripeChargeOutcome>(MemberList.Destination);

        CreateMap<Charge, StripeChargeDetails>(MemberList.Destination)
            .ForMember(x => x.OrderId, o => o.Ignore())
            .ForMember(x => x.DestinationId, o => o.Ignore())
            .ForMember(x => x.DisputeId, o => o.Ignore())
            ;
    }
}