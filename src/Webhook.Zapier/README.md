# Webhook.Zapier

A small ASP.NET Core (.NET 8) service that exposes your domain's **objects and
events to Zapier** through a single, generic REST API. The set of objects and
events is **discovered at runtime** from decorated types — never hardcoded in the
controllers or in the Zapier app — so the same integration serves any domain and
new objects show up in Zapier the moment you add a class.

```
Zapier (user building a Zap)                 Webhook.Zapier
  │  pick connection (API key) ───────────▶  GET  /zapier/me
  │  pick Object  (dynamic dropdown) ─────▶  GET  /zapier/objects
  │  pick Event   (dynamic dropdown) ─────▶  GET  /zapier/objects/{object}/events
  │  turn Zap ON  (REST Hook subscribe) ──▶  POST /zapier/subscriptions  {object,event,targetUrl}
  │  test trigger (perform list) ─────────▶  GET  /zapier/objects/{object}/events/{event}/samples
  │  turn Zap OFF (REST Hook unsub) ──────▶  DELETE /zapier/subscriptions/{id}
  ▼
your domain emits an event ─▶ IEventPublisher ─▶ Webhook.Publisher (Mongo + RabbitMQ)
                                                 └─▶ signed, retried POST to every subscribed targetUrl
```

Outbound delivery runs through the companion [`Webhook.Publisher`](../Webhook.Publisher/README.md)
pipeline: published events are stored in MongoDB, fanned out per subscription and
delivered as **signed** HTTP POSTs over RabbitMQ with durable, exponentially-backed-off
retries — so a Zap that's briefly unreachable still gets its event.

See [`docs/ZAPIER_SETUP.md`](docs/ZAPIER_SETUP.md) for step-by-step instructions
on building the Zapier-side app (it can't be fully automated; the doc explains why
and gives a complete, copy-pasteable Zapier Platform CLI app).

## Prerequisites

Because delivery uses `Webhook.Publisher`, the service needs **MongoDB and RabbitMQ**
reachable at startup (the topology/index initializer runs on boot). Point at them via
the `WebhookPublisher` section of `appsettings.json`. For local dev:

```bash
docker run -d -p 27017:27017 mongo:7
docker run -d -p 5672:5672 rabbitmq:3
```

## Quick start

```bash
dotnet run --project src/Webhook.Zapier
# GET /health -> {"status":"ok"}
```

All `/zapier/*` routes require an API key (`X-Api-Key` or `Authorization: Bearer`).
The shipped demo key is `demo-secret-key` (see `appsettings.json`).

```bash
KEY="demo-secret-key"; BASE="http://localhost:5000"

# what objects exist? (discovered, not hardcoded)
curl -s -H "X-Api-Key: $KEY" $BASE/zapier/objects

# what events does "deal" emit?
curl -s -H "X-Api-Key: $KEY" $BASE/zapier/objects/deal/events

# subscribe a callback (Zapier does this for you; here we use a test bin)
curl -s -H "X-Api-Key: $KEY" -H "Content-Type: application/json" \
  -d '{"object":"deal","event":"won","targetUrl":"https://example.com/hook"}' \
  $BASE/zapier/subscriptions

# fire a demo event — published to the durable pipeline, delivered to subscribers
curl -s -H "X-Api-Key: $KEY" -H "Content-Type: application/json" \
  -d '{"object":"deal","event":"won"}' \
  $BASE/zapier/mock/emit
```

## How discovery works

Discovery, the subscription bridge, the API-key gate and durable delivery all live in
the shared [`Webhook.Integrations.Core`](../Webhook.Integrations.Core) library (also
used by [`Webhook.N8n`](../Webhook.N8n/README.md)). This project only adds the Zapier
API controllers.

Mark a POCO with `[TriggerObject]`; optionally declare `[TriggerEvent]`s. That is the
*entire* contribution needed for it to appear in Zapier (and n8n):

```csharp
[TriggerObject(Key = "deal", Label = "Deal", Description = "An opportunity in the pipeline.")]
[TriggerEvent("created")]
[TriggerEvent("won",  Label = "Deal Won")]
[TriggerEvent("lost", Label = "Deal Lost")]
public sealed class MockDeal
{
    public string Id { get; set; } = "";
    public decimal Amount { get; set; }
    public string Stage { get; set; } = "";
    // ...
}
```

- A type with **no** `[TriggerEvent]` gets the default `created` / `updated` /
  `deleted` lifecycle (see `MockContact`).
- `ReflectionEventCatalog` scans the configured assemblies once at startup and the
  controllers read from `IEventCatalog`. Adding a class needs **no** controller,
  DI, or Zapier-app change.
- `ReflectionSampleFactory` generates deterministic example payloads from each
  type's properties, which Zapier shows as sample data while a user builds a Zap.

The shared mock domain lives in the core assembly; expose your own objects by passing
their assemblies to `AddIntegrationCore(...)`.

## Emitting real events

Replace the mock emitter with a call from your domain code — pass just the object
body as `data`; the publisher adds the envelope:

```csharp
// IEventPublisher is the Zapier-friendly seam over Webhook.Publisher.
await _events.PublishAsync("deal", "won", deal);
```

`PublisherEventPublisher` translates `(object, event)` into the publisher's
`(tenant, "object.event")` and calls `IWebhookPublisher.PublishAsync`. The publisher
stores the event, fans out to every matching subscription and enqueues a durable,
signed delivery to each Zapier callback URL. Retries follow the publisher's backoff
schedule; a Zap turned off (subscriber gone) is handled by the pipeline's delivery
status, not by pruning here.

### Delivered payload

Each subscriber receives the `Webhook.Publisher` envelope (signed Stripe-style):

```json
{
  "eventId": "…",            // also the Webhook-Id header — Zapier de-dupes on it
  "tenantId": "zapier",
  "eventName": "deal.won",
  "occurredAt": "2026-01-15T09:30:00Z",
  "schemaVersion": "1",
  "data": { "id": "deal_1001", "amount": 19.99, "stage": "open" }
}
```

## Subscription mapping

A Zapier REST Hook on `(object, event)` is stored as a `Webhook.Publisher`
`WebhookSubscription` under one tenant (`Zapier:Tenant`, default `zapier`) with event
name `"{object}.{event}"`. The core `IntegrationSubscriptionStore` is the single bridge
instance exposed as both the `ISubscriptionStore` (add/remove) and the publisher's
read-only `IWebhookSubscriptionStore` (fan-out lookup).

## Project layout

```
src/Webhook.Zapier/
  Controllers/          Auth (/me), Catalog (objects/events/triggers), Subscriptions, Mock
  DependencyInjection/  AddZapierIntegration → AddIntegrationCore("Zapier", "/zapier")
  docs/                 ZAPIER_SETUP.md
tests/Webhook.Zapier.Tests/  xUnit endpoint tests (publisher faked, no infra)

src/Webhook.Integrations.Core/   shared discovery, subscription bridge, delivery, auth
```

## Extensibility

- **Durable subscriptions** — `IntegrationSubscriptionStore` (in core) keeps
  subscriptions in memory; back it with a database (it implements both store
  interfaces) to survive restarts.
- **Catalog source** — `IEventCatalog` can be implemented over a database or a
  remote schema service instead of reflection.
- **Sample data** — implement `ISampleFactory` to return real recent records
  instead of synthetic samples (better Zapier "test trigger" UX).
