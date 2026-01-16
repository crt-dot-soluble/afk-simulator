using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Engine.Client.Services;

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated via Blazor dependency injection.")]
internal sealed class ClientErrorReporter
{
    private readonly HttpClient _httpClient;

    public ClientErrorReporter(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task ReportAsync(string source, Exception exception, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new ClientErrorPayload
            {
                Source = string.IsNullOrWhiteSpace(source) ? "unknown" : source,
                Message = exception?.Message ?? "Unhandled client exception",
                StackTrace = exception?.ToString(),
                Timestamp = DateTimeOffset.UtcNow
            };

            await _httpClient.PostAsJsonAsync("telemetry/errors", payload, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException logEx)
        {
            await LogLocalFailureAsync(source, logEx).ConfigureAwait(false);
        }
        catch (TaskCanceledException logEx)
        {
            await LogLocalFailureAsync(source, logEx).ConfigureAwait(false);
        }
        catch (NotSupportedException logEx)
        {
            await LogLocalFailureAsync(source, logEx).ConfigureAwait(false);
        }
        catch (InvalidOperationException logEx)
        {
            await LogLocalFailureAsync(source, logEx).ConfigureAwait(false);
        }
    }

    private static Task LogLocalFailureAsync(string source, Exception exception)
    {
        return Console.Error.WriteLineAsync($"[ClientErrorReporter] Unable to record '{source}': {exception}");
    }

    private sealed class ClientErrorPayload
    {
        public string Source { get; set; } = "unknown";
        public string Message { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
}
