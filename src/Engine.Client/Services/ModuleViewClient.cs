using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text;
using Engine.Core.Contracts;

namespace Engine.Client.Services;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Created via dependency injection")]
internal sealed class ModuleViewClient
{
    private readonly HttpClient _httpClient;

    public ModuleViewClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ModuleViewDocument>> ListAsync(string? userId = null,
        IReadOnlyDictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder("dashboard/view-documents");
        var hasQuery = false;

        void AppendQuery(string key, string value)
        {
            if (!hasQuery)
            {
                builder.Append('?');
                hasQuery = true;
            }
            else
            {
                builder.Append('&');
            }

            builder.Append(Uri.EscapeDataString(key));
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(value));
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            AppendQuery("userId", userId);
        }

        if (parameters is { Count: > 0 })
        {
            foreach (var (key, value) in parameters)
            {
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                AppendQuery(key, value);
            }
        }

        var requestUri = builder.ToString();
        var response = await _httpClient
            .GetFromJsonAsync<IReadOnlyList<ModuleViewDocument>>(requestUri, cancellationToken)
            .ConfigureAwait(false);
        return response ?? Array.Empty<ModuleViewDocument>();
    }
}
