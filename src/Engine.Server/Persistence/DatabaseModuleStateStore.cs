using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Engine.Core.Contracts;
using Engine.Server.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Engine.Server.Persistence;

[SuppressMessage("Performance", "CA1812", Justification = "Registered via dependency injection.")]
internal sealed class DatabaseModuleStateStore : IModuleStateStore, IAsyncDisposable
{
    private readonly IDbContextFactory<IncrementalEngineDbContext> _factory;
    private readonly Task _initializationTask;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public DatabaseModuleStateStore(IDbContextFactory<IncrementalEngineDbContext> factory)
    {
        _factory = factory;
        _initializationTask = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        using var db = await _factory.CreateDbContextAsync().ConfigureAwait(false);
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
    }

    public async ValueTask<ModuleStateRecord?> GetAsync(string moduleId, string stateKey,
        CancellationToken cancellationToken = default)
    {
        await _initializationTask.ConfigureAwait(false);
        using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.ModuleStates.AsNoTracking()
            .FirstOrDefaultAsync(state => state.ModuleId == moduleId && state.StateKey == stateKey,
                cancellationToken)
            .ConfigureAwait(false);
        return entity is null
            ? null
            : new ModuleStateRecord(entity.ModuleId, entity.StateKey, entity.Payload, entity.UpdatedAt);
    }

    public async ValueTask SaveAsync(string moduleId, string stateKey, ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        await _initializationTask.ConfigureAwait(false);
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var entity = await db.ModuleStates
                .FirstOrDefaultAsync(state => state.ModuleId == moduleId && state.StateKey == stateKey,
                    cancellationToken)
                .ConfigureAwait(false);
            var buffer = payload.ToArray();
            if (entity is null)
            {
                db.ModuleStates.Add(new ModuleStateEntity
                {
                    ModuleId = moduleId,
                    StateKey = stateKey,
                    Payload = buffer,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
            else
            {
                entity.Payload = buffer;
                entity.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async ValueTask DeleteAsync(string moduleId, string stateKey, CancellationToken cancellationToken = default)
    {
        await _initializationTask.ConfigureAwait(false);
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var entity = await db.ModuleStates
                .FirstOrDefaultAsync(state => state.ModuleId == moduleId && state.StateKey == stateKey,
                    cancellationToken)
                .ConfigureAwait(false);
            if (entity is null)
            {
                return;
            }

            db.ModuleStates.Remove(entity);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _writeLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
