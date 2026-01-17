using System.Linq;
using Engine.Core.Accounts;
using Engine.Core.Time;

namespace Engine.Core.Tests.Accounts;

internal sealed class AccountServiceTests
{
    [Fact]
    public async Task CreateUniverseRespectsLimit()
    {
        var rules = new AccountRulebook();
        rules.SetMaxUniversesPerUser(1);
        var service = new AccountService(rules, new DeterministicSystemClock(DateTimeOffset.UnixEpoch));
        var user = await service.RegisterUserAsync("pilot@example.invalid", "Password!123", "Test Pilot");
        await service.CreateUniverseAsync(user.Id, "Vanguard");

        var exception =
            await Assert.ThrowsAsync<AccountOperationException>(() =>
                service.CreateUniverseAsync(user.Id, "Overflow").AsTask());

        Assert.Equal(AccountErrorCodes.UniverseLimit, exception.Code);
    }

    [Fact]
    public async Task CharacterReceivesDefaultCurrency()
    {
        var rules = new AccountRulebook();
        rules.SetDefaultBaseCurrency(5_000);
        rules.SetDefaultPremiumCurrency(125);
        var service = new AccountService(rules, new DeterministicSystemClock(DateTimeOffset.UnixEpoch));
        var user = await service.RegisterUserAsync("investor@example.invalid", "Password!123", "Investor");
        var universe = await service.CreateUniverseAsync(user.Id, "Prime");

        var character = await service.CreateCharacterAsync(universe.Id, "Origin");

        Assert.Equal(5_000, character.BaseCurrency);
        Assert.Equal(125, character.PremiumCurrency);
        Assert.Equal(rules.Snapshot.DefaultSpriteAssetId, character.SpriteAssetId);
    }

    [Fact]
    public async Task StartNewCharacterGeneratesSequentialNames()
    {
        var service = new AccountService(new AccountRulebook(), new DeterministicSystemClock(DateTimeOffset.UnixEpoch));
        var user = await service.RegisterUserAsync("looper@example.invalid", "Password!123", "Looper");
        var universe = await service.CreateUniverseAsync(user.Id, "Loop One");

        var first = await service.StartNewCharacterAsync(universe.Id);
        var second = await service.StartNewCharacterAsync(universe.Id, "Manual Codename");

        Assert.Equal("Character 1", first.Name);
        Assert.Equal("Manual Codename", second.Name);
        var roster = await service.ListCharactersAsync(universe.Id);
        Assert.Equal(2, roster.Count);
    }

    [Fact]
    public async Task WalletSnapshotAggregatesAcrossUniverses()
    {
        var rules = new AccountRulebook();
        rules.SetDefaultBaseCurrency(100);
        rules.SetDefaultPremiumCurrency(5);
        var service = new AccountService(rules, new DeterministicSystemClock(DateTimeOffset.UnixEpoch));
        var user = await service.RegisterUserAsync("treasurer@example.invalid", "Password!123", "Treasurer");
        var primary = await service.CreateUniverseAsync(user.Id, "Primary");
        var alpha = await service.CreateCharacterAsync(primary.Id, "Alpha");
        var beta = await service.CreateCharacterAsync(primary.Id, "Beta");
        var secondary = await service.CreateUniverseAsync(user.Id, "Secondary");
        var gamma = await service.CreateCharacterAsync(secondary.Id, "Gamma");

        var snapshot = await service.GetWalletSnapshotAsync(user.Id);

        Assert.Equal(300, snapshot.Account.BaseCurrency);
        Assert.Equal(15, snapshot.Account.PremiumCurrency);
        Assert.Equal(2, snapshot.Universes.Count);
        Assert.Equal(3, snapshot.Characters.Count);
        Assert.Contains(snapshot.Characters, c => c.CharacterId == alpha.Id);
        Assert.Contains(snapshot.Universes, u => u.UniverseId == primary.Id && u.Wallet.BaseCurrency == 200);
        Assert.Contains(snapshot.Universes, u => u.UniverseId == secondary.Id && u.Wallet.BaseCurrency == 100);
    }

    [Fact]
    public async Task DepositCurrencyTargetsSingleCharacter()
    {
        var service = new AccountService(new AccountRulebook(), new DeterministicSystemClock(DateTimeOffset.UnixEpoch));
        var user = await service.RegisterUserAsync("banker@example.invalid", "Password!123", "Banker");
        var universe = await service.CreateUniverseAsync(user.Id, "Vault");
        var character = await service.CreateCharacterAsync(universe.Id, "Collector");

        var snapshot = await service.DepositCurrencyAsync(user.Id, null, character.Id, 1_000, 50);

        var updated = await service.ListCharactersAsync(universe.Id);
        var refreshed = updated.Single(c => c.Id == character.Id);
        Assert.Equal(character.BaseCurrency + 1_000, refreshed.BaseCurrency);
        Assert.Equal(character.PremiumCurrency + 50, refreshed.PremiumCurrency);
        Assert.Equal(snapshot.Account.BaseCurrency, refreshed.BaseCurrency);
        Assert.Equal(snapshot.Account.PremiumCurrency, refreshed.PremiumCurrency);
    }
}
