# TimeBase - Agent Guidelines

This document provides coding guidelines and commands for AI agents working in the TimeBase repository.

## Project Overview

TimeBase is an open-source, modular time series data provider service for financial data, built with:
- **.NET 10.0** (Core API and infrastructure)
- **Python 3.11+** (Provider SDK and data providers)
- **TimescaleDB** (PostgreSQL-based time series storage)
- **gRPC** (Communication protocol between core and providers)
- **Docker** (Containerization for core and providers)

**For complete architectural decisions and technical agreements, see [docs/AGREEMENTS.md](docs/AGREEMENTS.md)**

## Build Commands

### .NET Core

All .NET commands should be run from the `src/` directory:

```bash
# Restore dependencies
dotnet restore TimeBase.slnx

# Build the solution
dotnet build TimeBase.slnx

# Build in Release mode
dotnet build TimeBase.slnx --configuration Release

# Run the core application
cd TimeBase.Core
dotnet run

# Check code formatting
dotnet format --verify-no-changes TimeBase.slnx

# Apply code formatting
dotnet format TimeBase.slnx
```

### Testing

```bash
# Run all tests (requires Docker for integration tests)
cd src
dotnet test TimeBase.slnx

# Run unit tests only (no Docker required)
cd src
dotnet test TimeBase.slnx --filter "FullyQualifiedName!~Integration"

# Run tests with coverage
dotnet test TimeBase.slnx --collect:"XPlat Code Coverage" --results-directory ./TestResults

# Run tests in Release mode
dotnet test TimeBase.slnx --configuration Release --no-build --verbosity normal

# Run a single test project
dotnet test TimeBase.Core.Tests.Unit/TimeBase.Core.Tests.Unit.csproj

# Run a single test by name (using filter)
dotnet test --filter "FullyQualifiedName~ProviderRegistryTests.InstallProvider_CreatesNewProvider"

# Run tests by category/trait
dotnet test --filter "Category=Integration"
```

### Docker

```bash
# Build core Docker image
cd src
docker build -f docker/core/Dockerfile -t timebase/core:latest .

# Start development environment (database + core + providers)
cd src/docker
docker-compose -f docker-compose.dev.yml up --build

# Start only infrastructure (database + core)
cd src/docker
docker-compose up -d

# View logs
docker-compose logs -f

# Stop all services
docker-compose down

# Clean up (including volumes)
docker-compose down -v
```

### Python Provider SDK

```bash
# Install SDK for development
cd src/TimeBase.ProviderSdk
pip install -e .

# Run provider tests
python -m pytest

# Run a single test
python -m pytest tests/test_provider.py::test_specific_function
```

## Code Style Guidelines

### C# (.NET Core)

#### File Organization
- **Usings**: Place at the top, sorted alphabetically with `System` namespaces first
- **Namespace**: Use file-scoped namespaces (`namespace TimeBase.Core.Services;`)
- **One class per file** (unless nested/related classes)

#### Naming Conventions
- **Classes/Interfaces**: PascalCase (`ProviderRegistry`, `ITimeBaseMetrics`)
- **Methods/Properties**: PascalCase (`GetAllProvidersAsync`, `CreatedAt`)
- **Parameters/Local variables**: camelCase (`repositoryUrl`, `enabled`)
- **Private fields**: Prefix with underscore (`_logger`, `_dbContext`)
- **Private static fields**: Prefix with `s_` (`s_httpClient`)
- **Constants**: PascalCase (`MaxRetryCount`)
- **Interfaces**: Prefix with `I` (`IProviderRegistry`)
- **Type parameters**: Prefix with `T` (`TEntity`, `TResult`)

#### Type Preferences
- **Avoid `var`**: Use explicit types (`string name` not `var name`)
- **Nullable reference types**: Always enabled, use `?` for nullable types
- **Prefer language keywords**: Use `string` over `String`, `int` over `Int32`

#### Code Structure
- **Primary constructors**: Use for dependency injection (see `ProviderRegistry`)
- **Expression-bodied members**: Use for simple properties/accessors
- **Braces**: Always use braces for control flow (even single lines)
- **New lines**: Opening brace on new line (Allman style)
- **Async**: Suffix async methods with `Async` and return `Task` or `Task<T>`

#### Error Handling
- **Logging**: Use structured logging with ILogger (`logger.LogInformation("Installing {Slug}", slug)`)
- **Exceptions**: Let exceptions bubble up unless you can handle them meaningfully
- **Metrics**: Record operations with ITimeBaseMetrics for observability
- **Validation**: Use FluentValidation for request validation

#### Comments & Documentation
- **XML comments**: Use for public APIs (`/// <summary>`)
- **Inline comments**: Explain "why" not "what", sparingly
- **TODO comments**: Include issue number if applicable

#### Example Pattern
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TimeBase.Core.Services;

public class ExampleService(
    TimeBaseDbContext db,
    ILogger<ExampleService> logger)
{
    /// <summary>
    /// Gets data by ID with validation.
    /// </summary>
    public async Task<Data?> GetDataAsync(Guid id)
    {
        logger.LogInformation("Fetching data for {Id}", id);
        
        try
        {
            return await db.Data.FindAsync(id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch data {Id}", id);
            throw;
        }
    }
}
```

**See [src/.editorconfig](src/.editorconfig) for complete formatting rules.**

### Python (Provider SDK)

- **Style**: Follow PEP 8
- **Type hints**: Use type annotations for function parameters and returns
- **Async**: Use async/await for I/O operations
- **Naming**: snake_case for functions/variables, PascalCase for classes
- **Imports**: Grouped (standard library, third-party, local) with blank lines between
- **Docstrings**: Use for public functions and classes

## Project Architecture & Patterns

**For complete project structure and architecture details, see [docs/AGREEMENTS.md](docs/AGREEMENTS.md#18-project-structure--layering)**

### Layered Architecture (Clean Architecture)

**TimeBase.Core** (Application/API Layer)
- REST endpoints using Minimal APIs pattern
- Business logic services (ProviderRegistry, DataCoordinator)
- FluentValidation for request validation
- No direct database code

**TimeBase.Core.Infrastructure** (Data Access Layer)
- EF Core DbContext and entities
- Database migrations
- Infrastructure configuration

**TimeBase.Contracts** (Shared Protocols)
- gRPC protocol definitions (.proto files)

### Code Organization Patterns
- **Extension Methods**: Use for feature registration (`AddHealthChecks()`, `AddInfrastructure()`, `UseInfrastructure()`)
- **Fluent API**: Endpoint registration returns `IEndpointRouteBuilder` for chaining
- **Async by Default**: Use async methods throughout (`RunAsync`, `CloseAndFlushAsync`)
- **Clean Program.cs**: Keep entry point minimal and declarative

### Namespace Conventions
- Infrastructure: `TimeBase.Core.Infrastructure.*`
- Business services: `TimeBase.Core.Services`
- Health checks: `TimeBase.Core.Health`
- API endpoints: `TimeBase.Core`
- gRPC contracts: `TimeBase.Contracts`

## Testing Standards

- **Unit tests**: Use xUnit, FluentAssertions, and Moq
- **Integration tests**: Use WebApplicationFactory
- **Coverage**: Aim for 70%+ code coverage
- **Naming**: `MethodName_Scenario_ExpectedResult`
- **Arrange-Act-Assert**: Structure tests clearly

**For complete testing information, see [docs/CI-CD.md](docs/CI-CD.md)**

## Common Tasks

### Adding a New Endpoint
1. Define request/response models in `TimeBase.Core/Models/`
2. Add FluentValidation validators
3. Implement endpoint in `Endpoints.cs`
4. Add unit tests for validators
5. Add integration tests for the endpoint

### Database Changes
1. Modify entities in `TimeBase.Core.Infrastructure/Entities/`
2. Update `TimeBaseDbContext` if needed
3. Create migration: `dotnet ef migrations add MigrationName --project TimeBase.Core.Infrastructure --startup-project TimeBase.Core`
4. Apply migration: `dotnet ef database update --project TimeBase.Core.Infrastructure --startup-project TimeBase.Core`

### Adding a New Service
1. Create interface in `TimeBase.Core/Services/I{ServiceName}.cs`
2. Implement in `TimeBase.Core/Services/{ServiceName}.cs`
3. Register in `DependencyExtensions.cs`
4. Add unit tests in `TimeBase.Core.Tests.Unit/Services/`

## Important Notes

- **Never commit secrets**: Use user secrets for local development
- **Database**: Migrations are auto-applied on startup (see `Program.cs`)
- **.NET version**: Requires .NET 10.0 SDK
- **Docker**: Required for integration tests (Testcontainers with TimescaleDB)
- **Line endings**: CRLF (Windows-style) per `.editorconfig`
- **Indentation**: 4 spaces for C#, 2 spaces for XML/JSON
- **Test coverage target**: Minimum 70% code coverage
- **Logging**: Structured logging with Serilog, console output only
- **Metrics**: Custom business metrics via ITimeBaseMetrics interface

## Additional Resources

For detailed information, always consult these comprehensive documentation files:

- **[docs/AGREEMENTS.md](docs/AGREEMENTS.md)** - Complete technical decisions and architecture agreements
- **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** - System architecture and component details
- **[docs/CI-CD.md](docs/CI-CD.md)** - Build pipeline and test workflows
- **[docs/OBSERVABILITY.md](docs/OBSERVABILITY.md)** - Monitoring stack (OpenTelemetry, Prometheus, Jaeger, Grafana)
- **[docs/TESTING-LOCAL.md](docs/TESTING-LOCAL.md)** - Local development and testing guide
