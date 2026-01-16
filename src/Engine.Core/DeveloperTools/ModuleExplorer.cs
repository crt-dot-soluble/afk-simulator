using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Linq;
using System.Text.Json;
using Engine.Core.Contracts;

namespace Engine.Core.DeveloperTools;

public interface IModuleExplorer
{
    IReadOnlyCollection<ModuleSurface> ListSurfaces();

    ModuleSurface? Find(string moduleName);

    ValueTask<object?> ExecuteAsync(string moduleName, string commandName,
        IReadOnlyDictionary<string, object?>? parameters, CancellationToken cancellationToken);

    ValueTask<ModulePropertyUpdateResult> UpdatePropertyAsync(string moduleName, string propertyName, object? value,
        CancellationToken cancellationToken);

    IReadOnlyCollection<DeveloperModuleDescriptor> DescribeSurfaces();

    DeveloperModuleDescriptor DescribeSurface(string moduleName);

    IReadOnlyCollection<DeveloperAutocompleteEntry> BuildAutocomplete();
}

public sealed class ModuleExplorer : IModuleExplorer
{
    private readonly ConcurrentDictionary<string, ModuleSurface> _surfaces = new(StringComparer.OrdinalIgnoreCase);
    private readonly IEnumerable<IModuleContract> _modules;

    public ModuleExplorer(IEnumerable<IModuleContract> modules)
    {
        _modules = modules;
        RefreshSurfaces();
    }

    public IReadOnlyCollection<ModuleSurface> ListSurfaces() =>
        _surfaces.Values.OrderBy(surface => surface.Name).ToArray();

    public ModuleSurface? Find(string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        _surfaces.TryGetValue(moduleName, out var surface);
        return surface;
    }

    public async ValueTask<object?> ExecuteAsync(string moduleName, string commandName,
        IReadOnlyDictionary<string, object?>? parameters, CancellationToken cancellationToken)
    {
        var surface = RequireSurface(moduleName);
        var command =
            surface.Commands.SingleOrDefault(c =>
                string.Equals(c.Name, commandName, StringComparison.OrdinalIgnoreCase));
        if (command is null)
        {
            throw new InvalidOperationException($"Command '{commandName}' not found on module '{moduleName}'.");
        }

        var instance = surface.Instance;
        var arguments = BindArguments(command.Method, parameters);
        var result = command.Method.Invoke(instance, arguments);
        if (result is ValueTask valueTask)
        {
            await valueTask.ConfigureAwait(false);
            return null;
        }

        if (result is ValueTask<object?> asyncResult)
        {
            return await asyncResult.ConfigureAwait(false);
        }

        return result;
    }

    public async ValueTask<ModulePropertyUpdateResult> UpdatePropertyAsync(string moduleName, string propertyName,
        object? value, CancellationToken cancellationToken)
    {
        var surface = RequireSurface(moduleName);
        var property = surface.Properties.SingleOrDefault(p =>
            string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));
        if (property is null)
        {
            throw new InvalidOperationException(
                $"Inspectable property '{propertyName}' not found on module '{moduleName}'.");
        }

        var converted = ConvertValue(value, property.Property.PropertyType);
        property.Property.SetValue(surface.Instance, converted);

        if (property.Property.GetValue(surface.Instance) is ValueTask valueTask)
        {
            await valueTask.ConfigureAwait(false);
        }

        return new ModulePropertyUpdateResult(property.Name, converted);
    }

    public IReadOnlyCollection<DeveloperModuleDescriptor> DescribeSurfaces()
    {
        return _surfaces.Values
            .OrderBy(surface => surface.Name)
            .Select(ToDescriptor)
            .ToArray();
    }

    public DeveloperModuleDescriptor DescribeSurface(string moduleName)
    {
        var surface = RequireSurface(moduleName);
        return ToDescriptor(surface);
    }

    public IReadOnlyCollection<DeveloperAutocompleteEntry> BuildAutocomplete()
    {
        var entries = new List<DeveloperAutocompleteEntry>();
        foreach (var surface in _surfaces.Values)
        {
            entries.Add(new DeveloperAutocompleteEntry(surface.Name, "module", surface.Name));
            foreach (var property in surface.Properties)
            {
                entries.Add(new DeveloperAutocompleteEntry($"{surface.Name}.{property.Name}", "property",
                    surface.Name));
            }

            foreach (var command in surface.Commands)
            {
                entries.Add(new DeveloperAutocompleteEntry($"{surface.Name}/{command.Name}", "command", surface.Name));
            }
        }

        return entries.OrderBy(entry => entry.Token).ToArray();
    }

    private ModuleSurface RequireSurface(string moduleName)
    {
        if (_surfaces.TryGetValue(moduleName, out var surface))
        {
            return surface;
        }

        RefreshSurfaces();
        if (_surfaces.TryGetValue(moduleName, out surface))
        {
            return surface;
        }

        throw new InvalidOperationException($"Module '{moduleName}' does not expose a developer surface.");
    }

    private static object?[] BindArguments(MethodInfo method, IReadOnlyDictionary<string, object?>? parameters)
    {
        var parameterInfos = method.GetParameters();
        if (parameterInfos.Length == 0)
        {
            return Array.Empty<object?>();
        }

        var result = new object?[parameterInfos.Length];
        for (var i = 0; i < parameterInfos.Length; i++)
        {
            var parameterInfo = parameterInfos[i];
            if (parameters == null || !parameters.TryGetValue(parameterInfo.Name!, out var supplied))
            {
                if (parameterInfo.HasDefaultValue)
                {
                    result[i] = parameterInfo.DefaultValue;
                    continue;
                }

                throw new InvalidOperationException($"Missing parameter '{parameterInfo.Name}'.");
            }

            result[i] = ConvertValue(supplied, parameterInfo.ParameterType);
        }

        return result;
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    private static object? ConvertValue(object? input, Type targetType)
    {
        if (input is null)
        {
            if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null)
            {
                throw new InvalidOperationException($"Cannot assign null to non-nullable type '{targetType.Name}'.");
            }

            return null;
        }

        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (input is System.Text.Json.JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Null)
            {
                return null;
            }

            return jsonElement.Deserialize(underlying, JsonOptions);
        }

        if (underlying.IsInstanceOfType(input))
        {
            return input;
        }

        return Convert.ChangeType(input, underlying, CultureInfo.InvariantCulture);
    }

    private void RefreshSurfaces()
    {
        foreach (var module in _modules)
        {
            var surface = BuildSurface(module);
            if (surface != null)
            {
                _surfaces[surface.Name] = surface;
            }
        }
    }

    private static ModuleSurface? BuildSurface(IModuleContract module)
    {
        var type = module.GetType();
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(prop => prop.GetCustomAttribute<ModuleInspectableAttribute>() is not null && prop.CanRead &&
                           prop.CanWrite)
            .Select(prop => new ModulePropertyDescriptor(
                prop.Name,
                prop.PropertyType,
                prop,
                prop.GetCustomAttribute<ModuleInspectableAttribute>()!))
            .ToArray();

        var commands = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(method => (Method: method, Attribute: method.GetCustomAttribute<ModuleCommandAttribute>()))
            .Where(tuple => tuple.Attribute is not null)
            .Select(tuple => new ModuleCommandDescriptor(tuple.Attribute!.Name, tuple.Attribute.Description,
                tuple.Attribute.IsQuery, tuple.Method))
            .ToArray();

        var descriptor = module is IModuleDescriptorSource descriptorSource
            ? descriptorSource.Describe()
            : new ModuleDescriptor(module.Name, module.Version, Array.Empty<string>(), Array.Empty<string>(),
                Array.Empty<string>());

        return new ModuleSurface(module.Name, module.Version, descriptor, properties, commands, module);
    }

    private static DeveloperModuleDescriptor ToDescriptor(ModuleSurface surface)
    {
        var properties = surface.Properties
            .Select(descriptor => new DeveloperInspectableProperty(
                descriptor.Name,
                descriptor.Attribute.Label,
                descriptor.Attribute.Description,
                descriptor.Attribute.Group,
                descriptor.Property.PropertyType.Name,
                descriptor.Property.GetValue(surface.Instance)))
            .ToArray();

        var commands = surface.Commands
            .Select(descriptor => new DeveloperCommandDescriptor(
                descriptor.Name,
                descriptor.Description,
                descriptor.IsQuery,
                descriptor.Method.GetParameters()
                    .Select(parameter => new DeveloperCommandParameter(
                        parameter.Name ?? "arg",
                        parameter.ParameterType.Name,
                        parameter.HasDefaultValue,
                        parameter.HasDefaultValue ? parameter.DefaultValue : null))
                    .ToArray()))
            .ToArray();

        var capabilities = surface.Descriptor.Capabilities ?? Array.Empty<string>();
        var metadata = surface.Descriptor.Metadata;

        return new DeveloperModuleDescriptor(
            surface.Name,
            surface.Version,
            surface.Descriptor.Description,
            capabilities,
            metadata,
            properties,
            commands);
    }
}

public sealed record ModuleSurface(
    string Name,
    string Version,
    ModuleDescriptor Descriptor,
    IReadOnlyCollection<ModulePropertyDescriptor> Properties,
    IReadOnlyCollection<ModuleCommandDescriptor> Commands,
    IModuleContract Instance);

public sealed record ModulePropertyDescriptor(
    string Name,
    Type Type,
    PropertyInfo Property,
    ModuleInspectableAttribute Attribute);

public sealed record ModuleCommandDescriptor(
    string Name,
    string? Description,
    bool IsQuery,
    MethodInfo Method);

public sealed record ModulePropertyUpdateResult(string Name, object? Value);
