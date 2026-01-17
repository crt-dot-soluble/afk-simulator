using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Engine.Core.Accounts;
using Engine.Core.Contracts;
using Engine.Server.Persistence;
using Engine.Server.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Engine.Server.Accounts;

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by dependency injection.")]
internal sealed class PersistentAccountService : IAccountService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int HashIterations = 100_000;
    private const long MaxBaseCurrency = 10_000_000_000L;
    private const long MaxPremiumCurrency = 1_000_000_000L;

    private static readonly JsonSerializerOptions EquipmentSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly AccountRulebook _rulebook;
    private readonly ISystemClock _clock;
    private readonly IDbContextFactory<IncrementalEngineDbContext> _dbFactory;

    public PersistentAccountService(AccountRulebook rulebook, ISystemClock clock,
        IDbContextFactory<IncrementalEngineDbContext> dbFactory)
    {
        _rulebook = rulebook;
        _clock = clock;
        _dbFactory = dbFactory;
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

        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var exists = await db.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            throw new AccountOperationException(AccountErrorCodes.ValidationFailed, "Email is already registered.");
        }

        var salt = GenerateSalt();
        var hash = HashPassword(password, salt);
        var record = new UserEntity
        {
            Id = GenerateId("user"),
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            DisplayName = displayName.Trim(),
            CreatedAt = _clock.UtcNow,
            PasswordSalt = salt,
            PasswordHash = hash
        };

        await db.Users.AddAsync(record, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToRecord(record);
    }

    public async ValueTask<UserRecord?> AuthenticateAsync(string email, string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedEmail = NormalizeEmail(email);
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }

        var candidate = HashPassword(password, entity.PasswordSalt);
        return candidate.SequenceEqual(entity.PasswordHash) ? ToRecord(entity) : null;
    }

    public async ValueTask<UserRecord?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToRecord(entity);
    }

    public async ValueTask<IReadOnlyList<UserRecord>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entities = await db.Users.AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities
            .OrderBy(user => user.CreatedAt)
            .Select(ToRecord)
            .ToArray();
    }

    public async ValueTask<UniverseRecord?> GetUniverseAsync(string universeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(universeId);
        cancellationToken.ThrowIfCancellationRequested();
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.Universes.AsNoTracking()
            .FirstOrDefaultAsync(universe => universe.Id == universeId, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToRecord(entity);
    }

    public async ValueTask<IReadOnlyList<UniverseRecord>> ListUniversesAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await RequireUserAsync(userId, db, cancellationToken).ConfigureAwait(false);
        var entities = await db.Universes.AsNoTracking()
            .Where(universe => universe.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities
            .OrderByDescending(universe => universe.CreatedAt)
            .Select(ToRecord)
            .ToArray();
    }

    public async ValueTask<UniverseRecord> CreateUniverseAsync(string userId, string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        cancellationToken.ThrowIfCancellationRequested();

        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await RequireUserAsync(userId, db, cancellationToken).ConfigureAwait(false);
        var rules = _rulebook.Snapshot;
        var existing = await db.Universes.CountAsync(universe => universe.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (existing >= rules.MaxUniversesPerUser)
        {
            throw AccountErrors.UniverseLimit(userId);
        }

        var universe = new UniverseEntity
        {
            Id = GenerateId("universe"),
            UserId = userId,
            Name = name.Trim(),
            CreatedAt = _clock.UtcNow
        };

        await db.Universes.AddAsync(universe, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToRecord(universe);
    }

    public async ValueTask<IReadOnlyList<CharacterRecord>> ListCharactersAsync(string universeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(universeId);
        cancellationToken.ThrowIfCancellationRequested();
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var universe = await db.Universes.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == universeId, cancellationToken)
            .ConfigureAwait(false);
        if (universe is null)
        {
            throw AccountErrors.UniverseNotFound(universeId);
        }

        var characters = await db.Characters.AsNoTracking()
            .Where(character => character.UniverseId == universeId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return characters
            .OrderByDescending(character => character.CreatedAt)
            .Select(ToRecord)
            .ToArray();
    }

    public async ValueTask<CharacterRecord> CreateCharacterAsync(string universeId, string? name,
        string? spriteAssetId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(universeId);
        cancellationToken.ThrowIfCancellationRequested();
        var rules = _rulebook.Snapshot;
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var universe = await db.Universes
            .FirstOrDefaultAsync(item => item.Id == universeId, cancellationToken)
            .ConfigureAwait(false);
        if (universe is null)
        {
            throw AccountErrors.UniverseNotFound(universeId);
        }

        var characterCount = await db.Characters.CountAsync(character => character.UniverseId == universeId,
            cancellationToken).ConfigureAwait(false);
        if (characterCount >= rules.MaxCharactersPerUniverse)
        {
            throw AccountErrors.CharacterLimit(universeId);
        }

        var ordinal = characterCount + 1;
        var resolvedName = string.IsNullOrWhiteSpace(name) ? $"Character {ordinal}" : name.Trim();
        var entity = new CharacterEntity
        {
            Id = GenerateId("char"),
            UniverseId = universeId,
            Name = resolvedName,
            CreatedAt = _clock.UtcNow,
            BaseCurrency = rules.DefaultBaseCurrency,
            PremiumCurrency = rules.DefaultPremiumCurrency,
            EquipmentJson = SerializeEquipment(rules.StarterEquipment),
            SpriteAssetId = string.IsNullOrWhiteSpace(spriteAssetId)
                ? rules.DefaultSpriteAssetId
                : spriteAssetId!
        };

        await db.Characters.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToRecord(entity);
    }

    public ValueTask<CharacterRecord> StartNewCharacterAsync(string universeId, string? name = null,
        CancellationToken cancellationToken = default)
        => CreateCharacterAsync(universeId, name, null, cancellationToken);

    public async ValueTask<AccountWalletSnapshot> GetWalletSnapshotAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await RequireUserAsync(userId, db, cancellationToken).ConfigureAwait(false);
        var universes = (await db.Universes.AsNoTracking()
                .Where(universe => universe.UserId == userId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .OrderByDescending(universe => universe.CreatedAt)
            .ToList();

        var universeSnapshots = new List<UniverseWalletSnapshot>();
        var characterSnapshots = new List<CharacterWalletSnapshot>();
        var accountWallet = WalletBreakdown.Empty;

        foreach (var universe in universes)
        {
            var characters = (await db.Characters.AsNoTracking()
                    .Where(character => character.UniverseId == universe.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false))
                .OrderByDescending(character => character.CreatedAt)
                .ToList();
            var universeWallet = WalletBreakdown.Empty;
            foreach (var character in characters)
            {
                var wallet = new WalletBreakdown(character.BaseCurrency, character.PremiumCurrency);
                universeWallet = universeWallet.Add(wallet.BaseCurrency, wallet.PremiumCurrency);
                characterSnapshots.Add(new CharacterWalletSnapshot(character.Id, character.Name, universe.Id,
                    universe.Name, wallet));
            }

            universeSnapshots.Add(new UniverseWalletSnapshot(universe.Id, universe.Name, universeWallet));
            accountWallet = accountWallet.Add(universeWallet.BaseCurrency, universeWallet.PremiumCurrency);
        }

        return new AccountWalletSnapshot(accountWallet, universeSnapshots, characterSnapshots);
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

        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await RequireUserAsync(userId, db, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(characterId))
        {
            var character = await RequireCharacterAsync(characterId!, db, cancellationToken).ConfigureAwait(false);
            var universe = await RequireUniverseAsync(character.UniverseId, db, cancellationToken)
                .ConfigureAwait(false);
            EnsureUniverseOwnership(universe, userId);
            UpdateCharacterWallet(character, baseCurrency, premiumCurrency);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return await GetWalletSnapshotInternalAsync(userId, db, cancellationToken).ConfigureAwait(false);
        }

        List<CharacterEntity> targets;
        if (!string.IsNullOrWhiteSpace(universeId))
        {
            var universe = await RequireUniverseAsync(universeId!, db, cancellationToken).ConfigureAwait(false);
            EnsureUniverseOwnership(universe, userId);
            targets = await db.Characters
                .Where(character => character.UniverseId == universe.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (targets.Count == 0)
            {
                throw AccountErrors.WalletUnavailable($"universe '{universe.Name}'");
            }
        }
        else
        {
            targets = await db.Characters
                .Where(character => character.Universe!.UserId == userId)
                .Include(character => character.Universe)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (targets.Count == 0)
            {
                throw AccountErrors.WalletUnavailable($"user '{userId}'");
            }
        }

        DistributeAcrossCharacters(targets, baseCurrency, premiumCurrency);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetWalletSnapshotInternalAsync(userId, db, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask TopUpUserWalletAsync(string userId, long baseCurrency, long premiumCurrency,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (baseCurrency < 0 || premiumCurrency < 0)
        {
            throw new AccountOperationException(AccountErrorCodes.ValidationFailed,
                "Currency grants must be non-negative.");
        }

        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await RequireUserAsync(userId, db, cancellationToken).ConfigureAwait(false);
        var characters = await db.Characters.Where(character => character.Universe!.UserId == userId)
            .Include(character => character.Universe)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var character in characters)
        {
            var baseDelta = Math.Max(0, baseCurrency - character.BaseCurrency);
            var premiumDelta = Math.Max(0, premiumCurrency - character.PremiumCurrency);
            if (baseDelta > 0 || premiumDelta > 0)
            {
                UpdateCharacterWallet(character, baseDelta, premiumDelta);
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await db.Characters.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Universes.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Users.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    public AccountRulebookSnapshot SnapshotRules() => _rulebook.Snapshot;

    private static async Task<UserEntity> RequireUserAsync(string userId, IncrementalEngineDbContext db,
        CancellationToken cancellationToken)
    {
        var entity = await db.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            throw AccountErrors.UserNotFound(userId);
        }

        return entity;
    }

    private static async Task<UniverseEntity> RequireUniverseAsync(string universeId, IncrementalEngineDbContext db,
        CancellationToken cancellationToken)
    {
        var entity = await db.Universes.FirstOrDefaultAsync(universe => universe.Id == universeId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            throw AccountErrors.UniverseNotFound(universeId);
        }

        return entity;
    }

    private static async Task<CharacterEntity> RequireCharacterAsync(string characterId, IncrementalEngineDbContext db,
        CancellationToken cancellationToken)
    {
        var entity = await db.Characters.FirstOrDefaultAsync(character => character.Id == characterId,
            cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            throw AccountErrors.CharacterNotFound(characterId);
        }

        return entity;
    }

    private static void EnsureUniverseOwnership(UniverseEntity universe, string userId)
    {
        if (!string.Equals(universe.UserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            throw AccountErrors.UniverseNotFound(universe.Id);
        }
    }

    private static AccountWalletSnapshot BuildWalletSnapshot(IEnumerable<UniverseEntity> universes,
        IEnumerable<CharacterEntity> characters)
    {
        var universeLookup = universes.ToDictionary(u => u.Id, StringComparer.OrdinalIgnoreCase);
        var charactersByUniverse = characters.GroupBy(c => c.UniverseId, StringComparer.OrdinalIgnoreCase);
        var accountWallet = WalletBreakdown.Empty;
        var universeSnapshots = new List<UniverseWalletSnapshot>();
        var characterSnapshots = new List<CharacterWalletSnapshot>();

        foreach (var group in charactersByUniverse)
        {
            if (!universeLookup.TryGetValue(group.Key, out var universe))
            {
                continue;
            }

            var universeWallet = WalletBreakdown.Empty;
            foreach (var character in group)
            {
                var wallet = new WalletBreakdown(character.BaseCurrency, character.PremiumCurrency);
                universeWallet = universeWallet.Add(wallet.BaseCurrency, wallet.PremiumCurrency);
                characterSnapshots.Add(new CharacterWalletSnapshot(character.Id, character.Name, universe.Id,
                    universe.Name, wallet));
            }

            universeSnapshots.Add(new UniverseWalletSnapshot(universe.Id, universe.Name, universeWallet));
            accountWallet = accountWallet.Add(universeWallet.BaseCurrency, universeWallet.PremiumCurrency);
        }

        return new AccountWalletSnapshot(accountWallet, universeSnapshots, characterSnapshots);
    }

    private static async ValueTask<AccountWalletSnapshot> GetWalletSnapshotInternalAsync(string userId,
        IncrementalEngineDbContext db, CancellationToken cancellationToken)
    {
        var universes = await db.Universes.AsNoTracking()
            .Where(universe => universe.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var universeIds = universes.Select(universe => universe.Id).ToList();
        var characters = universeIds.Count == 0
            ? new List<CharacterEntity>()
            : await db.Characters.AsNoTracking()
                .Where(character => universeIds.Contains(character.UniverseId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        return BuildWalletSnapshot(universes, characters);
    }

    private static void DistributeAcrossCharacters(List<CharacterEntity> characters, long baseCurrency,
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

    private static void UpdateCharacterWallet(CharacterEntity character, long baseDelta, long premiumDelta)
    {
        var nextBase = ClampCurrency(character.BaseCurrency + baseDelta, 0, MaxBaseCurrency);
        var nextPremium = ClampCurrency(character.PremiumCurrency + premiumDelta, 0, MaxPremiumCurrency);
        character.BaseCurrency = nextBase;
        character.PremiumCurrency = nextPremium;
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

    private static UserRecord ToRecord(UserEntity entity) =>
        new(entity.Id, entity.Email, entity.DisplayName, entity.CreatedAt);

    private static UniverseRecord ToRecord(UniverseEntity entity) =>
        new(entity.Id, entity.UserId, entity.Name, entity.CreatedAt);

    private static CharacterRecord ToRecord(CharacterEntity entity)
    {
        return new CharacterRecord(
            entity.Id,
            entity.UniverseId,
            entity.Name,
            entity.CreatedAt,
            entity.BaseCurrency,
            entity.PremiumCurrency,
            DeserializeEquipment(entity.EquipmentJson),
            entity.SpriteAssetId);
    }

    private static string SerializeEquipment(EquipmentSlots slots)
    {
        return JsonSerializer.Serialize(slots, EquipmentSerializerOptions);
    }

    private static EquipmentSlots DeserializeEquipment(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return new EquipmentSlots(null, null, null, null, null, null, null, null, null);
        }

        return JsonSerializer.Deserialize<EquipmentSlots>(payload, EquipmentSerializerOptions)
               ?? new EquipmentSlots(null, null, null, null, null, null, null, null, null);
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
}
