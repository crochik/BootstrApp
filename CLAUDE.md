# CLAUDE.md

Guidance for working in this repo. It's a .NET monorepo of backend microservices that
share a common host and libraries. See [`README.md`](README.md) for the high-level map.

## Build / run / test

```bash
git submodule update --init --recursive        # several src/* projects are submodules
dotnet build BootstrApp.sln                     # whole solution
dotnet build src/<Service>/<Service>.csproj     # one project (faster; prefer this while iterating)
dotnet run   --project src/<Service>/<Service>.csproj
dotnet test  tests/UnitTests                     # xUnit + FluentAssertions
```

- **Target framework is `net10.0`** for every project, set centrally in
  `Directory.Build.props` (`global.json` pins the SDK with `rollForward: latestMajor`).
- `TreatWarningsAsErrors` is **false** — warnings are fine; don't churn code to silence them.
- **Known pre-existing breakage:** `dotnet build BootstrApp.sln` fails to restore
  `src/PI.QuickBooks` because its `NuGet.config` points at a local path on the original
  author's machine (`/Users/felipe/...`). This is unrelated to most work — build the
  specific project(s) you touched instead of the whole solution to verify changes.

## The service pattern (follow it for every service)

A service is `src/<Name>/` with `Program.cs`, `<Name>.csproj`, `Dockerfile`,
`kubernetes.ps1`, `tag.version`, `appsettings.json`, and `Controllers/` (+ `Services/`,
`Models/`). `Program` derives from `MicroserviceApp` (in `PI.Shared.App`):

```csharp
public class Program : MicroserviceApp
{
    protected override string Name => "MyService";

    public static async Task<int> Main(string[] args) { /* Serilog + IsWebApi ? RunWebApplication : RunJob */ }

    protected override void AddServices(IServiceCollection services)
    {
        base.AddServices(services);
        services.AddObjectTypeService();              // register your dependencies here
        AddLifetimeService<MyBackgroundService>(services); // long-running ILifetimeService consumers
    }

    protected override void AddPolicies(AuthorizationOptions options)
    {
        base.AddPolicies(options);                    // adds default/manager/admin/... policies
        options.AddPolicy("myscope", p => p.RequireRole(...).RequireScope("myscope"));
    }
}
```

- **Web API vs job:** the same binary runs as a Web API by default, or as a background job
  when the `PI_RUN_JOB` env var is set (`IsWebApi` is false). Jobs add a `JobService` hosted
  service.
- **Config:** `Configuration` (protected on `MicroserviceApp`) is available in `AddServices`.
  `appsettings.json` is usually minimal — real config comes from **AWS SSM** at runtime,
  keyed by `PI_ENVIRONMENT`. Put only safe defaults (e.g. message-queue names) in
  `appsettings.json`.
- **Controllers** derive from `APIController` (`PI.Shared.Controllers`); `Context`
  (`IContextWithActor`) gives `AccountId`/`UserId`/`OrganizationId`/`ProfileId`/`Role`.
  Guard with `[Authorize("<policy>")]` and route under `/<service>/v1/...`.
- **Deployment:** `kubernetes.ps1` bumps `tag.version`, publishes, builds/pushes the Docker
  image, and updates the OPS releases manifest.

## Data & messaging (Crochik.*)

- **MongoDB:** inject `MongoConnection` (singleton). Models carry
  `[BsonCollection("name")]` and `[BsonId]`. Query with the fluent API:
  `_connection.Filter<T>().Eq(x => x.AccountId, id).FirstOrDefaultAsync()`; updates via
  `.Update.Set(...).Inc(...).Push(...).UpdateAndGetOneAsync()` (atomic find-and-update).
- **RabbitMQ:** inject `IMessageBroker`. Background consumers derive from
  `AbstractMessageQueueService` + `ILifetimeService`, read their `QueueConfig` from the
  config section named after the class, `Bind` routing keys in `Init`, and handle messages
  in `OnMessageAsync`. Publish with `broker.PublishAsync(topic, IMessageBody)` or, for flow
  events, `broker.DispatchAsync(evt, route)`.
- **Object events** are routed `object.{ObjectType}.{id}.{action}` (action =
  create/update/delete); bind `object.#` to receive all of them. `ObjectTypeService` reads
  the account's object types and flattens objects (`GetFlatObjectAsync`).

## Auth

JWT bearer (IdentityServer). Policies are added in `AddPolicies`; common ones come from the
base (`default`, `rest`, `manager`, `managerplus`, `admin`). A service-specific policy
combines `RequireRole(...)` + `RequireScope("...")` (the `RequireScope` extension lives in
`Microsoft.AspNetCore.Authorization`, available transitively via `PI.Shared.App`).

## Conventions

- **Explicit `using`s** are the norm (most projects do **not** enable `ImplicitUsings`).
  A few newer libraries opt into `ImplicitUsings`/`Nullable` in their own `.csproj` — match
  the project you're editing.
- Match the surrounding file's style (naming, comment density, Newtonsoft vs System.Text.Json
  — the platform leans Newtonsoft).
- `src/Handlebars.Net`, `src/ExcelDataReader`, `src/CometD`, `src/NetCoreForce`,
  `src/IdentityServer4.AccessTokenValidation`, `src/TrustedForm`, `src/script-parser` are
  **git submodules** — don't edit them as if they were first-party.

## Outbound integrations (Zapier / n8n)

`src/PI.Shared.Integrations` is the shared engine: an `ObjectType`-driven catalog,
Mongo-persisted REST-Hook subscriptions, and a durable signed-delivery pipeline
(`WebhookEventListener` → outbox → `WebhookDeliveryWorker` + `WebhookOutboxReconciler`,
HMAC-signed). `src/Zapier`, `src/N8n` and the generic `src/Webhooks` are thin platform-shaped
adapters over it. To add another integration, copy that shape: a service with its
`[Authorize]` policy, a
`<Name>Subscription : IntegrationSubscription` model, controllers, and
`AddIntegrationServices<TSubscription>(Configuration)` + the three lifetime services. See
each service's `README.md` for the platform-side setup.
