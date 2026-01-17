using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Engine.Client.Components.ModuleViews;

[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Exposed as event payloads for Mission Control Razor components.")]
public sealed record ModuleViewSelection(string DocumentId, string BlockId, string ItemId);

[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Exposed as event payloads for Mission Control Razor components.")]
public sealed record ModuleViewActionRequest(
    string DocumentId,
    string ActionId,
    string Command,
    IReadOnlyDictionary<string, string?>? Parameters = null);
