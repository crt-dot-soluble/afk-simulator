using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Engine.Core.Accounts;

namespace Engine.Client.Services;

internal sealed class AccountClient
{
    private readonly HttpClient _httpClient;

    public AccountClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UserRecord> RegisterUserAsync(string email, string password, string displayName,
        CancellationToken cancellationToken = default)
    {
        var payload = new { email, password, displayName };
        var response = await _httpClient.PostAsJsonAsync(BuildUri("accounts/users"), payload, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<UserRecord>(cancellationToken: cancellationToken)
            .ConfigureAwait(false))!;
    }

    public async Task<UserRecord?> AuthenticateAsync(string email, string password,
        CancellationToken cancellationToken = default)
    {
        var payload = new { email, password };
        var response = await _httpClient.PostAsJsonAsync(BuildUri("accounts/authenticate"), payload, cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<UserRecord>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<UserRecord?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(BuildUri($"accounts/users/{userId}"), cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<UserRecord>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UniverseRecord>> GetUniversesAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(BuildUri($"accounts/users/{userId}/universes"), cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new AccountClientException(AccountErrorCodes.UserNotFound, "User record not found.");
        }

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content
            .ReadFromJsonAsync<List<UniverseRecord>>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return payload ?? new List<UniverseRecord>();
    }

    public async Task<UniverseRecord> CreateUniverseAsync(string userId, string name,
        CancellationToken cancellationToken = default)
    {
        var payload = new { name };
        var response = await _httpClient
            .PostAsJsonAsync(BuildUri($"accounts/users/{userId}/universes"), payload, cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new AccountClientException(AccountErrorCodes.UserNotFound, "User record not found.");
        }

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<UniverseRecord>(cancellationToken: cancellationToken)
            .ConfigureAwait(false))!;
    }

    public async Task<IReadOnlyList<CharacterRecord>> GetCharactersAsync(string universeId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient
            .GetAsync(BuildUri($"accounts/universes/{universeId}/characters"), cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new AccountClientException(AccountErrorCodes.UniverseNotFound, "Universe not found.");
        }

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content
            .ReadFromJsonAsync<List<CharacterRecord>>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return payload ?? new List<CharacterRecord>();
    }

    public async Task<CharacterRecord> CreateCharacterAsync(string universeId, string? name,
        string? spriteAssetId = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new { name, spriteAssetId };
        var response = await _httpClient
            .PostAsJsonAsync(BuildUri($"accounts/universes/{universeId}/characters"), payload, cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new AccountClientException(AccountErrorCodes.UniverseNotFound, "Universe not found.");
        }

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<CharacterRecord>(cancellationToken: cancellationToken)
            .ConfigureAwait(false))!;
    }

    public async Task<AccountWalletSnapshotDto> GetWalletsAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var response = await _httpClient.GetAsync(BuildUri($"accounts/users/{userId}/wallets"), cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<AccountWalletSnapshotDto>(cancellationToken: cancellationToken)
            .ConfigureAwait(false)) ?? new AccountWalletSnapshotDto();
    }

    public async Task<AccountWalletSnapshotDto> DepositWalletAsync(string userId, WalletDepositDto deposit,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(deposit);
        var payload = new
        {
            baseCurrency = deposit.BaseCurrency,
            premiumCurrency = deposit.PremiumCurrency,
            universeId = deposit.UniverseId,
            characterId = deposit.CharacterId
        };
        var response = await _httpClient
            .PostAsJsonAsync(BuildUri($"accounts/users/{userId}/wallets/deposit"), payload, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<AccountWalletSnapshotDto>(cancellationToken: cancellationToken)
            .ConfigureAwait(false)) ?? new AccountWalletSnapshotDto();
    }

    private Uri BuildUri(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Uri.TryCreate(relativePath, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        var baseAddress = _httpClient.BaseAddress
                          ?? throw new InvalidOperationException(
                              "HttpClient.BaseAddress must be configured for AccountClient.");
        return new Uri(baseAddress, relativePath);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        AccountError? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<AccountError>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            // ignored - fall back to raw body.
        }
        catch (NotSupportedException)
        {
            // ignored - fall back to raw body.
        }

        if (error is not null)
        {
            throw new AccountClientException(error.Code, error.Message);
        }

        var fallback = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new AccountClientException("unknown",
            string.IsNullOrWhiteSpace(fallback) ? "Account API error" : fallback);
    }
}

internal sealed class AccountClientException : Exception
{
    public AccountClientException()
    {
        Code = "unknown";
    }

    public AccountClientException(string message) : base(message)
    {
        Code = "unknown";
    }

    public AccountClientException(string message, Exception? innerException) : base(message, innerException)
    {
        Code = "unknown";
    }

    public AccountClientException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

[SuppressMessage("Performance", "CA1812", Justification = "Deserialized from JSON")]
internal sealed class AccountWalletSnapshotDto
{
    [JsonPropertyName("account")] public WalletBreakdownDto Account { get; set; } = new();

    [JsonPropertyName("universes")]
    public IReadOnlyList<UniverseWalletSnapshotDto> Universes { get; set; } = Array.Empty<UniverseWalletSnapshotDto>();

    [JsonPropertyName("characters")]
    public IReadOnlyList<CharacterWalletSnapshotDto> Characters { get; set; } =
        Array.Empty<CharacterWalletSnapshotDto>();
}

[SuppressMessage("Performance", "CA1812", Justification = "Deserialized from JSON")]
internal sealed class UniverseWalletSnapshotDto
{
    [JsonPropertyName("universeId")] public string UniverseId { get; set; } = string.Empty;
    [JsonPropertyName("universeName")] public string UniverseName { get; set; } = string.Empty;
    [JsonPropertyName("wallet")] public WalletBreakdownDto Wallet { get; set; } = new();
}

[SuppressMessage("Performance", "CA1812", Justification = "Deserialized from JSON")]
internal sealed class CharacterWalletSnapshotDto
{
    [JsonPropertyName("characterId")] public string CharacterId { get; set; } = string.Empty;
    [JsonPropertyName("characterName")] public string CharacterName { get; set; } = string.Empty;
    [JsonPropertyName("universeId")] public string UniverseId { get; set; } = string.Empty;
    [JsonPropertyName("universeName")] public string UniverseName { get; set; } = string.Empty;
    [JsonPropertyName("wallet")] public WalletBreakdownDto Wallet { get; set; } = new();
}

[SuppressMessage("Performance", "CA1812", Justification = "Deserialized from JSON")]
internal sealed class WalletBreakdownDto
{
    [JsonPropertyName("baseCurrency")] public long BaseCurrency { get; set; }
    [JsonPropertyName("premiumCurrency")] public long PremiumCurrency { get; set; }
}

internal sealed class WalletDepositDto
{
    public long BaseCurrency { get; set; }
    public long PremiumCurrency { get; set; }
    public string? UniverseId { get; set; }
    public string? CharacterId { get; set; }
}
