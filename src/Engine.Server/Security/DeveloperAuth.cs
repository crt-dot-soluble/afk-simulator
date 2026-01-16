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

namespace Engine.Server.Security;

public sealed class DeveloperAuthOptions
{
    public string ApiKey { get; set; } = string.Empty;
}

public sealed class DeveloperAuthEndpointFilter : IEndpointFilter, IEndpointMetadataProvider
{
    private readonly DeveloperAuthOptions _options;
    private readonly ILogger<DeveloperAuthEndpointFilter> _logger;

    public DeveloperAuthEndpointFilter(IConfiguration configuration, ILogger<DeveloperAuthEndpointFilter> logger)
    {
        _options = configuration.GetSection("Developer").Get<DeveloperAuthOptions>() ?? new DeveloperAuthOptions();
        _logger = logger;
    }

    public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        builder.Metadata.Add(new DeveloperAuthMetadata());
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return await next(context).ConfigureAwait(false);
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(DeveloperAuthDefaults.HeaderName, out var headerValue))
        {
            _logger.LogWarning("Developer endpoint blocked: missing header at {Url}", context.HttpContext.Request.GetDisplayUrl());
            return Results.Unauthorized();
        }

        if (!IsMatch(headerValue))
        {
            _logger.LogWarning("Developer endpoint blocked: invalid key at {Url}", context.HttpContext.Request.GetDisplayUrl());
            return Results.Unauthorized();
        }

        return await next(context).ConfigureAwait(false);
    }

    private bool IsMatch(string supplied)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(_options.ApiKey);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        if (expectedBytes.Length != suppliedBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }

    private sealed record DeveloperAuthMetadata;
}
