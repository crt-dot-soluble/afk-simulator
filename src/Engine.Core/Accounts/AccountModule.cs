using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Engine.Core.Assets;
using Engine.Core.Contracts;
using Engine.Core.DeveloperTools;
using Engine.Core.Rendering.Sprites;

namespace Engine.Core.Accounts;

/// <summary>
/// Exposes the account/universe domain to the module explorer and seeds avatar assets.
/// </summary>
public sealed class AccountModule : IModuleContract, IModuleDescriptorSource, IDashboardViewProvider,
    IModuleViewProvider
{
    private static readonly string[] Capabilities = { "accounts", "universes", "characters", "developer-tools" };
    private static readonly string[] Telemetry = { "accounts.character.created" };
    private static readonly string[] Resources = { "AvatarSprites", "AccountService" };
    private static readonly int[] SingleFrameZero = { 0 };
    private const string SummarySectionId = "accounts.universe.summary";
    private const string UniverseSectionId = "accounts.universe.section";
    private const string UniverseListBlockId = "accounts.universe.list";
    private const string CharacterSectionId = "accounts.character.section";
    private const string CharacterListBlockId = "accounts.character.list";
    private const string UniverseFormBlockId = "accounts.universe.form";
    private const string CharacterFormBlockId = "accounts.character.form";

    private static readonly SpriteAnimationClip[] EmberNomadAnimations =
    {
        new SpriteAnimationClip("idle", SingleFrameZero, TimeSpan.FromMilliseconds(600)),
        new SpriteAnimationClip("mine", SingleFrameZero, TimeSpan.FromMilliseconds(400)),
        new SpriteAnimationClip("forage", SingleFrameZero, TimeSpan.FromMilliseconds(400)),
        new SpriteAnimationClip("focus", SingleFrameZero, TimeSpan.FromMilliseconds(500))
    };

    private readonly AccountRulebook _rulebook;
    private readonly IAccountService _accounts;
    private readonly AssetManifest _assetManifest;
    private readonly SpriteLibrary _spriteLibrary;
    private bool _spriteRegistered;

    public AccountModule(AccountRulebook rulebook, IAccountService accounts, AssetManifest assetManifest,
        SpriteLibrary spriteLibrary)
    {
        _rulebook = rulebook;
        _accounts = accounts;
        _assetManifest = assetManifest;
        _spriteLibrary = spriteLibrary;
    }

    public string Name => "Accounts";
    public string Version => "0.1.0";

    [ModuleInspectable("Max universes per user", Group = "Limits",
        Description = "Universes a pilot may maintain (1-12)")]
    public int MaxUniversesPerUser
    {
        get => _rulebook.Snapshot.MaxUniversesPerUser;
        set => _rulebook.SetMaxUniversesPerUser(value);
    }

    [ModuleInspectable("Max characters per universe", Group = "Limits",
        Description = "Distinct character slots each universe may host (1-12)")]
    public int MaxCharactersPerUniverse
    {
        get => _rulebook.Snapshot.MaxCharactersPerUniverse;
        set => _rulebook.SetMaxCharactersPerUniverse(value);
    }

    [ModuleInspectable("Starter base currency", Group = "Economy",
        Description = "Default soft currency grant per character")]
    public long StarterBaseCurrency
    {
        get => _rulebook.Snapshot.DefaultBaseCurrency;
        set => _rulebook.SetDefaultBaseCurrency(value);
    }

    [ModuleInspectable("Starter premium currency", Group = "Economy",
        Description = "Default premium shard grant per character")]
    public long StarterPremiumCurrency
    {
        get => _rulebook.Snapshot.DefaultPremiumCurrency;
        set => _rulebook.SetDefaultPremiumCurrency(value);
    }

    [ModuleInspectable("Starter sprite", Group = "Visuals",
        Description = "Sprite identifier used for fresh characters")]
    public string StarterSprite
    {
        get => _rulebook.Snapshot.DefaultSpriteAssetId;
        set => _rulebook.SetDefaultSprite(value);
    }

    public ValueTask InitializeAsync(ModuleContext context, CancellationToken cancellationToken = default)
    {
        RegisterSpriteAsset();
        return ValueTask.CompletedTask;
    }

    public async ValueTask<ModuleHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var users = await _accounts.ListUsersAsync(cancellationToken).ConfigureAwait(false);
        var details = new Dictionary<string, string>
        {
            ["users"] = users.Count.ToString(CultureInfo.InvariantCulture),
            ["maxUniverses"] = _rulebook.Snapshot.MaxUniversesPerUser.ToString(CultureInfo.InvariantCulture),
            ["maxCharacters"] = _rulebook.Snapshot.MaxCharactersPerUniverse.ToString(CultureInfo.InvariantCulture)
        };
        return ModuleHealth.Healthy(details);
    }

    [ModuleCommand("list-pilots", Description = "Snapshot of all pilot records", IsQuery = true)]
    public ValueTask<IReadOnlyList<UserRecord>> ListPilotsAsync(CancellationToken cancellationToken = default) =>
        _accounts.ListUsersAsync(cancellationToken);

    [ModuleCommand("seed-demo-user", Description = "Create a sample pilot with multiple universes")]
    public async ValueTask<UserRecord> SeedDemoUserAsync(CancellationToken cancellationToken = default)
    {
        var alias = $"Demo-{Guid.NewGuid():N}"[..12];
        var email = $"{alias.ToUpperInvariant()}@demo.invalid";
        var user = await _accounts.RegisterUserAsync(email, "Password!123", alias, cancellationToken)
            .ConfigureAwait(false);
        var primary = await _accounts.CreateUniverseAsync(user.Id, "Starborn Vanguard", cancellationToken)
            .ConfigureAwait(false);
        await _accounts.CreateCharacterAsync(primary.Id, "Origin Spark", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await _accounts.StartNewCharacterAsync(primary.Id, "Prestige Nova", cancellationToken)
            .ConfigureAwait(false);
        var secondary = await _accounts.CreateUniverseAsync(user.Id, "Dust Runner", cancellationToken)
            .ConfigureAwait(false);
        await _accounts.CreateCharacterAsync(secondary.Id, "Nomad Zero", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return user;
    }

    [ModuleCommand("purge-accounts", Description = "Clear in-memory pilots, universes, and characters")]
    public async ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        await _accounts.ClearAsync(cancellationToken).ConfigureAwait(false);
    }

    public ModuleDescriptor Describe()
    {
        return new ModuleDescriptor(
            Name,
            Version,
            Capabilities,
            Resources,
            Telemetry,
            "Account, universe, and character provisioning",
            new Dictionary<string, string>
            {
                ["owner"] = "content",
                ["tier"] = "system",
                ["area"] = "accounts"
            });
    }

    public IReadOnlyCollection<DashboardViewDescriptor> DescribeViews()
    {
        return new[]
        {
            new DashboardViewDescriptor(DashboardViewIds.AccountUniverse, Name, "Universe Control",
                "Manage universes, characters, and loadouts.", DashboardViewZones.Hero, Order: 10, ColumnSpan: 3)
        };
    }

    public IReadOnlyCollection<ModuleViewDocument> DescribeModuleViews(ModuleViewContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var descriptor = DescribeViews().First();
        if (string.IsNullOrWhiteSpace(context.UserId))
        {
            return BuildUnauthenticatedDocument(descriptor);
        }

        try
        {
            var userId = context.UserId!;
            var universes = _accounts.ListUniversesAsync(userId)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            var wallet = _accounts.GetWalletSnapshotAsync(userId)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            var rules = _rulebook.Snapshot;
            var activeUniverse = ResolveActiveUniverse(universes, context.Parameters);
            var characters = activeUniverse is null
                ? Array.Empty<CharacterRecord>()
                : _accounts.ListCharactersAsync(activeUniverse.Id)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();

            var blocks = BuildViewBlocks(universes, characters, wallet, activeUniverse, rules);
            return new[] { new ModuleViewDocument(descriptor, blocks, new ModuleViewDataSource("PT15S")) };
        }
        catch (AccountOperationException ex)
        {
            return BuildErrorDocument(descriptor, ex.Message);
        }
    }

    private void RegisterSpriteAsset()
    {
        if (_spriteRegistered)
        {
            return;
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(AvatarSpriteSvg));
        _assetManifest.Register("avatars/ember-nomad", "sprite", "images/avatars/ember-nomad.svg", 96, 96, stream,
            "avatar", "accounts");
        _spriteLibrary.RegisterSingleFrame("avatars/ember-nomad", "avatars/ember-nomad", "idle", EmberNomadAnimations);
        _spriteRegistered = true;
    }

    private static ModuleViewDocument[] BuildUnauthenticatedDocument(
        DashboardViewDescriptor descriptor)
    {
        var blocks = new ModuleViewBlock[]
        {
            new ModuleViewSectionBlock(
                "accounts.universe.auth-required",
                "Link Pilot Identity",
                "Sign in or create an account to manage universes and characters.",
                new ModuleViewBlock[]
                {
                    new ModuleViewActionBarBlock(
                        "accounts.universe.auth-actions",
                        new[]
                        {
                            new ModuleViewActionDescriptor(
                                "open-auth",
                                "Open Auth",
                                "navigate:/",
                                Icon: "user",
                                IsPrimary: true)
                        })
                },
                new ModuleViewStyle("#72f5ff"))
        };

        return new[] { new ModuleViewDocument(descriptor, blocks) };
    }

    private static ModuleViewDocument[] BuildErrorDocument(DashboardViewDescriptor descriptor,
        string message)
    {
        var blocks = new ModuleViewBlock[]
        {
            new ModuleViewSectionBlock(
                "accounts.universe.error",
                "Account Unavailable",
                message,
                new ModuleViewBlock[]
                {
                    new ModuleViewActionBarBlock(
                        "accounts.universe.error-actions",
                        new[]
                        {
                            new ModuleViewActionDescriptor(
                                "retry-accounts",
                                "Retry",
                                "accounts.universes.refresh",
                                Icon: "refresh",
                                IsPrimary: true)
                        })
                },
                new ModuleViewStyle("#ff7e5f"))
        };

        return new[] { new ModuleViewDocument(descriptor, blocks) };
    }

    private static List<ModuleViewBlock> BuildViewBlocks(
        IReadOnlyList<UniverseRecord> universes,
        IReadOnlyList<CharacterRecord> characters,
        AccountWalletSnapshot wallet,
        UniverseRecord? activeUniverse,
        AccountRulebookSnapshot rules)
    {
        var blocks = new List<ModuleViewBlock>
        {
            BuildSummarySection(universes, wallet, rules),
            BuildUniverseSection(universes, wallet, activeUniverse),
            BuildUniverseForm(rules, universes.Count)
        };

        if (activeUniverse is null)
        {
            blocks.Add(new ModuleViewSectionBlock(
                CharacterSectionId,
                "Roster",
                "Create a universe to mint operatives.",
                Array.Empty<ModuleViewBlock>(),
                new ModuleViewStyle("#f0d7ff")));
            return blocks;
        }

        blocks.Add(BuildCharacterSection(activeUniverse, characters));
        blocks.Add(BuildCharacterForm(rules));
        return blocks;
    }

    private static ModuleViewSectionBlock BuildSummarySection(
        IReadOnlyList<UniverseRecord> universes,
        AccountWalletSnapshot wallet,
        AccountRulebookSnapshot rules)
    {
        var totalCharacters = wallet.Characters.Count;
        var metrics = new ModuleViewBlock[]
        {
            new ModuleViewMetricBlock(
                "accounts.summary.universes",
                "Universes",
                universes.Count.ToString(CultureInfo.InvariantCulture),
                Secondary: $"Limit {rules.MaxUniversesPerUser}",
                Icon: "planet"),
            new ModuleViewMetricBlock(
                "accounts.summary.characters",
                "Characters",
                totalCharacters.ToString(CultureInfo.InvariantCulture),
                Secondary: $"Limit {rules.MaxCharactersPerUniverse} / universe",
                Icon: "users"),
            new ModuleViewMetricBlock(
                "accounts.summary.wallet",
                "Account Wallet",
                FormatNumber(wallet.Account.BaseCurrency),
                Secondary: $"{FormatNumber(wallet.Account.PremiumCurrency)} shards",
                Icon: "wallet")
        };

        return new ModuleViewSectionBlock(
            SummarySectionId,
            "Universe Overview",
            "Account-wide provisioning limits and currencies.",
            metrics,
            new ModuleViewStyle("#72f5ff"));
    }

    private static ModuleViewSectionBlock BuildUniverseSection(
        IReadOnlyList<UniverseRecord> universes,
        AccountWalletSnapshot wallet,
        UniverseRecord? activeUniverse)
    {
        var items = universes
            .Select(universe =>
            {
                var snapshot = TryFindUniverseWallet(wallet, universe.Id);
                var characterCount = CountCharacters(wallet, universe.Id);
                return new ModuleViewListItem(
                    universe.Id,
                    universe.Name,
                    universe.CreatedAt.ToLocalTime().ToString("MMM d · HH:mm", CultureInfo.InvariantCulture),
                    snapshot is null
                        ? null
                        : $"{FormatNumber(snapshot.Wallet.BaseCurrency)} base · {FormatNumber(snapshot.Wallet.PremiumCurrency)} shards",
                    Accent: activeUniverse is not null &&
                            string.Equals(activeUniverse.Id, universe.Id, StringComparison.OrdinalIgnoreCase)
                        ? "#72f5ff"
                        : null,
                    Icon: characterCount > 0 ? "users" : "sparkles",
                    IsActive: activeUniverse is not null &&
                              string.Equals(activeUniverse.Id, universe.Id, StringComparison.OrdinalIgnoreCase),
                    Badges: new Dictionary<string, string>
                    {
                        ["Chars"] = characterCount.ToString(CultureInfo.InvariantCulture)
                    });
            })
            .ToArray();

        if (items.Length == 0)
        {
            items = new[]
            {
                new ModuleViewListItem(
                    "accounts.universe.empty",
                    "No universes yet",
                    "Forge a shard to mint your first operative.")
            };
        }

        var children = new ModuleViewBlock[]
        {
            new ModuleViewListBlock(UniverseListBlockId, "Universes", items, AllowSelection: items.Length > 0),
            new ModuleViewActionBarBlock(
                "accounts.universe.actions",
                new[]
                {
                    new ModuleViewActionDescriptor(
                        "refresh-universes",
                        "Refresh",
                        "accounts.universes.refresh",
                        Icon: "refresh")
                })
        };

        return new ModuleViewSectionBlock(
            UniverseSectionId,
            "Universe Foundry",
            "Select a shard or spin up a new one.",
            children,
            new ModuleViewStyle("#3dd6ff"));
    }

    private static ModuleViewFormBlock BuildUniverseForm(AccountRulebookSnapshot rules, int existingUniverses)
    {
        var fields = new ModuleViewFormField[]
        {
            new ModuleViewFormField(
                "name",
                "Universe Name",
                "text",
                Placeholder: "Aurora Expanse",
                Value: GenerateUniverseName(existingUniverses),
                Required: true,
                MaxLength: 32,
                Description: $"Limit {rules.MaxUniversesPerUser} universes per pilot." )
        };

        var actions = new ModuleViewActionDescriptor[]
        {
            new ModuleViewActionDescriptor(
                "create-universe",
                "Create Universe",
                "accounts.universe.create",
                Icon: "plus",
                IsPrimary: true)
        };

        return new ModuleViewFormBlock(
            UniverseFormBlockId,
            "Forge Universe",
            fields,
            actions,
            "Provision a deterministic shard for your operatives.",
            new ModuleViewStyle("#5df2ff"));
    }

    private static ModuleViewSectionBlock BuildCharacterSection(
        UniverseRecord universe,
        IReadOnlyList<CharacterRecord> characters)
    {
        var items = characters
            .Select(character => new ModuleViewListItem(
                character.Id,
                character.Name,
                character.CreatedAt.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
                $"{FormatNumber(character.BaseCurrency)} base",
                Icon: "shield",
                Badges: new Dictionary<string, string>
                {
                    ["Shards"] = FormatNumber(character.PremiumCurrency)
                }))
            .ToArray();

        if (items.Length == 0)
        {
            items = new[]
            {
                new ModuleViewListItem(
                    "accounts.characters.empty",
                    "No characters minted",
                    "Launch an operative to receive starter gear.")
            };
        }

        var children = new ModuleViewBlock[]
        {
            new ModuleViewListBlock(CharacterListBlockId, $"{universe.Name} Roster", items)
        };

        return new ModuleViewSectionBlock(
            CharacterSectionId,
            "Operative Roster",
            "Each operative accumulates soft currency for deposits.",
            children,
            new ModuleViewStyle("#ffb347"));
    }

    private static ModuleViewFormBlock BuildCharacterForm(AccountRulebookSnapshot rules)
    {
        var fields = new ModuleViewFormField[]
        {
            new ModuleViewFormField(
                "name",
                "Codename",
                "text",
                Placeholder: "Origin Spark",
                Value: GenerateCharacterName(),
                MaxLength: 32,
                Description: $"Limit {rules.MaxCharactersPerUniverse} characters per universe.")
        };

        var actions = new ModuleViewActionDescriptor[]
        {
            new ModuleViewActionDescriptor(
                "create-character",
                "Launch Operative",
                "accounts.character.create",
                Icon: "rocket",
                IsPrimary: true)
        };

        return new ModuleViewFormBlock(
            CharacterFormBlockId,
            "Launch Character",
            fields,
            actions,
            "Mint an operative inside the selected universe.",
            new ModuleViewStyle("#ff7e5f"));
    }

    private static UniverseRecord? ResolveActiveUniverse(
        IReadOnlyList<UniverseRecord> universes,
        IReadOnlyDictionary<string, string>? parameters)
    {
        if (universes.Count == 0)
        {
            return null;
        }

        if (parameters is not null &&
            parameters.TryGetValue("activeUniverseId", out var requestedId) &&
            !string.IsNullOrWhiteSpace(requestedId))
        {
            var match = universes.FirstOrDefault(universe =>
                string.Equals(universe.Id, requestedId, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return universes
            .OrderByDescending(universe => universe.CreatedAt)
            .FirstOrDefault();
    }

    private static string GenerateUniverseName(int existingUniverses) =>
        existingUniverses switch
        {
            0 => "Aurora Expanse",
            1 => "Nebula Forge",
            _ => $"Universe {existingUniverses + 1}"
        };

    private static string GenerateCharacterName() => "Origin Spark";

    private static string FormatNumber(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static UniverseWalletSnapshot? TryFindUniverseWallet(AccountWalletSnapshot snapshot, string universeId)
    {
        return snapshot.Universes.FirstOrDefault(entry =>
            string.Equals(entry.UniverseId, universeId, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountCharacters(AccountWalletSnapshot snapshot, string universeId)
    {
        return snapshot.Characters.Count(record =>
            string.Equals(record.UniverseId, universeId, StringComparison.OrdinalIgnoreCase));
    }

    private const string AvatarSpriteSvg = """
                                           <svg width="96" height="96" viewBox="0 0 96 96" xmlns="http://www.w3.org/2000/svg">
                                             <defs>
                                               <radialGradient id="glow" cx="50%" cy="35%" r="70%">
                                                 <stop offset="0%" stop-color="#ffdd99"/>
                                                 <stop offset="60%" stop-color="#ff7e5f"/>
                                                 <stop offset="100%" stop-color="#3b1c32"/>
                                               </radialGradient>
                                               <linearGradient id="cloak" x1="0%" y1="0%" x2="0%" y2="100%">
                                                 <stop offset="0%" stop-color="#62f1ff" stop-opacity="0.9"/>
                                                 <stop offset="100%" stop-color="#1e2959" stop-opacity="0.7"/>
                                               </linearGradient>
                                             </defs>
                                             <rect width="96" height="96" rx="18" fill="#070812"/>
                                             <circle cx="48" cy="38" r="26" fill="url(#glow)" opacity="0.9"/>
                                             <path d="M25 76 C32 54 64 54 71 76 Z" fill="url(#cloak)" stroke="#72f5ff" stroke-width="2" opacity="0.85"/>
                                             <circle cx="48" cy="40" r="16" fill="#0a0f24" stroke="#ffd4a4" stroke-width="2"/>
                                             <path d="M38 38 C42 28 54 28 58 38" stroke="#ffd4a4" stroke-width="3" fill="none" stroke-linecap="round"/>
                                             <circle cx="42" cy="42" r="3" fill="#fff"/>
                                             <circle cx="54" cy="42" r="3" fill="#fff"/>
                                             <path d="M40 50 C45 56 51 56 56 50" stroke="#ffb347" stroke-width="2" stroke-linecap="round"/>
                                             <path d="M48 60 L48 70" stroke="#ffb347" stroke-width="2" stroke-linecap="round"/>
                                           </svg>
                                           """;
}
