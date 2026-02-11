# CI/CD Pipeline

TimeBase uses GitHub Actions for continuous integration and delivery.

## Build Pipelines

### Core Service Build (`build-core.yml`)

**Triggers:**
- Push to `master` branch
- Pull requests to `master` branch
- Changes to core service files, tests, or core Docker files

**What it does:**
1. **Build & Test** (Ubuntu only)
   - Installs .NET 10.0 SDK
   - Restores NuGet dependencies
   - Builds solution in Release mode
   - Pulls TimescaleDB Docker image for integration tests
   - Runs all unit and integration tests using Testcontainers
   - Collects code coverage

2. **Coverage Reporting** (Ubuntu only)
   - Generates HTML coverage report
   - Uploads coverage artifacts
   - Comments coverage on PRs (minimum 70%)

3. **Code Quality** (Ubuntu only)
   - Verifies code formatting with `dotnet format`

4. **Docker Build** (Ubuntu only)
   - Builds Docker image
   - Tests image startup

**Requirements:**
- OS: Ubuntu Latest
- .NET: 10.0.x
- Docker: Available in GitHub Actions for Testcontainers

### E2E Tests (`e2e-tests.yml`)

**Triggers:**
- Push to `master` branch
- Pull requests to `master` branch
- Changes to core, infrastructure, SDK, Docker files, or minimal-provider

**What it does:**
1. **Build Stack Images**
   - Builds `timebase/core:latest` from source
   - Builds `timebase/minimal-provider:latest` from source

2. **Start Docker Compose Stack**
   - Starts TimescaleDB, seed container, core, and minimal-provider
   - Uses `docker-compose.dev.yml` with selective service start

3. **Wait for Readiness**
   - Polls `/health/ready` endpoint (max 60 seconds)
   - Ensures DB migrations and provider seeding complete

4. **Run E2E Tests** (6 tests)
   - Core health check (validates DB connection)
   - Provider registration (confirms seeding worked)
   - Provider details API (tests REST endpoint)
   - Provider symbols (validates gRPC GetSymbols unary RPC)
   - Historical data (validates full gRPC streaming pipeline)

5. **Failure Handling**
   - Dumps Docker Compose logs on failure
   - Uploads logs as artifacts for debugging

6. **Cleanup**
   - Always tears down containers and volumes

**Requirements:**
- OS: Ubuntu Latest
- Docker Compose: Available in GitHub Actions
- No external API dependencies (uses minimal-provider with dummy data)

**What it validates:**
- ✅ Docker images build with optimizations
- ✅ Containers start and become healthy
- ✅ Database migrations run successfully
- ✅ Provider seeding works
- ✅ gRPC communication (Core ↔ Provider)
- ✅ Full data pipeline (REST → Core → gRPC streaming → Provider → JSON response)

### Provider Build (`build-providers.yml`)

**Triggers:**
- Push to `main` branch
- Pull requests to `main` branch
- Changes to `providers/**`, `docker/**`, or workflow file

**What it does:**
1. **Build Provider Images** (Ubuntu only)
   - Builds Docker images for all providers in matrix
   - Current providers: `minimal-provider`, `yahoo-finance`
   - Supports multi-platform builds (linux/amd64, linux/arm64)
   - Pushes images to GitHub Container Registry (ghcr.io)
   - Generates build provenance attestation

2. **Test Provider Functionality** (Ubuntu only)
   - **Docker Smoke Test** (all providers): Verifies Docker image starts and Python runtime works
   - **Provider-Specific Tests**: Each provider can define custom tests via composite action
     - **Yahoo Finance** (`providers/yahoo-finance/.github/actions/test/action.yml`):
       - Comprehensive integration tests using crypto data (24/7 availability)
       - Tests capabilities, historical data, multiple symbols, intervals, data structure
       - Uses BTC-USD, ETH-USD for market-independent testing
       - Requires network access to Yahoo Finance API
     - **Minimal Provider**: Docker smoke test only (no custom tests)

**Requirements:**
- OS: Ubuntu Latest
- Python: 3.11 (for yahoo-finance tests)
- Network: Required for yahoo-finance tests (live API calls)

**Provider Matrix:**
| Provider | Build Context | Docker Test | Integration Tests |
|----------|--------------|-------------|-------------------|
| `minimal-provider` | `providers/examples/minimal-provider` | ✅ Python runtime | ❌ None |
| `yahoo-finance` | `providers/yahoo-finance` | ✅ Python runtime | ✅ 5 tests (crypto data) |

### Test Results

Test results are uploaded as artifacts on every run:
- `test-results` - Test results from test runs
- `coverage-results` - Code coverage data (Cobertura format)
- `coverage-report` - HTML coverage report

### Coverage Requirements

- **Minimum Coverage**: 70% (warning if below, doesn't fail build)
- **Format**: Cobertura XML
- **Report Types**: HTML, Cobertura, Badges

## Running Tests Locally

### E2E Tests (Docker Compose Stack)

The E2E tests validate the entire system running in Docker containers with real gRPC communication between Core and providers.

#### Prerequisites
- Docker and Docker Compose installed
- At least 4GB RAM available for containers

#### Run E2E Tests
```bash
# Build images
docker build -f src/docker/core/Dockerfile -t timebase/core:latest .
docker build -f providers/examples/minimal-provider/Dockerfile -t timebase/minimal-provider:latest .

# Start the stack
cd src/docker
docker compose -f docker-compose.dev.yml up -d core minimal-provider

# Wait for stack to be ready (or use the wait script below)
sleep 10

# Run manual tests
curl http://localhost:8080/health
curl http://localhost:8080/api/providers
curl http://localhost:8080/api/providers/minimal-provider/symbols
curl "http://localhost:8080/api/providers/minimal-provider/data/TEST?interval=1d&start=2024-01-01&end=2024-01-31"

# Tear down
docker compose -f docker-compose.dev.yml down -v
```

#### Wait for Health Script
Create a simple wait script or use this one-liner:
```bash
for i in {1..30}; do
  curl -s http://localhost:8080/health/ready && echo "Ready!" && break || sleep 2
done
```

#### What E2E Tests Validate

| Test | What It Validates | Technology Path |
|------|------------------|-----------------|
| Health endpoint | Core starts, DB connection works | REST → ASP.NET Health Checks → EF Core → TimescaleDB |
| Provider registration | Provider seeding works | Docker seed container → PostgreSQL |
| Provider details | Core can read provider config | REST → Core → PostgreSQL |
| Get symbols | gRPC communication works | REST → Core → gRPC client → Provider gRPC server |
| Historical data | Full data pipeline end-to-end | REST → Core → gRPC streaming → Provider → Core → JSON response |

The E2E tests use the **minimal-provider** because it generates dummy data without external API dependencies, making tests fast and reliable.

### Core (.NET) Tests

#### All Tests (including integration tests with Docker)
```bash
cd src
dotnet test TimeBase.slnx --configuration Release
```

#### Unit Tests Only (no Docker required)
```bash
cd src
dotnet test TimeBase.slnx --configuration Release --filter "FullyQualifiedName!~Integration"
```

#### With Coverage
```bash
cd src
dotnet test TimeBase.slnx --configuration Release \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults
```

#### Specific Test Project
```bash
cd src/TimeBase.Core.Tests
dotnet test --configuration Release
```

### Provider Tests

#### Yahoo Finance Provider
```bash
cd providers/yahoo-finance
pip install -r requirements.txt
python test_provider.py
```

**Notes:**
- Uses cryptocurrency data (BTC-USD, ETH-USD) for 24/7 availability
- Requires internet connection to access Yahoo Finance API
- Tests validate data structure and provider capabilities
- Safe to run anytime (no market hours dependency)

## Test Structure

### Core (.NET) Tests

```
src/
├── TimeBase.Core.Tests.Unit/
│   ├── Providers/
│   │   └── Services/
│   │       └── ProviderRegistryTests.cs      # Unit tests for ProviderRegistry
│   ├── Data/
│   │   └── Services/
│   │       └── DataCoordinatorTests.cs       # Unit tests for DataCoordinator
│   ├── Shared/
│   │   └── Validators/
│   │       └── ValidatorTests.cs              # Request validator tests
│   └── TimeBase.Core.Tests.Unit.csproj
│
└── TimeBase.Core.Tests.Integration/
    ├── Providers/
    │   └── ProviderEndpointsTests.cs         # Integration tests for provider endpoints
    ├── Data/
    │   └── DataEndpointsTests.cs             # Integration tests for data endpoints
    └── TimeBase.Core.Tests.Integration.csproj
```

**Total: 78 tests** (48 unit + 24 integration + 6 E2E)
- **Unit Tests** (48): ProviderRegistry, DataCoordinator, validators (no Docker required)
- **Integration Tests** (24): End-to-end endpoint tests (requires Docker/Testcontainers)
- **E2E Tests** (6): Full stack tests with Docker Compose (separate workflow)

### Provider Tests

Providers can define custom tests using **composite GitHub Actions**:

```
providers/
└── yahoo-finance/
    ├── .github/
    │   └── actions/
    │       └── test/
    │           └── action.yml              # Composite action for CI tests
    ├── src/
    │   └── main.py                         # Provider implementation
    ├── test_provider.py                    # Integration tests (run by action.yml)
    ├── requirements.txt                    # Runtime + test dependencies
    └── Dockerfile
```

#### Adding Tests to a Provider

1. **Create composite action**: `providers/<provider>/.github/actions/test/action.yml`
   ```yaml
   name: 'Test <Provider Name>'
   description: 'Runs integration tests for <Provider Name>'
   
   inputs:
     python-version:
       description: 'Python version to use'
       required: false
       default: '3.11'
   
   runs:
     using: 'composite'
     steps:
       - name: Set up Python
         uses: actions/setup-python@v5
         with:
           python-version: ${{ inputs.python-version }}
       
       - name: Install test dependencies
         shell: bash
         run: |
           cd providers/<provider>
           pip install -r requirements.txt
       
       - name: Run tests
         shell: bash
         run: |
           cd providers/<provider>
           python test_provider.py  # or pytest, etc.
   ```

2. **Update build-providers.yml**: Add provider to matrix and reference the action
   ```yaml
   - name: Run provider-specific tests
     if: matrix.provider == '<provider-name>'
     uses: ./providers/<provider>/.github/actions/test
   ```

3. **The action is automatically discovered and executed** during CI pipeline

## Code Quality Checks

### Formatting
```bash
cd src
dotnet format --verify-no-changes TimeBase.slnx
```

### Fix Formatting
```bash
cd src
dotnet format TimeBase.slnx
```

## Artifacts

All pipeline runs produce the following artifacts:

1. **Test Results** (.trx files)
   - Available for 90 days
   - Can be viewed in Azure DevOps Test Results viewer

2. **Coverage Report** (HTML)
   - Interactive HTML report
   - Detailed line-by-line coverage
   - Available for 90 days

3. **Coverage Data** (Cobertura XML)
   - Machine-readable coverage data
   - Used for PR comments and badges

## Pull Request Workflow

When you create a PR:

1. ✅ **Build & Test** runs (core build + tests)
2. ✅ **E2E Tests** run (full Docker stack validation)
3. 📊 **Coverage** is calculated and reported
4. 💬 **PR Comment** shows coverage percentage
5. ⚠️ **Warning** if coverage < 70% (doesn't block merge)
6. 🎨 **Code Format** is verified

## Viewing Results

### In GitHub UI
- Go to **Actions** tab
- Select workflow run
- View **Summary** for test results
- Download **Artifacts** for detailed reports

### Coverage Report
- Download `coverage-report` artifact
- Extract ZIP
- Open `index.html` in browser

## E2E Test Details

The E2E test job runs after the main build job and validates the full Docker Compose stack:

### Test Sequence

1. **Build Images**
   - Builds `timebase/core:latest` from source
   - Builds `timebase/minimal-provider:latest` from source

2. **Start Stack**
   - Uses `docker-compose.dev.yml` with selective service start
   - Starts only: `timescaledb` (DB), `seed-providers` (init), `core` (API), `minimal-provider` (gRPC provider)
   - DB is automatically seeded with provider configurations

3. **Wait for Ready**
   - Polls `/health/ready` endpoint with 2-second intervals (max 60 seconds)
   - Ensures DB migrations run and provider health monitor initializes

4. **Run Tests**
   - **Core Health** (`/health`) - Verifies Core is healthy and DB connection works
   - **Provider Registration** (`/api/providers`) - Confirms minimal-provider is registered
   - **Provider Details** (`/api/providers/minimal-provider`) - Validates provider metadata
   - **gRPC GetSymbols** (`/api/providers/minimal-provider/symbols`) - Tests gRPC unary RPC
   - **gRPC Historical Data** (`/api/providers/minimal-provider/data/...`) - Tests gRPC server-streaming RPC with full data pipeline

5. **Log Collection** (on failure)
   - Dumps all container logs to artifact
   - Helps debug failures in CI environment

6. **Cleanup**
   - Tears down all containers and volumes
   - Ensures clean state for subsequent runs

### Failure Handling

- **Early Exit**: Any test failure immediately stops execution and marks the job as failed
- **Log Upload**: Docker Compose logs are captured and uploaded as artifacts for debugging
- **Clean Teardown**: `if: always()` ensures containers are stopped even on failure

### Why Minimal Provider?

The E2E tests use `minimal-provider` instead of real providers (yahoo-finance, ccxt) because:

- **No External Dependencies**: Generates dummy data, no API rate limits or network issues
- **Fast**: Responds instantly without HTTP calls to external APIs
- **Reliable**: No dependency on external service availability
- **Sufficient Coverage**: Validates the same gRPC communication path as real providers

## Future Enhancements

- [x] Provider integration tests (end-to-end with Core API) - **Completed via E2E tests**
- [ ] Performance benchmarks
- [ ] Security scanning (Snyk, Dependabot)
- [ ] Docker image scanning
- [ ] Automatic releases
- [ ] Multi-platform Docker builds (ARM64) - Already implemented for providers
