# TimeBase Provider SDK

A Python SDK for building data providers that integrate with the TimeBase supervisor via gRPC. This SDK provides a clean, async interface for implementing financial time series data providers.

## Features

- **Async/Await Support**: Full async support for high-performance data streaming
- **Type Safety**: Strong typing with Python type hints
- **gRPC Integration**: Automatic protobuf code generation and server setup
- **Configuration Management**: YAML-based provider configuration
- **Error Handling**: Built-in retry logic and error handling
- **Logging**: Structured logging with configurable levels

## Installation

```bash
pip install timebase-sdk
```

## Quick Start

### 1. Create a Provider

```python
from timebase_sdk import TimeBaseProvider, run_grpc_server
from datetime import datetime, timedelta
import random

class MyProvider(TimeBaseProvider):
    """A simple example provider that generates dummy OHLCV data."""

    async def get_capabilities(self) -> dict:
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
        """Generate dummy OHLCV data for demonstration."""
        current = start_time
        count = 0

        while current <= end_time:
            if limit and count >= limit:
                break

            # Generate random OHLCV data
            base_price = 100.0
            open_price = base_price + random.uniform(-2, 2)
            high_price = open_price + random.uniform(0, 5)
            low_price = open_price - random.uniform(0, 5)
            close_price = base_price + random.uniform(-2, 2)
            volume = random.uniform(1000000, 5000000)

            yield {
                "symbol": symbol,
                "timestamp": current,
                "open": open_price,
                "high": high_price,
                "low": low_price,
                "close": close_price,
                "volume": volume,
                "interval": interval,
                "provider": self.config.slug
            }

            current += timedelta(days=1)
            count += 1

    async def stream_realtime_data(self, subscriptions):
        """Real-time streaming not supported in this example."""
        raise NotImplementedError("Real-time streaming not supported")

if __name__ == "__main__":
    provider = MyProvider()
    run_grpc_server(provider, port=50051)
```

### 2. Create Configuration (`config.yaml`)

```yaml
name: "My Data Provider"
version: "1.0.0"
slug: "my-provider"
description: "Example provider for TimeBase"

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

rate_limits:
  requests_per_minute: 60
  requests_per_day: 2000

options:
  api_key:
    type: string
    description: "API key for the data source"
    required: true
```

### 3. Run the Provider

```bash
python main.py
```

### 4. Install in TimeBase

```bash
# Install your provider
curl -X POST http://localhost:8080/api/providers \
  -H "Content-Type: application/json" \
  -d '{"repository": "https://github.com/yourusername/timebase-provider-my"}'

# Query data (provider is required)
curl "http://localhost:8080/api/data/AAPL?provider=my-provider&interval=1d&start=2024-01-01&end=2024-01-05"
```

## SDK Components

### TimeBaseProvider (Abstract Base Class)

The main class you'll inherit from to create providers.

#### Required Methods

- `get_capabilities()`: Return provider capabilities
- `get_historical_data()`: Fetch historical OHLCV data
- `stream_realtime_data()`: Stream real-time data (optional)

#### Optional Methods

- `health_check()`: Provider health status
- `setup()`: One-time initialization
- `cleanup()`: Cleanup resources

### Configuration Management

The SDK automatically loads `config.yaml` from the current directory:

```python
provider = MyProvider()  # Loads config.yaml automatically
print(provider.config.name)  # Access configuration
```

### gRPC Server

The `run_grpc_server()` function handles all gRPC setup:

```python
from timebase_sdk import run_grpc_server

provider = MyProvider()
run_grpc_server(provider, port=50051, host="0.0.0.0")
```

### Data Models

The SDK provides type hints and validation for all data structures:

```python
from typing import AsyncIterator
from datetime import datetime

async def get_historical_data(
    self,
    symbol: str,
    interval: str,
    start_time: datetime,
    end_time: datetime,
    limit: Optional[int] = None
) -> AsyncIterator[dict]:
    # Return OHLCV dictionaries
    yield {
        "symbol": symbol,
        "timestamp": datetime.utcnow(),
        "open": 100.0,
        "high": 105.0,
        "low": 95.0,
        "close": 102.0,
        "volume": 1000000.0,
        "interval": interval,
        "provider": self.config.slug,
        "metadata": {"dividend": "0.25"}  # Optional
    }
```

## Provider Development Guide

### 1. Choose Data Sources

Popular financial data sources:
- **Yahoo Finance**: Free, comprehensive stock data
- **Alpha Vantage**: Free API with 5 calls/minute
- **IEX Cloud**: Free tier available
- **Twelve Data**: Multiple exchanges
- **Binance API**: Crypto exchanges

### 2. Handle Rate Limits

Implement proper rate limiting:

```python
import asyncio
from timebase_sdk.utils import RateLimiter

class RateLimitedProvider(TimeBaseProvider):
    def __init__(self):
        super().__init__()
        self.rate_limiter = RateLimiter(requests_per_minute=60)

    async def get_historical_data(self, symbol, interval, start_time, end_time, limit=None):
        await self.rate_limiter.wait_if_needed()
        # Fetch data...
```

### 3. Error Handling

Handle common errors gracefully:

```python
from timebase_sdk.exceptions import ProviderError, RateLimitError

async def get_historical_data(self, symbol, interval, start_time, end_time, limit=None):
    try:
        # Fetch data
        data = await self.fetch_from_api(symbol, interval, start_time, end_time)
        for item in data:
            yield item
    except RateLimitError:
        # Wait and retry
        await asyncio.sleep(60)
        async for item in self.get_historical_data(symbol, interval, start_time, end_time, limit):
            yield item
    except ProviderError as e:
        self.logger.error(f"Provider error: {e}")
        raise
```

### 4. Real-time Streaming

Implement real-time streaming (optional):

```python
async def stream_realtime_data(self, subscriptions):
    """Stream real-time data based on subscription requests."""
    async for control in subscriptions:
        if control["action"] == "SUBSCRIBE":
            symbol = control["symbol"]
            interval = control["interval"]

            # Start streaming for this symbol
            async for data in self.stream_symbol(symbol, interval):
                yield data

        elif control["action"] == "UNSUBSCRIBE":
            # Stop streaming
            await self.stop_streaming(control["symbol"])
```

### 5. Configuration Options

Define configurable options:

```yaml
options:
  api_key:
    type: string
    description: "API key for premium features"
    required: false
  timeout:
    type: int
    description: "Request timeout in seconds"
    default: 30
    required: false
  retries:
    type: int
    description: "Number of retry attempts"
    default: 3
    min: 0
    max: 10
    required: false
```

Access options in code:

```python
api_key = self.config.options.get("api_key")
timeout = self.config.options.get("timeout", 30)
```

### 6. Logging

Use the built-in logger:

```python
self.logger.info(f"Fetching data for {symbol}")
self.logger.warning("Rate limit exceeded, waiting...")
self.logger.error(f"API error: {e}")
```

### 7. Testing

Create tests for your provider:

```python
import pytest
from my_provider import MyProvider

@pytest.mark.asyncio
async def test_get_capabilities():
    provider = MyProvider()
    caps = await provider.get_capabilities()
    assert caps["supports_historical"] == True
    assert "stocks" in caps["data_types"]

@pytest.mark.asyncio
async def test_get_historical_data():
    provider = MyProvider()
    data = []
    async for item in provider.get_historical_data("AAPL", "1d", start_time, end_time):
        data.append(item)

    assert len(data) > 0
    assert all(item["symbol"] == "AAPL" for item in data)
```

## Docker Integration

Create a `Dockerfile` for your provider:

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

# Run provider
EXPOSE 50051
CMD ["python", "src/main.py"]
```

Build and push:

```bash
# Build for multiple architectures
docker buildx build --platform linux/amd64,linux/arm64 -t ghcr.io/yourusername/timebase-amd64-my-provider .
docker push ghcr.io/yourusername/timebase-amd64-my-provider
```

## CLI Tools

The SDK includes CLI tools for provider development:

```bash
# Initialize a new provider
timebase-sdk init my-provider

# Generate protobuf code
timebase-sdk generate-proto

# Validate provider configuration
timebase-sdk validate config.yaml

# Test provider locally
timebase-sdk test --config config.yaml
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests
5. Run the test suite: `pytest`
6. Submit a pull request

## License

MIT License - see LICENSE file for details.