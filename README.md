# BootstrApp

A .NET monorepo of backend **microservices** for a multi-tenant business platform —
object/flow engine, identity, reporting, and a large fleet of third-party integrations
(Stripe, Slack, QuickBooks, Salesforce, SendGrid, Zapier, n8n, …). Every service is built
from the same shared foundation, so they look and deploy alike.

> New here? Start with [`CLAUDE.md`](CLAUDE.md) for the build/run commands, the service
> pattern, and the repo conventions.

## Layout

```
BootstrApp.sln              # the solution (~66 projects)
Directory.Build.props       # net10.0 for all projects
global.json                 # .NET SDK pin (rollForward latestMajor)
src/
  PI.Shared*                # shared libraries every service builds on
  <Service>/                # one folder per service (Program.cs + Controllers + …)
  <third-party>/            # git submodules (Handlebars.Net, ExcelDataReader, …)
tests/
  UnitTests/                # xUnit + FluentAssertions
  Ingress.Tests/
```

### Shared libraries (`src/PI.Shared*`)

| Project | Role |
| --- | --- |
| `PI.Shared` | Core models, contexts, flow events, extensions. |
| `PI.Shared.App` | `MicroserviceApp` host, `APIController`, auth/middleware/Swagger. |
| `PI.Shared.Data.Mongo` | MongoDB wiring (Crochik.Mongo `MongoConnection`). |
| `PI.Shared.Services` | `ObjectTypeService`, message-queue services, job runner. |
| `PI.Shared.Integrations` | Shared building blocks for outbound REST-Hook integrations (catalog, subscriptions, durable signed delivery). |
| `PI.Shared.Salesforce` / `PI.Shared.O365` / … | Vendor-specific shared code. |

### Services (`src/<Service>`)

Each service is an independent deployable that follows the same pattern (`Program :
MicroserviceApp`, `Dockerfile`, `kubernetes.ps1`, `tag.version`). Examples: `IDP`
(identity), `API`, `PI.Stripe`, `PI.Slack`, `PI.QuickBooks`, `PI.Salesforce`, `Zapier`,
`N8n`, `Ingress`.

## Getting started

```bash
# Submodules back several src/* third-party projects.
git submodule update --init --recursive

# Build everything (see CLAUDE.md for the one known pre-existing exception).
dotnet build BootstrApp.sln

# Or build/run a single service.
dotnet build src/Zapier/Zapier.csproj
dotnet run   --project src/Zapier/Zapier.csproj

# Tests
dotnet test tests/UnitTests
```

Services read most configuration from AWS Systems Manager (SSM) at runtime and run as a
**Web API** by default, or as a **background job** when `PI_RUN_JOB` is set. See
[`CLAUDE.md`](CLAUDE.md) for details.

## Subsystems worth knowing

- **Webhooks (inbound):** [`WEBHOOK.md`](WEBHOOK.md) — `Ingress` (`src/Ingress`) receives
  webhooks from many third parties through one dynamic, config-driven endpoint.
- **Integrations (outbound):** [`src/PI.Shared.Integrations`](src/PI.Shared.Integrations) —
  exposes platform objects/events to automation tools and delivers signed, durable,
  retried webhook POSTs. Consumed by [`src/Zapier`](src/Zapier/README.md),
  [`src/N8n`](src/N8n/README.md) and the generic [`src/Webhooks`](src/Webhooks/README.md)
  (any application can subscribe).
- **Logging:** [`ELK.md`](ELK.md).
