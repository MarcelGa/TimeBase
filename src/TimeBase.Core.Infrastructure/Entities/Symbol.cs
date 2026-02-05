namespace TimeBase.Core.Infrastructure.Entities;

public record Symbol(
    Guid Id,
    string SymbolValue,
    string? Name,
    string? Type,
    string? Exchange,
    string? Currency,
    string? Metadata,
    DateTime CreatedAt
);