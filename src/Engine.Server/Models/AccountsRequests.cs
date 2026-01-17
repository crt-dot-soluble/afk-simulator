using System.Diagnostics.CodeAnalysis;

namespace Engine.Server.Models;

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by ASP.NET Core model binding.")]
internal sealed record RegisterUserRequest(string Email, string Password, string DisplayName);

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by ASP.NET Core model binding.")]
internal sealed record AuthenticateUserRequest(string Email, string Password);

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by ASP.NET Core model binding.")]
internal sealed record CreateUniverseRequest(string Name);

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by ASP.NET Core model binding.")]
internal sealed record CreateCharacterRequest(string? Name, string? SpriteAssetId);

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by ASP.NET Core model binding.")]
internal sealed record WalletDepositRequest(long BaseCurrency, long PremiumCurrency, string? UniverseId,
    string? CharacterId);
