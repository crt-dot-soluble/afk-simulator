using System;
using System.Collections.Generic;
using System.Linq;
using Engine.Core.Contracts;

namespace Engine.Core.Presentation;

/// <summary>
/// Aggregates module-defined dashboard documents so the client can render panels generically.
/// </summary>
public sealed class ModuleViewCatalog
{
    private readonly IReadOnlyList<IModuleViewProvider> _providers;

    public ModuleViewCatalog(IEnumerable<IModuleViewProvider> providers)
    {
        _providers = (providers ?? throw new ArgumentNullException(nameof(providers))).ToArray();
    }

    public IReadOnlyCollection<ModuleViewDocument> List(ModuleViewContext? context = null)
    {
        if (!_providers.Any())
        {
            return Array.Empty<ModuleViewDocument>();
        }

        var scope = context ?? ModuleViewContext.Empty;
        var documents = new List<ModuleViewDocument>();
        foreach (var provider in _providers)
        {
            var views = provider.DescribeModuleViews(scope);
            if (views is null || views.Count == 0)
            {
                continue;
            }

            documents.AddRange(views.Where(static view => view is not null));
        }

        if (documents.Count == 0)
        {
            return Array.Empty<ModuleViewDocument>();
        }

        return documents
            .OrderBy(document => document.Descriptor.Zone, StringComparer.OrdinalIgnoreCase)
            .ThenBy(document => document.Descriptor.Order)
            .ThenBy(document => document.Descriptor.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
