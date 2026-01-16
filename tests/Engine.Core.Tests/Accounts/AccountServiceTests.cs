using Engine.Core.Accounts;
using Engine.Core.Time;

namespace Engine.Core.Tests.Accounts;

public sealed class AccountServiceTests
{
    [Fact]
    public async Task CreateAccountRespectsLimit()
    {
        var rules = new AccountRulebook();
        rules.SetMaxAccounts(1);
        var service = new AccountService(rules, new DeterministicSystemClock(DateTimeOffset.UnixEpoch));
        var user = await service.CreateUserAsync("Test Pilot");
        await service.CreateAccountAsync(user.Id, "Vanguard");

        var exception = await Assert.ThrowsAsync<AccountOperationException>(async () =>
            await service.CreateAccountAsync(user.Id, "Overflow").AsTask().ConfigureAwait(true));

        Assert.Equal(AccountErrorCodes.AccountLimit, exception.Code);
    }

    [Fact]
    public async Task NewLifeReceivesDefaultCurrency()
    {
        var rules = new AccountRulebook();
        rules.SetDefaultBaseCurrency(5_000);
        rules.SetDefaultPremiumCurrency(125);
        var service = new AccountService(rules, new DeterministicSystemClock(DateTimeOffset.UnixEpoch));
        var user = await service.CreateUserAsync("Investor");
        var account = await service.CreateAccountAsync(user.Id, "Prime");

        var profile = await service.CreateProfileAsync(account.Id, "Origin");

        Assert.Equal(5_000, profile.BaseCurrency);
        Assert.Equal(125, profile.PremiumCurrency);
        Assert.Equal(rules.Snapshot.DefaultSpriteAssetId, profile.SpriteAssetId);
    }

    [Fact]
    public async Task StartNewLifeGeneratesSequentialNames()
    {
        var service = new AccountService(new AccountRulebook(), new DeterministicSystemClock(DateTimeOffset.UnixEpoch));
        var user = await service.CreateUserAsync("Looper");
        var account = await service.CreateAccountAsync(user.Id, "Loop One");

        var first = await service.StartNewLifeAsync(account.Id);
        var second = await service.StartNewLifeAsync(account.Id, "Manual Codename");

        Assert.Equal("New Life 1", first.Name);
        Assert.Equal("Manual Codename", second.Name);
        var profiles = await service.ListProfilesAsync(account.Id);
        Assert.Equal(2, profiles.Count);
    }
}
