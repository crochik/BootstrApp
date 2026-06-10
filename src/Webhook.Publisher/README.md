# Webhook.Publisher

Outbound webhook delivery for multi-tenant systems. Application code publishes an
event; the library stores it, fans it out to each matching subscriber, and delivers
a **signed** HTTP POST with durable, exponentially-backed-off retries.

This is the *outbound* counterpart to the inbound `Webhook.Service` receiver in this
repository.

## Architecture

```
app ──PublishAsync──▶ MongoDB (event + per-delivery docs)
                  └──▶ RabbitMQ topic exchange  webhook.{tenant}.{event}  (body = deliveryId only)
                                 │
                       webhook.delivery.q ──▶ WebhookDeliveryWorker ──HTTP POST (signed)──▶ subscriber
                                 ▲                    │ retryable failure
                                 │                    ▼
                                 │        webhook.retry (direct) ──▶ webhook.retry.{tier}.q  (x-message-ttl)
                                 └──────────  TTL expiry dead-letters back  ◀──────────────┘
```

- **MongoDB is the source of truth.** Event payloads and per-delivery status/attempt
  history live there; RabbitMQ only ever carries a `deliveryId`.
- **Status is per delivery** (one document per *event × subscription*), so one
  subscriber can succeed while another retries.
- **Retries use the plugin-free "wait room" pattern**, realized as one fixed-TTL delay
  queue per backoff tier (avoids the head-of-line blocking of a single per-message-TTL
  queue). Default schedule spans ~24h: `10s, 30s, 1m, 5m, 15m, 30m, 1h, 3h, 6h, 12h, 24h`,
  bounded by `MaxRetryWindow`. After the last tier (or the window) a delivery is `Dead`.
- **Signing** is Stripe-style `t=...,v1=...` (HMAC-SHA256 over `"{unixSeconds}.{body}"`),
  which the inbound `SignedTimestampValidator("stripe")` verifies as-is.

## Quick start

```csharp
builder.Services
    .AddWebhookDeliveryWorker(builder.Configuration)
    .AddWebhookSubscriptionStore<JsonFileWebhookSubscriptionStore>();
```

Then publish:

```csharp
await publisher.PublishAsync("tenant-123", "order.created", new { orderId = 7 });
```

`AddWebhookPublisher` alone wires just the publish side (no worker). Replace the
subscription store with any `IWebhookSubscriptionStore` (e.g. database-backed) via
`AddWebhookSubscriptionStore<T>()`.

## Configuration

`WebhookPublisher` section (see `Configuration/*Options.cs` for all fields):

```json
{
  "WebhookPublisher": {
    "RabbitMq": { "HostName": "localhost", "ExchangePrefix": "webhook" },
    "Mongo":    { "ConnectionString": "mongodb://localhost:27017", "Database": "webhooks" },
    "Retry":    { "MaxRetryWindow": "1.00:00:00" },
    "Delivery": { "ConsumerCount": 3, "PrefetchCount": 20, "HttpTimeout": "00:00:10" }
  },
  "WebhookSubscriptions": {
    "Subscriptions": [
      {
        "Id": "s1",
        "TenantId": "tenant-123",
        "Url": "https://customer.example/webhooks",
        "Secret": "whsec_…",
        "Events": ["*"],
        "SignatureHeader": "Webhook-Signature"
      }
    ]
  }
}
```

## Delivered request

```
POST {subscription.Url}
Content-Type: application/json
Webhook-Signature: t=1700000000,v1=<hex>
Webhook-Id: <eventId>
Webhook-Event: order.created
Webhook-Timestamp: 1700000000

{ "eventId": "...", "tenantId": "tenant-123", "eventName": "order.created",
  "occurredAt": "2026-…Z", "schemaVersion": "1", "data": { "orderId": 7 } }
```

Verify: recompute `HMAC_SHA256(secret, "{t}.{rawBody}")` and compare to `v1`; reject if
`t` is outside your tolerance window. The `Webhook-Id` is a stable idempotency key.

## Reliability notes

- One long-lived auto-recovering connection; a pool of publisher-confirm channels.
  `PublishAsync` awaits the broker ack.
- Topology is declared once at startup (`WebhookTopologyHostedService`).
- An at-least-once **outbox reconciler** re-enqueues deliveries that should be in flight
  but are not (publisher crash before enqueue, lost comeback, worker crash mid-attempt).
  The worker's atomic claim (`TryMarkDeliveringAsync`) makes redundant re-enqueues no-ops.
- **Noisy-neighbor:** baseline is bounded prefetch + multiple consumers. For stronger
  isolation, bind a dedicated queue for a premium tenant (`webhook.{tenant}.#`); the
  `WebhookTopologyNames` helpers make this a config-only change. Note retried messages
  carry a `retry.{tier}` key on the way back (tenant/event come from MongoDB).

## Tests

- Unit (no Docker): `dotnet test --filter Category!=Integration`
- Integration (Testcontainers, needs Docker): RabbitMQ + MongoDB end-to-end, including
  retry-then-succeed and exhaust-to-`Dead`.
