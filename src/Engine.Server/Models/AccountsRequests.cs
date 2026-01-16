namespace Engine.Server.Models;

public sealed record CreateUserRequest(string DisplayName);

public sealed record CreateAccountRequest(string Label);

public sealed record CreateProfileRequest(string? Name, string? SpriteAssetId);
