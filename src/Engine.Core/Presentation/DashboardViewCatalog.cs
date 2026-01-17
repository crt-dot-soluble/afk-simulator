using System;
using System.Collections.Generic;
using System.Linq;
using Engine.Core.Contracts;

namespace Engine.Core.Presentation;

public sealed class DashboardViewCatalog
{
    private readonly IReadOnlyCollection<DashboardViewDescriptor> _views;

    public DashboardViewCatalog(IEnumerable<IDashboardViewProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _views = providers
            .SelectMany(provider => provider.DescribeViews())
            .OrderBy(view => view.Zone)
            .ThenBy(view => view.Order)
            .ThenBy(view => view.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyCollection<DashboardViewDescriptor> List() => _views;
}
