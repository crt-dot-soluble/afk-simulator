using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Engine.Core.Accounts;
using Engine.Core.Assets;
using Engine.Core.Contracts;
using Engine.Core.Rendering.Sprites;
using Xunit;

namespace Engine.Core.Tests.Accounts;

internal sealed class AccountModuleViewTests
{
    private const string TestUserId = "user-1";

    [Fact]
    public void DescribeModuleViewsWithoutUserReturnsAuthPrompt()
    {
        var module = CreateModule();

        var documents = module.DescribeModuleViews(ModuleViewContext.Empty);

        var document = Assert.Single(documents);
        Assert.Equal(DashboardViewIds.AccountUniverse, document.Descriptor.Id);
        var section = Assert.IsType<ModuleViewSectionBlock>(Assert.Single(document.Blocks));
        Assert.Equal("accounts.universe.auth-required", section.Id);
    }

    [Fact]
    public void DescribeModuleViewsWithDataBuildsUniverseAndRosterSections()
    {
        var service = new FakeAccountService();
        var universe = new UniverseRecord("universe-1", TestUserId, "Aurora Expanse",
            DateTimeOffset.UtcNow.AddMinutes(-5));
        var character = new CharacterRecord("char-1", universe.Id, "Origin Spark",
            DateTimeOffset.UtcNow.AddMinutes(-1), 1_000, 25, new EquipmentSlots(null, null, null, null, null, null,
                null, null, null), "avatars/ember-nomad");
        service.AddUniverse(universe);
        service.AddCharacter(character);

        var wallet = new AccountWalletSnapshot(
            new WalletBreakdown(5_000, 125),
            new[] { new UniverseWalletSnapshot(universe.Id, universe.Name, new WalletBreakdown(1_000, 10)) },
            new[]
            {
                new CharacterWalletSnapshot(character.Id, character.Name, universe.Id, universe.Name,
                    new WalletBreakdown(character.BaseCurrency, character.PremiumCurrency))
            });
        service.SetWalletSnapshot(wallet);

        var module = CreateModule(service);
        var context = new ModuleViewContext(TestUserId);

        var documents = module.DescribeModuleViews(context);

        var document = Assert.Single(documents);
        Assert.Equal(DashboardViewIds.AccountUniverse, document.Descriptor.Id);
        Assert.Contains(document.Blocks, block => block is ModuleViewFormBlock form &&
            string.Equals(form.Id, "accounts.universe.form", StringComparison.OrdinalIgnoreCase));
        var rosterSection = Assert.IsType<ModuleViewSectionBlock>(document.Blocks[^2]);
        Assert.Equal("accounts.character.section", rosterSection.Id);
        var universeList = Assert.IsType<ModuleViewListBlock>(Assert.IsType<ModuleViewSectionBlock>(document.Blocks[1]).Children[0]);
        Assert.True(universeList.AllowSelection);
        Assert.Contains(universeList.Items, item => item.IsActive && item.Id == universe.Id);
    }

    private static AccountModule CreateModule(FakeAccountService? service = null)
    {
        var manifest = new AssetManifest();
        using var stream = new MemoryStream(new byte[] { 0x1 });
        manifest.Register("avatars/ember-nomad", "sprite", "images/avatars/ember-nomad.svg", 96, 96, stream,
            "avatar");
        var sprites = new SpriteLibrary(manifest);
        var rulebook = new AccountRulebook();
        return new AccountModule(rulebook, service ?? new FakeAccountService(), manifest, sprites);
    }

    private sealed class FakeAccountService : IAccountService
    {
        private readonly List<UserRecord> _users = new();
        private readonly List<UniverseRecord> _universes = new();
        private readonly List<CharacterRecord> _characters = new();
        private AccountWalletSnapshot _walletSnapshot = new(
            WalletBreakdown.Empty,
            Array.Empty<UniverseWalletSnapshot>(),
            Array.Empty<CharacterWalletSnapshot>());

        public void AddUniverse(UniverseRecord universe)
        {
            _universes.Add(universe);
            if (!_users.Exists(user => user.Id == universe.UserId))
            {
                _users.Add(new UserRecord(universe.UserId, "demo@invalid", "Demo", DateTimeOffset.UtcNow));
            }
        }

        public void AddCharacter(CharacterRecord character)
        {
            _characters.Add(character);
        }

        public void SetWalletSnapshot(AccountWalletSnapshot snapshot)
        {
            _walletSnapshot = snapshot;
        }

        public AccountRulebookSnapshot SnapshotRules() => AccountRulebookSnapshot.CreateDefault();

        public ValueTask<UserRecord> RegisterUserAsync(string email, string password, string displayName,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<UserRecord?> AuthenticateAsync(string email, string password,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<UserRecord?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            return new ValueTask<UserRecord?>(_users.Find(user => user.Id == userId));
        }

        public ValueTask<IReadOnlyList<UserRecord>> ListUsersAsync(CancellationToken cancellationToken = default)
        {
            return new ValueTask<IReadOnlyList<UserRecord>>(_users);
        }

        public ValueTask<UniverseRecord?> GetUniverseAsync(string universeId,
            CancellationToken cancellationToken = default)
        {
            return new ValueTask<UniverseRecord?>(_universes.Find(universe => universe.Id == universeId));
        }

        public ValueTask<IReadOnlyList<UniverseRecord>> ListUniversesAsync(string userId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<UniverseRecord> result = _universes.FindAll(universe => universe.UserId == userId);
            return new ValueTask<IReadOnlyList<UniverseRecord>>(result);
        }

        public ValueTask<UniverseRecord> CreateUniverseAsync(string userId, string name,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<CharacterRecord>> ListCharactersAsync(string universeId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<CharacterRecord> result = _characters.FindAll(character => character.UniverseId == universeId);
            return new ValueTask<IReadOnlyList<CharacterRecord>>(result);
        }

        public ValueTask<CharacterRecord> CreateCharacterAsync(string universeId, string? name,
            string? spriteAssetId = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<CharacterRecord> StartNewCharacterAsync(string universeId, string? name = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<AccountWalletSnapshot> GetWalletSnapshotAsync(string userId,
            CancellationToken cancellationToken = default)
        {
            return new ValueTask<AccountWalletSnapshot>(_walletSnapshot);
        }

        public ValueTask<AccountWalletSnapshot> DepositCurrencyAsync(string userId, string? universeId,
            string? characterId, long baseCurrency, long premiumCurrency,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask TopUpUserWalletAsync(string userId, long baseCurrency, long premiumCurrency,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask ClearAsync(CancellationToken cancellationToken = default)
        {
            _users.Clear();
            _universes.Clear();
            _characters.Clear();
            _walletSnapshot = new AccountWalletSnapshot(WalletBreakdown.Empty,
                Array.Empty<UniverseWalletSnapshot>(), Array.Empty<CharacterWalletSnapshot>());
            return ValueTask.CompletedTask;
        }
    }
}
