namespace Engine.Server.Models;

internal sealed record RuntimeConfigResponse(
    bool RequireLogin,
    bool DeveloperAutoLoginEnabled,
    DeveloperAutoLoginDescriptor? DeveloperAutoLogin);

internal sealed record DeveloperAutoLoginDescriptor(string Email, string Password, string DisplayName);
