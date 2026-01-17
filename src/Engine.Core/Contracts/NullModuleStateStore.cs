using System;
using System.Threading;
using System.Threading.Tasks;

namespace Engine.Core.Contracts;

public sealed class NullModuleStateStore : IModuleStateStore
{
    public static NullModuleStateStore Instance { get; } = new();

    public ValueTask<ModuleStateRecord?> GetAsync(string moduleId, string stateKey,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult<ModuleStateRecord?>(null);

    public ValueTask SaveAsync(string moduleId, string stateKey, ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask DeleteAsync(string moduleId, string stateKey, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
