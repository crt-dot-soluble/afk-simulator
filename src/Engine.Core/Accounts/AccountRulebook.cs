using System;
using System.Threading;

namespace Engine.Core.Accounts;

public sealed class AccountRulebook
{
    private AccountRulebookSnapshot _snapshot = AccountRulebookSnapshot.CreateDefault();

    public AccountRulebookSnapshot Snapshot => Volatile.Read(ref _snapshot)!;

    public void SetMaxAccounts(int value) => Update(static (snapshot, next) =>
        snapshot with { MaxAccountsPerUser = Clamp(next) }, value);

    public void SetMaxProfiles(int value) => Update(static (snapshot, next) =>
        snapshot with { MaxProfilesPerAccount = Clamp(next) }, value);

    public void SetDefaultBaseCurrency(long value) => Update(static (snapshot, next) =>
        snapshot with { DefaultBaseCurrency = ClampCurrency(next, 0, 10_000_000_000) }, value);

    public void SetDefaultPremiumCurrency(long value) => Update(static (snapshot, next) =>
        snapshot with { DefaultPremiumCurrency = ClampCurrency(next, 0, 1_000_000_000) }, value);

    public void SetDefaultSprite(string spriteId) => Update(static (snapshot, next) =>
            snapshot with
                {
                    DefaultSpriteAssetId = string.IsNullOrWhiteSpace(next) ? snapshot.DefaultSpriteAssetId : next
                },
        spriteId);

    public void SetStarterEquipment(EquipmentSlots slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        Update(static (snapshot, next) => snapshot with { StarterEquipment = next }, slots);
    }

    private void Update<TState>(Func<AccountRulebookSnapshot, TState, AccountRulebookSnapshot> mutator, TState value)
    {
        ArgumentNullException.ThrowIfNull(mutator);
        while (true)
        {
            var current = Snapshot;
            var updated = mutator(current, value);
            var original = Interlocked.CompareExchange(ref _snapshot, updated, current);
            if (ReferenceEquals(original, current))
            {
                return;
            }
        }
    }

    private static int Clamp(int value) => Math.Clamp(value, 1, 12);

    private static long ClampCurrency(long value, long min, long max) => Math.Clamp(value, min, max);
}

public sealed record AccountRulebookSnapshot(
    int MaxAccountsPerUser,
    int MaxProfilesPerAccount,
    long DefaultBaseCurrency,
    long DefaultPremiumCurrency,
    EquipmentSlots StarterEquipment,
    string DefaultSpriteAssetId)
{
    public static AccountRulebookSnapshot CreateDefault()
    {
        return new AccountRulebookSnapshot(
            3,
            4,
            1_250,
            60,
            new EquipmentSlots(
                "ember_visor",
                "aurora_cape",
                "starforged_pendant",
                "ember_blade",
                "lumen_barrier",
                "solstice_jacket",
                "wayfarer_wrap",
                "glowstep_boots",
                "ember_grips"),
            "avatars/ember-nomad");
    }
}
