namespace Engine.Core.Contracts;

/// <summary>
/// Provides the catalog with a deterministic descriptor for a module without requiring manual registration.
/// </summary>
public interface IModuleDescriptorSource
{
    ModuleDescriptor Describe();
}
