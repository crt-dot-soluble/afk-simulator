using System;

namespace Engine.Server.Persistence.Entities;

internal sealed class CharacterEntity
{
    public string Id { get; set; } = string.Empty;

    public string UniverseId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
        = DateTimeOffset.UtcNow;

    public long BaseCurrency { get; set; }

    public long PremiumCurrency { get; set; }

    public string SpriteAssetId { get; set; } = string.Empty;

    public string EquipmentJson { get; set; } = "{}";

    public UniverseEntity? Universe { get; set; }
}
