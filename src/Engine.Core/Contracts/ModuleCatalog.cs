using System.Collections.Generic;

namespace Engine.Core.Contracts;

public sealed class ModuleCatalog
{
    private readonly List<ModuleDescriptor> _descriptors = new();
    private readonly object _gate = new();

    public void Register(ModuleDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        lock (_gate)
        {
            _descriptors.RemoveAll(d => d.Name == descriptor.Name);
            _descriptors.Add(descriptor);
        }
    }

    public IReadOnlyCollection<ModuleDescriptor> List()
    {
        lock (_gate)
        {
            return _descriptors.ToArray();
        }
    }
}
