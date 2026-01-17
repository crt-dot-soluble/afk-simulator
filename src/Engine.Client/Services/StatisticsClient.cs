using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Engine.Client.Services;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Created via dependency injection")]
internal sealed class StatisticsClient
{
    private readonly HttpClient _httpClient;

    public StatisticsClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<StatisticsSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var payload = await _httpClient.GetFromJsonAsync<StatisticsSnapshotDto>("statistics", cancellationToken)
            .ConfigureAwait(false);
        return payload ?? new StatisticsSnapshotDto();
    }

    public async Task<StatisticsSnapshotDto> ActivateSkillAsync(string skillId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillId);
        var response = await _httpClient.PostAsJsonAsync("statistics/skills/activate",
                new ActivateSkillRequest(skillId), cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StatisticsSnapshotDto>(cancellationToken: cancellationToken)
            .ConfigureAwait(false) ?? new StatisticsSnapshotDto();
    }

    private sealed record ActivateSkillRequest(
        [property: JsonPropertyName("skillId")]
        string SkillId);
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Deserialized from HTTP payloads")]
internal sealed class StatisticsSnapshotDto
{
    [JsonPropertyName("activeSkillId")] public string ActiveSkillId { get; set; } = string.Empty;

    [JsonPropertyName("totalSkillCurrency")]
    public double TotalSkillCurrency { get; set; }

    [JsonPropertyName("namespaces")]
    public IReadOnlyList<StatisticNamespaceDto> Namespaces { get; set; } =
        Array.Empty<StatisticNamespaceDto>();
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Deserialized from HTTP payloads")]
internal sealed class StatisticNamespaceDto
{
    [JsonPropertyName("namespaceId")] public string NamespaceId { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("categories")]
    public IReadOnlyList<StatisticCategoryDto> Categories { get; set; } = Array.Empty<StatisticCategoryDto>();
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Deserialized from HTTP payloads")]
internal sealed class StatisticCategoryDto
{
    [JsonPropertyName("categoryId")] public string CategoryId { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("entries")]
    public IReadOnlyList<StatisticEntryDto> Entries { get; set; } = Array.Empty<StatisticEntryDto>();
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Deserialized from HTTP payloads")]
internal sealed class StatisticEntryDto
{
    [JsonPropertyName("entryId")] public string EntryId { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("kind")] public string Kind { get; set; } = string.Empty;
    [JsonPropertyName("value")] public StatisticValueDto Value { get; set; } = new();
    [JsonPropertyName("accentColor")] public string AccentColor { get; set; } = "#72f5ff";
    [JsonPropertyName("defaultAnimation")] public string DefaultAnimation { get; set; } = "idle";
    [JsonPropertyName("isActive")] public bool IsActive { get; set; }
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Deserialized from HTTP payloads")]
internal sealed class StatisticValueDto
{
    [JsonPropertyName("level")] public int Level { get; set; }
    [JsonPropertyName("experience")] public double Experience { get; set; }
    [JsonPropertyName("bankedCurrency")] public double BankedCurrency { get; set; }

    [JsonPropertyName("currencyPerSecond")]
    public double CurrencyPerSecond { get; set; }
}

internal static class StatisticEntryKindsDto
{
    public const string Skill = "skill";
}
