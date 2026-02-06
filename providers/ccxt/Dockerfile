FROM python:3.11-slim

# Set environment variables
ENV PYTHONUNBUFFERED=1
ENV PYTHONDONTWRITEBYTECODE=1

# Install system dependencies
RUN apt-get update && apt-get install -y \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

# Create app user
RUN useradd --create-home --shell /bin/bash app

# Set work directory
WORKDIR /app

# Copy and install TimeBase SDK from local source
COPY src/TimeBase.ProviderSdk /tmp/timebase-sdk
RUN pip install --no-cache-dir --upgrade pip && \
    pip install --no-cache-dir /tmp/timebase-sdk

# Generate protobuf files in the installed package location
RUN python -c "import sys; from pathlib import Path; sdk_path = Path([p for p in sys.path if 'site-packages' in p][0]) / 'timebase_sdk'; print(f'SDK installed at: {sdk_path}')" && \
    python -m grpc_tools.protoc \
    --proto_path=/tmp/timebase-sdk/timebase_sdk/protos \
    --python_out=/usr/local/lib/python3.11/site-packages/timebase_sdk/generated \
    --grpc_python_out=/usr/local/lib/python3.11/site-packages/timebase_sdk/generated \
    /tmp/timebase-sdk/timebase_sdk/protos/provider.proto && \
    sed -i 's/^import provider_pb2/from . import provider_pb2/' /usr/local/lib/python3.11/site-packages/timebase_sdk/generated/provider_pb2_grpc.py && \
    rm -rf /tmp/timebase-sdk

# Copy requirements and install other dependencies
COPY providers/ccxt/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY providers/ccxt/src/ ./src/
COPY providers/ccxt/config.yaml .

# Change ownership to app user
RUN chown -R app:app /app

# Switch to app user
USER app

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD python -c "from timebase_sdk import TimeBaseProvider; print('OK')" || exit 1

# Expose gRPC port
EXPOSE 50051

# Run the provider
CMD ["python", "src/main.py"]
