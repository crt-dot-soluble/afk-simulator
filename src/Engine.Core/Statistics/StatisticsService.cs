using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Engine.Core.Contracts;

namespace Engine.Core.Statistics;

public sealed class StatisticsService : IStatisticsService
{
    private const string ModuleId = "statistics";
    private const string StateKey = "skill-progress";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<string, SkillDefinition> _definitions =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, SkillProgressState> _progress =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly IModuleStateStore _stateStore;
    private readonly object _gate = new();
    private string _activeSkillId = string.Empty;
    private double _totalCurrency;

    public StatisticsService(IModuleStateStore stateStore)
    {
        _stateStore = stateStore;
        RestoreState();
    }

    public SkillDefinition RegisterSkill(string id, string name, string description, double currencyPerSecond,
        string defaultAnimation, string accentColor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultAnimation);
        ArgumentException.ThrowIfNullOrWhiteSpace(accentColor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(currencyPerSecond);

        var definition = new SkillDefinition(id, name, description, currencyPerSecond, defaultAnimation, accentColor);
        if (!_definitions.TryAdd(id, definition))
        {
            throw new InvalidOperationException($"Skill '{id}' is already registered.");
        }

        _progress.GetOrAdd(id, _ => new SkillProgressState(id));
        PersistStateFireAndForget();
        return definition;
    }

    public IReadOnlyCollection<SkillDefinition> ListSkillDefinitions() => _definitions.Values
        .OrderBy(def => def.Name)
        .ToArray();

    public SkillStateSnapshot SnapshotSkillState()
    {
        lock (_gate)
        {
            var skills = _progress.Values
                .OrderBy(state => state.SkillId, StringComparer.OrdinalIgnoreCase)
                .Select(state => state.ToSnapshot())
                .ToArray();
            return new SkillStateSnapshot(_activeSkillId, _totalCurrency, skills);
        }
    }

    public StatisticsSnapshot SnapshotStatistics()
    {
        var state = SnapshotSkillState();
        var entries = ListSkillDefinitions()
            .Select(definition => BuildStatisticEntry(definition, state))
            .ToArray();

        var coreNamespace = new StatisticNamespaceSnapshot(
            StatisticNamespaces.Core,
            "AFK Simulator",
            new[]
            {
                new StatisticCategorySnapshot(
                    StatisticCategories.Skills,
                    "Skills",
                    entries)
            });

        return new StatisticsSnapshot(state.ActiveSkillId, state.TotalCurrency, new[] { coreNamespace });
    }

    public void ActivateSkill(string skillId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillId);
        if (!_definitions.ContainsKey(skillId))
        {
            throw new InvalidOperationException($"Skill '{skillId}' was not registered.");
        }

        lock (_gate)
        {
            _activeSkillId = skillId;
        }

        PersistStateFireAndForget();
    }

    internal ValueTask ProcessTickAsync(TimeSpan tickDuration, CancellationToken cancellationToken = default)
    {
        SkillDefinition? active;
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(_activeSkillId) || !_definitions.TryGetValue(_activeSkillId, out active))
            {
                return ValueTask.CompletedTask;
            }
        }

        var award = active.CurrencyPerSecond * tickDuration.TotalSeconds;
        var state = _progress.GetOrAdd(active.Id, id => new SkillProgressState(id));
        state.Grant(award);

        lock (_gate)
        {
            _totalCurrency += award;
        }

        return PersistStateAsync(cancellationToken);
    }

    private void RestoreState()
    {
        try
        {
            var record = _stateStore.GetAsync(ModuleId, StateKey, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            if (record is null || record.Payload.IsEmpty)
            {
                return;
            }

            var model = JsonSerializer.Deserialize<StatisticsPersistenceModel>(record.Payload.Span, SerializerOptions);
            if (model is null)
            {
                return;
            }

            lock (_gate)
            {
                _activeSkillId = model.ActiveSkillId ?? string.Empty;
                _totalCurrency = model.TotalCurrency;
                foreach (var skill in model.Skills)
                {
                    var state = _progress.GetOrAdd(skill.SkillId, id => new SkillProgressState(id));
                    state.Set(skill.Experience, skill.BankedCurrency);
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or OperationCanceledException)
        {
            // Persistence is best-effort; corrupted payloads should not prevent startup.
        }
    }

    private ValueTask PersistStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(BuildPersistenceModel(), SerializerOptions);
            return _stateStore.SaveAsync(ModuleId, StateKey, payload, cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or OperationCanceledException)
        {
            return ValueTask.CompletedTask;
        }
    }

    private void PersistStateFireAndForget()
    {
        _ = PersistStateAsync(CancellationToken.None).AsTask();
    }

    private StatisticsPersistenceModel BuildPersistenceModel()
    {
        lock (_gate)
        {
            var skills = _progress.Values
                .Select(state => new StatisticsSkillPersistenceModel(state.SkillId, state.Experience, state.Currency))
                .ToArray();
            return new StatisticsPersistenceModel(_activeSkillId, _totalCurrency, skills);
        }
    }

    private static StatisticEntrySnapshot BuildStatisticEntry(SkillDefinition definition, SkillStateSnapshot state)
    {
        var progress = state.Skills.FirstOrDefault(progress =>
            string.Equals(progress.SkillId, definition.Id, StringComparison.OrdinalIgnoreCase));
        var value = new StatisticValueSnapshot(
            progress?.Level ?? 1,
            progress?.Experience ?? 0d,
            progress?.BankedCurrency ?? 0d,
            definition.CurrencyPerSecond);
        var isActive = string.Equals(state.ActiveSkillId, definition.Id, StringComparison.OrdinalIgnoreCase);
        return new StatisticEntrySnapshot(
            definition.Id,
            definition.Name,
            definition.Description,
            StatisticEntryKinds.Skill,
            value,
            definition.AccentColor,
            definition.DefaultAnimation,
            isActive);
    }

    private sealed class SkillProgressState
    {
        private double _experience;
        private double _currency;

        public SkillProgressState(string skillId)
        {
            SkillId = skillId;
        }

        public string SkillId { get; }

        public double Experience => _experience;

        public double Currency => _currency;

        public void Grant(double amount)
        {
            _experience += amount;
            _currency += amount;
        }

        public void Set(double experience, double currency)
        {
            _experience = experience;
            _currency = currency;
        }

        public SkillProgressSnapshot ToSnapshot()
        {
            return new SkillProgressSnapshot(SkillId, _experience, SkillFormulas.CalculateLevel(_experience),
                _currency);
        }
    }
}
