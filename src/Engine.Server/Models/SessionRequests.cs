namespace Engine.Server.Models;

internal sealed record SessionCreateRequest(string Name);

internal sealed record SessionJoinRequest(string PlayerId);
