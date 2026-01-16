namespace Engine.Server.Models;

internal sealed record CreateUserRequest(string DisplayName);

internal sealed record CreateAccountRequest(string Label);

internal sealed record CreateProfileRequest(string? Name, string? SpriteAssetId);
