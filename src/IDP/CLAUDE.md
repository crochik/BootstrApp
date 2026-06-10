# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

`IDP` is the OpenID Connect / OAuth 2.0 identity provider service for the PI platform. It is an ASP.NET Core web app built on top of a vendored copy of IdentityServer4, backed by MongoDB, and integrated with the wider PI codebase via two sibling project references (`PI.Shared.Services`, `PI.Shared.O365`) located one directory up from this repo. The service handles interactive login (cookies + Razor views), external social/federated login (Microsoft, Google, GitHub, Salesforce, Okta, Typeform), magic-code login, impersonation, and a small set of API controllers protected by JWT bearer tokens it issues itself.

## Build / Run / Publish

This is a .NET project — there are no test, lint, or formatter commands wired up in this repo.

- Build: `dotnet build`
- Run locally: `dotnet run` (uses [Properties/launchSettings.json](Properties/launchSettings.json); HTTPS on `https://localhost:5000`, HTTP on `http://localhost:5004` per [appsettings.Development.json](appsettings.Development.json))
- Publish for container: `dotnet publish -c Release -o out -r linux-x64 --self-contained` (the [Dockerfile](Dockerfile) just `COPY ./out .`)
- Full publish + docker push + ops repo update: [kubernetes.ps1](kubernetes.ps1) (PowerShell, also tags git and edits `../../OPS/staging/config/releases.yaml`)

The [Dockerfile](Dockerfile) targets `mcr.microsoft.com/dotnet/aspnet:10.0.2`; the .csproj does not pin a TargetFramework (inherits from SDK). Sibling projects `../PI.Shared.Services` and `../PI.Shared.O365` must exist on disk for `dotnet build` to succeed.

In production the app additionally loads `/pi/settings/appsettings.json` (see [Program.cs:50](Program.cs#L50)) — overrides come from the cluster, not the repo.

## Architecture

### Composition root

[Program.cs](Program.cs) extends `MicroserviceApp` from `PI.Shared` and wires everything in `AddServices` / `Use`. The order of middleware is intentional:

`UseForwardedHeaders` → `UseStaticFiles` → `UseRouting` → `UseIdentityServer` → `UseAuthentication` → `UseAuthorization` → endpoints. `UseIdentityServer` runs **before** `UseAuthentication` because IdentityServer manages its own protocol endpoints; the subsequent auth middleware is only there so the API controllers (e.g. [IntegrationController](Controllers/IntegrationController.cs), [MagicCodeController](Controllers/MagicCodeController.cs)) can validate PI bearer tokens issued by this same IDP.

### Three authentication schemes (defined in [Consts.cs](Consts.cs))

- `PI` (`DefaultAuthenticationScheme`) — the primary cookie scheme; tickets are persisted in MongoDB via [MongoCacheTicketStore](MongoCacheTicketStore.cs) so cookies stay small (avoids the "headersize-issue-with-identityserver4" problem; see comment at [Extensions/ServicesExtensions.cs:33](Extensions/ServicesExtensions.cs#L33)).
- `External` (`ExternalCookieAuthenticationScheme`) — short-lived cookie used as the `SignInScheme` for every external provider; cleared at the start of `/account/login`.
- `Anonymous` — JWT bearer scheme used by clients that issue tokens with `sub == Guid.Empty`; [ProfileService.IsActiveAsync](Services/ProfileService.cs#L40) treats `Guid.Empty` as always active.

All three are configured in [Extensions/ServicesExtensions.cs](Extensions/ServicesExtensions.cs). External providers are read from `Authentication:<Provider>` config sections — if a section is missing, the corresponding `Add*` extension is a no-op, so providers can be enabled per-environment by config alone.

`JwtSecurityTokenHandler.DefaultInboundClaimTypeMap` is cleared and `DefaultMapInboundClaims` is set to `false` so claims pass through unmapped — necessary for IdentityServer4 to see the original `sub`/`name`/etc.

### IdentityServer4 stores → MongoDB

The four IdentityServer extensibility points are implemented as Mongo-backed stores in [Stores/](Stores/) and a profile/cors service in [Services/](Services/):

- [ClientStore](Stores/ClientStore.cs) — loads `PI.Shared.Models.AppClient` and AutoMaps to IdentityServer's `Client`.
- [ResourceStore](Stores/ResourceStore.cs) — API + identity resources.
- [PersistedGrantStore](Stores/PersistedGrantStore.cs) — auth codes, refresh tokens, consents.
- [CorsPolicyService](Services/CorsPolicyService.cs) — per-client CORS (the app deliberately does **not** call `app.UseCors` in [Program.cs:155](Program.cs#L155); IdentityServer applies CORS via this service).
- [ProfileService](Services/ProfileService.cs) — issues claims into tokens and gates `IsActive`.

AutoMapper profiles for these conversions live in [AutoMapper/](AutoMapper/).

Signing credentials: when `dataprotection.UseAWS` is true, the RSA private key is fetched from AWS SSM Parameter Store and registered via `AddSigningCredential`; otherwise `AddDeveloperSigningCredential` is used with a local `<name>.rsa` file ([Program.cs:122-136](Program.cs#L122-L136)).

### Vendored IdentityServer4

[IdentityServer4/](IdentityServer4/) is a vendored fork of the IdentityServer4 source (the upstream project was sunset). Its `.csproj` targets `netcoreapp3.1` and is **not** referenced by `IDP.csproj` — instead, IDP.csproj selectively `<Compile Remove>`s a handful of files from this directory so the rest are built directly into the IDP assembly. When changing identity-server internals, edit files in [IdentityServer4/](IdentityServer4/) directly; the package references at the top of [IDP.csproj](IDP.csproj) are commented out for this reason.

### Login flow & user lifecycle ([Services/LoginService.cs](Services/LoginService.cs))

This is the central piece — read it before changing anything in `Account*` controllers or identity providers. The flow inside `LoginUserAsync`:

1. Look up `AppClient` by `clientId` and verify the `loginInfo.LoginProvider` is configured for it.
2. Resolve the user in three escalating queries inside `GetUserAsync`:
   - exact match on `(IdentityProviderId, ExternalId)` already having a profile for this client;
   - **invitation**: a placeholder user with the same email, `IsActive=false`, `Identities=null`, and an `AppProfiles[profileKey]` entry — accepted automatically and activated;
   - fallback by identity alone (will trigger client-side auto-provisioning later).
3. If no user found → `autoProvisionUserAsync`: requires the provider's `Tenants` map to contain the user's tenant (or `"*"`), and `tenant.AutoProvisionUser.UserRole` to be set; new `Manager` role users also get an auto-provisioned `Organization`.
4. If user has no profile entry for the client → `autoProvisionClientAsync`: tries tenant-specific then client-default `AppProfiles[role]`.
5. Impersonation: an `acr_values` entry of the form `impersonate:<guid>` swaps the user; only allowed when the impersonator's role is strictly above the target's (see `FindAsync` switch on `EntityRoleId`). The original user is returned as `Impersonator`.

Invitation contract (mirrors [README.md](README.md)): a user with `IsActive=false`, `Identities=null`, and `AppProfiles[profileKey] != null` will be auto-activated on first login matching the invited email — this works **without** auto-provisioning being enabled for the client.

### Identity providers ([Services/](Services/))

`IIdentityProvider` is implemented by Microsoft / Google / GitHub / Salesforce providers (registered as multi-instance singletons in [Program.cs:69-75](Program.cs#L69-L75)). [LoginService](Services/LoginService.cs#L45) keys them by `Name`, which **must match the ASP.NET authentication scheme name** registered in [Extensions/ServicesExtensions.cs](Extensions/ServicesExtensions.cs) (`"Microsoft"`, `"Google"`, `"GitHub"`, `"Salesforce"`). Adding a new external provider means: add the `Add*` extension wiring the scheme, add an `IIdentityProvider` implementation with the matching `Name`, register it in `Program.cs`.

### Extension grants on `/connect/token`

API/SPA/mobile callers that want a JWT without a browser round-trip can use IS4 extension grants. Two are wired up today:

- `grant_type=passwordless` — [PasswordlessGrantValidator](Services/PasswordlessGrantValidator.cs) delegates to [PasswordlessService](Services/PasswordlessService.cs); two-step flow `POST /passwordless/start` (emit PIN via [IPasswordlessNotificationService](Services/IPasswordlessNotificationService.cs)) then `POST /connect/token` with `pin` + `code_verifier`.
- `grant_type=magic_code` — [MagicCodeGrantValidator](Services/MagicCodeGrantValidator.cs) reuses the existing [MagicCodeService.GetAndValidateAsync](Services/MagicCodeService.cs) pipeline; same codes as the browser `/account/login` flow.

To add a new grant: implement `IExtensionGrantValidator` with a unique `GrantType` string, register via `.AddExtensionGrantValidator<T>()` in `AddIdentityServer` ([Program.cs:117-124](Program.cs#L117-L124)). The validator only needs to set `subject` + `authenticationMethod`; IS4 mints the JWT through its normal pipeline (signing, scopes, lifetime, OAuth error envelope all reused). Token contents are determined by `ProfileService` (see next section), not by claims passed to `GrantValidationResult`.

### Claims pipeline ([ProfileService](Services/ProfileService.cs))

`ProfileService.GetProfileDataAsync` is the canonical place to add claims to issued JWTs. Custom claims passed to a `GrantValidationResult(...)` constructor live on the principal but **don't reach the token unless `ProfileService` emits them**. In particular [`AddStandardClaims`:102-105](Services/ProfileService.cs#L102-L105) unconditionally strips `preferred_username` and any claim with type starting with `"http"`, and [line 110](Services/ProfileService.cs#L110) overwrites `name` whenever the user has a `MainIdentity.ExternalIdentity`. New token claims (`pi_*` etc.) go in `AddStandardClaims` or `AuthorizationService.AddCalculatedClaimsAsync`.

### Views & static assets

The live login picker is the Razor view at [Views/Account/PickProvider.cshtml](Views/Account/PickProvider.cshtml), returned by [AccountController.PickProviderAsync](Controllers/AccountController.cs#L145). Razor views also cover Consent, LoggedOut, and shared layout/error. The static HTML in [wwwroot/](wwwroot/) (`index.html`, `loginerror.html`, `redirect.html`, `microsoft-success.html`, `microsoft-error.html`) is the older variant — `index.html` is no longer the active picker (a commented-out redirect at [AccountController.cs:82](Controllers/AccountController.cs#L82) shows the prior wiring). `endpoints.MapDefaultControllerRoute()` is in place so MVC works alongside attribute-routed controllers.

## Conventions worth knowing

- Newtonsoft.Json is the configured serializer (`AddNewtonsoftJson`) with `NullValueHandling.Ignore`.
- Logging is Serilog; controllers/services frequently use `_logger.AddScope(new { ... })` from `Crochik.Logging` to enrich a block of log lines.
- MongoDB access is via `Crochik.Mongo`'s fluent `Filter<T>()` API — chain `.Eq/.Ne/.In/.ElemMatchBuilder` then `.FirstOrDefaultAsync()`, or `.Update.Set(...).UpdateAndGetOneAsync()`. Don't reach for the raw driver. Comparison operators (`.Gt/.Gte/.Lt/.Lte`) are not used anywhere in this codebase — for range/inequality filtering, do the comparison in code after loading a narrow result set rather than introducing a new pattern.
- Mutations of `User`/`Account`/`Organization` should go through `ObjectTypeService` (`InsertAsync`, `FireObjectUpdatedAsync`) so audit/event side effects fire — do not write directly to those collections.
- **Per-client provider enablement is two flags, not one.** To turn on an external provider, magic-code, or passwordless flow for a given `AppClient` document in Mongo: (a) add the provider name as a key in `AuthenticationProviders` (service-level guards check this — e.g. [MagicCodeService.cs:42](Services/MagicCodeService.cs#L42), [PasswordlessService.cs:45](Services/PasswordlessService.cs#L45)) AND (b) for extension grants, include the grant-type value in `AllowedGrantTypes` (IS4 routes `/connect/token` requests via this list). Missing either silently fails.
- **`amr` (authentication method) values are part of the contract** with relying parties. The browser flow at [AccountController.GeneratePrincipal:281-297](Controllers/AccountController.cs#L281-L297) and the grant validators set distinct strings: `"MagicCode"`, `"passwordless"`, `"Microsoft"`, `"Google"`, `"GitHub"`, `"Salesforce"`. Keep these stable when changing flows.
- Swagger is registered only under `#if DEBUG` ([Program.cs:88-91](Program.cs#L88-L91), [Program.cs:176-178](Program.cs#L176-L178)).
- Health check is a single `GET /health` returning 200 ([Program.cs:186](Program.cs#L186)) — not the full `AddHealthChecks` pipeline (commented out).

## Things that look broken but aren't

- `IDP.csproj` has many commented-out `PackageReference` entries to IdentityServer4/IdentityModel — that's the vendored-fork pattern, not stale code.
- `app.UseCors(...)` is commented out in `Use` — CORS is intentionally delegated to `CorsPolicyService` via IdentityServer.
- `forwardedHeaderOptions.KnownNetworks/KnownProxies` are cleared so any upstream proxy is trusted — required behind the cluster's ingress; do not "fix" this without understanding the deployment.
- **MagicCode codes are not consumed on first use.** [MagicCodeService.GetAndValidateAsync](Services/MagicCodeService.cs#L16) does not flip `IsActive=false` after a successful redemption — the same code can be redeemed repeatedly (via the browser `/account/login` flow or the `magic_code` grant) until something externally deactivates the row. If single-use semantics are wanted, that's a deliberate change in `MagicCodeService` that affects both redemption channels equally.
