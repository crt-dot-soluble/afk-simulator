using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Engine.Core.Assets;
using Engine.Core.Contracts;
using Engine.Core.DeveloperTools;

namespace Engine.Core.Accounts;

/// <summary>
/// Exposes the account/new-life domain to the module explorer and seeds avatar assets.
/// </summary>
public sealed class AccountModule : IModuleContract, IModuleDescriptorSource
{
    private static readonly string[] Capabilities = { "accounts", "profiles", "developer-tools" };
    private static readonly string[] Telemetry = { "accounts.new-life.created" };
    private static readonly string[] Resources = { "AvatarSprites", "AccountService" };
    private readonly AccountRulebook _rulebook;
    private readonly IAccountService _accounts;
    private readonly AssetManifest _assetManifest;
    private bool _spriteRegistered;

    public AccountModule(AccountRulebook rulebook, IAccountService accounts, AssetManifest assetManifest)
    {
        _rulebook = rulebook;
        _accounts = accounts;
        _assetManifest = assetManifest;
    }

    public string Name => "Accounts";
    public string Version => "0.1.0";

    [ModuleInspectable("Max accounts per user", Group = "Limits", Description = "Hangars a pilot may maintain (1-12)")]
    public int MaxAccountsPerUser
    {
        get => _rulebook.Snapshot.MaxAccountsPerUser;
        set => _rulebook.SetMaxAccounts(value);
    }

    [ModuleInspectable("Max profiles per account", Group = "Limits",
        Description = "Distinct New Life slots each account may host (1-12)")]
    public int MaxProfilesPerAccount
    {
        get => _rulebook.Snapshot.MaxProfilesPerAccount;
        set => _rulebook.SetMaxProfiles(value);
    }

    [ModuleInspectable("Starter base currency", Group = "Economy",
        Description = "Default soft currency grant per life")]
    public long StarterBaseCurrency
    {
        get => _rulebook.Snapshot.DefaultBaseCurrency;
        set => _rulebook.SetDefaultBaseCurrency(value);
    }

    [ModuleInspectable("Starter premium currency", Group = "Economy",
        Description = "Default premium shard grant per life")]
    public long StarterPremiumCurrency
    {
        get => _rulebook.Snapshot.DefaultPremiumCurrency;
        set => _rulebook.SetDefaultPremiumCurrency(value);
    }

    [ModuleInspectable("Starter sprite", Group = "Visuals", Description = "Sprite identifier used for fresh lives")]
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
            ["maxAccounts"] = _rulebook.Snapshot.MaxAccountsPerUser.ToString(CultureInfo.InvariantCulture),
            ["maxProfiles"] = _rulebook.Snapshot.MaxProfilesPerAccount.ToString(CultureInfo.InvariantCulture)
        };
        return ModuleHealth.Healthy(details);
    }

    [ModuleCommand("list-pilots", Description = "Snapshot of all pilot records", IsQuery = true)]
    public ValueTask<IReadOnlyList<UserRecord>> ListPilotsAsync(CancellationToken cancellationToken = default) =>
        _accounts.ListUsersAsync(cancellationToken);

    [ModuleCommand("seed-demo-user", Description = "Create a sample pilot with multiple hangars")]
    public async ValueTask<UserRecord> SeedDemoUserAsync(CancellationToken cancellationToken = default)
    {
        var alias = $"Demo-{Guid.NewGuid():N}"[..12];
        var user = await _accounts.CreateUserAsync(alias, cancellationToken).ConfigureAwait(false);
        var primary = await _accounts.CreateAccountAsync(user.Id, "Starborn Vanguard", cancellationToken)
            .ConfigureAwait(false);
        await _accounts.CreateProfileAsync(primary.Id, "Origin Spark", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await _accounts.StartNewLifeAsync(primary.Id, "Prestige Nova", cancellationToken).ConfigureAwait(false);
        var secondary = await _accounts.CreateAccountAsync(user.Id, "Dust Runner", cancellationToken)
            .ConfigureAwait(false);
        await _accounts.CreateProfileAsync(secondary.Id, "Nomad Zero", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return user;
    }

    [ModuleCommand("purge-accounts", Description = "Clear in-memory pilots, hangars, and lives")]
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
            "Account & New Life provisioning",
            new Dictionary<string, string>
            {
                ["owner"] = "content",
                ["tier"] = "system",
                ["area"] = "accounts"
            });
    }

    private void RegisterSpriteAsset()
    {
        if (_spriteRegistered)
        {
            return;
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(AvatarSpriteSvg));
        _assetManifest.Register("avatars/ember-nomad", "sprite", "sprites/ember-nomad.svg", 96, 96, stream,
            "avatar", "accounts");
        _spriteRegistered = true;
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
