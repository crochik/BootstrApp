# PI.Shared.Integrations

Shared building blocks for **outbound "REST Hook" integrations** (Zapier, n8n, …):

- **Catalog** (`IObjectCatalog`) — triggerable objects/events discovered from the account's
  real `ObjectType` definitions.
- **Subscriptions** (`ISubscriptionStore`, `IntegrationSubscription`) — Mongo-persisted
  REST-Hook registrations, one collection per integration.
- **Delivery** — a durable, signed pipeline: `WebhookEventListener` → Mongo outbox
  (`WebhookEvent`/`WebhookDelivery`) → `WebhookDeliveryWorker` (HMAC-signed HTTP POST) →
  `WebhookOutboxReconciler` (retries).

Consumed by [`src/Zapier`](../Zapier/README.md) and [`src/N8n`](../N8n/README.md), which add
only their platform-shaped controllers.

## Publishing a webhook event to Zapier

In almost every case **you don't call anything integration-specific** — you fire the
platform's standard object lifecycle event, and the Zapier service's `WebhookEventListener`
(bound to `object.#`) matches subscriptions, flattens the object with the right RBAC, and
delivers a signed POST. The same event drives n8n; nothing in your code knows about either.

### Recommended — fire a standard object event

From any service that creates/updates/deletes an object, dispatch a `GenericFlowEvent` on the
object's routing key:

```csharp
using Crochik.Messaging;   // IMessageBroker
using Messages.Flow;       // GenericFlowEvent, DispatchAsync(...)
using PI.Shared.Models;    // FlowObjectEventRoute, IFlowObject

public class LeadNotifier
{
    private readonly IMessageBroker _broker;

    public LeadNotifier(IMessageBroker broker) => _broker = broker;

    // Call after the lead has been saved.
    public Task NotifyLeadUpdatedAsync(Lead lead)
    {
        // GenericFlowEvent captures AccountId, ObjectType and TargetId from the object.
        var evt = new GenericFlowEvent(lead);

        // Routing key -> object.{ObjectType}.{id}.update
        // The Zapier (and n8n) WebhookEventListener is bound to "object.#" and picks this up:
        // it resolves matching subscriptions, builds the per-subscriber payload, and delivers.
        return _broker.DispatchAsync(evt, FlowObjectEventRoute.Update.GetRoute(lead));
    }
}
```

That's it — a Zap subscribed to *Lead → Updated* now receives the delivery. Use
`FlowObjectEventRoute.Create` / `.Update` / `.Delete` for the respective lifecycle events.
`ObjectTypeService` already fires these on the normal create/update paths, so most code gets
Zapier delivery for free.

### Direct — publish an explicit payload

When you need to deliver a custom payload (not a stored object) to the subscriptions for an
object/event, resolve the targets and publish through `IEventPublisher`. This stores the
event plus one delivery per subscription and enqueues the signed, retried POSTs:

```csharp
using PI.Shared.Integrations.Delivery;       // IEventPublisher, WebhookEventData
using PI.Shared.Integrations.Subscriptions;  // ISubscriptionStore

public class CustomLeadTrigger
{
    private readonly ISubscriptionStore _subscriptions;
    private readonly IEventPublisher _publisher;

    public CustomLeadTrigger(ISubscriptionStore subscriptions, IEventPublisher publisher)
    {
        _subscriptions = subscriptions;
        _publisher = publisher;
    }

    /// <returns>The number of per-subscription deliveries enqueued.</returns>
    public async Task<int> PublishAsync(
        Guid accountId,
        Guid objectEntityId,          // the object's owning entity (scopes org-narrowed subs)
        string objectKey,             // e.g. "Lead"
        string eventKey,              // "Create" | "Update" | "Delete"
        IDictionary<string, object> payload)
    {
        // Find the subscriptions (in this integration's collection) that want this event.
        var targets = await _subscriptions.FindForDeliveryAsync(accountId, objectKey, eventKey, objectEntityId);
        if (targets.Count == 0)
        {
            return 0;
        }

        return await _publisher.PublishAsync(
            new WebhookEventData(accountId, objectKey, eventKey, payload),
            targets);
    }
}
```

> `eventKey` uses the catalog's lifecycle keys (`Create`/`Update`/`Delete`). Inject
> `ISubscriptionStore`/`IEventPublisher` (registered by
> `AddIntegrationServices<TSubscription>(Configuration)`); they resolve to the host
> service's own subscription collection.

### What the subscriber receives

Either path delivers the same signed envelope (`Webhook-Signature: t=…,v1=…` over
`"{t}.{rawBody}"`, plus `Webhook-Id`/`Webhook-Event`/`Webhook-Timestamp`):

```json
{
  "eventId": "…",
  "tenantId": "<accountId>",
  "eventName": "<object>.<event>",
  "occurredAt": "2026-01-15T09:30:00.0000000Z",
  "schemaVersion": "1",
  "data": { "…the flattened object…": "…" }
}
```
