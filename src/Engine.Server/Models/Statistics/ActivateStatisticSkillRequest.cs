using System.Diagnostics.CodeAnalysis;

namespace Engine.Server.Models.Statistics;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instantiated via ASP.NET Core model binding")]
internal sealed class ActivateStatisticSkillRequest
{
    public string SkillId { get; init; } = string.Empty;
}
