using System.Collections.Generic;

namespace Engine.Client.Components.ModuleViews;

public sealed record ModuleViewSelection(string DocumentId, string BlockId, string ItemId);

public sealed record ModuleViewActionRequest(
    string DocumentId,
    string ActionId,
    string Command,
    IReadOnlyDictionary<string, string?>? Parameters = null);
