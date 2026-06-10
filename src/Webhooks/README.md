# Webhooks

Generic **outbound webhook publisher**. Lets any application subscribe a callback URL to
platform object events (Create/Update/Delete on any object type) and receive a signed
webhook POST whenever one fires. The neutral sibling of [`Zapier`](../Zapier/README.md) and
[`N8n`](../N8n/README.md) — same engine, no platform-specific request/response shapes.

> Not to be confused with [`Webhook.Service`](../Webhook.Service) (the *inbound* receiver).
> This service is the *outbound* publisher.

Follows the standard service pattern (`Program : MicroserviceApp`, JWT auth, SSM config,
`Dockerfile`/`kubernetes.ps1`). All catalog/subscription/delivery logic is shared via
[`PI.Shared.Integrations`](../PI.Shared.Integrations); this project is a thin REST adapter.

## Auth

Every endpoint requires the `webhooks` JWT policy: a platform-issued bearer token carrying a
Manager/Admin/Root role and the `webhooks` scope. Send it as `Authorization: Bearer <token>`.

## Endpoints (`/webhooks/v1`)

| Method & path | Purpose |
| --- | --- |
| `GET /me` | Connection test — confirms the token and returns the caller's identity. |
| `GET /objects` | Subscribable objects (`{key,label,description}`). |
| `GET /objects/{object}/events` | Events an object can emit. |
| `GET /events` | Full flattened set of event types (`{object}.{event}`). |
| `POST /subscriptions` | Subscribe `{object,event,targetUrl}`. Returns the subscription **including its `secret`**. |
| `GET /subscriptions` | List the caller's subscriptions (without secrets). |
| `GET /subscriptions/{id}` | Fetch one subscription, including its `secret`. |
| `DELETE /subscriptions/{id}` | Unsubscribe (idempotent). |
| `GET /objects/{object}/events/{event}/samples` | Example delivered envelope, for testing. |

Objects/events are discovered from the account's real `ObjectType` definitions — adding an
object type in the platform surfaces a new event type with no code change.

## Subscribing (application flow)

```http
POST /webhooks/v1/subscriptions
Authorization: Bearer <token>
Content-Type: application/json

{ "object": "Lead", "event": "Update", "targetUrl": "https://my-app.example.com/hooks/leads" }
```

```json
201 Created
{
  "id": "…",
  "object": "Lead",
  "event": "Update",
  "targetUrl": "https://my-app.example.com/hooks/leads",
  "signatureHeader": "Webhook-Signature",
  "secret": "whsec_…",
  "createdOn": "2026-06-10T00:00:00Z"
}
```

Store the `secret`: it is returned here (and from `GET /subscriptions/{id}`) but **not** in
list responses. Remove the subscription with `DELETE /webhooks/v1/subscriptions/{id}`.

## Receiving & verifying deliveries

Each delivery is `POST`ed to `targetUrl` with the envelope below and these headers:
`Webhook-Signature: t=<unix>,v1=<hex>`, `Webhook-Id`, `Webhook-Event`, `Webhook-Timestamp`.

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

Verify the signature: compute `HMAC-SHA256(secret, "{t}.{rawBody}")` (hex) where `t` is the
`t=` value from the header, and compare to the `v1=` value. Reject large clock skews using
`Webhook-Timestamp` to prevent replays.

## Delivery

Object events are consumed generically (`object.#`), matched against subscriptions, and
delivered through the shared durable pipeline (Mongo outbox + HMAC signing + retry worker +
outbox reconciler). See [`PI.Shared.Integrations`](../PI.Shared.Integrations).

## Configuration

`WebhookEventListener` / `WebhookDeliveryWorker` queue names come from config (see
`appsettings.json`); delivery/retry tuning is the optional `WebhookDelivery` section.
