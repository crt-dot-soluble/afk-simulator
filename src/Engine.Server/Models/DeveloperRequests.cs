using System.Collections.Generic;
using System.Text.Json;

namespace Engine.Server.Models;

public sealed record DeveloperPropertyUpdateRequest(JsonElement Value);

public sealed record DeveloperCommandRequest(IReadOnlyDictionary<string, JsonElement>? Parameters);

public sealed record DeveloperProfileUpsertRequest(string Id, IReadOnlyDictionary<string, string> State);
