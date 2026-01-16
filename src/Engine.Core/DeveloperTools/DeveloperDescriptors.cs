using System.Collections.Generic;

namespace Engine.Core.DeveloperTools;

public sealed record DeveloperModuleDescriptor(
    string Name,
    string Version,
    string? Description,
    IReadOnlyCollection<string> Capabilities,
    IReadOnlyDictionary<string, string>? Metadata,
    IReadOnlyCollection<DeveloperInspectableProperty> Properties,
    IReadOnlyCollection<DeveloperCommandDescriptor> Commands);

public sealed record DeveloperInspectableProperty(
    string Name,
    string Label,
    string? Description,
    string? Group,
    string Type,
    object? Value);

public sealed record DeveloperCommandDescriptor(
    string Name,
    string? Description,
    bool IsQuery,
    IReadOnlyCollection<DeveloperCommandParameter> Parameters);

public sealed record DeveloperCommandParameter(
    string Name,
    string Type,
    bool HasDefaultValue,
    object? DefaultValue);

public sealed record DeveloperAutocompleteEntry(string Token, string Kind, string Source);

public sealed record DeveloperCommandResult(object? Result);
