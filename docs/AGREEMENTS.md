# TimeBase Technical Agreements

This document captures all technical decisions and agreements made during the design and implementation of TimeBase.

## Project Overview

**TimeBase** is an open-source, modular time series data provider service for financial data, inspired by Home Assistant's add-on architecture.

## Core Architecture Decisions

### 1. Technology Stack

| Component | Technology | Version | Rationale |
|-----------|------------|---------|-----------|
| **Supervisor Runtime** | .NET | 10 | Superior async performance, excellent gRPC support, mature ecosystem |
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

**Decision**: Use gRPC for all supervisor ↔ provider communication with bidirectional streaming support.

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
- **Symbol-centric**: Client specifies symbol, supervisor chooses provider
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
- Hot reload for .NET supervisor
- Live mounting for Python providers
- Integrated debugging support

### 11. Performance Targets

#### Personal/Small Team Scale

**Decision**: Optimize for < 100 concurrent users with room for growth.

**Target Metrics**:
- **Response Time**: < 500ms cached, < 2s fresh data
- **Concurrent Users**: 100+ simultaneous connections
- **Data Throughput**: 10,000+ data points/second ingestion
- **Memory Usage**: < 512MB supervisor, < 256MB per provider
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

#### Built-in Application Metrics

**Decision**: Include monitoring from day one, extensible architecture.

**Application Metrics**:
- ASP.NET Core built-in metrics
- gRPC call statistics
- Database connection pool status
- Provider health and response times

**Logging Strategy**:
- Structured JSON logging with Serilog
- Request correlation IDs
- Error tracking with stack traces
- Configurable log levels

**Health Checks**:
- Application startup/shutdown
- Database connectivity
- Provider availability
- External API dependencies

### 14. Deployment Strategy

#### Docker Compose for MVP, Kubernetes Future

**Decision**: Start with Docker Compose, design for Kubernetes migration.

**Development Deployment**:
- Single docker-compose.yml for all services
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

### 18. Community & Ecosystem

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
- ✅ Basic supervisor and SDK skeletons
- ✅ Docker infrastructure
- ✅ Database schema
- ✅ CI/CD pipelines

### Phase 2: Core Supervisor (NEXT)
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
- **Testing**: 80%+ coverage for supervisor code
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