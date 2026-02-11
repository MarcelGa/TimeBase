# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0.102-noble-amd64 AS build
WORKDIR /src

# Copy central package management files (required for restore)
COPY src/Directory.Build.props ./
COPY src/Directory.Packages.props ./

# Copy project files for restore
COPY src/TimeBase.Plugins.Contracts/TimeBase.Plugins.Contracts.csproj ./TimeBase.Plugins.Contracts/
COPY src/TimeBase.Core.Infrastructure/TimeBase.Core.Infrastructure.csproj ./TimeBase.Core.Infrastructure/
COPY src/TimeBase.Core/TimeBase.Core.csproj ./TimeBase.Core/

# Restore dependencies
RUN dotnet restore TimeBase.Core/TimeBase.Core.csproj

# Copy all source files
COPY src/TimeBase.Plugins.Contracts/ ./TimeBase.Plugins.Contracts/
COPY src/TimeBase.Core.Infrastructure/ ./TimeBase.Core.Infrastructure/
COPY src/TimeBase.Core/ ./TimeBase.Core/

# Build and publish with optimizations
WORKDIR /src/TimeBase.Core
RUN dotnet publish TimeBase.Core.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:PublishSingleFile=false \
    /p:DebuggerSupport=false \
    /p:EnableUnsafeBinaryFormatterSerialization=false \
    /p:EnableUnsafeUTF7Encoding=false \
    /p:InvariantGlobalization=false

## Runtime stage - using chiseled image (no shell, no package manager, minimal attack surface)
FROM mcr.microsoft.com/dotnet/aspnet:10.0.2-noble-chiseled-amd64 AS runtime

# Chiseled images run as non-root by default (app user, UID 1654)
WORKDIR /app
COPY --from=build /app/publish ./

# Chiseled images don't have curl, but ASP.NET health checks work without it
# The health endpoint can be checked via HTTP request from orchestrator

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
EXPOSE 50051

ENTRYPOINT ["dotnet", "TimeBase.Core.dll"]
