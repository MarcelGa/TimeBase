namespace TimeBase.Core.Infrastructure.Entities;

public record Provider(
    Guid Id,
    string Slug,
    string Name,
    string Version,
    bool Enabled,
    string RepositoryUrl,
    string? ImageUrl,
    string? GrpcEndpoint,  // gRPC endpoint (e.g., "timebase-yahoo-finance:50051")
    string? Config,
    string? Capabilities,
    DateTime CreatedAt,
    DateTime UpdatedAt
);