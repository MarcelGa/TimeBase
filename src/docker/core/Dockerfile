# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0.102-noble-amd64 AS build
WORKDIR /src

# Copy project files for restore
COPY TimeBase.Contracts/TimeBase.Contracts.csproj ./TimeBase.Contracts/
COPY TimeBase.Core.Infrastructure/TimeBase.Core.Infrastructure.csproj ./TimeBase.Core.Infrastructure/
COPY TimeBase.Core/TimeBase.Core.csproj ./TimeBase.Core/

# Restore dependencies
RUN dotnet restore TimeBase.Core/TimeBase.Core.csproj

# Copy all source files
COPY TimeBase.Contracts/ ./TimeBase.Contracts/
COPY TimeBase.Core.Infrastructure/ ./TimeBase.Core.Infrastructure/
COPY TimeBase.Core/ ./TimeBase.Core/

# Build and publish
WORKDIR /src/TimeBase.Core
RUN dotnet publish TimeBase.Core.csproj -c Release -o /app/publish

## Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0.2-noble-amd64 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Install curl for healthchecks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
EXPOSE 50051

ENTRYPOINT ["dotnet", "TimeBase.Core.dll"]
