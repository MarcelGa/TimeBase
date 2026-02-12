# TimeBase

[![Build Core](https://github.com/MarcelGa/TimeBase/actions/workflows/build-core.yml/badge.svg)](https://github.com/MarcelGa/TimeBase/actions/workflows/build-core.yml)
[![E2E Tests](https://github.com/MarcelGa/TimeBase/actions/workflows/e2e-tests.yml/badge.svg)](https://github.com/MarcelGa/TimeBase/actions/workflows/e2e-tests.yml)
[![Build Providers](https://github.com/MarcelGa/TimeBase/actions/workflows/build-providers.yml/badge.svg)](https://github.com/MarcelGa/TimeBase/actions/workflows/build-providers.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![Python](https://img.shields.io/badge/Python-3.11+-green)](https://python.org)
[![Tests](https://img.shields.io/badge/tests-73%20passing-brightgreen)](https://github.com/MarcelGa/TimeBase/actions)

Open-source, modular time series data provider service for financial data. Inspired by Home Assistant's add-on architecture, TimeBase provides a centralized core that manages pluggable data providers via Docker containers.

## Features

- **Modular Architecture**: Pluggable provider system (like Home Assistant add-ons)
- **Financial Data Focus**: Optimized for stocks, crypto, forex, and commodities
- **OHLCV Standard**: Consistent data format with optional extensions
- **gRPC Communication**: Efficient bidirectional streaming between components
- **TimescaleDB Storage**: High-performance time series database with compression
- **REST API**: Clean HTTP API for data access and provider management
- **WebSocket Support**: Real-time data streaming (future)
- **Docker Native**: Containerized deployment with Docker Compose
- **Observability**: Built-in monitoring with OpenTelemetry, Prometheus, Jaeger, and Grafana

## Quick Start

### Prerequisites

- **Docker Desktop** (for Windows/Mac) or Docker Engine (for Linux)
- **.NET 10.0 SDK** (for development)
- **Python 3.11+** (for provider development)
- **Git**

### 1. Clone the Repository

```bash
git clone https://github.com/marcelga/TimeBase.git
cd TimeBase
```

### 2. Start Development Environment

**Quick Start with Yahoo Finance Provider:**

```bash
# Start everything (database, core, and providers)
cd src/docker
docker-compose -f docker-compose.dev.yml up --build
```

This starts:
- **TimescaleDB**: PostgreSQL with TimescaleDB extension on port 5432
- **TimeBase Core**: .NET API server on port 8080
- **Yahoo Finance Provider**: Real market data provider on port 50053
- **Minimal Provider**: Test provider on port 50052

**Basic Start (Core only):**

```bash
# Start TimescaleDB and core only
cd src/docker
docker-compose up -d

# Verify services are running
docker-compose ps
```

For detailed testing instructions, see [docs/TESTING-LOCAL.md](docs/TESTING-LOCAL.md).

### Observability Stack (Optional)

TimeBase includes a comprehensive observability stack with metrics, tracing, and visualization:

```bash
# Start with observability services
cd src/docker
docker-compose --profile observability up -d
```

This adds:
- **Jaeger** (port 16686): Distributed tracing UI - http://localhost:16686
- **Prometheus** (port 9090): Metrics collection - http://localhost:9090
- **Grafana** (port 3000): Dashboards and visualization - http://localhost:3000 (admin/admin)

**Available Endpoints:**
- Metrics: http://localhost:8080/metrics
- Health checks:
  - Live: http://localhost:8080/health/live
  - Ready: http://localhost:8080/health/ready
  - Full: http://localhost:8080/health

For detailed information, see [docs/OBSERVABILITY.md](docs/OBSERVABILITY.md).

### 3. Test with Real Data

**Option A: Yahoo Finance Provider (Real Market Data)**

```bash
# Get Apple stock data for January 2024
curl "http://localhost:8080/api/data/AAPL?provider=yahoo-finance&interval=1d&start=2024-01-01&end=2024-01-31"

# Get Bitcoin price data
curl "http://localhost:8080/api/data/BTC-USD?provider=yahoo-finance&interval=1h&start=2024-01-01&end=2024-01-02"

# Get S&P 500 index data
curl "http://localhost:8080/api/data/^GSPC?provider=yahoo-finance&interval=1wk&start=2024-01-01&end=2024-12-31"
```

**Option B: Minimal Provider (Test Data)**

```bash
# Get test data
curl "http://localhost:8080/api/data/TEST?provider=minimal&interval=1d&start=2024-01-01&end=2024-01-05"
```

### 4. Access API Documentation

Open your browser to: http://localhost:8080/swagger

## Available Providers

### Yahoo Finance Provider (Production)

**Real market data for:**
- **Stocks**: US and international (AAPL, MSFT, TSLA, etc.)
- **Cryptocurrencies**: BTC-USD, ETH-USD, etc.
- **Indices**: ^GSPC (S&P 500), ^DJI (Dow Jones), ^IXIC (NASDAQ)
- **ETFs**: SPY, QQQ, VTI, etc.

**Intervals**: 1m, 5m, 15m, 30m, 1h, 1d, 1wk, 1mo

See [providers/yahoo-finance/README.md](providers/yahoo-finance/README.md) for details.

### Minimal Provider (Testing)

Generates synthetic OHLCV data for testing purposes.

### 5. Access API Documentation

Open your browser to: http://localhost:8080/swagger

## Architecture

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
│  └──────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Provider Manager                                     │  │
│  │  - Provider registry & lifecycle management          │  │
│  │  - Docker container orchestration                     │  │
│  └──────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Data Coordinator                                     │  │
│  │  - gRPC client pool for providers                    │  │
│  │  - Capability-based routing                          │  │
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
└─────────────────────────────────────────────────────────────┘
```

For detailed architecture information, see [ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Provider Development

### Creating Your First Provider

1. **Initialize a new provider**:
```bash
# Use the Python SDK
pip install timebase-sdk

# Create a new provider
timebase-sdk init my-provider
cd my-provider
```

2. **Implement your provider**:
```python
from timebase_sdk import TimeBaseProvider, run_grpc_server

class MyProvider(TimeBaseProvider):
    async def get_capabilities(self):
        return {
            "name": "My Data Provider",
            "version": "1.0.0",
            "slug": "my-provider",
            "supports_historical": True,
            "supports_realtime": False,
            "data_types": ["stocks"],
            "intervals": ["1d"],
        }
    
    async def get_historical_data(self, symbol, interval, start_time, end_time, limit=None):
        # Your data fetching logic here
        yield {
            "symbol": symbol,
            "timestamp": start_time,
            "open": 100.0,
            "high": 105.0,
            "low": 95.0,
            "close": 102.0,
            "volume": 1000000,
            "interval": interval,
            "provider": self.config.slug
        }

if __name__ == "__main__":
    provider = MyProvider()
    run_grpc_server(provider, port=50051)
```

3. **Configure your provider** (`config.yaml`):
```yaml
name: "My Data Provider"
version: "1.0.0"
slug: "my-provider"
description: "My custom financial data provider"

image: "ghcr.io/yourusername/timebase-{arch}-my-provider"
arch:
  - amd64
  - arm64

capabilities:
  historical_data: true
  real_time_streaming: false

data_types:
  - stocks

intervals:
  - 1d

options:
  api_key:
    type: string
    description: "API key for the data source"
    required: true
```

4. **Build and publish**:
```bash
# Build Docker image
docker build -t ghcr.io/yourusername/timebase-amd64-my-provider .

# Push to registry
docker push ghcr.io/yourusername/timebase-amd64-my-provider
```

5. **Install in TimeBase**:
```bash
curl -X POST http://localhost:8080/api/providers \
  -H "Content-Type: application/json" \
  -d '{"repository": "https://github.com/yourusername/timebase-provider-my"}'
```

For comprehensive provider development guides, see [PROVIDER DEVELOPMENT](docs/provider-development.md).

## API Reference

### REST API

The TimeBase REST API provides programmatic access to financial data:

```bash
# Get historical data (provider is required)
GET /api/data/AAPL?provider=yahoo-finance&interval=1d&start=2024-01-01&end=2024-12-31

# List providers
GET /api/providers

# Install provider
POST /api/providers
```

For complete API documentation, see [docs/api/rest-api.md](docs/api/rest-api.md) or visit http://localhost:8080/swagger.

### WebSocket API (Future)

Real-time data streaming will be available via WebSocket:

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:8080/hubs/realtime")
    .build();

await connection.start();
await connection.invoke("Subscribe", "AAPL", "1m");
```

## Development

### Prerequisites

- **.NET 10.0 SDK**: https://dotnet.microsoft.com/download
- **Python 3.11+**: https://python.org
- **Docker Desktop**: https://docker.com/products/docker-desktop
- **Git**: https://git-scm.com

### Local Development Setup

1. **Clone and setup**:
```bash
git clone https://github.com/marcelga/TimeBase.git
cd TimeBase
```

2. **Start infrastructure**:
```bash
cd docker
docker-compose up -d
```

3. **Build core**:
```bash
cd src
dotnet restore TimeBase.slnx
dotnet build TimeBase.slnx
```

4. **Run core**:
```bash
cd src/TimeBase.Core
dotnet run
```

5. **Install SDK for provider development**:
```bash
cd src/TimeBase.ProviderSdk
pip install -e .
```

### Testing

```bash
# Run core tests
cd src
dotnet test TimeBase.slnx

# Run provider SDK tests
cd src/TimeBase.ProviderSdk
python -m pytest

# Run integration tests
cd src/docker
docker-compose -f docker-compose.test.yml up --abort-on-container-exit
```

### Building Docker Images

```bash
# Build core
docker build -f src/docker/core/Dockerfile -t timebase/core:latest .

# Build provider
cd providers/examples/minimal-provider
docker build -t timebase/minimal-provider:latest .
```

## Deployment

### Docker Compose (Development)

```bash
# Development setup
docker-compose -f docker/docker-compose.yml up -d

# Production setup (future)
docker-compose -f docker/docker-compose.prod.yml up -d
```

### Kubernetes (Future)

TimeBase will support Kubernetes deployment with Helm charts for production environments.

## Configuration

### Core Configuration

Edit `src/TimeBase.Core/appsettings.json`:

```json
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
  }
}
```

### Provider Configuration

Each provider has its own `config.yaml` with capabilities and options.

## Roadmap

See [ROADMAP.md](docs/ROADMAP.md) for detailed development phases:

- ✅ **Phase 1**: Foundation (complete)
- 🔄 **Phase 2**: Core implementation (in progress)
- 📋 **Phase 3**: REST API (planned)
- 📋 **Phase 4**: Production provider (planned)
- 📋 **Phase 5**: Real-time streaming (future)
- 📋 **Phase 6**: Production polish (future)

## Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

### Quick Contribution Guide

1. **Fork** the repository
2. **Create** a feature branch: `git checkout -b feature/your-feature`
3. **Make** your changes with tests
4. **Run** the test suite: `dotnet test`
5. **Submit** a pull request

### Development Guidelines

- Follow the [C# coding conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use [PEP 8](https://pep8.org/) for Python code
- Write comprehensive unit and integration tests
- Update documentation for any API changes
- Ensure all CI checks pass

## License

TimeBase is licensed under the MIT License. See [LICENSE](LICENSE) for details.

## Support

- **Documentation**: https://timebase.dev/docs
- **GitHub Issues**: https://github.com/marcelga/TimeBase/issues
- **GitHub Discussions**: https://github.com/marcelga/TimeBase/discussions
- **Discord**: (coming soon)

## Acknowledgments

- **Home Assistant**: Inspiration for the add-on architecture
- **TimescaleDB**: High-performance time series database
- **Protocol Buffers**: Efficient data serialization
- **.NET Community**: Excellent development platform

---

## Project Status

**Current Phase**: Phase 1 (Foundation) - ✅ Complete

TimeBase is in active development. The core architecture is stable, but APIs and features may change. See [ROADMAP.md](docs/ROADMAP.md) for upcoming features and timelines.