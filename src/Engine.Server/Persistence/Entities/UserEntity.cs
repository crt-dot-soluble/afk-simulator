using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Engine.Server.Persistence.Entities;

internal sealed class UserEntity
{
    public string Id { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [SuppressMessage("Performance", "CA1819", Justification = "EF Core requires byte[] for BLOB columns.")]
    public byte[] PasswordHash { get; set; } = Array.Empty<byte>();

    [SuppressMessage("Performance", "CA1819", Justification = "EF Core requires byte[] for BLOB columns.")]
    public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();

    public ICollection<UniverseEntity> Universes { get; } = new List<UniverseEntity>();
}
