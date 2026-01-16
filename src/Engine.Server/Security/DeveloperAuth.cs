using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Engine.Core.DeveloperTools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Engine.Server.Security;

internal sealed class DeveloperAuthOptions
{
    public string ApiKey { get; set; } = string.Empty;
}

internal sealed class DeveloperAuthEndpointFilter : IEndpointFilter, IEndpointMetadataProvider
{
    private readonly DeveloperAuthOptions _options;
    private readonly ILogger<DeveloperAuthEndpointFilter> _logger;

    private static readonly Action<ILogger, string, Exception?> MissingHeaderLog =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(5001, nameof(MissingHeaderLog)),
            "Developer endpoint blocked: missing header at {Url}");

    private static readonly Action<ILogger, string, Exception?> InvalidKeyLog =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(5002, nameof(InvalidKeyLog)),
            "Developer endpoint blocked: invalid key at {Url}");

    public DeveloperAuthEndpointFilter(IConfiguration configuration, ILogger<DeveloperAuthEndpointFilter> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _options = configuration.GetSection("Developer").Get<DeveloperAuthOptions>() ?? new DeveloperAuthOptions();
        _logger = logger;
    }

    public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(builder);
        builder.Metadata.Add(new DeveloperAuthMetadata());
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return await next(context).ConfigureAwait(false);
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(DeveloperAuthDefaults.HeaderName,
                out StringValues headerValue))
        {
            MissingHeaderLog(_logger, context.HttpContext.Request.GetDisplayUrl(), null);
            return Results.Unauthorized();
        }

        var suppliedKey = headerValue.ToString();
        if (string.IsNullOrWhiteSpace(suppliedKey) || !IsMatch(suppliedKey))
        {
            InvalidKeyLog(_logger, context.HttpContext.Request.GetDisplayUrl(), null);
            return Results.Unauthorized();
        }

        return await next(context).ConfigureAwait(false);
    }

    private bool IsMatch(string supplied)
    {
        ArgumentException.ThrowIfNullOrEmpty(supplied);
        var expectedBytes = Encoding.UTF8.GetBytes(_options.ApiKey);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        return expectedBytes.Length == suppliedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }

    private sealed record DeveloperAuthMetadata;
}
