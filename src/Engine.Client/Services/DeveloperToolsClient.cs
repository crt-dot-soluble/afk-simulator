using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Engine.Core.DeveloperTools;
using Microsoft.Extensions.Configuration;

namespace Engine.Client.Services;

internal sealed class DeveloperToolsClient
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public DeveloperToolsClient(HttpClient http, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(configuration);
        _http = http;
        _apiKey = configuration["Developer:ApiKey"];
    }

    public async Task<IReadOnlyList<DeveloperModuleDescriptor>> ListModulesAsync(
        CancellationToken cancellationToken = default)
    {
        var response =
            await SendAsync<IReadOnlyList<DeveloperModuleDescriptor>>(HttpMethod.Get, "developer/modules", null,
                cancellationToken).ConfigureAwait(false);
        return response ?? Array.Empty<DeveloperModuleDescriptor>();
    }

    public async Task<DeveloperModuleDescriptor> GetModuleAsync(string moduleName,
        CancellationToken cancellationToken = default)
    {
        var response =
            await SendAsync<DeveloperModuleDescriptor>(HttpMethod.Get, $"developer/modules/{moduleName}", null,
                cancellationToken).ConfigureAwait(false);
        return response ?? throw new InvalidOperationException($"Module '{moduleName}' not found.");
    }

    public async Task<ModulePropertyUpdateResult> UpdatePropertyAsync(string moduleName, string propertyName,
        object? value, CancellationToken cancellationToken = default)
    {
        var payload = new { value };
        var body = await SendAsync<ModulePropertyUpdateResult>(HttpMethod.Post,
                $"developer/modules/{moduleName}/properties/{propertyName}", payload, cancellationToken)
            .ConfigureAwait(false);
        return body ?? throw new InvalidOperationException("Server returned empty update payload.");
    }

    public async Task<DeveloperCommandResult> ExecuteCommandAsync(string moduleName, string commandName,
        IReadOnlyDictionary<string, object?>? parameters, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            parameters
        };

        var body = await SendAsync<DeveloperCommandResult>(HttpMethod.Post,
            $"developer/modules/{moduleName}/commands/{commandName}", payload, cancellationToken).ConfigureAwait(false);
        return body ?? new DeveloperCommandResult(null);
    }

    public async Task<IReadOnlyList<DeveloperAutocompleteEntry>> GetAutocompleteAsync(
        CancellationToken cancellationToken = default)
    {
        var response =
            await SendAsync<IReadOnlyList<DeveloperAutocompleteEntry>>(HttpMethod.Get, "developer/autocomplete", null,
                cancellationToken).ConfigureAwait(false);
        return response ?? Array.Empty<DeveloperAutocompleteEntry>();
    }

    public async Task<IReadOnlyList<DeveloperProfile>> ListProfilesAsync(CancellationToken cancellationToken = default)
    {
        var response =
            await SendAsync<IReadOnlyList<DeveloperProfile>>(HttpMethod.Get, "developer/profiles", null,
                cancellationToken).ConfigureAwait(false);
        return response ?? Array.Empty<DeveloperProfile>();
    }

    public async Task<DeveloperProfile> UpsertProfileAsync(string id, IReadOnlyDictionary<string, string> state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        var payload = new DeveloperProfileUpsertRequest(id, state);
        var body = await SendAsync<DeveloperProfile>(HttpMethod.Post, "developer/profiles", payload, cancellationToken)
            .ConfigureAwait(false);
        return body ?? throw new InvalidOperationException("Profile upsert returned empty response.");
    }

    private async Task<T?> SendAsync<T>(HttpMethod method, string uri, object? payload,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, uri, payload);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(Options, cancellationToken).ConfigureAwait(false);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri, object? payload)
    {
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            request.Headers.TryAddWithoutValidation(DeveloperAuthDefaults.HeaderName, _apiKey);
        }

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload, options: Options);
        }

        return request;
    }

    private sealed record DeveloperProfileUpsertRequest(string Id, IReadOnlyDictionary<string, string> State);
}
