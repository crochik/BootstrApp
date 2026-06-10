# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

This is a **C# .NET 8 library project** that provides an MCP (Model Context Protocol) server framework with OIDC-only authentication. It is **not a runnable application** — there is no `Program.cs` or `appsettings.json` here. Concrete MCP servers consume this library by:

1. Subclassing [AbstractMCPServer.cs](AbstractMCPServer.cs) (which extends `MicroserviceApp` from `PI.Shared.App`).
2. Calling `services.AddMcpTools(tools => tools.AddToolType<MyTools>())` in their own `AddServices` override to register tool sources.
3. Providing their own `Program.cs`, `appsettings.json`, and tool classes.

The library validates JWT tokens via the configured OIDC provider's JWKS and exposes MCP tools over HTTP/JSON-RPC at `POST /mcp`. [MCP.csproj](MCP.csproj) project-references sibling projects `PI.Shared.App` and `PI.Shared.Services` (not in this workspace).

## Build

```bash
dotnet build
```

There is no `dotnet run` for this library — runtime testing happens in the consuming app.

## Architecture

### Entry point: AbstractMCPServer

[AbstractMCPServer.cs](AbstractMCPServer.cs) is an abstract base class. Its `AddServices` override:
- Registers Mongo (`AddMongoConnection` / `AddMongoAdapters`), Swagger, controllers (camelCase JSON, ignore-null-on-write), `IObjectTypeService`, and `IDataProtectionServiceProvider`.
- Registers `IOidcConfigurationService` and `IMcpProtocolHandler` as singletons.
- **Synchronously fetches the OIDC discovery doc and JWKS at startup** in `AddAuthentication`, then closes over the parsed `JsonWebKeySet` in `IssuerSigningKeyResolver`. JWKS is therefore cached for the process lifetime — restart required to pick up key rotation.

Subclasses must register their own tool sources via `AddMcpTools(...)`. The base does **not** register a default tool source.

### Request flow

```
POST /mcp
  → McpController.HandleSsePost()
    → HttpContext.AuthenticateAsync()   [JwtBearer, optional — no [Authorize] attribute]
    → initialize:           handled inline (returns protocolVersion 2025-03-26)
    → notifications/*:      returns 202 Accepted
    → all other methods:    McpProtocolHandler.HandleRequestAsync(context, request)
        → tools/list  → IToolMetadataService.GetAvailableToolsAsync()
        → tools/call  → IToolMetadataService.GetToolMetadataAsync() + IToolExecutionService.ExecuteToolAsync()
        → ping        → empty object
        → initialize  → also handled here (returns 2024-11-05; dead in normal flow because controller short-circuits)
```

After `AuthenticateAsync()` succeeds, `HttpContext.User` is set and the controller pulls `IContextWithActor` (from `PI.Shared.Models`) via `HttpContext.GetContextWithActor()` and passes it downstream as `IEntityContext?`. Tools with `RequiresAuthentication = true` get an auth-error result if context is null.

### Tool system: pluggable `IToolSource` composite

The canonical entry points (`IToolMetadataService` / `IToolExecutionService`) are implemented by [CompositeToolMetadataService.cs](Services/CompositeToolMetadataService.cs) and [CompositeToolExecutionService.cs](Services/CompositeToolExecutionService.cs). They aggregate over **all registered `IToolSource` instances** ([Services/IToolSource.cs](Services/IToolSource.cs)):

- **Metadata:** first-registration-wins on duplicate tool names; case-insensitive comparison.
- **Execution:** each source's `TryExecuteAsync` returns `null` if it doesn't own the tool, allowing the composite to fall through to the next source.
- **Order matters:** `IEnumerable<IToolSource>` is iterated in DI registration order.

Tool sources are registered via [McpToolsServiceExtensions.cs](Services/McpToolsServiceExtensions.cs):

```csharp
services.AddMcpTools(tools =>
{
    tools.AddToolType<UserProfileTools>();      // [McpTool]-decorated class (Transient in DI)
    tools.AddToolSource<DatabaseToolSource>();  // custom IToolSource (Singleton)
});
```

`AddToolType<T>()` only registers the shared `AttributeToolSource` if at least one type was added.

### Attribute-based tools

[Services/AttributeToolSource.cs](Services/AttributeToolSource.cs) discovers methods at startup via [Tools/Registry/ToolRegistry.cs](Tools/Registry/ToolRegistry.cs):

- `[McpTool]` ([Tools/Attributes/McpToolAttribute.cs](Tools/Attributes/McpToolAttribute.cs)) on a public instance method exposes it. `Name` defaults to the method's `snake_case`. `RequiresAuthentication` defaults to **true** (secure by default). Optional `ExamplePrompts`.
- `[McpParameter]` ([Tools/Attributes/McpParameterAttribute.cs](Tools/Attributes/McpParameterAttribute.cs)) sets parameter `Description` and `Required` (defaults `false`; the registry also marks a parameter required if it isn't `IsOptional` and isn't a nullable reference type, per `NullabilityInfoContext`).
- A parameter typed as `IEntityContext` (or subtype) is auto-bound to the request context and is **excluded** from the tool's JSON schema.
- JSON schema types map via `MapClrTypeToJsonType` (string/bool/number/array/object; `DateTime`/`DateTimeOffset` → `string` with `format: date-time`).
- Args are deserialized from `JsonElement` to the target CLR type, or coerced via `Convert.ChangeType` otherwise.
- Return values: `string` → text content; `ToolCallResult` → returned verbatim; `null` → "(no result)"; anything else → JSON-serialized to text content.

### Disabled / legacy code

These exist in the tree but are **not wired into DI** and should not be modified without intent:

- [Services/ToolExecutionService.cs](Services/ToolExecutionService.cs) — original hand-coded `switch` execution service (predates `IToolSource`). The composite path replaces it.
- [Services/AuthenticationService.cs](Services/AuthenticationService.cs) / [Services/IAuthenticationService.cs](Services/IAuthenticationService.cs) — pre-OIDC local JWT auth.
- [Middleware/TokenValidationMiddleware.cs](Middleware/TokenValidationMiddleware.cs) — superseded by `HttpContext.AuthenticateAsync()` in the controller.

## Configuration (consumer-supplied)

The consumer's `appsettings.json` must provide:
- `Oidc:DiscoveryUrl` — OIDC provider discovery URL (e.g. `https://idp.fci.cloud/.well-known/openid-configuration`). **Required.** Read both by `AbstractMCPServer.AddAuthentication` (synchronously at startup) and by `OidcConfigurationService` (with a 60-min TTL cache for the `/.well-known/oauth-authorization-server` proxy).

Endpoints exposed on the consuming app:
- `POST /mcp` — JSON-RPC entrypoint (controller is at route `mcp`).
- `POST /register` — RFC 7591 Dynamic Client Registration.
- `GET /.well-known/oauth-authorization-server` — proxies the OIDC config but rewrites `registration_endpoint` to local `/register` and overrides `grant_types_supported` / `scopes_supported`. Falls back to a local stub if OIDC is unreachable.
- `GET /.well-known/oauth-protected-resource` — RFC 9728 protected-resource metadata.

## Adding a New Tool (preferred attribute path)

1. Create or pick a tool class. Make it Transient-DI-friendly (constructor injection works).
2. Decorate a public method with `[McpTool(Description = "...", RequiresAuthentication = true)]`. Optionally annotate parameters with `[McpParameter(Description = "...", Required = true)]`. Add an `IEntityContext` parameter to receive the caller context.
3. In the consumer's `AddServices` override: `services.AddMcpTools(t => t.AddToolType<MyTools>());`.

For non-attribute sources (e.g., DB-driven tools), implement [IToolSource](Services/IToolSource.cs) and register via `tools.AddToolSource<T>()`.

## Key Quirks & Gotchas

- **Two `initialize` handlers:** the controller short-circuits with protocol version `2025-03-26`; the protocol handler's `2024-11-05` branch is dead in normal flow.
- **Static client credentials in `/register`:** `client_id = "mcp_inspector"`, `client_secret = "claude code"`. Placeholder — replace before any non-dev use.
- **No audience validation** (`ValidateAudience = false`); 5-minute clock skew tolerance.
- **JWKS not refreshed at runtime:** keys load once during `AddAuthentication` and reuse via `IssuerSigningKeyResolver`. Key rotation requires a process restart.
- **Tool list is recomputed on every `tools/list` request** — sources can vary their output per-call (tenant-aware, DB-backed, etc.).
- **Composite source order matters:** the first source whose `TryExecuteAsync` returns non-null wins for execution; the first source listing a name wins for metadata.
