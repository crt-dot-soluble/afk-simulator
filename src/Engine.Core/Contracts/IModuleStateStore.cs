using System;
using System.Threading;
using System.Threading.Tasks;

namespace Engine.Core.Contracts;

public interface IModuleStateStore
{
    ValueTask<ModuleStateRecord?> GetAsync(string moduleId, string stateKey,
        CancellationToken cancellationToken = default);

    ValueTask SaveAsync(string moduleId, string stateKey, ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default);

    ValueTask DeleteAsync(string moduleId, string stateKey, CancellationToken cancellationToken = default);
}

public sealed record ModuleStateRecord(string ModuleId, string StateKey, ReadOnlyMemory<byte> Payload,
    DateTimeOffset UpdatedAt);
