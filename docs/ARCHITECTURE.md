# TimeBase Architecture

## Overview

TimeBase is a modular time series data provider service inspired by Home Assistant's add-on architecture. It provides a centralized core that manages pluggable data providers, offering a unified interface for accessing financial time series data.

## Core Principles

### 1. Modular Architecture
- **Core**: Central orchestration service written in .NET 10
- **Providers**: Pluggable Docker containers that implement data fetching
- **Protocol**: gRPC for efficient communication between components
- **Storage**: TimescaleDB for optimized time series data storage

### 2. Data Standardization
- **OHLCV Format**: Open, High, Low, Close, Volume as core data structure
- **Extensions**: Optional metadata for provider-specific data
- **Time Series**: UTC timestamps with configurable intervals
- **Symbols**: Standardized symbol format (e.g., AAPL, BTC-USD)

### 3. Scalability Focus
- **Personal/Small Team**: Optimized for < 100 concurrent users
- **Efficient Storage**: TimescaleDB compression and retention policies
- **Connection Pooling**: Reused connections to data sources
- **Caching**: Query result caching for frequently accessed data

## System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Client Applications                      │
│         (Trading Bots, Analytics Tools, Dashboards)         │
└─────────────────────────────────────────────────────────────┘
                    ↓ REST API / WebSocket
┌─────────────────────────────────────────────────────────────┐
│                  TimeBase Core (.NET 10)                     │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  API Layer (ASP.NET Core + SignalR)                  │  │
│  │  - REST endpoints for historical data                │  │
│  │  - WebSocket hub for real-time streaming             │  │
│  │  - OpenAPI/Swagger documentation                      │  │
│  └──────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Provider Manager                                     │  │
│  │  - Provider registry & lifecycle management          │  │
│  │  - Docker container orchestration                     │  │
│  │  - Health monitoring & auto-restart                  │  │
│  └──────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Data Coordinator                                     │  │
│  │  - gRPC client pool for providers                    │  │
│  │  - Capability-based routing                          │  │
│  │  - Stream multiplexing                               │  │
│  └──────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Storage Manager (EF Core + Npgsql)                  │  │
│  │  - TimescaleDB abstraction                           │  │
│  │  - Query optimization & caching                      │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                    ↓ gRPC (Bidirectional)
┌─────────────────────────────────────────────────────────────┐
│              Data Providers (Docker Containers)              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │   Yahoo      │  │ Alpha Vantage│  │   Binance    │     │
│  │   Finance    │  │   Provider   │  │   Provider   │ ... │
│  │  (Python)    │  │   (Python)   │  │   (Python)   │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
│         Each implements TimeBase Provider gRPC Service       │
└─────────────────────────────────────────────────────────────┘
                            ↓
                    External Data Sources
            (Yahoo Finance API, Alpha Vantage, Exchanges)
```

## Component Details

### Core Service (.NET 10)

The core is the central orchestration component built with ASP.NET Core 10.

#### Key Components

**1. API Layer**
- **Framework**: ASP.NET Core Minimal APIs + Controllers
- **Authentication**: None (single-user MVP)
- **Documentation**: OpenAPI/Swagger integration
- **WebSocket**: SignalR for real-time streaming (Phase 5)

**2. Provider Manager**
- **Container Runtime**: Docker API integration
- **Lifecycle**: Install, start, stop, update providers
- **Registry**: YAML-based provider manifests
- **Health Checks**: Periodic provider status monitoring

**3. Data Coordinator**
- **Routing**: Capability-based provider selection
- **gRPC Pool**: Connection pooling to providers
- **Multiplexing**: Aggregate real-time streams (Phase 5)
- **Caching**: Redis-based query result caching

**4. Storage Manager**
- **ORM**: Entity Framework Core 10
- **Database**: TimescaleDB via Npgsql
- **Optimization**: Hypertable queries, compression
- **Migration**: Automatic schema updates

#### Configuration

```json
// appsettings.json
{
  "ConnectionStrings": {
    "TimeBaseDb": "Host=localhost;Database=timebase;Username=timebase;Password=timebase_dev"
  },
  "Providers": {
    "RegistryPath": "/data/providers",
    "HealthCheckInterval": "00:01:00"
  },
  "Grpc": {
    "MaxConcurrentStreams": 100,
    "MaxMessageSize": 1048576
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "TimeBase": "Debug"
    }
  }
}
```

### Data Providers (Python 3.11+)

Providers are Docker containers that implement the TimeBase Provider gRPC service.

#### Provider Structure

```
provider/
├── config.yaml          # Provider manifest
├── Dockerfile           # Container definition
├── requirements.txt     # Python dependencies
├── src/
│   └── main.py          # Provider implementation
└── README.md            # Documentation
```

#### Provider Manifest (config.yaml)

```yaml
name: "Yahoo Finance Provider"
version: "1.0.0"
slug: "yahoo-finance"
description: "Historical stock data from Yahoo Finance"

image: "ghcr.io/marcelga/timebase-{arch}-yahoo-finance"
arch:
  - amd64
  - arm64

capabilities:
  historical_data: true
  real_time_streaming: false
  backfill: true

data_types:
  - stocks
  - etfs
  - indices

intervals:
  - 1m
  - 5m
  - 15m
  - 1h
  - 1d
  - 1wk
  - 1mo

rate_limits:
  requests_per_minute: 60
  requests_per_day: 2000

options:
  timeout:
    type: int
    description: "Request timeout in seconds"
    default: 30
```

### Database Schema (TimescaleDB)

#### Core Tables

**1. providers**
```sql
CREATE TABLE providers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    slug VARCHAR(100) UNIQUE NOT NULL,
    name VARCHAR(255) NOT NULL,
    version VARCHAR(50) NOT NULL,
    repository_url TEXT,
    image_url TEXT NOT NULL,
    enabled BOOLEAN DEFAULT true,
    config JSONB,
    capabilities JSONB NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
```

**2. symbols**
```sql
CREATE TABLE symbols (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    symbol VARCHAR(50) UNIQUE NOT NULL,
    name VARCHAR(255),
    type VARCHAR(50),  -- 'stock', 'etf', 'crypto', etc.
    metadata JSONB,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
```

**3. time_series_data (Hypertable)**
```sql
CREATE TABLE time_series_data (
    time TIMESTAMPTZ NOT NULL,
    symbol VARCHAR(50) NOT NULL,
    provider_id UUID NOT NULL REFERENCES providers(id),
    interval VARCHAR(10) NOT NULL,

    -- OHLCV data
    open DOUBLE PRECISION NOT NULL,
    high DOUBLE PRECISION NOT NULL,
    low DOUBLE PRECISION NOT NULL,
    close DOUBLE PRECISION NOT NULL,
    volume DOUBLE PRECISION NOT NULL,

    -- Optional metadata
    metadata JSONB,

    CONSTRAINT time_series_data_pkey PRIMARY KEY (time, symbol, interval, provider_id)
);

-- Convert to hypertable
SELECT create_hypertable('time_series_data', 'time');
```

#### Indexes and Optimization

```sql
-- Performance indexes
CREATE INDEX idx_time_series_symbol ON time_series_data (symbol, time DESC);
CREATE INDEX idx_time_series_interval ON time_series_data (interval, time DESC);
CREATE INDEX idx_time_series_provider ON time_series_data (provider_id, time DESC);

-- Compression policy (data older than 7 days)
ALTER TABLE time_series_data SET (
    timescaledb.compress,
    timescaledb.compress_segmentby = 'symbol,interval,provider_id'
);
SELECT add_compression_policy('time_series_data', INTERVAL '7 days');

-- Continuous aggregate for daily data
CREATE MATERIALIZED VIEW daily_ohlcv
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 day', time) AS day,
    symbol,
    provider_id,
    first(open, time) AS open,
    max(high) AS high,
    min(low) AS low,
    last(close, time) AS close,
    sum(volume) AS volume
FROM time_series_data
WHERE interval = '1d'
GROUP BY day, symbol, provider_id;
```

### Communication Protocol (gRPC)

#### Service Definition

```protobuf
service DataProvider {
  // Get provider capabilities
  rpc GetCapabilities(google.protobuf.Empty) returns (ProviderCapabilities);

  // Historical data (core → provider, streaming response)
  rpc GetHistoricalData(HistoricalDataRequest) returns (stream TimeSeriesData);

  // Real-time streaming (bidirectional)
  rpc StreamRealTimeData(stream StreamControl) returns (stream TimeSeriesData);

  // Health check
  rpc HealthCheck(google.protobuf.Empty) returns (HealthStatus);
}
```

#### Key Messages

**TimeSeriesData** (Core data structure)
```protobuf
message TimeSeriesData {
  string symbol = 1;
  google.protobuf.Timestamp timestamp = 2;

  // OHLCV data
  double open = 3;
  double high = 4;
  double low = 5;
  double close = 6;
  double volume = 7;

  string interval = 8;
  string provider = 9;

  // Optional metadata
  map<string, string> metadata = 10;
}
```

**HistoricalDataRequest**
```protobuf
message HistoricalDataRequest {
  string symbol = 1;
  string interval = 2;
  google.protobuf.Timestamp start_time = 3;
  google.protobuf.Timestamp end_time = 4;
  optional int32 limit = 5;
}
```

### Docker Infrastructure

#### Development Setup (docker-compose.yml)

```yaml
services:
  timescaledb:
    image: timescale/timescaledb:2.17.2-pg16
    environment:
      POSTGRES_DB: timebase
      POSTGRES_USER: timebase
      POSTGRES_PASSWORD: timebase_dev
    ports:
      - "5432:5432"
    volumes:
      - ./timescaledb/init.sql:/docker-entrypoint-initdb.d/init.sql
      - timescale-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U timebase"]
      interval: 10s
      timeout: 5s
      retries: 5

  core:
    build:
      context: ..
      dockerfile: docker/core/Dockerfile
    ports:
      - "8080:8080"
      - "50051:50051"
    depends_on:
      timescaledb:
        condition: service_healthy
    environment:
      ConnectionStrings__TimeBaseDb: "Host=timescaledb;Database=timebase;Username=timebase;Password=timebase_dev"
      ASPNETCORE_ENVIRONMENT: Development

volumes:
  timescale-data:
```

#### Provider Container Structure

```dockerfile
FROM python:3.11-slim

WORKDIR /app

# Install dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy provider code
COPY src/ ./src/
COPY config.yaml .

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD python -c "import grpc; # health check logic"

# Expose gRPC port
EXPOSE 50051

# Run provider
CMD ["python", "src/main.py"]
```

### Security Considerations

#### Container Security
- **Non-root user**: Providers run as non-root user
- **Minimal base images**: Python slim images
- **Read-only filesystems**: Where possible
- **Resource limits**: CPU and memory limits in Docker Compose

#### Network Security
- **Internal networking**: Provider communication over internal Docker network
- **No external exposure**: gRPC ports not exposed externally
- **API authentication**: Bearer token authentication (future)

#### Data Security
- **Encrypted storage**: Database encryption at rest (future)
- **Input validation**: Strict validation of all inputs
- **Rate limiting**: Prevent abuse of external APIs
- **Audit logging**: All data access logged

### Performance Characteristics

#### Target Metrics
- **Query Latency**: < 500ms for cached queries, < 2s for fresh data
- **Concurrent Connections**: Support 100+ simultaneous clients
- **Data Ingestion**: Handle 10,000+ data points per second
- **Memory Usage**: < 512MB for core, < 256MB per provider
- **Storage Efficiency**: 70%+ compression with TimescaleDB

#### Scaling Strategy
- **Horizontal**: Multiple core instances (future)
- **Vertical**: Optimize single-instance performance
- **Caching**: Multi-layer caching (memory, Redis)
- **Connection Pooling**: Reuse connections to providers and database

### Monitoring and Observability

#### Metrics
- **Application Metrics**: ASP.NET Core metrics
- **Database Metrics**: TimescaleDB statistics
- **Provider Metrics**: Health and performance data
- **System Metrics**: CPU, memory, network usage

#### Logging
- **Structured Logging**: Serilog with JSON output
- **Log Levels**: Debug, Info, Warning, Error
- **Log Aggregation**: Centralized logging (future)
- **Audit Trail**: All data access and mutations logged

#### Health Checks
- **Application Health**: ASP.NET Core health checks
- **Database Health**: Connection and query tests
- **Provider Health**: gRPC health checks
- **External Dependencies**: API endpoint availability

### Deployment Strategy

#### Development
- **Docker Compose**: Local development environment
- **Hot Reload**: .NET hot reload for core
- **Volume Mounting**: Live code changes for providers

#### Production
- **Container Orchestration**: Docker Compose with production config
- **Reverse Proxy**: nginx for load balancing (future)
- **SSL/TLS**: HTTPS termination
- **Backup**: Automated database backups
- **Updates**: Rolling updates for providers

### Extension Points

#### Provider Ecosystem
- **Provider Registry**: Centralized registry of available providers
- **Version Management**: Semantic versioning for providers
- **Compatibility Matrix**: Core ↔ Provider compatibility

#### API Extensions
- **GraphQL**: Alternative query interface (future)
- **WebSocket**: Real-time streaming (Phase 5)
- **REST Hooks**: Webhook notifications (future)

#### Data Extensions
- **Custom Schemas**: Beyond OHLCV (tick data, fundamentals)
- **Aggregation**: Built-in aggregation functions
- **Analytics**: Basic statistical functions

This architecture provides a solid foundation for a scalable, modular time series data platform while keeping complexity manageable for personal and small team use cases.