# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview
This is a .NET 9.0 ASP.NET Core Web API for Kubernetes job and cron job management named PI.K8S. It provides REST endpoints to manage Kubernetes cron jobs and continuously monitors job completions in the background.

## Architecture
The application uses a layered architecture with:
- **Web API Layer**: ASP.NET Core controllers exposing REST endpoints
- **Service Layer**: `KubernetesService` for Kubernetes operations
- **Background Service**: `JobMonitorService` for continuous job monitoring
- **Kubernetes Client**: Official client for cluster communication

## API Endpoints

### CronJob Management
- `GET /api/cronjob` - Lists all cron jobs in a namespace
- `POST /api/cronjob/{cronJobName}/run` - Runs a cron job immediately
- `GET /api/cronjob/jobs` - Lists all jobs in a namespace

### Query Parameters
- `namespace` (optional) - Target Kubernetes namespace (defaults to "default")

## Core Services

### KubernetesService
- `GetCronJobsAsync()` - Lists all cron jobs in a namespace
- `RunCronJobImmediatelyAsync()` - Executes a cron job immediately with pause/resume logic
- `GetJobsAsync()` - Lists all jobs in a namespace
- `GetJobAsync()` - Gets a specific job

### JobMonitorService
- Background service that watches for completed jobs
- Collects pod logs and analyzes success/failure
- Runs continuously as a hosted service

## Key Features
- RESTful API for cron job management
- Background monitoring of all job completions
- Automatic pause/resume logic for concurrent execution prevention
- Pod log collection and success determination
- Swagger/OpenAPI documentation
- In-cluster Kubernetes authentication

## Common Commands

### Build and Run
```bash
dotnet build
dotnet run
```

### Development Commands
```bash
# Clean build artifacts
dotnet clean

# Restore NuGet packages
dotnet restore

# Run with specific configuration
dotnet run --configuration Debug
dotnet run --configuration Release
```

### API Testing
When running locally, Swagger UI is available at:
- https://localhost:5001/swagger (HTTPS)
- http://localhost:5000/swagger (HTTP)

### Kubernetes Deployment
The service is designed to run as a pod within the Kubernetes cluster using in-cluster configuration.

## Project Structure
- `PI.K8S.csproj` - Web API project file with dependencies
- `Program.cs` - API startup and configuration
- `Controllers/CronJobController.cs` - REST API endpoints
- `Services/KubernetesService.cs` - Kubernetes operations
- `Services/JobMonitorService.cs` - Background job monitoring
- `obj/` - Build artifacts directory (auto-generated)

## Dependencies
- `KubernetesClient` - Official Kubernetes API client
- `Microsoft.AspNetCore.OpenApi` - OpenAPI/Swagger support
- `Swashbuckle.AspNetCore` - Swagger UI implementation