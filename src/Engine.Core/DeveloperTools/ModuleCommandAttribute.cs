using System;

namespace Engine.Core.DeveloperTools;

/// <summary>
/// Declares a developer command or query exposed by a module.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ModuleCommandAttribute : Attribute
{
    public ModuleCommandAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public string Name { get; }

    public string? Description { get; set; }

    public bool IsQuery { get; set; }
}
