# Plan: Require auth on `/register` and advertise it in OAuth metadata

## Context

The RFC 7591 Dynamic Client Registration endpoint at `POST /register` is currently **open** — any unauthenticated caller can register a client. The existing OAuth metadata document (`GET /.well-known/oauth-authorization-server`) advertises the endpoint URL but says nothing about authentication.

Two changes are needed:

1. **Require** an Initial Access Token (IAT) on `POST /register`, validated against the same OIDC IdP that already gates `/mcp`.
2. **Advertise** the requirement so clients (and humans) can discover it without first making a failed call. Two complementary mechanisms — the discoverable metadata field and the 401 challenge.

The existing infrastructure makes both cheap: JwtBearer is already configured and the `OnChallenge` event in [AbstractMCPServer.cs:122-141](AbstractMCPServer.cs#L122-L141) already produces an RFC 9728-compliant `WWW-Authenticate` header. Once `/register` calls `ChallengeAsync(...)`, that path lights up automatically.

## Approach

**Token type:** Reuse the existing JwtBearer scheme — any valid OIDC access token from the configured IdP gates `/register`. This matches the project's existing pattern (manual `AuthenticateAsync` + `ChallengeAsync`, no `[Authorize]` attribute) used in [Controllers/McpController.cs:57-62](Controllers/McpController.cs#L57-L62). A future `dcr:register` scope check is an additive follow-up if a tighter trust boundary is needed.

**Advertising:** Both mechanisms, with the 401 challenge being load-bearing for MCP clients:

- The existing `OnChallenge` event will run automatically once `Register` calls `ChallengeAsync(JwtBearerDefaults.AuthenticationScheme)`. Inspector / Claude Desktop / Claude Code already consume the resulting `WWW-Authenticate: Bearer realm="mcp", ..., resource_metadata="..."` header.
- Add a non-standard but commonly used `registration_endpoint_auth_methods_supported: ["bearer"]` field to the OAuth metadata. RFC 8414 §2 explicitly allows additional fields; ignored by clients that don't recognize it; useful as a static hint for humans, test suites, and any tooling that prefers discovery over a round-trip 401.

## Changes

### 1. [Controllers/McpController.cs:214-242](Controllers/McpController.cs#L214-L242) — `Register` action

Insert the project's manual-auth pattern at the top of the action, before `oidcConfigService.RegisterClientAsync(...)`:

```csharp
var authResult = await HttpContext.AuthenticateAsync();
if (!authResult.Succeeded)
{
    await HttpContext.ChallengeAsync(JwtBearerDefaults.AuthenticationScheme);
    return new EmptyResult();
}
HttpContext.User = authResult.Principal;
```

Mirrors [HandleSsePost lines 57-62 / 95 / 150](Controllers/McpController.cs#L57-L62). `EmptyResult()` is required (not `Unauthorized()`) because `OnChallenge` calls `HandleResponse()` and writes the status/headers itself; returning a result with content would let MVC overwrite the carefully built `WWW-Authenticate` body.

Leave the rest of the action untouched.

### 2. [Services/OidcConfigurationService.cs:184-194](Services/OidcConfigurationService.cs#L184-L194) — drop dead IAT comment

Delete the commented-out IAT block (and its `// 1. Initial Access Token (IAT) Check` heading). The check now lives in the controller, matching the project's convention. The comment as written references `Request.Headers` (a controller-only symbol) and would not compile if uncommented in a service.

### 3. [Models/McpModels.cs:419-456](Models/McpModels.cs#L419-L456) — `OAuthServerMetadata`

Add one nullable property after `RegistrationEndpoint` (after line 446):

```csharp
[JsonPropertyName("registration_endpoint_auth_methods_supported")]
public string[]? RegistrationEndpointAuthMethodsSupported { get; set; }
```

The global JSON config (`DefaultIgnoreCondition = WhenWritingNull` at [AbstractMCPServer.cs:34](AbstractMCPServer.cs#L34)) means leaving it `null` cleanly omits the field — handy if a future config flag wants to switch back to open registration.

### 4. [Services/OidcConfigurationService.cs:120-155](Services/OidcConfigurationService.cs#L120-L155) — `GetOAuthMetadataAsync`

In the `OAuthServerMetadata` initializer, add:

```csharp
RegistrationEndpointAuthMethodsSupported = new[] { "bearer" },
```

## Verification

Assume server at `http://localhost:5000` and a valid IdP-issued bearer token in `$TOKEN`.

**Unauthenticated → 401 + RFC 9728 challenge:**

```bash
curl -i -X POST http://localhost:5000/register \
  -H 'Content-Type: application/json' \
  -d '{"redirect_uris":["http://localhost:9999/cb"],"client_name":"verify"}'
```

Expect `401`, and `WWW-Authenticate: Bearer realm="mcp", error="invalid_token", resource_metadata="http://localhost:5000/.well-known/oauth-protected-resource/mcp"`.

**Bad token → 401:**

```bash
curl -i -X POST http://localhost:5000/register \
  -H 'Authorization: Bearer not-a-real-token' \
  -H 'Content-Type: application/json' \
  -d '{"redirect_uris":["http://localhost:9999/cb"],"client_name":"verify"}'
```

Expect `401` with `error_description` set (confirms JWT validation actually ran, not just a missing-header rejection).

**Valid token → 201:**

```bash
curl -i -X POST http://localhost:5000/register \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"redirect_uris":["http://localhost:9999/cb"],"client_name":"verify"}'
```

Expect `201 Created`, `Location:` header, JSON body with `client_id`, `client_secret`, `registration_access_token`.

**Metadata advertises the requirement:**

```bash
curl -s http://localhost:5000/.well-known/oauth-authorization-server | jq .
```

Confirm response includes:

```json
"registration_endpoint": "http://localhost:5000/register",
"registration_endpoint_auth_methods_supported": ["bearer"]
```

## Critical files

- [Controllers/McpController.cs](Controllers/McpController.cs) — `Register` action gets manual auth check
- [Services/OidcConfigurationService.cs](Services/OidcConfigurationService.cs) — drop dead IAT block; populate new metadata field in `GetOAuthMetadataAsync`
- [Models/McpModels.cs](Models/McpModels.cs) — add `RegistrationEndpointAuthMethodsSupported` to `OAuthServerMetadata`
- [AbstractMCPServer.cs](AbstractMCPServer.cs) — **no edits**; existing `OnChallenge` is what makes the 401 path work

## Notes / follow-ups

- **Trust boundary:** Reusing the user JwtBearer scheme means any user with a valid IdP access token can register a client. If that's too permissive, the additive next step is a `dcr:register` scope check after `AuthenticateAsync` succeeds — does not change discovery semantics, does not require new infrastructure.
- **Non-standard field:** `registration_endpoint_auth_methods_supported` has no IANA registration. Inspector/Claude clients will ignore it; its audience is humans and internal tooling. If you don't see value, drop changes 3 and 4 — the 401+`WWW-Authenticate` path is by itself RFC-compliant and is what MCP clients react to.
- **Hardcoded client credentials** (`"mcp_inspector"` / `"claude code"` at [Services/OidcConfigurationService.cs:212,222](Services/OidcConfigurationService.cs#L212)) are unrelated to this change but every successful registration still overwrites the same record — worth a separate ticket.
