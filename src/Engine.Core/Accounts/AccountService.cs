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
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int HashIterations = 100_000;
    private const long MaxBaseCurrency = 10_000_000_000L;
    private const long MaxPremiumCurrency = 1_000_000_000L;

    private readonly AccountRulebook _rulebook;
    private readonly ISystemClock _clock;
    private readonly ConcurrentDictionary<string, UserRecord> _users = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, UserCredential> _credentialsByEmail =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, UniverseRecord> _universes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CharacterRecord> _characters = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, UserUniverses> _universesByUser =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, UniverseCharacters> _charactersByUniverse =
        new(StringComparer.OrdinalIgnoreCase);

    public AccountService(AccountRulebook rulebook, ISystemClock clock)
    {
        _rulebook = rulebook;
        _clock = clock;
    }

    public async ValueTask<UserRecord> RegisterUserAsync(string email, string password, string displayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedEmail = NormalizeEmail(email);
        if (!IsValidEmail(normalizedEmail))
        {
            throw new AccountOperationException(AccountErrorCodes.ValidationFailed, "Email address is invalid.");
        }

        if (_credentialsByEmail.ContainsKey(normalizedEmail))
        {
            throw new AccountOperationException(AccountErrorCodes.ValidationFailed, "Email is already registered.");
        }

        var trimmedName = displayName.Trim();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = GenerateId("user");
            var record = new UserRecord(id, normalizedEmail, trimmedName, _clock.UtcNow);
            if (_users.TryAdd(id, record))
            {
                var salt = GenerateSalt();
                var hash = HashPassword(password, salt);
                _credentialsByEmail[normalizedEmail] = new UserCredential(id, normalizedEmail, hash, salt);
                _universesByUser.TryAdd(id, new UserUniverses());
                return record;
            }

            await Task.Yield();
        }
    }

    public ValueTask<UserRecord?> AuthenticateAsync(string email, string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedEmail = NormalizeEmail(email);
        if (!_credentialsByEmail.TryGetValue(normalizedEmail, out var credential))
        {
            return ValueTask.FromResult<UserRecord?>(null);
        }

        var candidate = HashPassword(password, credential.Salt);
        if (!candidate.SequenceEqual(credential.Hash))
        {
            return ValueTask.FromResult<UserRecord?>(null);
        }

        return ValueTask.FromResult(_users.TryGetValue(credential.UserId, out var record) ? record : null);
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

    public ValueTask<UniverseRecord?> GetUniverseAsync(string universeId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(universeId);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_universes.TryGetValue(universeId, out var record) ? record : null);
    }

    public ValueTask<IReadOnlyList<UniverseRecord>> ListUniversesAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_users.ContainsKey(userId))
        {
            throw AccountErrors.UserNotFound(userId);
        }

        var bucket = _universesByUser.GetOrAdd(userId, _ => new UserUniverses());
        return ValueTask.FromResult<IReadOnlyList<UniverseRecord>>(bucket.Snapshot());
    }

    public ValueTask<UniverseRecord> CreateUniverseAsync(string userId, string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_users.ContainsKey(userId))
        {
            throw AccountErrors.UserNotFound(userId);
        }

        var resolvedName = name.Trim();
        var rules = _rulebook.Snapshot;
        var universe = new UniverseRecord(GenerateId("universe"), userId, resolvedName, _clock.UtcNow);
        var bucket = _universesByUser.GetOrAdd(userId, _ => new UserUniverses());
        if (!bucket.TryAdd(universe, rules.MaxUniversesPerUser))
        {
            throw AccountErrors.UniverseLimit(userId);
        }

        _universes[universe.Id] = universe;
        return ValueTask.FromResult(universe);
    }

    public ValueTask<IReadOnlyList<CharacterRecord>> ListCharactersAsync(string universeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(universeId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_universes.ContainsKey(universeId))
        {
            throw AccountErrors.UniverseNotFound(universeId);
        }

        var bucket = _charactersByUniverse.GetOrAdd(universeId, _ => new UniverseCharacters());
        return ValueTask.FromResult<IReadOnlyList<CharacterRecord>>(bucket.Snapshot());
    }

    public ValueTask<CharacterRecord> CreateCharacterAsync(string universeId, string? name,
        string? spriteAssetId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(universeId);
        cancellationToken.ThrowIfCancellationRequested();
        var universe = RequireUniverse(universeId);
        var rules = _rulebook.Snapshot;
        var bucket = _charactersByUniverse.GetOrAdd(universeId, _ => new UniverseCharacters());
        var ordinal = bucket.Count + 1;
        var resolvedName = string.IsNullOrWhiteSpace(name) ? $"Character {ordinal}" : name!.Trim();
        var record = new CharacterRecord(
            GenerateId("char"),
            universe.Id,
            resolvedName,
            _clock.UtcNow,
            rules.DefaultBaseCurrency,
            rules.DefaultPremiumCurrency,
            rules.StarterEquipment,
            string.IsNullOrWhiteSpace(spriteAssetId) ? rules.DefaultSpriteAssetId : spriteAssetId!);

        if (!bucket.TryAdd(record, rules.MaxCharactersPerUniverse))
        {
            throw AccountErrors.CharacterLimit(universeId);
        }

        _characters[record.Id] = record;
        return ValueTask.FromResult(record);
    }

    public ValueTask<CharacterRecord> StartNewCharacterAsync(string universeId, string? name = null,
        CancellationToken cancellationToken = default)
        => CreateCharacterAsync(universeId, name, null, cancellationToken);

    public ValueTask<AccountWalletSnapshot> GetWalletSnapshotAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        RequireUser(userId);
        return ValueTask.FromResult(BuildWalletSnapshot(userId));
    }

    public async ValueTask<AccountWalletSnapshot> DepositCurrencyAsync(string userId, string? universeId,
        string? characterId,
        long baseCurrency,
        long premiumCurrency,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (baseCurrency < 0 || premiumCurrency < 0)
        {
            throw new AccountOperationException(AccountErrorCodes.ValidationFailed,
                "Currency grants must be non-negative.");
        }

        if (baseCurrency == 0 && premiumCurrency == 0)
        {
            return await GetWalletSnapshotAsync(userId, cancellationToken).ConfigureAwait(false);
        }

        RequireUser(userId);

        if (!string.IsNullOrWhiteSpace(characterId))
        {
            var character = RequireCharacter(characterId);
            EnsureUniverseOwnership(RequireUniverse(character.UniverseId), userId);
            UpdateCharacterWallet(character, baseCurrency, premiumCurrency);
            return await GetWalletSnapshotAsync(userId, cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(universeId))
        {
            var universe = RequireUniverse(universeId);
            EnsureUniverseOwnership(universe, userId);
            var roster = await ListCharactersAsync(universe.Id, cancellationToken).ConfigureAwait(false);
            if (roster.Count == 0)
            {
                throw AccountErrors.WalletUnavailable($"universe '{universe.Name}'");
            }

            DistributeAcrossCharacters(roster, baseCurrency, premiumCurrency);
            return await GetWalletSnapshotAsync(userId, cancellationToken).ConfigureAwait(false);
        }

        var universes = await ListUniversesAsync(userId, cancellationToken).ConfigureAwait(false);
        var characters = new List<CharacterRecord>();
        foreach (var universe in universes)
        {
            var roster = await ListCharactersAsync(universe.Id, cancellationToken).ConfigureAwait(false);
            characters.AddRange(roster);
        }

        if (characters.Count == 0)
        {
            throw AccountErrors.WalletUnavailable($"user '{userId}'");
        }

        DistributeAcrossCharacters(characters, baseCurrency, premiumCurrency);
        return await GetWalletSnapshotAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask TopUpUserWalletAsync(string userId, long baseCurrency, long premiumCurrency,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        RequireUser(userId);

        var universes = await ListUniversesAsync(userId, cancellationToken).ConfigureAwait(false);
        foreach (var universe in universes)
        {
            var roster = await ListCharactersAsync(universe.Id, cancellationToken).ConfigureAwait(false);
            foreach (var character in roster)
            {
                var baseDelta = Math.Max(0, baseCurrency - character.BaseCurrency);
                var premiumDelta = Math.Max(0, premiumCurrency - character.PremiumCurrency);
                if (baseDelta > 0 || premiumDelta > 0)
                {
                    UpdateCharacterWallet(character, baseDelta, premiumDelta);
                }
            }
        }
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _users.Clear();
        _universes.Clear();
        _characters.Clear();
        _credentialsByEmail.Clear();
        _universesByUser.Clear();
        _charactersByUniverse.Clear();
        return ValueTask.CompletedTask;
    }

    public AccountRulebookSnapshot SnapshotRules() => _rulebook.Snapshot;

    private UniverseRecord RequireUniverse(string universeId)
    {
        if (_universes.TryGetValue(universeId, out var record))
        {
            return record;
        }

        throw AccountErrors.UniverseNotFound(universeId);
    }

    private UserRecord RequireUser(string userId)
    {
        if (_users.TryGetValue(userId, out var record))
        {
            return record;
        }

        throw AccountErrors.UserNotFound(userId);
    }

    private CharacterRecord RequireCharacter(string characterId)
    {
        if (_characters.TryGetValue(characterId, out var record))
        {
            return record;
        }

        throw AccountErrors.CharacterNotFound(characterId);
    }

    private static void EnsureUniverseOwnership(UniverseRecord universe, string userId)
    {
        if (!string.Equals(universe.UserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            throw AccountErrors.UniverseNotFound(universe.Id);
        }
    }

    private void DistributeAcrossCharacters(IReadOnlyList<CharacterRecord> characters, long baseCurrency,
        long premiumCurrency)
    {
        if (characters.Count == 0)
        {
            return;
        }

        var baseAllocations = SplitAmount(baseCurrency, characters.Count);
        var premiumAllocations = SplitAmount(premiumCurrency, characters.Count);
        for (var i = 0; i < characters.Count; i++)
        {
            if (baseAllocations[i] == 0 && premiumAllocations[i] == 0)
            {
                continue;
            }

            UpdateCharacterWallet(characters[i], baseAllocations[i], premiumAllocations[i]);
        }
    }

    private static long[] SplitAmount(long amount, int buckets)
    {
        var allocations = new long[buckets];
        if (amount <= 0 || buckets <= 0)
        {
            return allocations;
        }

        var share = amount / buckets;
        var remainder = amount % buckets;
        for (var i = 0; i < buckets; i++)
        {
            allocations[i] = share + (i < remainder ? 1 : 0);
        }

        return allocations;
    }

    private CharacterRecord UpdateCharacterWallet(CharacterRecord record, long baseDelta, long premiumDelta)
    {
        var nextBase = ClampCurrency(record.BaseCurrency + baseDelta, 0, MaxBaseCurrency);
        var nextPremium = ClampCurrency(record.PremiumCurrency + premiumDelta, 0, MaxPremiumCurrency);
        var updated = record with { BaseCurrency = nextBase, PremiumCurrency = nextPremium };
        _characters[updated.Id] = updated;
        var bucket = _charactersByUniverse.GetOrAdd(updated.UniverseId, _ => new UniverseCharacters());
        bucket.Update(updated);
        return updated;
    }

    private static long ClampCurrency(long value, long min, long max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private AccountWalletSnapshot BuildWalletSnapshot(string userId)
    {
        var universes = _universesByUser.GetOrAdd(userId, _ => new UserUniverses()).Snapshot();
        var universeSummaries = new List<UniverseWalletSnapshot>(universes.Length);
        var characterSummaries = new List<CharacterWalletSnapshot>();
        var accountWallet = WalletBreakdown.Empty;

        foreach (var universe in universes)
        {
            var bucket = _charactersByUniverse.GetOrAdd(universe.Id, _ => new UniverseCharacters());
            var roster = bucket.Snapshot();
            var universeWallet = WalletBreakdown.Empty;
            foreach (var character in roster)
            {
                universeWallet = universeWallet.Add(character.BaseCurrency, character.PremiumCurrency);
                characterSummaries.Add(new CharacterWalletSnapshot(character.Id, character.Name, universe.Id,
                    universe.Name,
                    new WalletBreakdown(character.BaseCurrency, character.PremiumCurrency)));
            }

            universeSummaries.Add(new UniverseWalletSnapshot(universe.Id, universe.Name, universeWallet));
            accountWallet = accountWallet.Add(universeWallet.BaseCurrency, universeWallet.PremiumCurrency);
        }

        return new AccountWalletSnapshot(accountWallet, universeSummaries, characterSummaries);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static bool IsValidEmail(string email) =>
        email.Contains('@', StringComparison.Ordinal) && email.Contains('.', StringComparison.Ordinal);

    private static byte[] GenerateSalt()
    {
        var buffer = new byte[SaltSize];
        RandomNumberGenerator.Fill(buffer);
        return buffer;
    }

    private static byte[] HashPassword(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, HashIterations, HashAlgorithmName.SHA256, HashSize);
    }

    private static string GenerateId(string prefix)
    {
        Span<byte> buffer = stackalloc byte[6];
        RandomNumberGenerator.Fill(buffer);
        var token = Convert.ToHexString(buffer);
        return $"{prefix}-{token}";
    }

    private sealed class UserUniverses
    {
        private readonly List<UniverseRecord> _universes = new();
        private readonly object _gate = new();

        public bool TryAdd(UniverseRecord record, int maxUniverses)
        {
            lock (_gate)
            {
                if (_universes.Count >= maxUniverses)
                {
                    return false;
                }

                _universes.Add(record);
                return true;
            }
        }

        public UniverseRecord[] Snapshot()
        {
            lock (_gate)
            {
                return _universes
                    .OrderByDescending(universe => universe.CreatedAt)
                    .ToArray();
            }
        }
    }

    private sealed class UniverseCharacters
    {
        private readonly List<CharacterRecord> _characters = new();
        private readonly object _gate = new();

        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _characters.Count;
                }
            }
        }

        public bool TryAdd(CharacterRecord record, int maxCharacters)
        {
            lock (_gate)
            {
                if (_characters.Count >= maxCharacters)
                {
                    return false;
                }

                _characters.Add(record);
                return true;
            }
        }

        public void Update(CharacterRecord record)
        {
            lock (_gate)
            {
                var index = _characters.FindIndex(c =>
                    string.Equals(c.Id, record.Id, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    _characters[index] = record;
                }
            }
        }

        public CharacterRecord[] Snapshot()
        {
            lock (_gate)
            {
                return _characters
                    .OrderByDescending(character => character.CreatedAt)
                    .ToArray();
            }
        }
    }

    private sealed record UserCredential(string UserId, string Email, byte[] Hash, byte[] Salt);
}
