# CI/CD Pipeline

TimeBase uses GitHub Actions for continuous integration and delivery.

## Build Pipelines

### Core Service Build (`build-core.yml`)

**Triggers:**
- Push to `master` branch
- Pull requests to `master` branch
- Changes to core service files

**What it does:**
1. **Build & Test** (Ubuntu only)
   - Verifies Docker is available
   - Installs .NET 10.0 SDK
   - Restores NuGet dependencies
   - Builds solution in Release mode
   - Runs unit tests only (integration tests require local Docker and are skipped in CI)
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

**Note**: Integration tests are excluded from CI because they require Docker with Testcontainers. Run integration tests locally with Docker available.

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

### All Tests (including integration tests with Docker)
```bash
cd src
dotnet test TimeBase.slnx --configuration Release
```

### Unit Tests Only (no Docker required)
```bash
cd src
dotnet test TimeBase.slnx --configuration Release --filter "FullyQualifiedName!~Integration"
```

### With Coverage
```bash
cd src
dotnet test TimeBase.slnx --configuration Release \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults
```

### Specific Test Project
```bash
cd src/TimeBase.Core.Tests
dotnet test --configuration Release
```

## Test Structure

```
src/
└── TimeBase.Core.Tests/
    ├── Services/
    │   └── ProviderRegistryTests.cs      # 10 tests
    ├── Models/
    │   └── ValidatorTests.cs              # 37 tests
    └── TimeBase.Core.Tests.csproj
```

**Total: 47 tests**
- ProviderRegistry service tests (10)
- Request validator tests (37)

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

1. ✅ **Build & Test** runs on Ubuntu + Windows
2. 📊 **Coverage** is calculated and reported
3. 💬 **PR Comment** shows coverage percentage
4. ⚠️ **Warning** if coverage < 70% (doesn't block merge)
5. 🎨 **Code Format** is verified

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

## Future Enhancements

- [ ] Integration tests
- [ ] Performance benchmarks
- [ ] Security scanning (Snyk, Dependabot)
- [ ] Docker image scanning
- [ ] Automatic releases
- [ ] Multi-platform Docker builds (ARM64)
