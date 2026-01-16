using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Engine.Core.DeveloperTools;

/// <summary>
/// Tracks developer profiles containing saved inspector layouts or scratch data.
/// </summary>
public sealed class DeveloperProfileStore
{
    private readonly ConcurrentDictionary<string, DeveloperProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _storagePath;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly object _fileGate = new();

    public DeveloperProfileStore(DeveloperProfileStoreOptions? options = null)
    {
        _storagePath = string.IsNullOrWhiteSpace(options?.StoragePath) ? null : options!.StoragePath;
        LoadFromDisk();
    }

    public DeveloperProfile GetOrCreate(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _profiles.GetOrAdd(id,
            new DeveloperProfile(id, DateTimeOffset.UtcNow, new Dictionary<string, string>()));
    }

    public DeveloperProfile Upsert(string id, IDictionary<string, string> state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(state);
        var profile = new DeveloperProfile(id, DateTimeOffset.UtcNow, new Dictionary<string, string>(state));
        _profiles[id] = profile;
        Persist();
        return profile;
    }

    public bool TryDelete(string id)
    {
        var removed = _profiles.TryRemove(id, out _);
        if (removed)
        {
            Persist();
        }

        return removed;
    }

    public void Clear()
    {
        _profiles.Clear();
        Persist();
    }

    public IReadOnlyCollection<DeveloperProfile> List() =>
        _profiles.Values.OrderByDescending(p => p.LastUpdated).ToArray();

    private void LoadFromDisk()
    {
        if (string.IsNullOrWhiteSpace(_storagePath) || !File.Exists(_storagePath))
        {
            return;
        }

        var payload = File.ReadAllText(_storagePath);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<DeveloperProfileRecord>>(payload, JsonOptions);
            if (items is null)
            {
                return;
            }

            foreach (var item in items)
            {
                _profiles[item.Id] = new DeveloperProfile(item.Id, item.LastUpdated,
                    new Dictionary<string, string>(item.State));
            }
        }
        catch (IOException)
        {
            // Ignore malformed payloads to avoid blocking startup.
        }
        catch (JsonException)
        {
            // Ignore malformed payloads to avoid blocking startup.
        }
    }

    private void Persist()
    {
        if (string.IsNullOrWhiteSpace(_storagePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var snapshot = _profiles.Values
            .Select(profile => new DeveloperProfileRecord(profile.Id, profile.LastUpdated,
                new Dictionary<string, string>(profile.State)))
            .OrderByDescending(record => record.LastUpdated)
            .ToArray();

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        lock (_fileGate)
        {
            File.WriteAllText(_storagePath, json);
        }
    }
}

public sealed record DeveloperProfile(string Id, DateTimeOffset LastUpdated, IReadOnlyDictionary<string, string> State);

public sealed record DeveloperProfileStoreOptions(string StoragePath);

internal sealed record DeveloperProfileRecord(string Id, DateTimeOffset LastUpdated, Dictionary<string, string> State);
