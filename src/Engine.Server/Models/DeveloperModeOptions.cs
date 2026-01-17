using System.Diagnostics.CodeAnalysis;

namespace Engine.Server.Models;

[SuppressMessage("Performance", "CA1812", Justification = "Activated via options binding")]
internal sealed class DeveloperModeOptions
{
    public bool AutoLogin { get; set; }
    public string Email { get; set; } = "developer@afk.local";
    public string Password { get; set; } = "LocalDev!123";
    public string DisplayName { get; set; } = "Developer";
    public string PrimaryUniverseName { get; set; } = "Developer Sandbox";
    public string PrimaryCharacterName { get; set; } = "Dev Vanguard";
    public long BaseCurrency { get; set; } = 250_000_000;
    public long PremiumCurrency { get; set; } = 1_000_000;
}
