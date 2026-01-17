using System;
using System.Collections.Generic;

namespace Engine.Server.Persistence.Entities;

internal sealed class UniverseEntity
{
    public string Id { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public UserEntity? User { get; set; }

    public ICollection<CharacterEntity> Characters { get; } = new List<CharacterEntity>();
}
