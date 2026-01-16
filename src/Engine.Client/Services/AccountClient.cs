using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

    public async Task<UserRecord> CreateUserAsync(string displayName, CancellationToken cancellationToken = default)
    {
        var payload = new { displayName };
        var response = await _httpClient.PostAsJsonAsync(BuildUri("accounts/users"), payload, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<UserRecord>(cancellationToken: cancellationToken)
            .ConfigureAwait(false))!;
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

    public async Task<IReadOnlyList<AccountRecord>> GetAccountsAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(BuildUri($"accounts/users/{userId}/accounts"), cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new AccountClientException(AccountErrorCodes.UserNotFound, "User record not found.");
        }

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content
            .ReadFromJsonAsync<List<AccountRecord>>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return payload ?? new List<AccountRecord>();
    }

    public async Task<AccountRecord> CreateAccountAsync(string userId, string label,
        CancellationToken cancellationToken = default)
    {
        var payload = new { label };
        var response = await _httpClient
            .PostAsJsonAsync(BuildUri($"accounts/users/{userId}/accounts"), payload, cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new AccountClientException(AccountErrorCodes.UserNotFound, "User record not found.");
        }

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<AccountRecord>(cancellationToken: cancellationToken)
            .ConfigureAwait(false))!;
    }

    public async Task<IReadOnlyList<CharacterProfileRecord>> GetProfilesAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(BuildUri($"accounts/accounts/{accountId}/profiles"), cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new AccountClientException(AccountErrorCodes.AccountNotFound, "Account not found.");
        }

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content
            .ReadFromJsonAsync<List<CharacterProfileRecord>>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return payload ?? new List<CharacterProfileRecord>();
    }

    public async Task<CharacterProfileRecord> CreateProfileAsync(string accountId, string? name,
        CancellationToken cancellationToken = default)
    {
        var payload = new { name };
        var response = await _httpClient
            .PostAsJsonAsync(BuildUri($"accounts/accounts/{accountId}/profiles"), payload, cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new AccountClientException(AccountErrorCodes.AccountNotFound, "Account not found.");
        }

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<CharacterProfileRecord>(cancellationToken: cancellationToken)
            .ConfigureAwait(false))!;
    }

    private Uri BuildUri(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Uri.TryCreate(relativePath, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        var baseAddress = _httpClient.BaseAddress
                         ?? throw new InvalidOperationException("HttpClient.BaseAddress must be configured for AccountClient.");
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
