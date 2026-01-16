using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Engine.Core.Accounts;

public interface IAccountService
{
    ValueTask<UserRecord> CreateUserAsync(string displayName, CancellationToken cancellationToken = default);

    ValueTask<UserRecord?> GetUserAsync(string userId, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<UserRecord>> ListUsersAsync(CancellationToken cancellationToken = default);

    ValueTask<AccountRecord?> GetAccountAsync(string accountId, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<AccountRecord>> ListAccountsAsync(string userId, CancellationToken cancellationToken = default);

    ValueTask<AccountRecord> CreateAccountAsync(string userId, string label, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<CharacterProfileRecord>> ListProfilesAsync(string accountId, CancellationToken cancellationToken = default);

    ValueTask<CharacterProfileRecord> CreateProfileAsync(string accountId, string? name, string? spriteAssetId = null,
        CancellationToken cancellationToken = default);

    ValueTask<CharacterProfileRecord> StartNewLifeAsync(string accountId, string? name = null,
        CancellationToken cancellationToken = default);

    ValueTask ClearAsync(CancellationToken cancellationToken = default);

    AccountRulebookSnapshot SnapshotRules();
}
