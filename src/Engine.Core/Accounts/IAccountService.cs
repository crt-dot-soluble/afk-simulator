using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Engine.Core.Accounts;

public interface IAccountService
{
    ValueTask<UserRecord> RegisterUserAsync(string email, string password, string displayName,
        CancellationToken cancellationToken = default);

    ValueTask<UserRecord?> AuthenticateAsync(string email, string password,
        CancellationToken cancellationToken = default);

    ValueTask<UserRecord?> GetUserAsync(string userId, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<UserRecord>> ListUsersAsync(CancellationToken cancellationToken = default);

    ValueTask<UniverseRecord?> GetUniverseAsync(string universeId, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<UniverseRecord>> ListUniversesAsync(string userId,
        CancellationToken cancellationToken = default);

    ValueTask<UniverseRecord> CreateUniverseAsync(string userId, string name,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<CharacterRecord>> ListCharactersAsync(string universeId,
        CancellationToken cancellationToken = default);

    ValueTask<CharacterRecord> CreateCharacterAsync(string universeId, string? name, string? spriteAssetId = null,
        CancellationToken cancellationToken = default);

    ValueTask<CharacterRecord> StartNewCharacterAsync(string universeId, string? name = null,
        CancellationToken cancellationToken = default);

    ValueTask<AccountWalletSnapshot> GetWalletSnapshotAsync(string userId,
        CancellationToken cancellationToken = default);

    ValueTask<AccountWalletSnapshot> DepositCurrencyAsync(string userId, string? universeId, string? characterId,
        long baseCurrency,
        long premiumCurrency,
        CancellationToken cancellationToken = default);

    ValueTask TopUpUserWalletAsync(string userId, long baseCurrency, long premiumCurrency,
        CancellationToken cancellationToken = default);

    ValueTask ClearAsync(CancellationToken cancellationToken = default);

    AccountRulebookSnapshot SnapshotRules();
}
