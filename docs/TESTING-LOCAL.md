# Testing TimeBase Locally with Yahoo Finance Provider

This guide shows you how to run and test TimeBase with the Yahoo Finance provider locally.

## Quick Start

### 1. Start All Services (Recommended for Development)

```bash
cd src/docker
docker-compose -f docker-compose.dev.yml up --build
```

This starts:
- **TimescaleDB** on port 5432
- **TimeBase Core** on port 8080 (API)
- **Yahoo Finance Provider** on port 50053 (gRPC)
- **Minimal Provider** on port 50052 (gRPC) - for testing

### 2. Verify Services are Running

```bash
# Check all containers are healthy
docker-compose -f docker-compose.dev.yml ps

# Check TimeBase Core health
curl http://localhost:8080/health

# View logs
docker-compose -f docker-compose.dev.yml logs -f
```

### 3. Test Yahoo Finance Provider

#### Get Stock Data (Apple)
```bash
curl "http://localhost:8080/api/providers/yahoo/data/AAPL?interval=1d&start=2024-01-01&end=2024-01-31"
```

#### Get Cryptocurrency Data (Bitcoin)
```bash
curl "http://localhost:8080/api/providers/yahoo/data/BTC-USD?interval=1h&start=2024-01-01&end=2024-01-02"
```

#### Get Index Data (S&P 500)
```bash
curl "http://localhost:8080/api/providers/yahoo/data/^GSPC?interval=1wk&start=2024-01-01&end=2024-12-31"
```

#### Get ETF Data (SPDR S&P 500)
```bash
curl "http://localhost:8080/api/providers/yahoo/data/SPY?interval=1d&start=2024-01-01&end=2024-01-31"
```

### 4. View API Documentation

Open in browser:
```
http://localhost:8080/swagger
```

### 5. Check Provider Status

```bash
# List all providers
curl http://localhost:8080/api/providers

# Get specific provider info
curl http://localhost:8080/api/providers/yahoo-finance
```

## Alternative: Start with Observability Stack

For full monitoring with Prometheus, Jaeger, and Grafana:

```bash
cd src/docker
docker-compose --profile observability --profile providers up --build
```

Access:
- **Grafana**: http://localhost:3000 (admin/admin)
- **Prometheus**: http://localhost:9090
- **Jaeger**: http://localhost:16686
- **TimeBase API**: http://localhost:8080

## Troubleshooting

### Provider Not Responding

Check provider logs:
```bash
docker logs timebase-yahoo-finance
```

Restart provider:
```bash
docker-compose -f docker-compose.dev.yml restart yahoo-finance-provider
```

### No Data Returned

1. Check if symbol is valid (Yahoo Finance format)
2. Verify date range is reasonable
3. Check provider logs for errors
4. Test with minimal provider first:
   ```bash
   curl "http://localhost:8080/api/providers/minimal/data/TEST?interval=1d&start=2024-01-01&end=2024-01-05"
   ```

### Rate Limit Errors

Yahoo Finance has limits:
- 60 requests per minute
- 2000 requests per day

Wait a minute or reduce request frequency.

### Database Connection Issues

Reset database:
```bash
docker-compose -f docker-compose.dev.yml down -v
docker-compose -f docker-compose.dev.yml up --build
```

## Stop Services

```bash
# Stop all services
docker-compose -f docker-compose.dev.yml down

# Stop and remove volumes (fresh start)
docker-compose -f docker-compose.dev.yml down -v
```

## Development Workflow

### Build Only One Service

```bash
# Rebuild just the Yahoo Finance provider
docker-compose -f docker-compose.dev.yml build yahoo-finance-provider

# Rebuild and restart
docker-compose -f docker-compose.dev.yml up -d --build yahoo-finance-provider
```

### View Specific Logs

```bash
# Core logs
docker logs -f timebase-core

# Yahoo Finance provider logs
docker logs -f timebase-yahoo-finance

# Database logs
docker logs -f timebase-db
```

### Test New Code Changes

1. Make changes to provider code
2. Rebuild:
   ```bash
   docker-compose -f docker-compose.dev.yml build yahoo-finance-provider
   ```
3. Restart:
   ```bash
   docker-compose -f docker-compose.dev.yml up -d yahoo-finance-provider
   ```

## Sample API Calls

### Historical Data

```bash
# Tesla daily data
curl "http://localhost:8080/api/providers/yahoo/data/TSLA?interval=1d&start=2024-01-01&end=2024-01-31"

# Microsoft 5-minute data (recent only)
curl "http://localhost:8080/api/providers/yahoo/data/MSFT?interval=5m&start=2024-01-23&end=2024-01-24"

# Gold futures
curl "http://localhost:8080/api/providers/yahoo/data/GC=F?interval=1d&start=2024-01-01&end=2024-01-31"
```

### Data Summary

```bash
curl "http://localhost:8080/api/data/AAPL/summary"
```

### Providers for Symbol

```bash
curl "http://localhost:8080/api/data/AAPL/providers"
```

## Next Steps

1. **Explore API**: Visit http://localhost:8080/swagger
2. **Monitor**: Check logs for data flow
3. **Customize**: Modify provider code and rebuild
4. **Add Providers**: Create your own provider following the pattern
