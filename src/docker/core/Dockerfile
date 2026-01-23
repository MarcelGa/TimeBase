# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0.102-noble-amd64 AS build
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
FROM mcr.microsoft.com/dotnet/aspnet:10.0.2-noble-chiseled-extra-amd64 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
EXPOSE 50051

ENTRYPOINT ["dotnet", "TimeBase.Core.dll"]
