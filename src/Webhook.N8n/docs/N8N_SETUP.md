# Setting up the n8n integration

This service exposes your objects and events over a generic REST API. The n8n side —
how a workflow subscribes to events — is configured **inside your n8n instance**, and
there are two ways to wire it up:

- **[Option A — a custom n8n node](#option-a--custom-n8n-node-recommended)**
  (recommended): a trigger node whose object/event dropdowns and webhook
  subscribe/unsubscribe are driven live by this service. Build it once; it never
  changes as you add objects and events on the C# side.
- **[Option B — the built-in Webhook node](#option-b--built-in-webhook-node-no-custom-code)**
  (no custom code): paste n8n's webhook URL into this service's subscribe endpoint by
  hand.

> **Why a node can't be created automatically.** An n8n integration is a *node* —
> code that runs inside an n8n instance (self-hosted or cloud). There's no API to
> inject one from your backend. What this design does instead is make the node
> **tiny and static**: a single webhook trigger whose object/event lists come from
> this service's `loadOptions` endpoints, and whose lifecycle calls the
> subscribe/unsubscribe endpoints. You build it once and forget it.

| n8n concept                         | This service                                                  |
|-------------------------------------|---------------------------------------------------------------|
| Credential test                     | `GET /n8n/me`                                                  |
| `loadOptions` — objects             | `GET /n8n/objects` → `[{name,value}]`                          |
| `loadOptions` — events              | `GET /n8n/objects/{object}/events`                            |
| `webhookMethods.checkExists`        | `GET /n8n/subscriptions/exists?object=&event=&targetUrl=`      |
| `webhookMethods.create`             | `POST /n8n/subscriptions` `{object,event,targetUrl}` → `{id}`  |
| `webhookMethods.delete`             | `DELETE /n8n/subscriptions/{id}`                              |
| event delivery                      | this service POSTs the payload to the node's webhook URL       |

---

## Prerequisites

1. **Deploy this service** somewhere n8n can reach over HTTPS (n8n calls *in* to
   subscribe and pull dropdowns; this service calls *out* to n8n's webhook URL to
   deliver events). It also needs **MongoDB and RabbitMQ** (the `Webhook.Publisher`
   pipeline) reachable at startup — see the `WebhookPublisher` section of
   `appsettings.json`.
2. **Pick an API key** (`Zapier`-style): set it under `N8n:ApiKeys` in
   `appsettings.json` (or the `N8n__ApiKeys__0` env var). The shipped demo key is
   `demo-secret-key` — change it.
3. **An n8n instance** (self-hosted is easiest for custom nodes).

Replace `https://your-host.example.com` with your deployed base URL throughout.

---

## Option A — custom n8n node (recommended)

Scaffold with n8n's starter and add the two files below.

```bash
git clone https://github.com/n8n-io/n8n-nodes-starter.git n8n-nodes-webhook-integration
cd n8n-nodes-webhook-integration
npm install
```

### 1. Credentials — `credentials/WebhookIntegrationApi.credentials.ts`

```ts
import {
  IAuthenticateGeneric,
  ICredentialTestRequest,
  ICredentialType,
  INodeProperties,
} from 'n8n-workflow';

export class WebhookIntegrationApi implements ICredentialType {
  name = 'webhookIntegrationApi';
  displayName = 'Webhook Integration API';

  properties: INodeProperties[] = [
    { displayName: 'Base URL', name: 'baseUrl', type: 'string', default: 'https://your-host.example.com' },
    { displayName: 'API Key', name: 'apiKey', type: 'string', typeOptions: { password: true }, default: '' },
  ];

  // Sent on every request the node makes.
  authenticate: IAuthenticateGeneric = {
    type: 'generic',
    properties: { headers: { 'X-Api-Key': '={{$credentials.apiKey}}' } },
  };

  // "Test" button in the credential UI → GET /n8n/me.
  test: ICredentialTestRequest = {
    request: { baseURL: '={{$credentials.baseUrl}}', url: '/n8n/me' },
  };
}
```

### 2. Node — `nodes/WebhookIntegrationTrigger/WebhookIntegrationTrigger.node.ts`

```ts
import {
  IHookFunctions,
  ILoadOptionsFunctions,
  INodePropertyOptions,
  INodeType,
  INodeTypeDescription,
  IWebhookFunctions,
  IWebhookResponseData,
} from 'n8n-workflow';

export class WebhookIntegrationTrigger implements INodeType {
  description: INodeTypeDescription = {
    displayName: 'Webhook Integration Trigger',
    name: 'webhookIntegrationTrigger',
    icon: 'fa:bolt',
    group: ['trigger'],
    version: 1,
    description: 'Starts a workflow when an object event occurs',
    defaults: { name: 'Webhook Integration Trigger' },
    inputs: [],
    outputs: ['main'],
    credentials: [{ name: 'webhookIntegrationApi', required: true }],
    webhooks: [{ name: 'default', httpMethod: 'POST', responseMode: 'onReceived', path: 'webhook' }],
    properties: [
      {
        displayName: 'Object Name or ID',
        name: 'object',
        type: 'options',
        typeOptions: { loadOptionsMethod: 'getObjects' },
        default: '',
        required: true,
        description: 'Choose from the list — loaded from your API at config time.',
      },
      {
        displayName: 'Event Name or ID',
        name: 'event',
        type: 'options',
        typeOptions: { loadOptionsMethod: 'getEvents', loadOptionsDependsOn: ['object'] },
        default: '',
        required: true,
      },
    ],
  };

  methods = {
    loadOptions: {
      async getObjects(this: ILoadOptionsFunctions): Promise<INodePropertyOptions[]> {
        const { baseUrl } = await this.getCredentials('webhookIntegrationApi');
        return (await this.helpers.httpRequestWithAuthentication.call(this, 'webhookIntegrationApi', {
          method: 'GET', baseURL: baseUrl as string, url: '/n8n/objects', json: true,
        })) as INodePropertyOptions[];
      },
      async getEvents(this: ILoadOptionsFunctions): Promise<INodePropertyOptions[]> {
        const object = this.getCurrentNodeParameter('object') as string;
        if (!object) return [];
        const { baseUrl } = await this.getCredentials('webhookIntegrationApi');
        return (await this.helpers.httpRequestWithAuthentication.call(this, 'webhookIntegrationApi', {
          method: 'GET', baseURL: baseUrl as string, url: `/n8n/objects/${object}/events`, json: true,
        })) as INodePropertyOptions[];
      },
    },
  };

  webhookMethods = {
    default: {
      async checkExists(this: IHookFunctions): Promise<boolean> {
        const { baseUrl } = await this.getCredentials('webhookIntegrationApi');
        const res = await this.helpers.httpRequestWithAuthentication.call(this, 'webhookIntegrationApi', {
          method: 'GET', baseURL: baseUrl as string, url: '/n8n/subscriptions/exists', json: true,
          qs: {
            object: this.getNodeParameter('object'),
            event: this.getNodeParameter('event'),
            targetUrl: this.getNodeWebhookUrl('default'),
          },
        });
        if (res.exists) { this.getWorkflowStaticData('node').subscriptionId = res.id; return true; }
        return false;
      },
      async create(this: IHookFunctions): Promise<boolean> {
        const { baseUrl } = await this.getCredentials('webhookIntegrationApi');
        const res = await this.helpers.httpRequestWithAuthentication.call(this, 'webhookIntegrationApi', {
          method: 'POST', baseURL: baseUrl as string, url: '/n8n/subscriptions', json: true,
          body: {
            object: this.getNodeParameter('object'),
            event: this.getNodeParameter('event'),
            targetUrl: this.getNodeWebhookUrl('default'),
          },
        });
        this.getWorkflowStaticData('node').subscriptionId = res.id;
        return true;
      },
      async delete(this: IHookFunctions): Promise<boolean> {
        const data = this.getWorkflowStaticData('node');
        if (!data.subscriptionId) return true;
        const { baseUrl } = await this.getCredentials('webhookIntegrationApi');
        await this.helpers.httpRequestWithAuthentication.call(this, 'webhookIntegrationApi', {
          method: 'DELETE', baseURL: baseUrl as string, url: `/n8n/subscriptions/${data.subscriptionId}`, json: true,
        });
        delete data.subscriptionId;
        return true;
      },
    },
  };

  async webhook(this: IWebhookFunctions): Promise<IWebhookResponseData> {
    // The delivered envelope becomes the trigger output.
    return { workflowData: [this.helpers.returnJsonArray(this.getBodyData())] };
  }
}
```

> The `object` dropdown is filled from `/n8n/objects`; the `event` dropdown re-loads
> from `/n8n/objects/{object}/events` whenever the object changes
> (`loadOptionsDependsOn`). That cascade is what keeps the node generic.

### 3. Build and install

```bash
npm run build
# Self-hosted n8n picks up custom nodes from ~/.n8n/custom (or N8N_CUSTOM_EXTENSIONS):
mkdir -p ~/.n8n/custom && cp -r dist/* ~/.n8n/custom/
# restart n8n
```

For wider distribution, publish the package to npm as `n8n-nodes-*` and install it via
**Settings → Community Nodes** (self-hosted) — adding new objects/events never requires
republishing.

### 4. Use it

1. Create a workflow, add **Webhook Integration Trigger**.
2. Create a credential: set the Base URL and API key; **Test** should return the
   connection name from `/n8n/me`.
3. Pick an **Object** then an **Event** (both dropdowns are pulled live).
4. **Activate** the workflow — n8n calls `checkExists` then `create`, registering its
   webhook URL. Deactivating calls `delete`.
5. Fire an event (see [Testing](#testing-end-to-end)); the workflow runs with the
   delivered envelope as its output.

---

## Option B — built-in Webhook node (no custom code)

If you don't want to build a node, use n8n's built-in **Webhook** node and register
its URL by hand.

1. Add a **Webhook** node, method **POST**. Copy its **Production URL**
   (e.g. `https://n8n.example/webhook/abcd`).
2. Register it once with this service:

   ```bash
   curl -s -H "X-Api-Key: demo-secret-key" -H "Content-Type: application/json" \
     -d '{"object":"deal","event":"won","targetUrl":"https://n8n.example/webhook/abcd"}' \
     https://your-host.example.com/n8n/subscriptions
   # → {"id":"sub_000001_…"}  (keep the id to unsubscribe later)
   ```
3. **Activate** the workflow. Events now POST to the webhook node and run the workflow.
4. To stop delivery: `DELETE https://your-host.example.com/n8n/subscriptions/{id}`.

The trade-off vs Option A: no automatic lifecycle (you subscribe/unsubscribe manually)
and no dropdowns — but zero custom code.

---

## Testing end-to-end

With a workflow active (either option), fire a demo event:

```bash
curl -s -H "X-Api-Key: demo-secret-key" -H "Content-Type: application/json" \
  -d '{"object":"deal","event":"won"}' \
  https://your-host.example.com/n8n/mock/emit
```

This service publishes to the durable pipeline, which POSTs the payload to the n8n
webhook URL and runs the workflow. In production, that publish is triggered by your
domain calling `IEventPublisher.PublishAsync(...)` instead of `/n8n/mock/emit`.

## Payload shape

Delivery runs through `Webhook.Publisher`, so every delivery (and the
`/n8n/.../samples` endpoint) is its envelope:

```json
{
  "eventId": "3f1c…",
  "tenantId": "n8n",
  "eventName": "deal.won",
  "occurredAt": "2026-01-15T09:30:00.0000000+00:00",
  "schemaVersion": "1",
  "data": { "id": "deal_1001", "name": "Acme renewal", "amount": 19.99, "stage": "open" }
}
```

- `eventName` is `"{object}.{event}"`; `eventId` is also the **`Webhook-Id`** header.
- Each POST is signed Stripe-style in `Webhook-Signature`
  (`t=<unix>,v1=<hmac-sha256 of "{t}.{rawBody}">`) with a per-subscription secret. n8n
  doesn't verify it by default; validate it in a Code/Function node if you want
  tamper-evidence.

## Troubleshooting

| Symptom                                   | Likely cause / fix                                                            |
|-------------------------------------------|-------------------------------------------------------------------------------|
| Credential test fails (401)               | Wrong API key, or the base URL is unreachable from n8n.                        |
| Object/Event dropdowns empty              | `/n8n/objects` unreachable, or no `[TriggerObject]` types discovered.         |
| Event dropdown doesn't change with object | `loadOptionsDependsOn: ['object']` missing on the event property.             |
| Workflow never runs                       | Not active; this service can't reach RabbitMQ/Mongo or the webhook URL; or the webhook wasn't registered (Option B). |
| Event arrives late, then runs             | Expected — delivery is durable with backoff retries.                          |
| New object doesn't appear                 | Re-open the node (dropdowns re-fetch on edit); no node rebuild needed.        |
