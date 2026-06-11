# Zapier

Outbound **Zapier** integration service. Lets a Zap subscribe to platform object events
(Create/Update/Delete on any object type) and receive a signed webhook POST whenever one
fires.

Follows the standard service pattern (`Program : MicroserviceApp`, JWT auth, SSM config,
`Dockerfile`/`kubernetes.ps1`). All catalog/subscription/delivery logic is shared with the
N8n service via [`PI.Shared.Integrations`](../PI.Shared.Integrations); this project is a
thin Zapier-shaped adapter.

## Auth

Every endpoint requires the `zapier` JWT policy: a platform-issued bearer token carrying a
Manager/Admin/Root role and the `zapier` scope. Send it as `Authorization: Bearer <token>`.

## Endpoints (`/zapier/v1`)

| Method & path | Purpose |
| --- | --- |
| `GET /user` | Connection test — returns the current user. |
| `GET /objects` | Source for Zapier's "Object" dropdown (`{key,label,description}`). |
| `GET /objects/{object}/events` | Source for the dependent "Event" dropdown. |
| `POST /subscriptions` | Subscribe: register Zapier's callback URL for an object/event. |
| `DELETE /subscriptions/{id}` | Unsubscribe (idempotent). |
| `GET /objects/{object}/events/{event}/samples` | Example delivered envelope for the "test trigger" step. |

Objects/events are discovered from the account's real `ObjectType` definitions — adding an
object type in the platform surfaces a new trigger with no code change.

## Setting up the Zapier integration

Build a Zapier app with a single **REST Hook** trigger that talks to this service. Use the
[Zapier Platform UI](https://developer.zapier.com) (Visual Builder) or the Zapier CLI. In the
examples below `{{...}}` are Zapier template variables and the base URL is your deployment's
host, e.g. `https://api.example.com` — all paths are under `/zapier/v1`.

### 1. Authentication

The connection holds the user's platform bearer token (a JWT with the `zapier` scope).

- **Auth type:** API Key (a single secret the user pastes — their bearer token).
- Add an input field, e.g. `api_key`.
- Under **App settings → Authentication**, add a request header to every call:
  `Authorization: Bearer {{bundle.authData.api_key}}`
- **Test** request: `GET /zapier/v1/user`. A `200` confirms the token; show the returned
  user as the connection label.

### 2. Trigger input fields (dynamic dropdowns)

- **Object** — a dynamic dropdown sourced from `GET /zapier/v1/objects`; map `key` → value,
  `label` → label.
- **Event** — a dynamic dropdown that *depends on* Object, sourced from
  `GET /zapier/v1/objects/{{bundle.inputData.object}}/events`; map `key` → value, `label` → label.

### 3. REST Hook (subscribe / unsubscribe / perform)

- **Subscribe** — `POST /zapier/v1/subscriptions` with JSON body:
  ```json
  { "object": "{{bundle.inputData.object}}", "event": "{{bundle.inputData.event}}", "targetUrl": "{{bundle.targetUrl}}" }
  ```
  Store `id` from the response (Zapier's `subscribeData`).
- **Unsubscribe** — `DELETE /zapier/v1/subscriptions/{{bundle.subscribeData.id}}`.
- **Perform** (handles each incoming hook) — return `[bundle.cleanedRequest]`. The POSTed body
  is the envelope described under *Delivery*; map fields from its `data` object.
- **Perform List** (sample/test data) — `GET /zapier/v1/objects/{{bundle.inputData.object}}/events/{{bundle.inputData.event}}/samples`.
  It already returns an array, so Zapier can use it directly for the "Test trigger" step.

### 4. Verifying the delivery signature (optional)

Each delivery carries `Webhook-Signature: t=<unix>,v1=<hex>`, where the signature is
`HMAC-SHA256(secret, "{t}.{rawBody}")` plus `Webhook-Id` / `Webhook-Event` / `Webhook-Timestamp`
headers. The per-subscription `secret` is currently held server-side only, so signature
verification is optional and not wired into the Zap; expose the secret on subscribe if your
security model requires Zaps to verify it.

## Delivery

Object events are consumed generically (`object.#`), matched against subscriptions, and
delivered through the shared durable pipeline (Mongo outbox + HMAC signing + retry worker +
outbox reconciler). The delivered body is:

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

The service also hosts the unrelated **`HttpCallOut` flow action** runner
([`Services/WebhookService.cs`](Services/WebhookService.cs)) — a templated outbound HTTP call
fired as a flow step. This is separate from Zapier subscriptions.

## Configuration

`WebhookEventListener` / `WebhookDeliveryWorker` / `WebhookService` queue names come from
config (see `appsettings.json`); delivery/retry tuning is the optional `WebhookDelivery`
section.
