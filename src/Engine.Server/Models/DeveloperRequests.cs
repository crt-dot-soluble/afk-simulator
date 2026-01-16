using System.Collections.Generic;
using System.Text.Json;

namespace Engine.Server.Models;

internal sealed record DeveloperPropertyUpdateRequest(JsonElement Value);

internal sealed record DeveloperCommandRequest(IReadOnlyDictionary<string, JsonElement>? Parameters);

internal sealed record DeveloperProfileUpsertRequest(string Id, IReadOnlyDictionary<string, string>? State);
