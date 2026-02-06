# TimeBase Technical Agreements

This document captures all technical decisions and agreements made during the design and implementation of TimeBase.

## Project Overview

**TimeBase** is an open-source, modular time series data provider service for financial data, inspired by Home Assistant's add-on architecture.

## Core Architecture Decisions

### 1. Technology Stack

| Component | Technology | Version | Rationale |
|-----------|------------|---------|-----------|
| **Core Runtime** | .NET | 10 | Superior async performance, excellent gRPC support, mature ecosystem |
| **Web Framework** | ASP.NET Core | 10 | Unified with .NET runtime, excellent performance, built-in features |
| **Provider SDK** | Python | 3.11+ | Most popular for financial data processing, rich ecosystem |
| **Communication** | gRPC | Latest | Efficient binary protocol, bidirectional streaming, strong typing |
| **Database** | TimescaleDB | 2.17+ | PostgreSQL-based, optimized for time series, excellent compression |
| **Database Driver** | Npgsql | 10+ | Official PostgreSQL driver for .NET |
| **ORM** | Entity Framework Core | 10 | Mature, type-safe, excellent PostgreSQL support |
| **Container Runtime** | Docker | 20.10+ | Industry standard, excellent tooling |
| **Orchestration** | Docker Compose | 2.0+ | Simple for small teams, good for development |
| **CI/CD** | GitHub Actions | Latest | Integrated with GitHub, excellent ecosystem |

### 2. Data Model

#### Core Data Format: OHLCV + Extensions

**Decision**: Standardize on OHLCV (Open, High, Low, Close, Volume) as the primary data format with optional metadata extensions.

**Rationale**:
- OHLCV is the universal standard for financial time series data
- Covers 95%+ of use cases (stocks, crypto, forex, commodities)
- Extensions allow providers to add custom data without breaking clients
- Future-proof for tick data, order books, corporate actions, etc.

**Structure**:
```protobuf
message TimeSeriesData {
  string symbol = 1;
  google.protobuf.Timestamp timestamp = 2;
  double open = 3;
  double high = 4;
  double low = 5;
  double close = 6;
  double volume = 7;
  string interval = 8;
  string provider = 9;
  map<string, string> metadata = 10;  // Optional extensions
}
```

**Intervals Supported**:
- `1m`, `5m`, `15m`, `30m`
- `1h`, `4h`, `1d`
- `1wk`, `1mo`

### 3. Communication Protocol

#### gRPC with Bidirectional Streaming

**Decision**: Use gRPC for all core ↔ provider communication with bidirectional streaming support.

**Rationale**:
- **Performance**: Binary protocol, more efficient than REST/HTTP
- **Strong Typing**: Protocol buffers provide type safety
- **Streaming**: Bidirectional streams for real-time data
- **Ecosystem**: Excellent tooling for both .NET and Python
- **Future-proof**: Easy to add new features without breaking changes

**Service Definition**:
```protobuf
service DataProvider {
  rpc GetCapabilities(google.protobuf.Empty) returns (ProviderCapabilities);
  rpc GetHistoricalData(HistoricalDataRequest) returns (stream TimeSeriesData);
  rpc StreamRealTimeData(stream StreamControl) returns (stream TimeSeriesData);
  rpc HealthCheck(google.protobuf.Empty) returns (HealthStatus);
}
```

### 4. Provider Architecture

#### Docker-based Add-ons (Home Assistant Inspired)

**Decision**: Providers are Docker containers with YAML manifests, following Home Assistant's add-on model.

**Rationale**:
- **Isolation**: Each provider runs in its own container
- **Portability**: Easy deployment across environments
- **Versioning**: Docker images provide immutable versions
- **Ecosystem**: Rich tooling and registry infrastructure
- **Security**: Container isolation prevents conflicts

**Provider Structure**:
```
provider/
├── config.yaml          # Manifest with capabilities, requirements
├── Dockerfile           # Multi-arch container definition
├── requirements.txt     # Python dependencies
├── src/main.py          # Provider implementation
└── README.md            # Documentation
```

### 5. Client API Design

#### Hybrid: Symbol-centric + Provider-aware

**Decision**: Support both query patterns:
- **Symbol-centric**: Client specifies symbol, core chooses provider
- **Provider-aware**: Client explicitly chooses provider + symbol

**Rationale**:
- **Symbol-centric**: Simpler for most users, automatic provider selection
- **Provider-aware**: Full control, comparison between providers, debugging
- **Hybrid**: Best of both worlds, no breaking changes to add later

**API Endpoints**:
```http
# Symbol-centric
GET /api/data/AAPL?interval=1d&start=2024-01-01&end=2024-12-31

# Provider-aware
GET /api/providers/yahoo-finance/data/AAPL?interval=1d&start=2024-01-01&end=2024-12-31

# Discovery
GET /api/symbols/AAPL/providers  # List available providers for symbol
```

### 6. Provider Routing Strategy

#### Capability-based Matching

**Decision**: When multiple providers support a symbol, use capability-based matching to select the best provider.

**Matching Criteria** (in order of priority):
1. **Data Freshness**: Provider with most recent data
2. **Rate Limits**: Provider with available quota
3. **Performance**: Provider with lowest response time
4. **Reliability**: Provider with highest uptime
5. **Cost**: Free providers preferred over paid

**Fallback Strategy**:
- Try primary provider first
- Automatic failover to backup providers
- Return error only if all providers fail

### 7. Error Handling Strategy

#### Fail Fast with Intelligent Fallback

**Decision**: Fail fast for individual requests but provide intelligent fallback options.

**Error Types**:
- **Provider Unavailable**: Try other providers automatically
- **Rate Limited**: Return cached data with warning, or queue request
- **Invalid Data**: Log error, skip bad data points
- **Network Timeout**: Retry with exponential backoff
- **Authentication Failed**: Disable provider, alert administrator

**Client Response Strategy**:
- **Partial Success**: Return available data + warning about missing data
- **Complete Failure**: Return error with suggested alternatives
- **Stale Data**: Include timestamp of last successful update

### 8. Authentication & Multi-tenancy

#### Single-user MVP, Multi-user Future

**Decision**: Start with single-user mode, design for multi-user extensibility.

**Phase 1 (MVP)**:
- No authentication required
- All data accessible to single user
- Simple configuration via appsettings.json

**Future Phases**:
- JWT-based authentication
- User isolation with row-level security
- API key management for external services
- Team sharing features

### 9. Storage Strategy

#### TimescaleDB with Compression & Retention

**Decision**: Use TimescaleDB hypertables with automatic compression and configurable retention.

**Compression Policy**:
- Compress data older than 7 days
- Segment by symbol, interval, provider
- ~70%+ space savings

**Retention Policy** (configurable):
- Keep daily data indefinitely
- Keep intraday data for 1 year
- Keep tick data for 1 month

**Continuous Aggregates**:
- Pre-computed daily OHLCV for performance
- Automatic refresh every hour

### 10. Development Workflow

#### GitHub-first with Docker Development

**Decision**: Optimize for GitHub ecosystem with containerized development.

**Repository Structure**:
- Main branch: Production-ready code
- Feature branches: All development
- Pull requests: Code review required
- Releases: GitHub releases with Docker images

**Development Environment**:
- Docker Compose for local development
- Hot reload for .NET core
- Live mounting for Python providers
- Integrated debugging support

### 11. Performance Targets

#### Personal/Small Team Scale

**Decision**: Optimize for < 100 concurrent users with room for growth.

**Target Metrics**:
- **Response Time**: < 500ms cached, < 2s fresh data
- **Concurrent Users**: 100+ simultaneous connections
- **Data Throughput**: 10,000+ data points/second ingestion
- **Memory Usage**: < 512MB core, < 256MB per provider
- **Storage Efficiency**: 70%+ compression ratio

### 12. Security Approach

#### Defense in Depth

**Decision**: Implement security best practices without over-engineering for MVP.

**Container Security**:
- Non-root users in containers
- Minimal base images (Python slim)
- Read-only filesystems where possible
- Resource limits and health checks

**Network Security**:
- Internal Docker networking for provider communication
- gRPC ports not exposed externally
- Input validation on all APIs
- Rate limiting to prevent abuse

**Data Security**:
- No sensitive data storage initially
- Input sanitization and validation
- Audit logging of all operations
- Future: Database encryption at rest

### 13. Monitoring & Observability

#### Built-in Application Metrics with Full Observability Stack

**Decision**: Include comprehensive monitoring from day one, with full observability stack for development.

**Application Metrics**:
- ASP.NET Core built-in metrics (requests, duration, active connections)
- Runtime metrics (GC, CPU, memory, lock contention)
- gRPC call statistics
- Database connection pool status
- Provider health and response times
- Prometheus `/metrics` endpoint for scraping

**Logging Strategy**:
- Structured logging with Serilog
- Console output only (no file logging for containerized deployment)
- Enrichers: Machine name, Thread ID, Process ID
- Request correlation IDs via Serilog.AspNetCore
- Error tracking with stack traces
- Configurable log levels per namespace

**Observability Stack** (Added 2026-01-23):
- **Serilog**: Structured logging with configurable outputs
- **OpenTelemetry**: Vendor-neutral instrumentation
  - ASP.NET Core instrumentation (HTTP requests)
  - Entity Framework Core instrumentation (database queries)
  - HTTP Client instrumentation (outbound requests)
  - Runtime instrumentation (GC, CPU, memory)
- **Jaeger** (port 16686): Distributed tracing UI
  - OTLP gRPC receiver on port 4317
  - Visualize request flows and timing
- **Prometheus** (port 9090): Metrics collection and storage
  - Scrapes `/metrics` endpoint every 15s
  - PromQL query interface
- **Grafana** (port 3000): Visualization and dashboards
  - Pre-configured datasources (Prometheus, Jaeger)
  - Included ASP.NET Core overview dashboard
  - Custom dashboard creation
  
**Development vs Production**:
- **Development**: Full stack available via `--profile observability`
- **Production**: OTLP exporters configurable via environment variables
- All components optional and can be disabled via configuration

**Health Checks** (Future):
- Application startup/shutdown
- Database connectivity
- Provider availability
- External API dependencies

See [OBSERVABILITY.md](OBSERVABILITY.md) for detailed setup and usage instructions.

### 14. Deployment Strategy

#### Docker Compose for MVP, Kubernetes Future

**Decision**: Start with Docker Compose with profile-based services, design for Kubernetes migration.

**Development Deployment**:
- Single docker-compose.yml for all services
- Profile-based optional services:
  - Default: TimescaleDB + TimeBase.Core
  - `--profile observability`: Adds Jaeger, Prometheus, Grafana
  - `--profile providers`: Adds example providers
- Volume mounting for live development
- Environment-based configuration

**Production Deployment**:
- Separate docker-compose.prod.yml
- Environment secrets management
- Automated backups and monitoring
- Rolling update capabilities

### 15. Testing Strategy

#### Integration-first with Unit Coverage

**Decision**: Focus on integration tests for MVP, comprehensive unit tests for core logic.

**Test Pyramid**:
- **Integration Tests**: End-to-end provider communication (primary focus)
- **Unit Tests**: Core business logic, data transformation
- **Contract Tests**: gRPC API compatibility
- **Performance Tests**: Load testing and benchmarks

**CI/CD Testing**:
- Build verification on all PRs
- Integration tests in CI pipeline
- Code coverage reporting
- Security scanning

### 16. Documentation Strategy

#### Developer-focused with User Guides

**Decision**: Comprehensive technical documentation with practical examples.

**Documentation Types**:
- **Architecture Docs**: System design and decisions
- **API Reference**: REST and gRPC API documentation
- **Provider Development**: SDK usage and best practices
- **Deployment Guides**: Setup and configuration
- **Contributing Guide**: Development workflow

**Tools**:
- Markdown for all documentation
- OpenAPI/Swagger for API docs
- MkDocs or similar for organized docs site (future)

### 17. Licensing & Open Source

#### MIT License with CLA

**Decision**: MIT license for maximum adoption, Contributor License Agreement for governance.

**Rationale**:
- MIT: Permissive, allows commercial use
- CLA: Protects project's ability to relicense if needed
- GitHub Community Standards: Follow best practices

### 18. Project Structure & Layering

#### Clean Architecture with Vertical Slice Organization

**Decision**: Organize the .NET solution into separate projects following Clean Architecture principles, with the Core project organized by feature (vertical slices) rather than technical concerns.

**Rationale**:
- **Separation of Concerns**: Clear boundaries between web/API, business logic, and data access
- **Testability**: Each layer can be tested independently
- **Maintainability**: Changes in one layer don't ripple through the entire system
- **Dependency Direction**: Dependencies flow inward (Core → Infrastructure, not the other way)
- **Feature Cohesion**: Related code is grouped by business capability, not technical layer

**Project Structure**:

```
src/
├── Directory.Build.props                    # Shared MSBuild properties
├── Directory.Packages.props                 # Central NuGet package version management
├── TimeBase.Core/                           # Web API & Application Layer (organized by features)
│   ├── Providers/                          # Provider management feature
│   │   ├── DependencyExtensions.cs        # AddProviders(), MapProviders()
│   │   ├── Endpoints.cs                    # Provider API endpoints
│   │   ├── Models/
│   │   │   ├── InstallProviderRequest.cs  # Request models
│   │   │   ├── SetProviderEnabledRequest.cs
│   │   │   └── Responses.cs                # Provider-specific responses
│   │   └── Services/
│   │       ├── IProviderRegistry.cs, ProviderRegistry.cs
│   │       ├── IProviderClient.cs, ProviderClient.cs
│   │       └── ProviderHealthMonitor.cs
│   │
│   ├── Data/                               # Data query & streaming feature
│   │   ├── DependencyExtensions.cs        # AddData(), MapData()
│   │   ├── Endpoints.cs                    # Data API endpoints
│   │   ├── Hubs/
│   │   │   └── MarketHub.cs                # SignalR hub for real-time data
│   │   ├── Models/
│   │   │   ├── GetHistoricalDataRequest.cs
│   │   │   ├── DataSummary.cs
│   │   │   └── Responses.cs                # Data-specific responses
│   │   └── Services/
│   │       ├── IDataCoordinator.cs, DataCoordinator.cs
│   │       ├── IMarketBroadcaster.cs, MarketBroadcaster.cs
│   │       └── RealTimeStreamingService.cs
│   │
│   ├── Shared/                             # Cross-cutting concerns
│   │   ├── DependencyExtensions.cs        # AddShared()
│   │   ├── Filters/
│   │   │   └── GlobalValidationFilter.cs
│   │   ├── Models/
│   │   │   └── ErrorResponse.cs            # Truly shared response models
│   │   └── Services/
│   │       └── ITimeBaseMetrics.cs, TimeBaseMetrics.cs
│   │
│   ├── Health/                             # Health check feature
│   │   ├── DependencyExtensions.cs        # AddHealthChecks()
│   │   └── Endpoints.cs                    # Health check endpoints
│   │
│   ├── Program.cs                          # Application entry point
│   └── appsettings.json                    # Configuration
│
├── TimeBase.Core.Infrastructure/           # Data Access Layer
│   ├── Data/
│   │   ├── TimeBaseDbContext.cs           # EF Core DbContext
│   │   └── TimeBaseDbContextFactory.cs    # Design-time factory for migrations
│   ├── Entities/                           # Database entities
│   │   ├── Provider.cs                    # Provider entity
│   │   ├── Symbol.cs                      # Symbol entity
│   │   └── TimeSeriesData.cs              # Time series data entity
│   ├── Migrations/                         # EF Core migrations
│   └── DependencyExtensions.cs             # Infrastructure setup and configuration
│
└── TimeBase.Plugins.Contracts/             # gRPC Protocol Definitions (referenced only by providers)
    └── protos/                             # .proto files
```

**Layering Rules**:

1. **TimeBase.Core** (Application/API Layer)
   - **Organization**: Vertical slices by feature (Providers/, Data/, Shared/, Health/)
   - **Feature Structure**: Each feature contains DependencyExtensions, Endpoints, Models, Services
   - **References**: TimeBase.Core.Infrastructure, TimeBase.Plugins.Contracts
   - **Responsibilities**: HTTP handling, business logic orchestration, feature registration
   - **No direct database code**: Uses DbContext via dependency injection from Infrastructure
   - **Self-Registration**: Feature modules expose `AddFeature()` and `MapFeature()` methods
   - **Fluent API**: Endpoint registration returns `IEndpointRouteBuilder` for chaining

2. **TimeBase.Core.Infrastructure** (Data Access Layer)
   - **Contains**: DbContext, entities, migrations, infrastructure configuration
   - **References**: None (except EF Core packages)
   - **Responsibilities**: Database operations, data persistence, infrastructure setup
   - **Exposes**: Entities and DbContext to Core layer
   - **Registration**: Provides `AddInfrastructure()` and `UseInfrastructure()` extension methods

3. **TimeBase.Plugins.Contracts** (Shared Protocols)
   - **Contains**: gRPC protocol definitions (.proto files)
   - **References**: None
   - **Responsibilities**: API contracts between Core and data providers
   - **Language-agnostic**: Definitions work across .NET and Python
   - **Usage**: Referenced only by provider implementations (not by Core directly for business logic)

**Migration History**:
- **2026-01-23**: Extracted data layer into `TimeBase.Core.Infrastructure` project
  - Moved all EF Core entities from `TimeBase.Core/Entities/` to `TimeBase.Core.Infrastructure/Entities/`
  - Moved DbContext and factory from `TimeBase.Core/Data/` to `TimeBase.Core.Infrastructure/Data/`
  - Moved all migrations to Infrastructure project with namespace updates
  - Removed EF Core packages from Core project
  - Core now references Infrastructure for database access
- **2026-01-23**: Refactored code organization for better separation of concerns
  - Created `TimeBase.Core/Health/` namespace for health check configuration
  - Moved health check registration to dedicated `Health/DependencyExtensions.cs`
  - Moved health check endpoint mappings to `Health/Endpoints.cs`
  - Created `TimeBase.Core.Infrastructure/DependencyExtensions.cs` for infrastructure setup
  - Simplified `Program.cs` by extracting configuration to extension methods
  - Implemented fluent API pattern for endpoint registration
  - Changed to async methods throughout (`RunAsync`, `CloseAndFlushAsync`)
- **2026-02-06**: Reorganized Core project using vertical slice architecture
  - Split by feature (Providers/, Data/, Shared/) instead of technical concerns (Services/, Models/)
  - Moved provider management code to `Providers/` feature folder
  - Moved data query/streaming code to `Data/` feature folder
  - Created feature-specific Models/ and Services/ subfolders within each feature
  - Moved API responses to feature-specific Responses.cs files (ProviderResponse, DataResponse)
  - Maintained Shared/ for truly cross-cutting concerns (GlobalValidationFilter, TimeBaseMetrics, ErrorResponse)
- **2026-02-06**: Implemented feature module pattern with self-registration
  - Each feature has `DependencyExtensions.cs` with `AddFeature()` and `MapFeature()` methods
  - Self-contained feature registration (services + endpoints + hubs)
  - Created `Providers/DependencyExtensions.cs` with `AddProviders()` and `MapProviders()`
  - Created `Data/DependencyExtensions.cs` with `AddData()` and `MapData()`
  - Created `Shared/DependencyExtensions.cs` with `AddShared()`
  - Deleted old monolithic `Shared/Services/DependencyExtensions.cs`
  - Updated `Program.cs` to use feature module registration
- **2026-02-06**: Added central package management
  - Created `src/Directory.Packages.props` for centralized NuGet version management
  - Removed `Version` attributes from all `.csproj` PackageReference elements
  - Enabled `ManagePackageVersionsCentrally` property in all projects
  - Consolidated OpenTelemetry packages to consistent versions (1.10.0-beta.1)
  - Single source of truth for package versions across the solution

**Namespace Conventions**:
- Core entities: `TimeBase.Core.Infrastructure.Entities`
- Core data access: `TimeBase.Core.Infrastructure.Data`
- Infrastructure configuration: `TimeBase.Core.Infrastructure`
- Features: `TimeBase.Core.{Feature}.*` (e.g., `TimeBase.Core.Providers.Services`)
- Feature models: `TimeBase.Core.{Feature}.Models.*`
- Feature services: `TimeBase.Core.{Feature}.Services.*`
- Shared cross-cutting: `TimeBase.Core.Shared.*`
- Health checks: `TimeBase.Core.Health`
- gRPC contracts: `TimeBase.Plugins.Contracts`

**Code Organization Patterns** (Established 2026-01-23, Updated 2026-02-06):
- **Feature Modules**: Each feature has its own `DependencyExtensions.cs` with self-registration methods
  - `AddProviders()` + `MapProviders()` - Provider management feature
  - `AddData()` + `MapData()` - Data query and streaming feature
  - `AddShared()` - Shared cross-cutting services (metrics, validation)
  - `AddHealthChecks()` - Health check services
  - `AddInfrastructure()` + `UseInfrastructure()` - Infrastructure configuration
- **Vertical Slices**: Code organized by business capability, not technical layer
  - Providers/ - All provider management code (models, services, endpoints)
  - Data/ - All data query and streaming code (models, services, endpoints, hubs)
  - Shared/ - Only truly shared cross-cutting code (filters, metrics, error responses)
  - Health/ - Health check endpoints and configuration
- **Fluent API**: Endpoint registration returns `IEndpointRouteBuilder` for chaining
- **Central Package Management**: `Directory.Packages.props` for all NuGet versions (single source of truth)
- **Async by Default**: Use async methods throughout (`RunAsync`, `CloseAndFlushAsync`)
- **Clean Program.cs**: Minimal entry point using feature module registration pattern

**Future Considerations**:
- Repository pattern may be added if query logic becomes complex
- CQRS pattern could be introduced for read/write separation
- Domain events could be added for decoupled communication
- Additional layers (e.g., TimeBase.Core.Domain) if domain logic grows

### 19. Community & Ecosystem

#### Provider Registry & Marketplace

**Decision**: Create a provider ecosystem with discoverability.

**Registry Strategy**:
- GitHub-based provider registry
- Automated discovery via GitHub API
- Quality scoring and user reviews
- Featured/official providers

**Community Features**:
- Provider contribution guidelines
- Issue templates and labels
- Discussion forums (GitHub Discussions)
- Regular release cadence

## Implementation Phases

### Phase 1: Foundation (COMPLETED)
- ✅ Project structure and tooling
- ✅ gRPC protocol definitions
- ✅ Basic core and SDK skeletons
- ✅ Docker infrastructure
- ✅ Database schema
- ✅ CI/CD pipelines

### Phase 2: Core Implementation (NEXT)
- EF Core data models and repository pattern
- Provider registry and lifecycle management
- Capability-based routing implementation
- Basic REST API for provider management
- Health monitoring and error handling

### Phase 3: REST API
- Complete historical data API
- Query optimization and caching
- OpenAPI documentation
- Request validation and rate limiting

### Phase 4: Production Provider
- Yahoo Finance provider implementation
- Docker multi-arch builds
- Automated publishing to registry
- Comprehensive testing and documentation

### Phase 5: Real-time Streaming (Future)
- SignalR WebSocket implementation
- Stream multiplexing
- Real-time provider examples
- Performance optimization

### Phase 6: Production Polish (Future)
- Monitoring and alerting
- Security hardening
- Performance optimization
- Comprehensive documentation

## Quality Standards

### Code Quality
- **Code Reviews**: Required for all PRs
- **Testing**: 80%+ coverage for core code
- **Linting**: Automated code style enforcement
- **Security**: Regular dependency updates and scans

### Documentation Quality
- **API Docs**: 100% of public APIs documented
- **Code Comments**: Complex logic explained
- **Examples**: Working code examples for all features
- **Tutorials**: Step-by-step guides for common tasks

### Performance Standards
- **Latency**: < 500ms for cached queries
- **Throughput**: 10,000+ data points/second
- **Reliability**: 99.9% uptime target
- **Efficiency**: < 512MB memory usage

## Risk Mitigation

### Technical Risks
- **gRPC Complexity**: Start simple, add streaming later
- **Database Scaling**: TimescaleDB designed for scale
- **Provider Reliability**: Health checks and failover
- **Performance**: Monitor and optimize from day one

### Project Risks
- **Scope Creep**: Phased approach with clear deliverables
- **Technology Learning**: Start with familiar technologies
- **Community Adoption**: Focus on quality over quantity
- **Maintenance Burden**: Minimal dependencies, automated testing

## Success Criteria

### Functional Success
- ✅ Reliable access to financial time series data
- ✅ Easy to add new data providers
- ✅ Scales to personal/small team usage
- ✅ Professional API and documentation

### Technical Success
- ✅ Clean, maintainable codebase
- ✅ Comprehensive test coverage
- ✅ Good performance and reliability
- ✅ Secure by default

### Community Success
- ✅ Active contributor community
- ✅ Rich ecosystem of providers
- ✅ Good documentation and support
- ✅ Regular releases and updates

---

## Agreement Sign-off

This document represents the agreed-upon technical foundation for TimeBase. All major architectural decisions, technology choices, and implementation strategies have been discussed and approved.

**Last Updated**: Phase 1 Completion
**Next Review**: Phase 2 Planning