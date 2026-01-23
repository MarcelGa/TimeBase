# TimeBase REST API Specification

This document specifies the REST API for TimeBase, providing programmatic access to financial time series data.

## Overview

The TimeBase REST API provides two main query patterns:

1. **Symbol-centric**: Client specifies a symbol, core automatically selects the best provider
2. **Provider-aware**: Client explicitly chooses both provider and symbol

## Base URL

```
http://localhost:8080/api
```

## Authentication

**Phase 1**: No authentication required (single-user mode)

**Future**: Bearer token authentication
```
Authorization: Bearer <token>
```

## Common Parameters

### Time Parameters
All time parameters accept ISO 8601 format:
```
2024-01-01T00:00:00Z
2024-01-01
2024-01-01T09:30:00-05:00
```

### Interval Parameters
Supported intervals:
- `1m`, `5m`, `15m`, `30m`
- `1h`, `4h`, `1d`
- `1wk`, `1mo`

### Response Format
All responses are JSON. Successful responses include:
```json
{
  "success": true,
  "data": { ... },
  "meta": {
    "requestId": "uuid",
    "timestamp": "2024-01-01T00:00:00Z",
    "processingTimeMs": 150
  }
}
```

Error responses:
```json
{
  "success": false,
  "error": {
    "code": "PROVIDER_UNAVAILABLE",
    "message": "Provider yahoo-finance is currently unavailable",
    "details": { ... }
  },
  "meta": { ... }
}
```

---

## Historical Data Endpoints

### Get Historical Data (Symbol-centric)

Automatically selects the best available provider for the requested symbol.

**Endpoint**: `GET /api/data/{symbol}`

**Parameters**:
- `symbol` (path): Financial symbol (e.g., `AAPL`, `BTC-USD`)
- `interval` (query, required): Time interval
- `start` (query, required): Start date/time
- `end` (query, required): End date/time
- `limit` (query, optional): Maximum number of data points (default: unlimited)

**Example Request**:
```http
GET /api/data/AAPL?interval=1d&start=2024-01-01&end=2024-12-31
```

**Example Response**:
```json
{
  "success": true,
  "data": {
    "symbol": "AAPL",
    "interval": "1d",
    "provider": "yahoo-finance",
    "count": 252,
    "data": [
      {
        "timestamp": "2024-01-02T00:00:00Z",
        "open": 184.22,
        "high": 186.95,
        "low": 183.89,
        "close": 185.64,
        "volume": 82488200
      },
      {
        "timestamp": "2024-01-03T00:00:00Z",
        "open": 183.89,
        "high": 185.92,
        "low": 183.43,
        "close": 184.25,
        "volume": 58414500
      }
    ]
  },
  "meta": {
    "requestId": "550e8400-e29b-41d4-a716-446655440000",
    "timestamp": "2024-01-01T10:30:00Z",
    "processingTimeMs": 245
  }
}
```

### Get Historical Data (Provider-aware)

Explicitly specifies which provider to use.

**Endpoint**: `GET /api/providers/{providerSlug}/data/{symbol}`

**Parameters**:
- `providerSlug` (path): Provider identifier (e.g., `yahoo-finance`)
- `symbol` (path): Financial symbol
- `interval` (query, required): Time interval
- `start` (query, required): Start date/time
- `end` (query, required): End date/time
- `limit` (query, optional): Maximum number of data points

**Example Request**:
```http
GET /api/providers/yahoo-finance/data/AAPL?interval=1d&start=2024-01-01&end=2024-12-31
```

**Example Response**: Same format as symbol-centric endpoint, but guaranteed to use the specified provider.

---

## Provider Management Endpoints

### List Providers

Get a list of all installed providers.

**Endpoint**: `GET /api/providers`

**Example Response**:
```json
{
  "success": true,
  "data": {
    "providers": [
      {
        "slug": "yahoo-finance",
        "name": "Yahoo Finance Provider",
        "version": "1.0.0",
        "enabled": true,
        "status": "healthy",
        "capabilities": {
          "historical": true,
          "realtime": false,
          "dataTypes": ["stocks", "etfs", "indices"],
          "intervals": ["1m", "5m", "15m", "1h", "1d", "1wk", "1mo"]
        },
        "rateLimits": {
          "requestsPerMinute": 60,
          "requestsPerDay": 2000
        }
      },
      {
        "slug": "alpha-vantage",
        "name": "Alpha Vantage Provider",
        "version": "1.0.0",
        "enabled": true,
        "status": "healthy",
        "capabilities": {
          "historical": true,
          "realtime": false,
          "dataTypes": ["stocks", "forex", "crypto"],
          "intervals": ["1m", "5m", "15m", "30m", "1h", "1d"]
        }
      }
    ]
  }
}
```

### Get Provider Details

Get detailed information about a specific provider.

**Endpoint**: `GET /api/providers/{providerSlug}`

**Example Response**:
```json
{
  "success": true,
  "data": {
    "slug": "yahoo-finance",
    "name": "Yahoo Finance Provider",
    "version": "1.0.0",
    "description": "Historical stock data from Yahoo Finance",
    "author": "Marcel Galovič <galovic.marcel@gmail.com>",
    "repository": "https://github.com/marcelga/timebase-provider-yahoo",
    "enabled": true,
    "status": "healthy",
    "lastHealthCheck": "2024-01-01T10:30:00Z",
    "capabilities": { ... },
    "config": {
      "timeout": 30,
      "retryAttempts": 3
    }
  }
}
```

### Install Provider

Install a new provider from a GitHub repository.

**Endpoint**: `POST /api/providers`

**Request Body**:
```json
{
  "repository": "https://github.com/marcelga/timebase-provider-yahoo"
}
```

**Example Response**:
```json
{
  "success": true,
  "data": {
    "slug": "yahoo-finance",
    "name": "Yahoo Finance Provider",
    "status": "installing",
    "message": "Provider installation started. This may take a few minutes."
  }
}
```

### Update Provider Configuration

Update configuration for an installed provider.

**Endpoint**: `PUT /api/providers/{providerSlug}/config`

**Request Body**:
```json
{
  "timeout": 60,
  "apiKey": "your-api-key-here"
}
```

### Uninstall Provider

Remove a provider from the system.

**Endpoint**: `DELETE /api/providers/{providerSlug}`

**Example Response**:
```json
{
  "success": true,
  "data": {
    "message": "Provider yahoo-finance has been uninstalled successfully"
  }
}
```

---

## Symbol Endpoints

### Search Symbols

Search for available symbols across all providers.

**Endpoint**: `GET /api/symbols`

**Parameters**:
- `query` (query, optional): Search term (e.g., "Apple", "AAPL")
- `type` (query, optional): Filter by type (`stock`, `crypto`, `forex`, etc.)
- `limit` (query, optional): Maximum results (default: 50)

**Example Request**:
```http
GET /api/symbols?query=Apple&type=stock&limit=10
```

**Example Response**:
```json
{
  "success": true,
  "data": {
    "symbols": [
      {
        "symbol": "AAPL",
        "name": "Apple Inc.",
        "type": "stock",
        "exchange": "NASDAQ",
        "providers": ["yahoo-finance", "alpha-vantage"]
      },
      {
        "symbol": "AAPL.MX",
        "name": "Apple Inc.",
        "type": "stock",
        "exchange": "MEX",
        "providers": ["yahoo-finance"]
      }
    ],
    "total": 2
  }
}
```

### Get Symbol Info

Get detailed information about a specific symbol.

**Endpoint**: `GET /api/symbols/{symbol}`

**Example Response**:
```json
{
  "success": true,
  "data": {
    "symbol": "AAPL",
    "name": "Apple Inc.",
    "type": "stock",
    "exchange": "NASDAQ",
    "currency": "USD",
    "providers": [
      {
        "slug": "yahoo-finance",
        "intervals": ["1m", "5m", "1d", "1wk"],
        "lastUpdate": "2024-01-01T16:00:00Z"
      },
      {
        "slug": "alpha-vantage",
        "intervals": ["1m", "5m", "1d"],
        "lastUpdate": "2024-01-01T16:00:00Z"
      }
    ]
  }
}
```

### Get Providers for Symbol

List all providers that support a specific symbol.

**Endpoint**: `GET /api/symbols/{symbol}/providers`

**Example Response**:
```json
{
  "success": true,
  "data": {
    "symbol": "AAPL",
    "providers": [
      {
        "slug": "yahoo-finance",
        "name": "Yahoo Finance Provider",
        "intervals": ["1m", "5m", "15m", "1h", "1d", "1wk", "1mo"],
        "priority": 1,
        "status": "healthy"
      },
      {
        "slug": "alpha-vantage",
        "name": "Alpha Vantage Provider",
        "intervals": ["1m", "5m", "15m", "30m", "1h", "1d"],
        "priority": 2,
        "status": "healthy"
      }
    ]
  }
}
```

---

## System Endpoints

### Health Check

Check the overall health of the TimeBase system.

**Endpoint**: `GET /api/health`

**Example Response**:
```json
{
  "success": true,
  "data": {
    "status": "healthy",
    "version": "1.0.0",
    "uptime": "2d 4h 30m",
    "services": {
      "database": "healthy",
      "core": "healthy",
      "providers": {
        "yahoo-finance": "healthy",
        "alpha-vantage": "healthy"
      }
    }
  }
}
```

### Get API Version

Get version information about the API.

**Endpoint**: `GET /api/version`

**Example Response**:
```json
{
  "success": true,
  "data": {
    "version": "1.0.0",
    "build": "2024-01-01T10:00:00Z",
    "commit": "abc123def456",
    "supportedVersions": ["1.0"]
  }
}
```

---

## Error Codes

### Provider Errors
- `PROVIDER_NOT_FOUND`: Specified provider doesn't exist
- `PROVIDER_UNAVAILABLE`: Provider is installed but not responding
- `PROVIDER_RATE_LIMITED`: Provider has exceeded rate limits
- `PROVIDER_CAPABILITY_MISSING`: Provider doesn't support requested operation

### Data Errors
- `SYMBOL_NOT_FOUND`: Symbol not found in any provider
- `INVALID_INTERVAL`: Unsupported interval for symbol/provider
- `INVALID_TIME_RANGE`: Invalid start/end time combination
- `DATA_UNAVAILABLE`: No data available for the requested parameters

### System Errors
- `DATABASE_ERROR`: Database connection or query error
- `INTERNAL_ERROR`: Unexpected server error
- `MAINTENANCE_MODE`: System is temporarily unavailable for maintenance

### Client Errors
- `INVALID_REQUEST`: Malformed request or missing required parameters
- `UNAUTHORIZED`: Authentication required (future)
- `FORBIDDEN`: Insufficient permissions (future)

---

## Rate Limiting

**Phase 1**: No rate limiting (MVP)

**Future Implementation**:
- Per-client rate limits
- Burst allowance
- Rate limit headers in responses

```
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 950
X-RateLimit-Reset: 1640995200
```

---

## Pagination

For endpoints that return lists, pagination is supported:

**Parameters**:
- `page` (query, optional): Page number (1-based, default: 1)
- `pageSize` (query, optional): Items per page (default: 50, max: 1000)

**Response Format**:
```json
{
  "success": true,
  "data": {
    "items": [...],
    "pagination": {
      "page": 1,
      "pageSize": 50,
      "totalItems": 1250,
      "totalPages": 25,
      "hasNext": true,
      "hasPrevious": false
    }
  }
}
```

---

## Content Types

### Request Content Types
- `application/json`: All request bodies
- `application/x-www-form-urlencoded`: Simple form data (future)

### Response Content Types
- `application/json`: Default response format
- `application/xml`: Alternative format (future)
- `text/csv`: Data export (future)

### Compression
- Request compression: `gzip`, `deflate`
- Response compression: Automatic for responses > 1KB

---

## Cross-Origin Resource Sharing (CORS)

**Phase 1**: Allow all origins for development

**Future**: Configurable CORS policy
```
Access-Control-Allow-Origin: *
Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS
Access-Control-Allow-Headers: Content-Type, Authorization, X-Requested-With
```

---

## API Versioning

API versioning uses URL path versioning:

```
/api/v1/data/AAPL
/api/v1/providers
```

**Version Compatibility**:
- Breaking changes: New major version (v2, v3, etc.)
- Backward compatible: Same version with deprecation warnings
- Experimental: `/api/experimental/` prefix

---

## Webhooks (Future)

Register webhooks for real-time notifications:

**Endpoint**: `POST /api/webhooks`

**Request Body**:
```json
{
  "url": "https://your-app.com/webhook",
  "events": ["data.updated", "provider.failed"],
  "filters": {
    "symbols": ["AAPL", "GOOGL"],
    "providers": ["yahoo-finance"]
  }
}
```

---

## OpenAPI Specification

The complete API is documented with OpenAPI 3.0 and available at:

```
GET /swagger/v1/swagger.json
```

Interactive documentation available at:

```
GET /swagger
```

---

## Client Libraries

### Official Libraries (Future)
- **JavaScript/TypeScript**: Browser and Node.js
- **Python**: Async client library
- **C#**: .NET client for integration
- **Go**: High-performance client

### Community Libraries
Community-maintained clients for other languages are encouraged.

---

## Support

For API support and questions:

- **Documentation**: https://timebase.dev/docs/api
- **GitHub Issues**: https://github.com/marcelga/TimeBase/issues
- **GitHub Discussions**: https://github.com/marcelga/TimeBase/discussions

---

## Changelog

### Version 1.0.0 (Current)
- Initial API release
- Symbol-centric and provider-aware data queries
- Provider management endpoints
- Symbol search and discovery
- Basic health checks and system information

### Planned for v1.1.0
- WebSocket real-time streaming endpoints
- Advanced query filters and aggregations
- Data export formats (CSV, Excel)
- Batch data operations

### Planned for v2.0.0
- Authentication and multi-user support
- Advanced analytics and technical indicators
- Alerting and notification system
- GraphQL API alternative