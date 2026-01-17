using System;

namespace Engine.Core.Accounts;

public sealed record UserRecord(string Id, string Email, string DisplayName, DateTimeOffset CreatedAt);

public sealed record UniverseRecord(string Id, string UserId, string Name, DateTimeOffset CreatedAt);

public sealed record CharacterRecord(
    string Id,
    string UniverseId,
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

public sealed record WalletBreakdown(long BaseCurrency, long PremiumCurrency)
{
    public static WalletBreakdown Empty { get; } = new(0, 0);

    public WalletBreakdown Add(long baseCurrency, long premiumCurrency)
    {
        checked
        {
            return new WalletBreakdown(BaseCurrency + baseCurrency, PremiumCurrency + premiumCurrency);
        }
    }
}

public sealed record UniverseWalletSnapshot(string UniverseId, string UniverseName, WalletBreakdown Wallet);

public sealed record CharacterWalletSnapshot(
    string CharacterId,
    string CharacterName,
    string UniverseId,
    string UniverseName,
    WalletBreakdown Wallet);

public sealed record AccountWalletSnapshot(
    WalletBreakdown Account,
    IReadOnlyList<UniverseWalletSnapshot> Universes,
    IReadOnlyList<CharacterWalletSnapshot> Characters);
