# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-bullseye-slim AS build
WORKDIR /src

# Copy solution & project files
COPY TimeBase.sln ./
COPY src/TimeBase.Supervisor/TimeBase.Supervisor.csproj ./src/TimeBase.Supervisor/
COPY src/TimeBase.Contracts/TimeBase.Contracts.csproj ./src/TimeBase.Contracts/

RUN dotnet restore TimeBase.sln

# Copy all source files
COPY . ./

WORKDIR /src/src/TimeBase.Supervisor
RUN dotnet publish TimeBase.Supervisor.csproj -c Release -o /app/publish

## Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-bullseye-slim AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
EXPOSE 50051

ENTRYPOINT ["dotnet", "TimeBase.Supervisor.dll"]
