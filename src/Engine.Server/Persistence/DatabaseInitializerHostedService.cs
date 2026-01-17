using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Engine.Server.Persistence;

[SuppressMessage("Performance", "CA1812", Justification = "Activated via dependency injection.")]
internal sealed class DatabaseInitializerHostedService : IHostedService
{
    private readonly IDbContextFactory<IncrementalEngineDbContext> _factory;
    private readonly ILogger<DatabaseInitializerHostedService> _logger;

    private static readonly Action<ILogger, string, Exception?> DatabaseEnsuringLog = LoggerMessage.Define<string>(
        LogLevel.Information,
        new EventId(1, "DatabaseEnsuring"),
        "Ensuring Incremental Engine database is created using provider {Provider}...");

    private static readonly Action<ILogger, string, Exception?> DatabaseReadyLog = LoggerMessage.Define<string>(
        LogLevel.Information,
        new EventId(2, "DatabaseReady"),
        "Database ready (provider: {Provider}).");

    public DatabaseInitializerHostedService(IDbContextFactory<IncrementalEngineDbContext> factory,
        ILogger<DatabaseInitializerHostedService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var provider = db.Database.ProviderName ?? "unknown";
        DatabaseEnsuringLog(_logger, provider, null);
        await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        DatabaseReadyLog(_logger, provider, null);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
