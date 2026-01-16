namespace Engine.Server.Models;

public sealed record SessionCreateRequest(string Name);

public sealed record SessionJoinRequest(string PlayerId);
