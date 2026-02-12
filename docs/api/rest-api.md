# TimeBase REST API Specification

This document specifies the REST API for TimeBase, providing programmatic access to financial time series data.

## Overview

The TimeBase REST API provides data query and provider management capabilities:

1. **Data queries**: Client specifies a symbol and provider to fetch historical data
2. **Provider management**: Install, configure, and monitor data providers

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
Supported intervals (validated by core):
- `1m`, `5m`, `15m`, `30m`
- `1h`, `4h`
- `1d`
- `1wk`, `1mo`

---

## Data Endpoints

### Get Historical Data

Fetches historical OHLCV data for a symbol from a specified provider.

**Endpoint**: `GET /api/data/{symbol}`

**Parameters**:
- `symbol` (path, required): Financial symbol (e.g., `AAPL`, `BTC-USD`)
- `provider` (query, required): Provider slug to use (e.g., `yahoo-finance`)
- `interval` (query, optional): Time interval (default: `1d`)
- `start` (query, optional): Start date/time (default: 30 days ago)
- `end` (query, optional): End date/time (default: now)

**Example Request**:
```http
GET /api/data/AAPL?provider=yahoo-finance&interval=1d&start=2024-01-01&end=2024-12-31
```

**Example Response**:
```json
{
  "symbol": "AAPL",
  "interval": "1d",
  "start": "2024-01-01T00:00:00Z",
  "end": "2024-12-31T00:00:00Z",
  "count": 252,
  "data": [
    {
      "time": "2024-01-02T00:00:00Z",
      "symbol": "AAPL",
      "providerId": "550e8400-e29b-41d4-a716-446655440000",
      "interval": "1d",
      "open": 184.22,
      "high": 186.95,
      "low": 183.89,
      "close": 185.64,
      "volume": 82488200,
      "metadata": null
    },
    {
      "time": "2024-01-03T00:00:00Z",
      "symbol": "AAPL",
      "providerId": "550e8400-e29b-41d4-a716-446655440000",
      "interval": "1d",
      "open": 183.89,
      "high": 185.92,
      "low": 183.43,
      "close": 184.25,
      "volume": 58414500,
      "metadata": null
    }
  ]
}
```

### Get Data Summary

Get a summary of available data for a symbol.

**Endpoint**: `GET /api/data/{symbol}/summary`

**Example Request**:
```http
GET /api/data/AAPL/summary
```

**Example Response**:
```json
{
  "summary": {
    "symbol": "AAPL",
    "totalDataPoints": 1260,
    "earliestDate": "2020-01-01T00:00:00Z",
    "latestDate": "2024-12-31T00:00:00Z",
    "providers": 1,
    "intervals": ["1d", "1wk"]
  }
}
```

### Get Providers for Symbol

List all providers that support a specific symbol.

**Endpoint**: `GET /api/data/{symbol}/providers`

**Example Request**:
```http
GET /api/data/AAPL/providers
```

**Example Response**:
```json
{
  "symbol": "AAPL",
  "count": 1,
  "providers": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "slug": "yahoo-finance",
      "name": "Yahoo Finance Provider",
      "version": "1.0.0",
      "enabled": true,
      "repositoryUrl": "https://github.com/MarcelGa/TimeBase",
      "imageUrl": null,
      "grpcEndpoint": "localhost:50053",
      "config": null,
      "capabilities": null,
      "createdAt": "2024-01-01T00:00:00Z",
      "updatedAt": "2024-01-01T00:00:00Z"
    }
  ]
}
```

---

## Provider Management Endpoints

### List Providers

Get a list of all installed providers.

**Endpoint**: `GET /api/providers`

**Example Response**:
```json
{
  "providers": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "slug": "yahoo-finance",
      "name": "Yahoo Finance Provider",
      "version": "1.0.0",
      "enabled": true,
      "repositoryUrl": "https://github.com/MarcelGa/TimeBase",
      "grpcEndpoint": "localhost:50053",
      "createdAt": "2024-01-01T00:00:00Z",
      "updatedAt": "2024-01-01T00:00:00Z"
    }
  ]
}
```

### Get Provider Symbols

Get symbols available from providers.

**Endpoint**: `GET /api/providers/symbols`

**Parameters**:
- `provider` (query, optional): Filter by provider slug

**Example Request**:
```http
GET /api/providers/symbols?provider=yahoo-finance
```

**Example Response**:
```json
{
  "providers": [
    {
      "slug": "yahoo-finance",
      "name": "yahoo-finance",
      "symbols": [
        {
          "symbol": "AAPL",
          "name": "Apple Inc.",
          "type": "stock",
          "intervals": ["1d", "1wk", "1mo"],
          "metadata": null
        },
        {
          "symbol": "BTC-USD",
          "name": "Bitcoin USD",
          "type": "crypto",
          "intervals": ["1d", "1h"],
          "metadata": null
        }
      ]
    }
  ],
  "totalSymbols": 2
}
```

### Get Provider Details

Get detailed information about a specific provider.

**Endpoint**: `GET /api/providers/{slug}`

**Example Request**:
```http
GET /api/providers/yahoo-finance
```

**Example Response**:
```json
{
  "provider": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "slug": "yahoo-finance",
    "name": "Yahoo Finance Provider",
    "version": "1.0.0",
    "enabled": true,
    "repositoryUrl": "https://github.com/MarcelGa/TimeBase",
    "grpcEndpoint": "localhost:50053",
    "createdAt": "2024-01-01T00:00:00Z",
    "updatedAt": "2024-01-01T00:00:00Z"
  }
}
```

### Install Provider

Install a new provider from a GitHub repository.

**Endpoint**: `POST /api/providers`

**Request Body**:
```json
{
  "repository": "https://github.com/MarcelGa/TimeBase"
}
```

**Example Response** (201 Created):
```json
{
  "message": "Provider installed successfully",
  "provider": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "slug": "yahoo-finance",
    "name": "Yahoo Finance Provider",
    "version": "1.0.0",
    "enabled": true,
    "repositoryUrl": "https://github.com/MarcelGa/TimeBase",
    "grpcEndpoint": "localhost:50053",
    "createdAt": "2024-01-01T00:00:00Z",
    "updatedAt": "2024-01-01T00:00:00Z"
  }
}
```

### Uninstall Provider

Remove a provider from the system.

**Endpoint**: `DELETE /api/providers/{slug}`

**Example Response**:
```json
{
  "message": "Provider uninstalled successfully"
}
```

### Enable/Disable Provider

Enable or disable a provider.

**Endpoint**: `PATCH /api/providers/{slug}/enabled`

**Request Body**:
```json
{
  "enabled": false
}
```

**Example Response**:
```json
{
  "message": "Provider disabled successfully",
  "provider": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "slug": "yahoo-finance",
    "name": "Yahoo Finance Provider",
    "version": "1.0.0",
    "enabled": false,
    "repositoryUrl": "https://github.com/MarcelGa/TimeBase",
    "imageUrl": null,
    "grpcEndpoint": "localhost:50053",
    "config": null,
    "capabilities": null,
    "createdAt": "2024-01-01T00:00:00Z",
    "updatedAt": "2024-01-01T00:00:00Z"
  }
}
```

### Refresh Provider Capabilities

Refresh capabilities for a specific provider.

**Endpoint**: `POST /api/providers/{slug}/capabilities`

**Example Response**:
```json
{
  "message": "Provider capabilities updated successfully",
  "provider": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "slug": "yahoo-finance",
    "name": "Yahoo Finance Provider",
    "version": "1.0.0",
    "enabled": true,
    "repositoryUrl": "https://github.com/MarcelGa/TimeBase",
    "imageUrl": null,
    "grpcEndpoint": "localhost:50053",
    "config": null,
    "capabilities": null,
    "createdAt": "2024-01-01T00:00:00Z",
    "updatedAt": "2024-01-01T00:00:00Z"
  },
  "capabilities": {
    "name": "Yahoo Finance Provider",
    "version": "1.0.0",
    "slug": "yahoo-finance",
    "supportsHistorical": true,
    "supportsRealtime": true,
    "dataTypes": ["stocks", "etfs", "indices", "crypto"],
    "intervals": ["1m", "5m", "15m", "1h", "1d", "1wk", "1mo"]
  }
}
```

### Refresh All Provider Capabilities

Refresh capabilities for all enabled providers.

**Endpoint**: `POST /api/providers/capabilities/refresh`

**Example Response**:
```json
{
  "message": "All provider capabilities updated successfully",
  "count": 2,
  "providers": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "slug": "yahoo-finance",
      "name": "Yahoo Finance Provider",
      "version": "1.0.0",
      "enabled": true,
      "repositoryUrl": "https://github.com/MarcelGa/TimeBase",
      "imageUrl": null,
      "grpcEndpoint": "localhost:50053",
      "config": null,
      "capabilities": null,
      "createdAt": "2024-01-01T00:00:00Z",
      "updatedAt": "2024-01-01T00:00:00Z"
    },
    {
      "id": "660e8400-e29b-41d4-a716-446655440001",
      "slug": "minimal",
      "name": "Minimal Provider",
      "version": "1.0.0",
      "enabled": true,
      "repositoryUrl": "https://github.com/MarcelGa/TimeBase",
      "imageUrl": null,
      "grpcEndpoint": "localhost:50054",
      "config": null,
      "capabilities": null,
      "createdAt": "2024-01-01T00:00:00Z",
      "updatedAt": "2024-01-01T00:00:00Z"
    }
  ]
}
```

### Check Provider Health

Check the health status of a specific provider.

**Endpoint**: `GET /api/providers/{slug}/health`

**Example Response**:
```json
{
  "provider": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "slug": "yahoo-finance",
    "name": "Yahoo Finance Provider"
  },
  "healthy": true,
  "checkedAt": "2024-01-01T10:30:00Z"
}
```

---

## Health Endpoints

Health check endpoints are available at the root level (not under `/api`).

### Liveness Check

Simple check that the application is running.

**Endpoint**: `GET /health/live`

**Example Response**:
```json
{
  "status": "Healthy"
}
```

### Readiness Check

Check that the application and its dependencies are ready to serve requests.

**Endpoint**: `GET /health/ready`

**Example Response**:
```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "database",
      "status": "Healthy",
      "description": "Database connection is healthy",
      "duration": 15.5
    }
  ],
  "totalDuration": 20.3
}
```

### Full Health Check

Comprehensive health check of all system components.

**Endpoint**: `GET /health`

**Example Response**:
```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "database",
      "status": "Healthy",
      "description": "Database connection is healthy",
      "duration": 15.5
    },
    {
      "name": "provider-yahoo-finance",
      "status": "Healthy",
      "description": null,
      "duration": 50.2
    }
  ],
  "totalDuration": 65.7
}
```

---

## Metrics Endpoint

Prometheus metrics are available for monitoring.

**Endpoint**: `GET /metrics`

Returns Prometheus-formatted metrics including:
- ASP.NET Core HTTP request metrics
- Runtime metrics (GC, CPU, memory)
- Custom TimeBase business metrics

---

## Error Responses

Error responses use ASP.NET Core Problem Details format:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Provider 'unknown' not found",
  "status": 404
}
```

Or for validation errors:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "repository": ["The repository field is required."]
  }
}
```

### Common Error Codes

- `400 Bad Request`: Invalid request parameters or validation failure
- `404 Not Found`: Provider or symbol not found
- `500 Internal Server Error`: Unexpected server error

---

## Rate Limiting

Rate limiting is configured via `IpRateLimiting` settings. Default limits can be configured in `appsettings.json`.

---

## OpenAPI Specification

Interactive API documentation is available in development mode:

**Swagger UI**: `GET /swagger`

**OpenAPI JSON**: `GET /swagger/v1/swagger.json`

---

## Support

For API support and questions:

- **GitHub Issues**: https://github.com/MarcelGa/TimeBase/issues
- **GitHub Discussions**: https://github.com/MarcelGa/TimeBase/discussions
