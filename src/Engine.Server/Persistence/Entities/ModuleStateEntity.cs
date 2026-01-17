using System;
using System.Diagnostics.CodeAnalysis;

namespace Engine.Server.Persistence.Entities;

internal sealed class ModuleStateEntity
{
    public int Id { get; set; }

    public string ModuleId { get; set; } = string.Empty;

    public string StateKey { get; set; } = string.Empty;

    [SuppressMessage("Performance", "CA1819", Justification = "EF Core requires byte[] for BLOB columns.")]
    public byte[] Payload { get; set; } = Array.Empty<byte>();

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
