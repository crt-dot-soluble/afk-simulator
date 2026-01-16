using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Engine.Core.Contracts;

namespace Engine.Core.Accounts;

public sealed class AccountService : IAccountService
{
    private readonly AccountRulebook _rulebook;
    private readonly ISystemClock _clock;
    private readonly ConcurrentDictionary<string, UserRecord> _users = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, AccountRecord> _accounts = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, CharacterProfileRecord> _profiles =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, UserAccounts> _accountsByUser = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, AccountProfiles> _profilesByAccount =
        new(StringComparer.OrdinalIgnoreCase);

    public AccountService(AccountRulebook rulebook, ISystemClock clock)
    {
        _rulebook = rulebook;
        _clock = clock;
    }

    public async ValueTask<UserRecord> CreateUserAsync(string displayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        cancellationToken.ThrowIfCancellationRequested();

        var trimmed = displayName.Trim();
        while (true)
        {
            var id = GenerateId("user");
            var record = new UserRecord(id, trimmed, _clock.UtcNow);
            if (_users.TryAdd(id, record))
            {
                _accountsByUser.TryAdd(id, new UserAccounts());
                return record;
            }

            await Task.Yield();
        }
    }

    public ValueTask<UserRecord?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_users.TryGetValue(userId, out var record) ? record : null);
    }

    public ValueTask<IReadOnlyList<UserRecord>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = _users.Values
            .OrderBy(user => user.CreatedAt)
            .ToArray();
        return ValueTask.FromResult<IReadOnlyList<UserRecord>>(snapshot);
    }

    public ValueTask<AccountRecord?> GetAccountAsync(string accountId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_accounts.TryGetValue(accountId, out var record) ? record : null);
    }

    public ValueTask<IReadOnlyList<AccountRecord>> ListAccountsAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_users.ContainsKey(userId))
        {
            throw AccountErrors.UserNotFound(userId);
        }

        var bucket = _accountsByUser.GetOrAdd(userId, _ => new UserAccounts());
        return ValueTask.FromResult<IReadOnlyList<AccountRecord>>(bucket.Snapshot());
    }

    public ValueTask<AccountRecord> CreateAccountAsync(string userId, string label,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_users.ContainsKey(userId))
        {
            throw AccountErrors.UserNotFound(userId);
        }

        var resolvedLabel = label.Trim();
        var rules = _rulebook.Snapshot;
        var account = new AccountRecord(GenerateId("acct"), userId, resolvedLabel, _clock.UtcNow);
        var bucket = _accountsByUser.GetOrAdd(userId, _ => new UserAccounts());
        if (!bucket.TryAdd(account, rules.MaxAccountsPerUser))
        {
            throw AccountErrors.AccountLimit(userId);
        }

        _accounts[account.Id] = account;
        return ValueTask.FromResult(account);
    }

    public ValueTask<IReadOnlyList<CharacterProfileRecord>> ListProfilesAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_accounts.ContainsKey(accountId))
        {
            throw AccountErrors.AccountNotFound(accountId);
        }

        var bucket = _profilesByAccount.GetOrAdd(accountId, _ => new AccountProfiles());
        return ValueTask.FromResult<IReadOnlyList<CharacterProfileRecord>>(bucket.Snapshot());
    }

    public ValueTask<CharacterProfileRecord> CreateProfileAsync(string accountId, string? name,
        string? spriteAssetId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        cancellationToken.ThrowIfCancellationRequested();
        var account = RequireAccount(accountId);
        var rules = _rulebook.Snapshot;
        var bucket = _profilesByAccount.GetOrAdd(accountId, _ => new AccountProfiles());
        var ordinal = bucket.Count + 1;
        var resolvedName = string.IsNullOrWhiteSpace(name) ? $"New Life {ordinal}" : name!.Trim();
        var record = new CharacterProfileRecord(
            GenerateId("life"),
            account.Id,
            resolvedName,
            _clock.UtcNow,
            rules.DefaultBaseCurrency,
            rules.DefaultPremiumCurrency,
            rules.StarterEquipment,
            string.IsNullOrWhiteSpace(spriteAssetId) ? rules.DefaultSpriteAssetId : spriteAssetId!);

        if (!bucket.TryAdd(record, rules.MaxProfilesPerAccount))
        {
            throw AccountErrors.ProfileLimit(accountId);
        }

        _profiles[record.Id] = record;
        return ValueTask.FromResult(record);
    }

    public ValueTask<CharacterProfileRecord> StartNewLifeAsync(string accountId, string? name = null,
        CancellationToken cancellationToken = default)
    {
        return CreateProfileAsync(accountId, name, null, cancellationToken);
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _users.Clear();
        _accounts.Clear();
        _profiles.Clear();
        _accountsByUser.Clear();
        _profilesByAccount.Clear();
        return ValueTask.CompletedTask;
    }

    public AccountRulebookSnapshot SnapshotRules() => _rulebook.Snapshot;

    private AccountRecord RequireAccount(string accountId)
    {
        if (_accounts.TryGetValue(accountId, out var record))
        {
            return record;
        }

        throw AccountErrors.AccountNotFound(accountId);
    }

    private static string GenerateId(string prefix)
    {
        Span<byte> buffer = stackalloc byte[6];
        RandomNumberGenerator.Fill(buffer);
        var token = Convert.ToHexString(buffer);
        return $"{prefix}-{token}";
    }

    private sealed class UserAccounts
    {
        private readonly List<AccountRecord> _accounts = new();
        private readonly object _gate = new();

        public bool TryAdd(AccountRecord record, int maxAccounts)
        {
            lock (_gate)
            {
                if (_accounts.Count >= maxAccounts)
                {
                    return false;
                }

                _accounts.Add(record);
                return true;
            }
        }

        public AccountRecord[] Snapshot()
        {
            lock (_gate)
            {
                return _accounts
                    .OrderByDescending(account => account.CreatedAt)
                    .ToArray();
            }
        }
    }

    private sealed class AccountProfiles
    {
        private readonly List<CharacterProfileRecord> _profiles = new();
        private readonly object _gate = new();

        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _profiles.Count;
                }
            }
        }

        public bool TryAdd(CharacterProfileRecord record, int maxProfiles)
        {
            lock (_gate)
            {
                if (_profiles.Count >= maxProfiles)
                {
                    return false;
                }

                _profiles.Add(record);
                return true;
            }
        }

        public CharacterProfileRecord[] Snapshot()
        {
            lock (_gate)
            {
                return _profiles
                    .OrderByDescending(profile => profile.CreatedAt)
                    .ToArray();
            }
        }
    }
}
