# Webhook.N8n

A small ASP.NET Core (.NET 8) service that exposes your domain's **objects and
events to [n8n](https://n8n.io)** through a single, generic REST API. The set of
objects and events is **discovered at runtime** from decorated types — never
hardcoded in the controllers or in the n8n node — so the same integration serves any
domain and new objects show up in n8n the moment you add a class.

It is the n8n sibling of [`Webhook.Zapier`](../Webhook.Zapier/README.md); both are thin
API layers over the shared [`Webhook.Integrations.Core`](../Webhook.Integrations.Core)
(discovery, subscription bridge, API-key gate) and deliver through the
[`Webhook.Publisher`](../Webhook.Publisher/README.md) pipeline.

```
n8n (user building a workflow)               Webhook.N8n
  │  add credential (API key) ────────────▶  GET  /n8n/me
  │  pick Object (loadOptions) ───────────▶  GET  /n8n/objects                → [{name,value}]
  │  pick Event  (loadOptions) ───────────▶  GET  /n8n/objects/{object}/events
  │  activate workflow (webhook create) ──▶  POST /n8n/subscriptions  {object,event,targetUrl}
  │  (checkExists before create) ─────────▶  GET  /n8n/subscriptions/exists?object=&event=&targetUrl=
  │  deactivate workflow (webhook delete)─▶  DELETE /n8n/subscriptions/{id}
  ▼
your domain emits an event ─▶ IEventPublisher ─▶ Webhook.Publisher (Mongo + RabbitMQ)
                                                 └─▶ signed, retried POST to the n8n webhook URL
```

The n8n trigger node's webhook lifecycle (`checkExists` / `create` / `delete`) maps
directly onto the subscription endpoints, and its `loadOptions` dropdowns are fed by
the catalog endpoints. See [`docs/N8N_SETUP.md`](docs/N8N_SETUP.md) for the custom
node (it can't be fully automated; the doc explains why and gives a complete,
copy-pasteable node) and a no-custom-node alternative using n8n's built-in Webhook node.

## Prerequisites

Delivery uses `Webhook.Publisher`, so the service needs **MongoDB and RabbitMQ**
reachable at startup. Configure them in the `WebhookPublisher` section of
`appsettings.json`. For local dev:

```bash
docker run -d -p 27017:27017 mongo:7
docker run -d -p 5672:5672 rabbitmq:3
```

## Quick start

```bash
dotnet run --project src/Webhook.N8n
# GET /health -> {"status":"ok"}
```

All `/n8n/*` routes require an API key (`X-Api-Key` or `Authorization: Bearer`).
The shipped demo key is `demo-secret-key` (see `appsettings.json`).

```bash
KEY="demo-secret-key"; BASE="http://localhost:5000"

# what objects exist? (discovered, not hardcoded) — n8n {name,value} shape
curl -s -H "X-Api-Key: $KEY" $BASE/n8n/objects

# events for "deal"
curl -s -H "X-Api-Key: $KEY" $BASE/n8n/objects/deal/events

# register an n8n webhook URL (the node does this on activation)
curl -s -H "X-Api-Key: $KEY" -H "Content-Type: application/json" \
  -d '{"object":"deal","event":"won","targetUrl":"https://n8n.example/webhook/abc"}' \
  $BASE/n8n/subscriptions

# fire a demo event — published to the durable pipeline, delivered to subscribers
curl -s -H "X-Api-Key: $KEY" -H "Content-Type: application/json" \
  -d '{"object":"deal","event":"won"}' \
  $BASE/n8n/mock/emit
```

## How it differs from the Zapier integration

Everything domain-related (discovery, subscription bridge, sample generation, durable
delivery, API-key auth) is shared in `Webhook.Integrations.Core`. This project only
adds n8n-shaped controllers:

- **Dropdowns** return n8n's `loadOptions` shape `[{name, value, description}]` (vs
  Zapier's `{key, label}`).
- **Webhook lifecycle** adds a `checkExists` endpoint
  (`GET /n8n/subscriptions/exists`) alongside create/delete, matching an n8n trigger
  node's `webhookMethods`.
- **Routes** live under `/n8n`, gated by the `N8n` config section and the `n8n` tenant.

## Delivered payload

Each subscriber (the n8n webhook URL) receives the `Webhook.Publisher` envelope
(signed Stripe-style in `Webhook-Signature`):

```json
{
  "eventId": "…",
  "tenantId": "n8n",
  "eventName": "deal.won",
  "occurredAt": "2026-01-15T09:30:00Z",
  "schemaVersion": "1",
  "data": { "id": "deal_1001", "amount": 19.99, "stage": "open" }
}
```

## Project layout

```
src/Webhook.N8n/
  Controllers/    Credential (/me), Catalog (objects/events), Subscriptions
                  (create/delete/checkExists/samples), Mock (emit)
  DependencyInjection/  AddN8nIntegration → AddIntegrationCore("N8n", "/n8n")
  docs/           N8N_SETUP.md
tests/Webhook.N8n.Tests/  xUnit endpoint tests (publisher faked, no infra)
```

See `Webhook.Integrations.Core` for how runtime discovery works (mark a POCO with
`[TriggerObject]` / `[TriggerEvent]` and it appears here automatically).
