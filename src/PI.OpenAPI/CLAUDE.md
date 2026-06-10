# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

PI.OpenAPI is a .NET microservice that parses OpenAPI specifications and converts them into the platform's internal object type and operation models. It can both import OpenAPI documents to create object types and generate OpenAPI specs from existing object types.

This service is part of a larger monorepo (SchedOnl.sln) containing multiple microservices and shared libraries.

## Architecture

### Core Components

**Parser Layer** (`Parser/OpenApiParser.cs`)
- Reads OpenAPI v3 documents (YAML/JSON) using Microsoft.OpenApi library
- Converts OpenAPI schemas into platform ObjectTypes with Fields
- Parses operations into Operation models with request/response definitions
- Handles schema references and resolves missing links between components
- Maps OpenAPI types to platform FormField types (TextField, NumberField, SelectField, etc.)

**Service Layer** (`Services/`)
- `ActionService`: Message queue consumer that executes OpenAPI operations as HTTP callouts
  - Handles authentication via integration tokens
  - Resolves parameters and request bodies using Handlebars expressions
  - Stores HTTP callouts to MongoDB for audit
  - Processes responses and creates objects from returned data
- `GitHubService`: Fetches OpenAPI specs from GitHub repositories
- `Jobs/ExportObjectTypesJob`: Batch job to export ObjectTypes to YAML format

**API Controllers** (`Controllers/`)
- `DocumentController`: Import OpenAPI documents and parse them into ObjectTypes/Operations
- `GenerateController`: Generate OpenAPI specs from existing ObjectTypes (generic, per-profile, or per-object)
- `ObjectTypeController`: Export ObjectTypes to YAML, manage database indices
- `RepositoryController`: Integration with GitHub for fetching OpenAPI documents

**Writer Layer** (`Writer/ObjectWriter.cs`)
- Custom writer for serializing OpenAPI components

### Key Models (from PI.Shared)

- `ObjectType`: Platform's schema definition with fields, permissions (RBAC), constraints
- `FieldTemplate`: Wrapper combining a FormField with field-level RBAC permissions
- `FormField`: Base type for all field types (TextField, ObjectField, ChildrenField, etc.)
- `Operation`: Represents an OpenAPI operation with request/response specifications
- `Schema`: Wrapper around ObjectType that includes raw OpenAPI representation
- `Document`: Represents an uploaded OpenAPI document with namespace and metadata

### Data Flow

**Import Flow:**
1. Upload OpenAPI spec → DocumentController creates Document record
2. Parser loads spec and converts schemas → ObjectTypes with Fields
3. Parser extracts operations → Operation records with parameters/payloads
4. Save ObjectTypes and Operations to MongoDB
5. ObjectTypes can then be used throughout the platform

**Generation Flow:**
1. GenerateController receives request for profile/system
2. OpenApiSpecGenerator queries ObjectTypes from MongoDB
3. Converts ObjectTypes/Fields back to OpenAPI schemas
4. Generates operations for user actions if requested
5. Returns OpenAPI v3 YAML document

**Execution Flow:**
1. FlowRun triggers OpenApiOperation action → ActionService receives message
2. Load Operation and Document from MongoDB
3. Resolve parameters/body from FlowRun context using expressions
4. Get integration auth token if needed
5. Execute HTTP request
6. Parse response and create objects in MongoDB
7. Update FlowRun with response data

## Dependencies

### Project References
- `PI.Shared.App`: Base microservice infrastructure, MicroserviceApp base class
- `PI.Shared.Services`: Core services (ObjectTypeService, RemoteFileService, etc.)
- `PI.Shared.Services.OpenApiGenerator`: OpenApiSpecGenerator for generating specs
- `PI.Shared.Services.FileStorage`: AWS S3 integration for file storage

### Key NuGet Packages
- `Octokit`: GitHub API client for repository operations

### Shared Libraries (from OpenAPI.NET submodule)
- Uses Microsoft.OpenApi via PI.Shared.Services.OpenApiGenerator package reference
- Located at `/workspace/OpenAPI.NET` in the monorepo

## Development Commands

### Build
The project is built as part of the monorepo solution. From the workspace root:
```bash
dotnet build SchedOnl.sln
```

Or build just this project:
```bash
dotnet build PI.OpenAPI/PI.OpenAPI.csproj
```

### Run
The service can run in two modes based on `Startup.IsWebApi` flag:

**API Mode** (default):
```bash
dotnet run --project PI.OpenAPI/PI.OpenAPI.csproj
```

**Job Mode** (runs ExportObjectTypesJob):
Set environment/config to disable web API mode.

### Configuration
- Settings loaded from `/pi/settings/appsettings.json` (Kubernetes mounted config)
- Uses Serilog with Elasticsearch logging in API mode
- Requires MongoDB connection configuration
- Requires RabbitMQ for message queue (ActionService)

## Important Patterns

### Field Type Mapping
When parsing OpenAPI schemas, the parser maps types to platform FormFields:
- `string` → TextField (or specialized: EmailField, UrlField, DateField, PasswordField)
- `string` with `enum` → SelectField or MultiSelectField (for arrays)
- `integer`/`number` → NumberField
- `boolean` → CheckboxField
- `array` of objects → ChildrenField with embedded ObjectType
- `object` → ObjectField (references another ObjectType)
- `object` with properties → Nested embedded ObjectType

### Reference Resolution
OpenAPI `$ref` references are resolved in two passes:
1. First pass: Build all schemas, track MissingRefs for unresolved references
2. Second pass: `ResolveMissingLinks()` resolves references after all components are parsed

### Namespace Convention
ObjectTypes created from OpenAPI schemas use namespace from the Document:
- Format: `{namespace}.{SchemaName}`
- Operation parameters: `{namespace}.operation.{operationId}.Parameters`
- Request bodies: `{namespace}.operation.{operationId}.Body`
- Responses: `{namespace}.operation.{operationId}.Response.{statusCode}`

### RBAC (Role-Based Access Control)
All ObjectTypes and Fields include RBAC permissions:
- Default: Account and Admin roles get Read, Create, Update
- Fields can be readonly (`ReadOnly` in OpenAPI) or writeonly (`WriteOnly`)
- Permissions: Read, Update, SetOnCreate

## Testing

Tests are located in the `/workspace/UnitTests` directory at the monorepo level.

## Deployment

The service runs in Kubernetes. Deployment scripts:
- `kubernetes.ps1`: PowerShell deployment script
- `tag.version`: Version tag file
- Uses Docker multi-stage build (see `Dockerfile`)

## Database Collections

### MongoDB Collections Used
- `ObjectType.1`: Platform object type definitions
- `Schema`: OpenAPI schemas with raw definitions
- `Operation`: OpenAPI operations
- `Document`: Uploaded OpenAPI documents
- `RemoteFile`: File metadata for uploaded specs
- `FlowRun`: Workflow execution context
- `HttpCallOut`: HTTP request/response audit logs
- `Integration`: Integration definitions
- `IntegrationConfigurationWithToken`: Per-entity integration credentials
