# Build stage - install dependencies with build tools
FROM python:3.11-alpine AS builder

# Set environment variables
ENV PYTHONUNBUFFERED=1
ENV PYTHONDONTWRITEBYTECODE=1

# Install build dependencies
RUN apk add --no-cache \
    gcc \
    musl-dev \
    linux-headers \
    libffi-dev

WORKDIR /build

# Copy and install TimeBase SDK from local source
COPY src/TimeBase.ProviderSdk /tmp/timebase-sdk
RUN pip install --no-cache-dir --upgrade pip && \
    pip install --no-cache-dir /tmp/timebase-sdk

# Generate protobuf files in the installed package location
RUN python -m grpc_tools.protoc \
    --proto_path=/tmp/timebase-sdk/timebase_sdk/protos \
    --python_out=/usr/local/lib/python3.11/site-packages/timebase_sdk/generated \
    --grpc_python_out=/usr/local/lib/python3.11/site-packages/timebase_sdk/generated \
    /tmp/timebase-sdk/timebase_sdk/protos/provider.proto && \
    sed -i 's/^import provider_pb2/from . import provider_pb2/' /usr/local/lib/python3.11/site-packages/timebase_sdk/generated/provider_pb2_grpc.py

# Copy requirements and install provider dependencies
COPY providers/examples/minimal-provider/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Runtime stage - minimal image without build tools
FROM python:3.11-alpine AS runtime

# Set environment variables
ENV PYTHONUNBUFFERED=1
ENV PYTHONDONTWRITEBYTECODE=1

# Install only runtime dependencies (no build tools)
RUN apk add --no-cache libstdc++

# Create non-root user
RUN addgroup -g 1000 app && \
    adduser -D -u 1000 -G app app

WORKDIR /app

# Copy installed packages from builder
COPY --from=builder /usr/local/lib/python3.11/site-packages /usr/local/lib/python3.11/site-packages

# Copy application code
COPY providers/examples/minimal-provider/src/ ./src/
COPY providers/examples/minimal-provider/config.yaml .

# Change ownership to app user
RUN chown -R app:app /app

# Switch to non-root user
USER app

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD python -c "import asyncio; from timebase_sdk import TimeBaseProvider; print('OK')" || exit 1

# Expose gRPC port
EXPOSE 50051

# Run the provider
CMD ["python", "src/main.py"]