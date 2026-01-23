using System;

namespace TimeBase.Core.Entities;

public record Provider(
    Guid Id,
    string Slug,
    string Name,
    string Version,
    bool Enabled,
    string RepositoryUrl,
    string ImageUrl,
    string Config,
    string Capabilities,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
