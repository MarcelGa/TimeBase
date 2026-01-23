# Build stage
# Note: Using .NET 9 SDK to build .NET 10 project until .NET 10 images are available
FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS build
WORKDIR /src

# Copy solution & project files
COPY TimeBase.slnx ./
COPY TimeBase.Core/TimeBase.Core.csproj ./TimeBase.Core/
COPY TimeBase.Core.Infrastructure/TimeBase.Core.Infrastructure.csproj ./TimeBase.Core.Infrastructure/
COPY TimeBase.Contracts/TimeBase.Contracts.csproj ./TimeBase.Contracts/

RUN dotnet restore TimeBase.slnx

# Copy all source files
COPY . ./

WORKDIR /src/TimeBase.Core
RUN dotnet publish TimeBase.Core.csproj -c Release -o /app/publish

## Runtime stage
# Note: Using .NET 9 runtime until .NET 10 images are available
FROM mcr.microsoft.com/dotnet/aspnet:9.0-bookworm-slim AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
EXPOSE 50051

ENTRYPOINT ["dotnet", "TimeBase.Core.dll"]
