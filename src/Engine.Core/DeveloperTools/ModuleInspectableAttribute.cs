using System;

namespace Engine.Core.DeveloperTools;

/// <summary>
/// Marks a module property as inspectable/editable through the developer console.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ModuleInspectableAttribute : Attribute
{
    public ModuleInspectableAttribute(string label)
    {
        Label = label;
    }

    public string Label { get; }

    public string? Description { get; set; }

    public string? Group { get; set; }
}
