using TimeBase.Core.Infrastructure.Entities;
using TimeBase.Core.Providers.Services;

namespace TimeBase.Core.Providers.Models;

// Provider endpoints responses
public record GetProvidersResponse(
    List<Provider> Providers
);

public record GetProviderResponse(
    Provider Provider
);

public record InstallProviderResponse(
    string Message,
    Provider Provider
);

public record UninstallProviderResponse(
    string Message
);

public record SetProviderEnabledResponse(
    string Message,
    Provider Provider
);

public record RefreshProviderCapabilitiesResponse(
    string Message,
    Provider Provider,
    ProviderCapabilities? Capabilities
);

public record RefreshAllCapabilitiesResponse(
    string Message,
    int Count,
    List<Provider> Providers
);

public record ProviderHealthInfo(
    Guid Id,
    string Slug,
    string Name
);

public record CheckProviderHealthResponse(
    ProviderHealthInfo Provider,
    bool Healthy,
    DateTime CheckedAt
);

public record ProviderSymbolsInfo(
    string Slug,
    string Name,
    List<ProviderSymbol> Symbols
);

public record GetProviderSymbolsResponse(
    List<ProviderSymbolsInfo> Providers,
    int TotalSymbols
);