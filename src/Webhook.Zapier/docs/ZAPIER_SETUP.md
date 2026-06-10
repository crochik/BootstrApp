# Setting up the Zapier integration

This service exposes your objects and events over a generic REST API. The Zapier
**app** (the thing users see in Zapier's editor) is a separate artifact that lives
**inside Zapier's platform** and must be created in your Zapier account.

> **Why this isn't fully automatic.** A Zapier integration is owned by Zapier and
> defined in their platform (a versioned app, reviewed and published by Zapier).
> There is no API to create one from your backend. What you *can* do — and what this
> design does — is make the app **tiny and static**: a single REST Hook trigger whose
> object and event lists are pulled live from this service via **dynamic dropdowns**.
> The result is that you build the Zapier app once and never touch it again, even as
> you add new objects and events on the C# side.

You build the Zapier app **once** using either:

- **[Option A — Zapier Platform CLI](#option-a--zapier-platform-cli-recommended)**
  (recommended; the whole app is copy-pasteable below), or
- **[Option B — Zapier Platform UI](#option-b--zapier-platform-ui)** (no-code, visual).

Both wire up the same four interactions this service exposes:

| Zapier concept            | This service                                                  |
|---------------------------|---------------------------------------------------------------|
| Authentication (API key)  | `GET /zapier/me`                                              |
| "Object" dynamic dropdown | `GET /zapier/objects`                                         |
| "Event" dynamic dropdown  | `GET /zapier/objects/{object}/events`                         |
| Trigger — subscribe       | `POST /zapier/subscriptions` `{object,event,targetUrl}` → `{id}` |
| Trigger — unsubscribe     | `DELETE /zapier/subscriptions/{id}`                          |
| Trigger — perform list    | `GET /zapier/objects/{object}/events/{event}/samples`        |
| Event delivery (REST Hook)| this service POSTs the payload to the subscribed `targetUrl`  |

---

## Prerequisites

1. **Deploy this service** somewhere Zapier can reach over HTTPS (Zapier calls *in*
   to subscribe and pull dropdowns, and this service calls *out* to Zapier's callback
   URLs to deliver events). A public URL or tunnel (e.g. ngrok) is required. The
   service also needs **MongoDB and RabbitMQ** (the `Webhook.Publisher` delivery
   pipeline) reachable at startup — configure them in the `WebhookPublisher` section
   of `appsettings.json`.
2. **Pick an API key.** Set it in `appsettings.json` (or the `Zapier__ApiKeys__0`
   environment variable). The shipped demo key is `demo-secret-key` — change it.
3. **A Zapier account.** The free tier is enough to build and test a private app.

Throughout, replace `https://your-host.example.com` with your deployed base URL.

---

## Option A — Zapier Platform CLI (recommended)

The CLI keeps the whole app in version control as a few small files. Because the app
only reads dropdowns and manages REST Hooks, **you never edit it when you add objects
or events** — those are discovered from this service at runtime.

### 1. Install and authenticate

```bash
npm install -g zapier-platform-cli
zapier login
```

### 2. Scaffold the app

```bash
zapier init webhook-zapier-app --template minimal
cd webhook-zapier-app
```

### 3. Configure the base URL

Set your deployed URL as an environment variable for the app:

```bash
zapier env:set 1.0.0 BASE_URL=https://your-host.example.com
```

### 4. Replace `index.js`

This is the complete app. It is intentionally generic: the only triggers it defines
are the dropdown feeders and one REST Hook trigger driven entirely by data this
service returns.

```js
const BASE = process.env.BASE_URL; // set via: zapier env:set <ver> BASE_URL=...

// ---- Authentication: API key, verified against GET /zapier/me ----------------
const authentication = {
  type: 'custom',
  fields: [
    { key: 'apiKey', label: 'API Key', required: true, type: 'string' },
  ],
  test: { url: `${BASE}/zapier/me` },
  connectionLabel: '{{json.name}}',
};

// Send the API key on every request.
const includeApiKey = (request, z, bundle) => {
  request.headers['X-Api-Key'] = bundle.authData.apiKey;
  return request;
};

// ---- Hidden trigger: populate the "Object" dropdown --------------------------
const listObjects = {
  key: 'objects',
  noun: 'Object',
  display: { label: 'List Objects', hidden: true, description: 'Dropdown source.' },
  operation: {
    perform: {
      url: `${BASE}/zapier/objects`,
      // Map to {id,label} that a dropdown expects.
      // Each item is {key,label,description}; "key" becomes the stored value.
    },
    // Tell Zapier which fields are the value/label for the dropdown.
    outputFields: [{ key: 'key' }, { key: 'label' }],
  },
};

// ---- Hidden trigger: populate the "Event" dropdown (depends on object) -------
const listEvents = {
  key: 'events',
  noun: 'Event',
  display: { label: 'List Events', hidden: true, description: 'Dropdown source.' },
  operation: {
    perform: {
      url: `${BASE}/zapier/objects/{{bundle.inputData.object}}/events`,
    },
    inputFields: [{ key: 'object', required: true }],
    outputFields: [{ key: 'key' }, { key: 'label' }],
  },
};

// ---- The real trigger: a REST Hook over any object/event ---------------------
const newEvent = {
  key: 'new_event',
  noun: 'Event',
  display: {
    label: 'New Event',
    description: 'Triggers when a chosen event happens on a chosen object.',
  },
  operation: {
    type: 'hook',
    inputFields: [
      {
        key: 'object',
        label: 'Object',
        required: true,
        dynamic: 'objects.key.label', // dropdown fed by listObjects
        altersDynamicFields: true,     // re-fetch events when object changes
      },
      {
        key: 'event',
        label: 'Event',
        required: true,
        dynamic: 'events.key.label',   // dropdown fed by listEvents
      },
    ],

    // Subscribe: register Zapier's target URL for {object,event}.
    performSubscribe: {
      url: `${BASE}/zapier/subscriptions`,
      method: 'POST',
      body: {
        object: '{{bundle.inputData.object}}',
        event: '{{bundle.inputData.event}}',
        targetUrl: '{{bundle.targetUrl}}',
      },
    },
    // Unsubscribe: remove it (Zapier passes back what performSubscribe returned).
    performUnsubscribe: {
      url: `${BASE}/zapier/subscriptions/{{bundle.subscribeData.id}}`,
      method: 'DELETE',
    },

    // Runs on each incoming POST from this service: Zapier hands us the body.
    perform: (z, bundle) => [bundle.cleanedRequest],

    // "Test trigger": pull a sample so the user has data to map.
    performList: {
      url: `${BASE}/zapier/objects/{{bundle.inputData.object}}/events/{{bundle.inputData.event}}/samples`,
    },

    sample: {
      eventId: 'evt_sample',
      tenantId: 'zapier',
      eventName: 'deal.won',
      occurredAt: '2026-01-15T09:30:00.0000000+00:00',
      schemaVersion: '1',
      data: { id: 'deal_1001', name: 'Acme renewal', amount: 19.99, stage: 'open' },
    },
  },
};

module.exports = {
  version: require('./package.json').version,
  platformVersion: require('zapier-platform-core').version,
  authentication,
  beforeRequest: [includeApiKey],
  triggers: {
    [listObjects.key]: listObjects,
    [listEvents.key]: listEvents,
    [newEvent.key]: newEvent,
  },
};
```

> **Note on `dynamic`.** `objects.key.label` means: feed this dropdown from the
> `objects` trigger, using each item's `key` as the value and `label` as the display
> text. `altersDynamicFields: true` on the Object field makes Zapier re-pull the
> Event dropdown whenever the object changes — that's the cascade.

### 5. Validate, push, and test

```bash
zapier validate
zapier push
zapier test            # runs the auth/dropdown calls against your BASE_URL
```

Then open the app in Zapier (`zapier open`) and build a test Zap — see
[Testing end-to-end](#testing-end-to-end).

### 6. Invite users or go public

- **Private use / a few users:** `zapier users:add teammate@example.com 1.0.0`.
- **Public listing:** `zapier promote 1.0.0` and follow Zapier's review checklist
  (branding, descriptions, a working test account). Nothing here changes when you
  add new objects — they appear in the existing dropdowns automatically.

---

## Option B — Zapier Platform UI

Prefer clicking to coding? Build the same app at
<https://developer.zapier.com> → **Start a Zapier Integration**.

1. **Authentication → API Key.**
   - Add an input field `apiKey`.
   - Set the **Test** request to `GET https://your-host.example.com/zapier/me`.
   - Add a header to all requests: `X-Api-Key: {{bundle.authData.apiKey}}`.
   - Set the **Connection Label** to `{{json.name}}`.

2. **Trigger → "New Event" → REST Hook.**
   - **Input Designer**, add two fields:
     - `object` — type *Dynamic Dropdown*, source the **List Objects** trigger
       (created next), value `key`, label `label`. Check *"Alters dynamic fields."*
     - `event` — type *Dynamic Dropdown*, source the **List Events** trigger, value
       `key`, label `label`.
   - **Subscribe:** `POST https://your-host.example.com/zapier/subscriptions` with
     JSON body `{ "object": "{{bundle.inputData.object}}", "event":
     "{{bundle.inputData.event}}", "targetUrl": "{{bundle.targetUrl}}" }`.
   - **Unsubscribe:** `DELETE
     https://your-host.example.com/zapier/subscriptions/{{bundle.subscribeData.id}}`.
   - **Perform List:** `GET
     https://your-host.example.com/zapier/objects/{{bundle.inputData.object}}/events/{{bundle.inputData.event}}/samples`.

3. **Two hidden helper triggers** (these feed the dropdowns above):
   - **List Objects** — `GET https://your-host.example.com/zapier/objects`; mark it
     *hidden*.
   - **List Events** — `GET
     https://your-host.example.com/zapier/objects/{{bundle.inputData.object}}/events`;
     add an input field `object`; mark it *hidden*.

4. **Save**, then test as below.

---

## Testing end-to-end

1. In Zapier, **create a Zap**, choose your app, and connect it with your API key.
   The connection should label itself with the name from `GET /zapier/me`.
2. Pick an **Object** (e.g. *Deal*) — the dropdown is filled from `/zapier/objects`.
   Pick an **Event** (e.g. *Deal Won*) — filled from the object's `/events`.
3. **Test trigger.** Zapier calls the samples endpoint and shows the example payload.
4. **Turn the Zap on.** Zapier calls `POST /zapier/subscriptions`; you can confirm
   the subscription landed.
5. **Fire a real event.** For the mock service:

   ```bash
   curl -s -H "X-Api-Key: demo-secret-key" -H "Content-Type: application/json" \
     -d '{"object":"deal","event":"won"}' \
     https://your-host.example.com/zapier/mock/emit
   ```

   The service POSTs the payload to Zapier's callback URL and your Zap runs. In a
   real deployment, that delivery is triggered by your domain calling
   `IEventPublisher.PublishAsync(...)` instead of the `/zapier/mock/emit` endpoint.
6. **Turn the Zap off.** Zapier calls `DELETE /zapier/subscriptions/{id}`.

---

## Payload shape

Delivery runs through the `Webhook.Publisher` pipeline, so every delivery (and every
sample) is its envelope — the same shape across all objects, so field mapping in
Zapier is stable:

```json
{
  "eventId": "3f1c…",
  "tenantId": "zapier",
  "eventName": "deal.won",
  "occurredAt": "2026-01-15T09:30:00.0000000+00:00",
  "schemaVersion": "1",
  "data": { "id": "deal_1001", "name": "Acme renewal", "amount": 19.99, "stage": "open" }
}
```

- `eventName` is `"{object}.{event}"`.
- `eventId` is unique per event and is also sent as the **`Webhook-Id`** header —
  Zapier de-duplicates triggers on it. (`Webhook-Event` and `Webhook-Timestamp`
  headers are also sent.)
- `data` holds the object's fields (generated from the CLR type by
  `ReflectionSampleFactory` in the mock; your real records in production).

### Signed deliveries (optional to verify)

Each POST is signed Stripe-style in the `Webhook-Signature` header
(`t=<unix>,v1=<hmac-sha256 of "{t}.{rawBody}">`) using a per-subscription secret.
Zapier doesn't verify this by default; if you want tamper-evidence, validate it in a
Code step. The same scheme is what the inbound `Webhook.Service` `stripe` validator
checks, so the two halves of this repo interoperate.

---

## Troubleshooting

| Symptom                                   | Likely cause / fix                                                            |
|-------------------------------------------|-------------------------------------------------------------------------------|
| Connection test fails (401)               | Wrong API key, or `X-Api-Key` header not attached on every request.           |
| Object/Event dropdowns are empty          | `/zapier/objects` unreachable from Zapier, or no `[ZapierObject]` types found.|
| Event dropdown doesn't change with object | `altersDynamicFields` not set on the Object field.                            |
| Zap never fires                           | Subscribe didn't store `targetUrl`; this service can't reach RabbitMQ/Mongo or Zapier's URL; or no delivery worker is running. |
| Event fires late, then arrives            | Expected — delivery is durable with backoff retries; a briefly-unreachable Zap is retried, not dropped. |
| Triggers stop after turning a Zap off     | Expected — Zapier calls unsubscribe (`DELETE`) and the subscription is removed.|
| New object doesn't appear in Zapier       | Reconnect the Zap step (dropdowns are re-fetched on edit); no app change needed.|
