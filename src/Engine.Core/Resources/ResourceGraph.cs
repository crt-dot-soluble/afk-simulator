using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Engine.Core.Resources;

/// <summary>
/// Deterministic resource graph responsible for IdleOn-style resource generation and conversion math.
/// </summary>
public sealed class ResourceGraph
{
    private readonly Dictionary<string, ResourceNodeDefinition> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> _state = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ResourceEdgeDefinition> _edges = new();
    private readonly object _gate = new();

    public void UpsertNode(ResourceNodeDefinition definition, double initialValue = 0d)
    {
        ArgumentNullException.ThrowIfNull(definition);
        lock (_gate)
        {
            _nodes[definition.Id] = definition;
            _state[definition.Id] = definition.Clamp(initialValue);
        }
    }

    public void UpsertEdge(ResourceEdgeDefinition edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        lock (_gate)
        {
            if (!_nodes.ContainsKey(edge.SourceId) || !_nodes.ContainsKey(edge.TargetId))
            {
                throw new InvalidOperationException("Edges can only connect known nodes.");
            }

            var existingIndex = _edges.FindIndex(e => e.Id == edge.Id);
            if (existingIndex >= 0)
            {
                _edges[existingIndex] = edge;
            }
            else
            {
                _edges.Add(edge);
                _edges.Sort(static (a, b) => string.CompareOrdinal(a.Id, b.Id));
            }
        }
    }

    public IReadOnlyDictionary<string, ResourceSnapshot> Advance(TimeSpan delta)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(delta, TimeSpan.Zero);

        lock (_gate)
        {
            var seconds = delta.TotalSeconds;
            foreach (var (id, definition) in _nodes)
            {
                var generated = definition.GenerationPerSecond * seconds;
                var nextValue = definition.Clamp(_state[id] + generated);
                _state[id] = nextValue;
            }

            foreach (var edge in _edges)
            {
                var available = _state[edge.SourceId];
                var transferable = Math.Min(available, edge.RatePerSecond * seconds);
                if (transferable <= 0)
                {
                    continue;
                }

                _state[edge.SourceId] -= transferable;
                var gain = transferable * edge.Efficiency;
                var targetDefinition = _nodes[edge.TargetId];
                _state[edge.TargetId] = targetDefinition.Clamp(_state[edge.TargetId] + gain);
            }

            return _state.Select(static kvp => new ResourceSnapshot(kvp.Key, kvp.Value))
                .ToImmutableDictionary(static snapshot => snapshot.Id);
        }
    }

    public IReadOnlyDictionary<string, double> ExportState()
    {
        lock (_gate)
        {
            return _state.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value,
                StringComparer.OrdinalIgnoreCase);
        }
    }
}
