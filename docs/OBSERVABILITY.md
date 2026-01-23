# Observability Stack

TimeBase includes a comprehensive observability stack for monitoring, tracing, and analyzing your time-series data infrastructure in development and production environments.

## Stack Components

### 1. Jaeger - Distributed Tracing
- **Purpose**: Track and visualize requests across services
- **Access**: http://localhost:16686
- **Features**:
  - Trace ASP.NET Core HTTP requests
  - Trace Entity Framework Core database queries
  - Trace HTTP client requests to external services
  - Visualize request flows and timing

### 2. Prometheus - Metrics Collection
- **Purpose**: Collect and store time-series metrics
- **Access**: http://localhost:9090
- **Features**:
  - Scrapes metrics from TimeBase.Core `/metrics` endpoint
  - Stores historical metric data
  - Provides PromQL query interface
  - ASP.NET Core metrics (requests, duration, errors)
  - Runtime metrics (GC, CPU, memory)

### 3. Grafana - Visualization & Dashboards
- **Purpose**: Create dashboards and visualize metrics
- **Access**: http://localhost:3000
- **Default Credentials**: `admin` / `admin`
- **Features**:
  - Pre-configured Prometheus datasource
  - Pre-configured Jaeger datasource
  - Basic ASP.NET Core overview dashboard included
  - Create custom dashboards for your metrics

## Quick Start

### Start with Observability Stack

```bash
cd src/docker
docker-compose --profile observability up
```

This starts:
- TimescaleDB (database)
- TimeBase.Core (API service)
- Jaeger (tracing UI)
- Prometheus (metrics collector)
- Grafana (visualization)

### Access the UIs

After starting the stack, access:

1. **Jaeger UI**: http://localhost:16686
   - Select "TimeBase.Core" from the service dropdown
   - Search for traces
   - View detailed request timelines

2. **Prometheus**: http://localhost:9090
   - Query metrics with PromQL
   - Example: `http_server_request_duration_seconds_sum`
   - Check scraping targets at http://localhost:9090/targets

3. **Grafana**: http://localhost:3000
   - Login: `admin` / `admin`
   - Navigate to "Dashboards" → "TimeBase Overview"
   - Create custom dashboards

### Start Without Observability

If you only need the core service and database:

```bash
cd src/docker
docker-compose up
```

This starts only TimescaleDB and TimeBase.Core (without observability tools).

## OpenTelemetry Configuration

TimeBase uses OpenTelemetry for instrumentation, which provides vendor-neutral telemetry data collection.

### Default Configuration

Located in `src/TimeBase.Core/appsettings.json`:

```json
{
  "OpenTelemetry": {
    "ServiceName": "TimeBase.Core",
    "ServiceVersion": "1.0.0",
    "Tracing": {
      "Enabled": true,
      "ConsoleExporter": false,
      "OtlpExporter": true,
      "OtlpEndpoint": "http://localhost:4317"
    },
    "Metrics": {
      "Enabled": true,
      "ConsoleExporter": false,
      "OtlpExporter": true,
      "OtlpEndpoint": "http://localhost:4317"
    }
  }
}
```

### Docker Environment Override

When running with docker-compose, the OTLP endpoint is overridden to point to Jaeger:

```yaml
environment:
  OpenTelemetry__Tracing__OtlpEndpoint: http://jaeger:4317
  OpenTelemetry__Metrics__OtlpEndpoint: http://jaeger:4317
```

### Disable Observability

To run locally without sending telemetry data, update `appsettings.json`:

```json
{
  "OpenTelemetry": {
    "Tracing": {
      "OtlpExporter": false
    },
    "Metrics": {
      "OtlpExporter": false
    }
  }
}
```

## Instrumentation Details

### Automatic Tracing

TimeBase automatically traces:
- **ASP.NET Core**: HTTP requests, routing, middleware
- **Entity Framework Core**: Database queries, connections
- **HTTP Client**: Outbound HTTP requests

### Automatic Metrics

TimeBase automatically collects:
- **ASP.NET Core Metrics**:
  - `http_server_request_duration_seconds` - Request duration histogram
  - `http_server_active_requests` - Active request count
  - `http_server_request_count` - Total request count
  
- **Runtime Metrics**:
  - `process_runtime_dotnet_gc_*` - Garbage collection stats
  - `process_runtime_dotnet_monitor_lock_contention_count` - Lock contention
  - `process_cpu_count` - Available CPU cores
  - `process_memory_usage` - Memory usage

### Prometheus Metrics Endpoint

The Prometheus scraping endpoint is available at:

```
http://localhost:8080/metrics
```

This endpoint exposes all OpenTelemetry metrics in Prometheus format.

## Pre-configured Dashboard

The included "TimeBase Overview" dashboard shows:
- HTTP request rate and latency
- Active requests
- Error rates
- GC performance
- Memory usage

### Creating Custom Dashboards

1. Access Grafana at http://localhost:3000
2. Navigate to "Dashboards" → "New" → "New Dashboard"
3. Add panels with PromQL queries:
   ```promql
   # Request rate
   rate(http_server_request_count_total[5m])
   
   # Average latency
   rate(http_server_request_duration_seconds_sum[5m]) / 
   rate(http_server_request_duration_seconds_count[5m])
   
   # Error rate
   rate(http_server_request_count_total{http_response_status_code=~"5.."}[5m])
   ```

## Production Considerations

### OTLP Collector

In production, consider using an OTLP collector instead of sending directly to backends:

```
Application → OTLP Collector → Jaeger/Prometheus/Other Backends
```

This provides:
- Buffering and retry logic
- Data transformation and filtering
- Multiple backend support
- Reduced application overhead

### Security

1. **Grafana**: Change default admin password immediately
2. **Prometheus**: Use authentication and TLS in production
3. **Jaeger**: Configure proper access controls
4. **Network**: Use internal networks, not exposed ports

### Performance

- OTLP exporters use batching to minimize overhead
- Sampling can be configured for high-traffic environments
- Prometheus scraping is pull-based and non-blocking

### Data Retention

Configure retention policies:
- **Prometheus**: Default 15 days (configurable)
- **Jaeger**: Configure storage backend retention
- **Grafana**: Metadata only, relies on datasources

## Troubleshooting

### No Traces in Jaeger

1. Check Jaeger is running: `docker ps | grep jaeger`
2. Verify OTLP endpoint configuration
3. Check TimeBase.Core logs for OpenTelemetry errors
4. Verify `OtlpExporter: true` in configuration

### Prometheus Not Scraping

1. Check Prometheus targets: http://localhost:9090/targets
2. Verify TimeBase.Core `/metrics` endpoint is accessible
3. Check `prometheus.yml` scrape configuration
4. Ensure TimeBase.Core is running and healthy

### Grafana Datasource Issues

1. Verify Prometheus is running and accessible
2. Check datasource configuration in Grafana settings
3. Test datasource connection
4. Check network connectivity between containers

### High Memory Usage

1. Reduce Prometheus retention period
2. Increase scrape interval (default: 15s)
3. Configure OpenTelemetry sampling for traces
4. Limit Jaeger storage retention

## Architecture

```
┌─────────────────┐
│ TimeBase.Core   │
│                 │
│ OpenTelemetry  │
│  - Tracing     │─────OTLP(4317)────┐
│  - Metrics     │                    │
│  - Prometheus  │                    ▼
│    (/metrics)  │              ┌──────────┐
└────────┬────────┘              │  Jaeger  │
         │                       │          │
         │ HTTP Scrape           │  :16686  │
         │ (every 15s)           └──────────┘
         │                             │
         ▼                             │
    ┌──────────┐                       │
    │Prometheus│                       │
    │          │                       │
    │  :9090   │                       │
    └────┬─────┘                       │
         │                             │
         └──────────┬──────────────────┘
                    │
                    ▼
              ┌──────────┐
              │ Grafana  │
              │          │
              │  :3000   │
              └──────────┘
```

## Resources

- [OpenTelemetry Documentation](https://opentelemetry.io/docs/)
- [Jaeger Documentation](https://www.jaegertracing.io/docs/)
- [Prometheus Documentation](https://prometheus.io/docs/)
- [Grafana Documentation](https://grafana.com/docs/)
- [ASP.NET Core Metrics](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/built-in-metrics-aspnetcore)
