# Ingress — Webhook Receiver Service

`Ingress` (`src/Ingress`) is a configurable microservice for receiving webhooks from many
different third parties through a **single dynamic entry point** — with **no code
changes** to add, remove, or reconfigure a webhook. It follows the standard
[`MicroserviceApp`](src/PI.Shared.App) pattern (Serilog/ElasticSearch logging, AWS SSM
config, Swagger, MongoDB) like the platform's other services.

> **Outbound webhooks too:** the companion library
> [`src/PI.Shared.Integrations`](src/PI.Shared.Integrations) is the *outbound* half —
> exposes platform objects/events to automation tools and delivers them as signed HTTP
> POSTs, stored in MongoDB with durable exponential-backoff retries. The generic outbound
> service is [`src/Webhooks`](src/Webhooks/README.md).
>
> **Zapier / n8n integrations:** [`src/Zapier`](src/Zapier/README.md) and
> [`src/N8n`](src/N8n/README.md) expose your objects and events through a single generic
> REST API — discovered at runtime from the account's real object types (not hardcoded) —
> driving the platforms' dynamic dropdowns and REST Hooks. Triggers are delivered through
> the shared `PI.Shared.Integrations` pipeline above (signed, durable, retried). See each
> service's `README.md` for the platform-side setup.

Each configured webhook is assigned a **UUID**. All deliveries hit one route,
`/ingress/{uuid}`, and the service looks up that UUID's configuration to
decide how to authenticate, parse, answer registration handshakes, dispatch, and
respond. The endpoint is anonymous — external providers can't present a platform JWT,
so per-webhook authenticity is enforced by the validator pipeline, not ASP.NET policies.

```
POST|GET /ingress/{uuid}
        │
        ▼
  lookup definition ──▶ validate (auth) ──▶ registration handshake?
                                                   │ yes ──▶ echo challenge
                                                   │ no
                                                   ▼
                                   parse body ──▶ handler ──▶ build response
```

## Quick start

```bash
dotnet run --project src/Ingress
# runs as a Web API by default; GET /health returns 200, Swagger at /ingress/swagger.
# Set PI_RUN_JOB to run as a background job instead.
```

Webhook definitions are stored in MongoDB (the `ingress.Definition` collection) and looked
up globally by their `Uuid`. Add, edit, or disable a definition by writing to that
collection — no restart needed.

## Configuration

Each definition is a document in the `ingress.Definition` collection
(see `Configuration/WebhookDefinition.cs`, keyed by `Uuid` as its `_id`):

| Field          | Description                                                            |
|----------------|------------------------------------------------------------------------|
| `Uuid`         | Route segment and document id: `/ingress/{uuid}`.                      |
| `Name`         | Friendly name (used in logs, handler dispatch, response tokens).       |
| `Enabled`      | When `false`, the endpoint responds `404`.                            |
| `Handler`      | Name of the `IWebhookHandler` to invoke (default `logging`).           |
| `Format`       | Body format: `json`, `form`, `xml`, `raw`.                            |
| `Auth`         | Array of validators — **all** must pass (logical AND).                |
| `Registration` | Registration / verification handshake behaviour.                       |
| `Response`     | Status, content type and body template for successful deliveries.      |

The JSON snippets below show the shape of each nested config object (the same property
names are used in the stored documents).

### Authentication (`Auth[]`)

Combine any number of these; an empty array (or a single `none`) means no auth.

```jsonc
// HMAC over the raw body (GitHub / Shopify / Stripe / Slack style)
{ "Type": "hmac", "Header": "X-Hub-Signature-256",
  "Algorithm": "sha256", "Encoding": "hex", "Prefix": "sha256=", "Secret": "..." }

// Static bearer token: Authorization: Bearer <token>
{ "Type": "bearer", "Token": "..." }

// API key header (default header X-Api-Key)
{ "Type": "apikey", "Header": "X-Api-Key", "Token": "..." }

// HTTP Basic
{ "Type": "basic", "Username": "...", "Password": "..." }

// Source IP / CIDR allowlist (set TrustForwardedFor only behind a trusted proxy)
{ "Type": "ipAllowlist", "Ranges": ["173.252.0.0/16"], "TrustForwardedFor": false }

// Twilio: HMAC-SHA1 over the URL + sorted form params (X-Twilio-Signature).
// Set Url to the exact public webhook URL when behind a proxy/tunnel.
{ "Type": "twilio", "Header": "X-Twilio-Signature", "Token": "<auth-token>",
  "Url": "https://example.com/ingress/<uuid>" }

// ECDSA over {timestamp}{body}, P-256/SHA-256 (e.g. SendGrid Event Webhook).
{ "Type": "ecdsa", "Header": "X-Twilio-Email-Event-Webhook-Signature",
  "TimestampHeader": "X-Twilio-Email-Event-Webhook-Timestamp", "PublicKey": "<base64-spki>" }

// Structured timestamped HMAC-SHA256 over {timestamp}.{body}. The timestamp is
// parsed from the signature header itself. Types: stripe (t=,v1=), docuseal
// (timestamp.signature), openphone (hmac;1;timestamp;sig, base64-decoded key).
{ "Type": "stripe", "Header": "Stripe-Signature", "Secret": "whsec_..." }

// Mutual-TLS client certificate (e.g. Kubernetes admission, Salesforce).
// Requires the server to negotiate client certs; fails closed otherwise.
{ "Type": "clientCert", "Thumbprints": ["AB12..."], "Subject": "CN=apiserver" }

// JSON body field equals a value; "[]" iterates an array (Microsoft clientState).
{ "Type": "bodyField", "Path": "value[].clientState", "Value": "<shared-secret>" }

// No authentication
{ "Type": "none" }
```

`hmac` supports `sha1`/`sha256`/`sha512`, `hex`/`base64` encoding, an optional
header `Prefix`, and a constant-time comparison. It also accepts a `Template`
(tokens `{body}` / `{timestamp}` from `TimestampHeader`) for schemes that sign
more than the body — e.g. Slack's `"Template": "v0:{timestamp}:{body}"`. All
signature validators verify against the exact raw request body (buffered so it
survives form parsing).

### Registration handshakes (`Registration`)

Many providers verify a webhook URL before sending events:

```jsonc
// Meta / Facebook: GET echoes the challenge query param after checking a verify token
{ "Mode": "challengeQuery", "ChallengeParam": "hub.challenge",
  "VerifyParam": "hub.verify_token", "VerifyValue": "..." }

// Slack: a JSON body of type=url_verification is answered by echoing `challenge`
{ "Mode": "challengeBody", "TriggerField": "type",
  "TriggerValue": "url_verification", "ChallengeField": "challenge" }

// No handshake
{ "Mode": "none" }
```

### Response templates (`Response`)

```jsonc
{ "Status": 200, "FailureStatus": 401, "ContentType": "application/json",
  "Body": "{\"ok\":true,\"hook\":\"{{name}}\"}" }
```

Body tokens: `{{uuid}}`, `{{name}}`, and `{{json:path.to.field}}` (dot-path lookup
into the JSON request body).

## Provider coverage

Worked examples for common providers (replace the `change-me-*` secrets) are available to
seed the `ingress.Definition` collection. Verification of each scheme is covered by tests.

| Provider          | Verification / registration            | Delivery auth                                   | Config                                   |
|-------------------|----------------------------------------|-------------------------------------------------|------------------------------------------|
| **GitHub**        | —                                      | HMAC-SHA256 hex, `X-Hub-Signature-256: sha256=` | `hmac`                                   |
| **Facebook/Meta** | GET `hub.challenge` + `hub.verify_token` | HMAC-SHA256 over body                          | `challengeQuery` + `hmac`                |
| **Google**        | first message has `X-Goog-Resource-State: sync` | `X-Goog-Channel-Token` equals your token | `apikey`                                 |
| **SendGrid**      | —                                      | ECDSA P-256 over `{timestamp}{body}`            | `ecdsa`                                  |
| **Twilio**        | —                                      | HMAC-SHA1 over URL + sorted params / `bodySHA256` | `twilio`                               |
| **CompanyCam**    | —                                      | HMAC-SHA1 base64, `X-CompanyCam-Signature`      | `hmac` (`sha1`/`base64`)                 |
| **Microsoft 365** | POST `?validationToken=` echoed text/plain | `clientState` in each `value[]`            | `challengeQuery` + `bodyField`           |
| **Stripe**        | —                                      | `t=,v1=` HMAC-SHA256 over `t.body`              | `stripe`                                 |
| **Slack**         | `url_verification` body challenge      | HMAC-SHA256 over `v0:timestamp:body`            | `challengeBody` + `hmac` (`Template`)    |
| **Kubernetes**    | — (responds with AdmissionReview `uid`) | mutual TLS client certificate                  | `clientCert` + `{{json:request.uid}}`    |
| **Marchex**       | —                                      | `ms-signature: sha256=` HMAC-SHA256 (or Basic)  | `hmac` (or `basic`)                      |
| **OpenPhone**     | —                                      | `hmac;1;ts;sig` HMAC-SHA256 over `ts.body`      | `openphone`                              |
| **QuickBooks**    | —                                      | `intuit-signature` base64 HMAC-SHA256 of body   | `hmac` (`sha256`/`base64`)               |
| **Salesforce**    | —                                      | mutual TLS / IP allowlist (SOAP XML)            | `ipAllowlist` / `clientCert`             |
| **Typeform**      | —                                      | `Typeform-Signature: sha256=` base64 HMAC       | `hmac` (`sha256`/`base64`)               |
| **Zapier**        | —                                      | no native signature; shared header / URL secret | `apikey` / `none`                        |

Notes:
- **Twilio** handles both form deliveries (URL + sorted params) and JSON deliveries
  (URL + `bodySHA256` query, with the body hash verified).
- **Kubernetes / Salesforce** use mutual TLS — configure Kestrel to negotiate
  client certificates (`ClientCertificateMode`) for the `clientCert` validator to
  see the certificate; it fails closed otherwise.
- **Microsoft 365** rich notifications also include JWT `validationTokens`; verifying
  those (against Microsoft's signing keys) belongs in a handler.

## Adding a handler (the only code extension point)

```csharp
public sealed class OrderCreatedHandler : IWebhookHandler
{
    public string Name => "order-created";

    public Task<WebhookResult> HandleAsync(WebhookContext ctx, CancellationToken ct = default)
    {
        // ctx.Payload is a JsonElement for "json", a Dictionary<string,string> for "form",
        // an XDocument for "xml", or byte[] for "raw". ctx.RawBody / ctx.Headers / ctx.Query
        // are always available.
        return Task.FromResult(WebhookResult.Default); // or WebhookResult.Custom(...)
    }
}
```

Register it in `Program.AddServices` (after `AddWebhookEngine`), then reference it from a
definition via `"Handler": "order-created"`:

```csharp
services.AddWebhookHandler<OrderCreatedHandler>();
```

## Extensibility

- **Config store** — the pipeline depends only on `IWebhookConfigStore`. The shipped
  `MongoWebhookConfigStore` reads the `ingress.Definition` collection; an
  `InMemoryWebhookConfigStore` is provided for tests. Swap in another implementation
  without touching the controller or processor.
- **Validators / parsers** — additional `IWebhookValidator` / `IPayloadParser`
  implementations registered in DI are picked up automatically.

## Project layout

```
src/Ingress/
  Program.cs          MicroserviceApp ("Ingress") — bootstraps the engine + body buffering
  Controllers/        WebhookController — POST/GET/PUT /ingress/{uuid}
  Engine/             WebhookProcessor, WebhookContext/Result, RegistrationHandshake
  Config/             IWebhookConfigStore (+ Mongo and in-memory stores)
  Configuration/      WebhookDefinition, Auth/Registration/Response config models
  Validation/         HMAC (+template), token/api-key, basic, IP allowlist, Twilio,
                      ECDSA, signed-timestamp (Stripe/DocuSeal/OpenPhone), clientCert,
                      bodyField + pipeline
  Formats/            JSON / form / XML / raw payload parsers
  Handlers/           IWebhookHandler, registry, default LoggingWebhookHandler
  Responses/          ResponseBuilder (status + content type + body templating)
tests/Ingress.Tests/   xUnit unit + in-process engine tests
```

## Tests

```bash
dotnet test tests/Ingress.Tests
```
