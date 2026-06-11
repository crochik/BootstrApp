# LeadsPiper.com — Zapier Integration (CLI)

> Get the most of your leads with the help of your friendly bard.

This is the Zapier Platform **CLI** app that lets a Zap subscribe to platform object
events (Create / Update / Delete on any object type) and receive a signed webhook whenever
one fires. It is a thin client over the deployed `Zapier` service (`/zapier/v1`), which owns
all catalog/subscription/delivery logic (see [`../src/Zapier/README.md`](../src/Zapier/README.md)).

- **Module type:** CommonJS (`require` / `module.exports`).
- **Auth:** OAuth2 authorization-code flow against IdentityServer (`idp.fci.cloud`), requesting
  the `zapier` scope. End users log in with their own credentials — no shared API key.
- **Service base URL:** `https://rproxy-fci.fci.cloud`

---

## Prerequisites

```bash
npm install -g zapier-platform-cli
zapier-platform login        # uses your existing Zapier account
```

---

## 1. First-time setup (register + push + env)

`env:set` operates on a deployed version, so the app must exist server-side **before** you can
set env vars. Order matters:

```bash
# From the project root:
npm install

# Create the integration in your Zapier account and link this folder (writes .zapierapprc).
# If the app already exists in Zapier, use `zapier-platform link` instead of `register`.
zapier-platform register

# Upload version 1.0.0 so the version exists server-side.
zapier-platform push

# Now set the four env vars on that version:
zapier-platform env:set 1.0.0 BASE_URL=https://rproxy-fci.fci.cloud
zapier-platform env:set 1.0.0 IDP_URL=https://idp.fci.cloud
zapier-platform env:set 1.0.0 CLIENT_ID=YOUR_ZAPIER_CLIENT_ID
zapier-platform env:set 1.0.0 CLIENT_SECRET=YOUR_ZAPIER_CLIENT_SECRET
```

> The first `push` runs `validate` with the env vars still undefined. Validation makes no live
> HTTP calls, so it passes — but don't run `zapier-platform test` until after the `env:set`
> calls above, or requests will hit `undefined/zapier/v1/...`.

These resolve to:

| Purpose             | URL                                              |
| ------------------- | ------------------------------------------------ |
| Authorize           | `https://idp.fci.cloud/connect/authorize`        |
| Token / refresh     | `https://idp.fci.cloud/connect/token`            |
| Connection test     | `https://rproxy-fci.fci.cloud/zapier/v1/user`    |

Sanity-check the OIDC endpoints once: open
`https://idp.fci.cloud/.well-known/openid-configuration` and confirm `authorization_endpoint`
and `token_endpoint` end in `/connect/authorize` and `/connect/token`. If there's a path
prefix, adjust the URLs in `authentication.js`.

---

## 2. IdentityServer client (one-time, server side)

Register a client for Zapier in IdentityServer (`idp.fci.cloud`):

- **Grant types:** `authorization_code` + `refresh_token`
- **Allowed scopes:** `openid`, `profile`, `zapier`, `offline_access`
- **Redirect URI:** the exact value Zapier shows for this app — run `zapier-platform describe`
  (or look under **Authentication** in the Zapier UI). For CLI apps it looks like
  `https://zapier.com/dashboard/auth/oauth/return/App<ID>CLIAPI/`.
- **Client secret:** must match the `CLIENT_SECRET` env var above.
- The issued access token **must** carry the `zapier` scope **and** a Manager/Admin/Root role
  claim — otherwise the service's `zapier` policy rejects calls with `401` even though login
  succeeded.

---

## 3. Updating after a code change

As long as the version in `package.json` is unchanged, just:

```bash
zapier-platform validate
zapier-platform push
```

`push` overwrites the same version in place; env vars persist across pushes — no need to
re-register or re-set them.

- **Bumping the version** (e.g. `1.0.1`) creates a *new* version with **no** env vars; you'd
  re-run the four `env:set` commands for it. Push the same version to avoid that.
- **Existing connections don't relabel or re-auth on push.** `connectionLabel` is computed at
  connect time, so already-connected accounts keep their old label until reconnected.

---

## 4. Project structure

| File                        | Role                                                              |
| --------------------------- | ----------------------------------------------------------------- |
| `index.js`                  | App definition; global bearer header + 401→refresh middleware.    |
| `authentication.js`         | OAuth2 config (authorize / token / refresh), test, label.         |
| `triggers/objects.js`       | Dynamic dropdown source — `GET /objects`.                         |
| `triggers/events.js`        | Dynamic dropdown source (depends on object) — `GET /objects/{object}/events`. |
| `triggers/object_event.js`  | The REST Hook trigger (subscribe / unsubscribe / perform / list). |

### Key design notes / gotchas

- **Bearer header lives in `index.js`, not on the auth object.** Putting extra properties on
  the `authentication` export fails `validate` with
  *"is not allowed to have the additional property …"*. The request middleware goes in
  `beforeRequest` in `index.js`.
- **`connectionLabel` must read from `bundle.inputData`** (the test response), e.g.
  `(z, bundle) => bundle.inputData.name`. `{{json.name}}` does **not** interpolate here — `json`
  is only valid inside a request definition — and renders literally.
- **JSON casing:** if the `/user` response serializes as PascalCase (`{"Name":"…"}`), use
  `bundle.inputData.Name`. Verify with:
  ```bash
  curl -s https://rproxy-fci.fci.cloud/zapier/v1/user -H "Authorization: Bearer <token>" | head
  ```
- **`altersDynamicFields: true`** on the `object` input field is what refreshes the Event
  dropdown when the user changes the object.
- **`offline_access`** scope is required for Zapier to obtain a refresh token; `autoRefresh:
  true` + the `afterResponse` 401 handler keep connections alive when the access token expires.

---

## 5. Testing end-to-end

1. `zapier-platform validate` → passes.
2. Build a Zap on the **Object Event** trigger → **Connect** an account. You'll be redirected to
   `idp.fci.cloud` to log in and consent to the `zapier` scope. The connection label shows the
   user's name from `/user`.
3. Pick **Object** + **Event** from the dropdowns (sourced live from the service).
4. **Test trigger** pulls a sample envelope via `/samples`.
5. Turn the Zap **on** → a row appears in the `zapier.Subscription` Mongo collection
   (`performSubscribe`). Trigger a real Create/Update/Delete on that object → the webhook fires
   through to the Zap's action. Turning the Zap **off** calls `performUnsubscribe`.

### Delivered payload

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

Zap users map fields out of the `data` object.
