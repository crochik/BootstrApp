# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

### Building and Running
```bash
# Build the application
dotnet build

# Build and publish for release
dotnet publish -c Release -o out

# Run the application locally
dotnet run

# Run in development mode
dotnet run --environment Development
```

### Docker Development
```bash
# Build Docker image
docker build -t crochik/langchain:latest .

# Run Docker container
docker run -p 8080:80 crochik/langchain:latest
```

### Deployment
```powershell
# Full build and deploy pipeline (uses PowerShell)
.\kubernetes.ps1

# With specific commit message
.\kubernetes.ps1 -message "Your commit message here"
```

## Architecture Overview

This is a .NET 8.0 Web API microservice called "LangChain" that provides AI assistant functionality using OpenAI integration. The application can run in two modes:

1. **WebAPI Mode**: Serves HTTP API endpoints for chat and assistant functionality
2. **Job Mode**: Runs as a background service for processing tasks

### Key Components

**Controllers**:
- `AbstractChatController`: Base controller for chat functionality with MongoDB and OpenAI integration
- `ApiChatController` & `ApiAssistantController`: API endpoints for chat and assistant features
- `ChatController` & `AssistantController`: Web controllers
- `TestController`: Testing endpoints at `/langchain/v1/test`

**Services**:
- `AssistantService`: Core assistant functionality with MongoDB integration
- `AssistantCompletionService`: Handles AI completion requests
- `DocumentTemplateService`: Manages document templates
- `OpenAIAssistantProvider`: OpenAI API integration implementation

**Data Layer**:
- Uses MongoDB via `MongoConnection` from the Crochik.Mongo library
- Shared models and services from `PI.Shared.*` libraries
- Entity-based authorization with roles (User, Manager, Admin, Root, Profile)

### Project Structure

The application inherits from `MicroserviceApp` base class and follows a microservice architecture pattern. It references several shared libraries:
- `PI.Shared.App`: Common application functionality
- `PI.Shared.Services`: Shared service implementations
- `Handlebars.Net`: Template processing

### Authentication & Authorization

Uses JWT-based authentication with IdentityServer4 integration. The "rest" policy requires:
- Valid JWT subject claim
- Specific role membership
- "rest" scope

### Configuration

- Primary config: `appsettings.json` and `appsettings.Development.json`
- Additional settings loaded from `/pi/settings/appsettings.json`
- Uses Serilog for logging with Elasticsearch integration

### Testing

No automated test suite is present. Testing is performed via API endpoints exposed by `TestController`.

### Build Process

1. **Version Management**: Uses date-based versioning (YYYYMMDD.increment) stored in `tag.version`
2. **Docker Build**: Multi-stage build targeting Linux AMD64 platform
3. **Git Integration**: Automatic commits and tagging via `kubernetes.ps1`
4. **Kubernetes Deployment**: Updates staging configuration and triggers deployment

### Development Notes

- The application uses dependency injection extensively
- MongoDB is the primary data store
- OpenAI API key and configuration required for AI functionality
- Can be run locally with `dotnet run` or in containers
- PowerShell script handles the complete CI/CD pipeline