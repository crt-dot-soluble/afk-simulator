using System;

namespace Engine.Core.Accounts;

public sealed record UserRecord(string Id, string DisplayName, DateTimeOffset CreatedAt);

public sealed record AccountRecord(string Id, string UserId, string Label, DateTimeOffset CreatedAt);

public sealed record CharacterProfileRecord(
    string Id,
    string AccountId,
    string Name,
    DateTimeOffset CreatedAt,
    long BaseCurrency,
    long PremiumCurrency,
    EquipmentSlots Equipment,
    string SpriteAssetId);

public sealed record EquipmentSlots(
    string? Head,
    string? Cape,
    string? Neck,
    string? Weapon,
    string? Shield,
    string? Body,
    string? Legs,
    string? Feet,
    string? Hands);
