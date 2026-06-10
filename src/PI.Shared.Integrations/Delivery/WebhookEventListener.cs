using System.Dynamic;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.Shared.App;
using PI.Shared.Extensions;
using PI.Shared.Integrations.Subscriptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace PI.Shared.Integrations.Delivery;

/// <summary>
/// Generic outbound trigger. Listens to <c>object.#</c> — every object lifecycle event
/// for every object type — and, for each, finds the matching REST Hook subscriptions,
/// builds the per-subscriber (RBAC-flattened) payload and hands it to the
/// <see cref="IEventPublisher"/> for durable, signed delivery. There is no per-type
/// special casing: a subscription on any object type just works.
/// </summary>
public sealed class WebhookEventListener : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypes;
    private readonly ISubscriptionStore _store;
    private readonly IEventPublisher _publisher;

    public WebhookEventListener(
        ILogger<WebhookEventListener> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        MongoConnection connection,
        ObjectTypeService objectTypes,
        ISubscriptionStore store,
        IEventPublisher publisher)
        : base(logger, configuration, messageBroker)
    {
        _connection = connection;
        _objectTypes = objectTypes;
        _store = store;
        _publisher = publisher;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        // Every object lifecycle event for every object type: object.{type}.{id}.{action}
        MessageBroker.Bind(queue, "object.#");
        mapper.Register<GenericFlowEvent>();
    }

    protected override async Task OnMessageAsync(IMessage message)
    {
        try
        {
            if (message.Body is GenericFlowEvent evt && message.RoutingKey.StartsWith("object."))
            {
                await HandleAsync(message.RoutingKey, evt);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process object event {RoutingKey}", message.RoutingKey);
        }

        message.Acknowledge();
    }

    private async Task HandleAsync(string routingKey, GenericFlowEvent evt)
    {
        var eventKey = MapAction(routingKey);
        if (eventKey is null || string.IsNullOrEmpty(evt.ObjectType)) return;

        var accountContext = new IntegrationAccountContext(evt.AccountId);

        var objectType = await _objectTypes.GetAsync(accountContext, evt.ObjectType);
        if (objectType is null) return;

        // The object's owning entity scopes org-narrowed subscriptions.
        var doc = await _connection.Filter<ExpandoObject>(objectType.CollectionName, objectType.DatabaseName)
            .Eq(Model.IdFieldName, evt.TargetId)
            .FirstOrDefaultAsync();
        if (doc is null) return;

        var objectEntityId = ((IDictionary<string, object>)doc).TryGetGuidParam("EntityId", out var entityId)
            ? entityId
            : Guid.Empty;

        var subscriptions = await _store.FindForDeliveryAsync(evt.AccountId, evt.ObjectType, eventKey, objectEntityId);
        if (subscriptions.Count == 0) return;

        // Group by profile so the RBAC-flattened payload is built once per distinct view.
        foreach (var group in subscriptions.GroupBy(s => s.ProfileId))
        {
            var sample = group.First();
            var profileContext = ProfileContext.Create(sample.ProfileId, evt.AccountId, sample.EntityId, sample.ClientId, objectEntityId);

            var flat = await _objectTypes.GetFlatObjectAsync(profileContext, objectType, evt.TargetId);
            if (flat is null) continue;

            var count = await _publisher.PublishAsync(
                new WebhookEventData(evt.AccountId, evt.ObjectType, eventKey, flat),
                group.ToList());

            Logger.LogInformation("Published {ObjectType}/{Event} to {Count} subscription(s) for profile {ProfileId}",
                evt.ObjectType, eventKey, count, sample.ProfileId);
        }
    }

    /// <summary>Maps the routing-key action segment to a lifecycle key (Create/Update/Delete).</summary>
    private static string MapAction(string routingKey)
    {
        var action = routingKey.Split('.').LastOrDefault();
        return action?.ToLowerInvariant() switch
        {
            "create" => nameof(FlowObjectEventRoute.Create),
            "update" => nameof(FlowObjectEventRoute.Update),
            "delete" => nameof(FlowObjectEventRoute.Delete),
            _ => null,
        };
    }
}

/// <summary>
/// Minimal account-scoped context for the listener, which has no request. Admin role
/// within the event's account — enough to resolve the object type and read the object.
/// </summary>
internal sealed class IntegrationAccountContext : IEntityContext
{
    public IntegrationAccountContext(Guid accountId)
    {
        AccountId = accountId;
    }

    public EntityRoleId Role => EntityRoleId.Admin;
    public Guid? UserId => null;
    public Guid? OrganizationId => null;
    public Guid? AccountId { get; }
    public Guid? ProfileId => null;
    public Guid[] AllProfileIds => Array.Empty<Guid>();
    public string ClientId => null;
    public Guid? EntityId => AccountId;
    public IEnumerable<Guid> GetEntityIds() => AccountId.HasValue ? new[] { AccountId.Value } : Enumerable.Empty<Guid>();
    public IReadOnlyDictionary<string, string[]> Claims => null;
}
