# N8n

Outbound **n8n** integration service. Lets an n8n *trigger node* subscribe to platform
object events (Create/Update/Delete on any object type) and receive a signed webhook POST
whenever one fires.

Follows the standard service pattern (`Program : MicroserviceApp`, JWT auth, SSM config,
`Dockerfile`/`kubernetes.ps1`). All catalog/subscription/delivery logic is shared with the
Zapier service via [`PI.Shared.Integrations`](../PI.Shared.Integrations); this project is a
thin n8n-shaped adapter.

## Auth

Every endpoint requires the `n8n` JWT policy: a platform-issued bearer token carrying a
Manager/Admin/Root role and the `n8n` scope. Send it as `Authorization: Bearer <token>`.

## Endpoints (`/n8n/v1`)

| Method & path | Purpose |
| --- | --- |
| `GET /me` | Credential test — confirms the token and returns the caller's identity. |
| `GET /objects` | `loadOptions` source for the "Object" dropdown (`{name,value,description}`). |
| `GET /objects/{object}/events` | `loadOptions` source for the dependent "Event" dropdown. |
| `POST /subscriptions` | `create`: register the node's webhook URL for an object/event. |
| `DELETE /subscriptions/{id}` | `delete`: remove a webhook (idempotent). |
| `GET /subscriptions/exists` | `checkExists`: avoid duplicate registrations. |
| `GET /objects/{object}/events/{event}/samples` | Example delivered envelope for the node's test step. |

Objects/events are discovered from the account's real `ObjectType` definitions — adding an
object type in the platform surfaces a new trigger with no code change.

## Setting up the n8n integration

n8n trigger nodes are code (a community node package). Below is the mapping from the node's
credential and webhook lifecycle to this service's endpoints; base URL is your deployment's
host, e.g. `https://api.example.com`, and all paths are under `/n8n/v1`.

### 1. Credential

Create a credential (e.g. `PlatformApi`) that stores the user's platform bearer token (a JWT
with the `n8n` scope) and sends it on every request:

- Field: `token` (password).
- `authenticate`: add header `Authorization: Bearer ={{$credentials.token}}`.
- `test`: `GET /n8n/v1/me` — a `200` confirms the token and labels the connection.

### 2. Trigger node — `loadOptions` dropdowns

- **Object** — `loadOptions` method calling `GET /n8n/v1/objects`. The response is already
  `{ name, value, description }`, the shape n8n expects for `INodePropertyOptions`.
- **Event** — a dependent `loadOptions` method calling
  `GET /n8n/v1/objects/{object}/events` (pass the selected object).

### 3. Trigger node — `webhookMethods`

Implement the node's `webhookMethods.default`:

- **`checkExists`** — `GET /n8n/v1/subscriptions/exists?object=<object>&event=<event>&targetUrl=<n8n webhook URL>`.
  Returns `{ "exists": true|false, "id": "…" }`; return `exists`.
- **`create`** — `POST /n8n/v1/subscriptions` with
  `{ "object": "<object>", "event": "<event>", "targetUrl": "<n8n webhook URL>" }`.
  Save the returned `id` to the node's static data.
- **`delete`** — `DELETE /n8n/v1/subscriptions/{id}` using the saved id.

The node's `webhook()` method receives each POST; emit the body's `data` object as the
workflow item (see *Delivery*).

### 4. Sample / pinned data (optional)

`GET /n8n/v1/objects/{object}/events/{event}/samples` returns a one-element array with the
delivered envelope, useful for the node's "fetch test event" / pin-data step.

### 5. Quick start without a custom node

You can prototype without packaging a node:

1. Add a **Webhook** node (method `POST`) and copy its production URL.
2. Add an **HTTP Request** node with the credential above and call
   `POST /n8n/v1/subscriptions` with `object`/`event`/`targetUrl` (the Webhook URL) — run it
   once to register.
3. Activate the workflow; the Webhook node now receives deliveries. To stop, call
   `DELETE /n8n/v1/subscriptions/{id}` with the id returned in step 2.

### 6. Verifying the delivery signature (optional)

Each delivery carries `Webhook-Signature: t=<unix>,v1=<hex>`, where the signature is
`HMAC-SHA256(secret, "{t}.{rawBody}")` plus `Webhook-Id` / `Webhook-Event` /
`Webhook-Timestamp` headers. The per-subscription `secret` is currently held server-side
only, so verification is optional; expose the secret on subscribe if your security model
requires it.

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

## Configuration

`WebhookEventListener` / `WebhookDeliveryWorker` queue names come from config (see
`appsettings.json`); delivery/retry tuning is the optional `WebhookDelivery` section.
