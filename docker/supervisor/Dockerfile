# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["TimeBase.sln", "."]
COPY ["src/TimeBase.Supervisor/TimeBase.Supervisor.csproj", "src/TimeBase.Supervisor/"]
COPY ["src/TimeBase.Contracts/TimeBase.Contracts.csproj", "src/TimeBase.Contracts/"]

RUN dotnet restore "TimeBase.sln"

# Copy source code
COPY ["src/TimeBase.Supervisor/", "src/TimeBase.Supervisor/"]
COPY ["src/TimeBase.Contracts/", "src/TimeBase.Contracts/"]

# Build the application
WORKDIR "/src/src/TimeBase.Supervisor"
RUN dotnet build "TimeBase.Supervisor.csproj" -c Release -o /app/build

# Publish the application
RUN dotnet publish "TimeBase.Supervisor.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create a non-root user
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser:appuser /app
USER appuser

# Copy published application
COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose ports
EXPOSE 8080 50051

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Run the application
ENTRYPOINT ["dotnet", "TimeBase.Supervisor.dll"]