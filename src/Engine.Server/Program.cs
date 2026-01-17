using System.Collections.Generic;
using System.IO;
using System.Linq;
using Engine.Core;
using Engine.Core.Accounts;
using Engine.Core.Contracts;
using Engine.Core.DeveloperTools;
using Engine.Core.Multiplayer;
using Engine.Core.Presentation;
using Engine.Core.Rendering;
using Engine.Core.Rendering.Sprites;
using Engine.Core.Statistics;
using Engine.Server.Accounts;
using Engine.Server.Developer;
using Engine.Server.Hubs;
using Engine.Server.Logging;
using Engine.Server.Models;
using Engine.Server.Models.Sprites;
using Engine.Server.Models.Statistics;
using Engine.Server.Options;
using Engine.Server.Persistence;
using Engine.Server.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIncrementalEngine();
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("Database"));
builder.Services.AddDbContextFactory<IncrementalEngineDbContext>((serviceProvider, optionsBuilder) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
    switch (options.Provider)
    {
        case DatabaseProvider.PostgreSql:
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                throw new InvalidOperationException(
                    "Database:ConnectionString is required when using the PostgreSql provider.");
            }

            optionsBuilder.UseNpgsql(options.ConnectionString, sql => sql.EnableRetryOnFailure());
            break;
        case DatabaseProvider.Supabase:
            var supabaseConnection = options.Supabase?.ConnectionString;
            if (string.IsNullOrWhiteSpace(supabaseConnection))
            {
                supabaseConnection = options.ConnectionString;
            }

            if (string.IsNullOrWhiteSpace(supabaseConnection))
            {
                throw new InvalidOperationException(
                    "Database:Supabase:ConnectionString (or Database:ConnectionString) is required when using the Supabase provider.");
            }

            optionsBuilder.UseNpgsql(supabaseConnection, sql => sql.EnableRetryOnFailure());
            break;
        default:
            var dataDirectory = string.IsNullOrWhiteSpace(options.DataDirectory)
                ? Path.Combine(environment.ContentRootPath, "App_Data")
                : options.DataDirectory!;
            Directory.CreateDirectory(dataDirectory);
            var dbPath = Path.Combine(dataDirectory, options.DatabaseName);
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
            break;
    }
});
builder.Services.AddHostedService<DatabaseInitializerHostedService>();
builder.Services.AddSingleton<IModuleStateStore, DatabaseModuleStateStore>();
builder.Services.AddSingleton<IAccountService, PersistentAccountService>();
var cachingSection = builder.Configuration.GetSection("Caching");
builder.Services.Configure<CachingOptions>(cachingSection);
var caching = cachingSection.Get<CachingOptions>() ?? new CachingOptions();
if (caching.Provider == CacheProvider.Redis && !string.IsNullOrWhiteSpace(caching.RedisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options => options.Configuration = caching.RedisConnectionString);
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddSignalR();
builder.Services.AddResponseCompression();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("default", policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true);
    });
});
builder.Services.Configure<DeveloperModeOptions>(builder.Configuration.GetSection("DeveloperMode"));
builder.Services.AddSingleton<DeveloperModeBootstrapper>();

var app = builder.Build();

app.UseResponseCompression();
app.UseCors("default");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", async (IEnumerable<IModuleContract> modules, CancellationToken token) =>
{
    var responses = new List<object>();
    foreach (var module in modules)
    {
        var health = await module.CheckHealthAsync(token).ConfigureAwait(false);
        responses.Add(new { module.Name, module.Version, Status = health.Status.ToString(), Details = health.Details });
    }

    return Results.Ok(responses);
});

app.MapGet("/modules", (ModuleCatalog catalog) => Results.Ok(catalog.List()));
app.MapGet("/dashboard/views", (DashboardViewCatalog catalog) => Results.Ok(catalog.List()));
app.MapGet("/dashboard/view-documents", (HttpRequest request, ModuleViewCatalog catalog) =>
{
    string? userId = null;
    var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var (key, value) in request.Query)
    {
        if (string.Equals(key, "userId", StringComparison.OrdinalIgnoreCase))
        {
            userId = value;
            continue;
        }

        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
        {
            parameters[key] = value!;
        }
    }

    var context = new ModuleViewContext(string.IsNullOrWhiteSpace(userId) ? null : userId,
        parameters.Count == 0 ? null : parameters);
    return Results.Ok(catalog.List(context));
});
app.MapGet("/runtime/config", (IOptions<DeveloperModeOptions> options) =>
{
    var opt = options.Value;
    var descriptor = opt.AutoLogin
        ? new DeveloperAutoLoginDescriptor(opt.Email, opt.Password, opt.DisplayName)
        : null;
    return Results.Ok(new RuntimeConfigResponse(true, opt.AutoLogin, descriptor));
});

var developer = app.MapGroup("/developer");
developer.AddEndpointFilter<DeveloperAuthEndpointFilter>();
developer.MapGet("/modules", (IModuleExplorer explorer) => Results.Ok(explorer.DescribeSurfaces()));
developer.MapGet("/modules/{moduleName}",
    (string moduleName, IModuleExplorer explorer) => Results.Ok(explorer.DescribeSurface(moduleName)));
developer.MapPost("/modules/{moduleName}/properties/{propertyName}", async (string moduleName, string propertyName,
    DeveloperPropertyUpdateRequest request, IModuleExplorer explorer, CancellationToken token) =>
{
    var result = await explorer.UpdatePropertyAsync(moduleName, propertyName, request.Value, token)
        .ConfigureAwait(false);
    return Results.Ok(result);
});
developer.MapPost("/modules/{moduleName}/commands/{commandName}", async (string moduleName, string commandName,
    DeveloperCommandRequest request, IModuleExplorer explorer, CancellationToken token) =>
{
    IReadOnlyDictionary<string, object?>? parameters = null;
    if (request.Parameters is { Count: > 0 })
    {
        parameters = request.Parameters.ToDictionary(static kvp => kvp.Key, static kvp => (object?)kvp.Value);
    }

    var response = await explorer.ExecuteAsync(moduleName, commandName, parameters, token).ConfigureAwait(false);
    return Results.Ok(new DeveloperCommandResult(response));
});
developer.MapGet("/autocomplete", (IModuleExplorer explorer) => Results.Ok(explorer.BuildAutocomplete()));
developer.MapGet("/profiles", (DeveloperProfileStore profiles) => Results.Ok(profiles.List()));
developer.MapPost("/profiles",
    (DeveloperProfileUpsertRequest request, DeveloperProfileStore profiles) =>
    {
        var payload = request.State is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(request.State, StringComparer.OrdinalIgnoreCase);
        return Results.Ok(profiles.Upsert(request.Id, payload));
    });

var accountsApi = app.MapGroup("/accounts");
accountsApi.MapPost("/users", async (RegisterUserRequest request, IAccountService accounts, CancellationToken token) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) ||
        string.IsNullOrWhiteSpace(request.DisplayName))
    {
        return Results.BadRequest(new AccountError(AccountErrorCodes.ValidationFailed,
            "Email, password, and display name are required."));
    }

    try
    {
        var user = await accounts.RegisterUserAsync(request.Email, request.Password, request.DisplayName, token)
            .ConfigureAwait(false);
        return Results.Created($"/accounts/users/{user.Id}", user);
    }
    catch (AccountOperationException ex)
    {
        return Results.BadRequest(new AccountError(ex.Code, ex.Message));
    }
});

accountsApi.MapPost("/authenticate",
    async (AuthenticateUserRequest request, IAccountService accounts, CancellationToken token) =>
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new AccountError(AccountErrorCodes.ValidationFailed,
                "Email and password are required."));
        }

        var user = await accounts.AuthenticateAsync(request.Email, request.Password, token)
            .ConfigureAwait(false);
        return user is null ? Results.Unauthorized() : Results.Ok(user);
    });

accountsApi.MapGet("/users/{userId}", async (string userId, IAccountService accounts, CancellationToken token) =>
{
    var user = await accounts.GetUserAsync(userId, token).ConfigureAwait(false);
    return user is null
        ? Results.NotFound(new AccountError(AccountErrorCodes.UserNotFound, $"User '{userId}' was not found."))
        : Results.Ok(user);
});

accountsApi.MapGet("/users/{userId}/universes",
    async (string userId, IAccountService accounts, CancellationToken token) =>
    {
        try
        {
            var snapshot = await accounts.ListUniversesAsync(userId, token).ConfigureAwait(false);
            return Results.Ok(snapshot);
        }
        catch (AccountOperationException ex) when (ex.Code == AccountErrorCodes.UserNotFound)
        {
            return Results.NotFound(new AccountError(ex.Code, ex.Message));
        }
    });

accountsApi.MapPost("/users/{userId}/universes",
    async (string userId, CreateUniverseRequest request, IAccountService accounts, CancellationToken token) =>
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new AccountError(AccountErrorCodes.ValidationFailed, "Name is required."));
        }

        try
        {
            var universe = await accounts.CreateUniverseAsync(userId, request.Name, token).ConfigureAwait(false);
            return Results.Created($"/accounts/universes/{universe.Id}", universe);
        }
        catch (AccountOperationException ex) when (ex.Code is AccountErrorCodes.UserNotFound)
        {
            return Results.NotFound(new AccountError(ex.Code, ex.Message));
        }
        catch (AccountOperationException ex) when (ex.Code is AccountErrorCodes.UniverseLimit)
        {
            return Results.Conflict(new AccountError(ex.Code, ex.Message));
        }
    });

accountsApi.MapGet("/universes/{universeId}/characters",
    async (string universeId, IAccountService accounts, CancellationToken token) =>
    {
        var universe = await accounts.GetUniverseAsync(universeId, token).ConfigureAwait(false);
        if (universe is null)
        {
            return Results.NotFound(new AccountError(AccountErrorCodes.UniverseNotFound,
                $"Universe '{universeId}' was not found."));
        }

        var characters = await accounts.ListCharactersAsync(universeId, token).ConfigureAwait(false);
        return Results.Ok(characters);
    });

accountsApi.MapPost("/universes/{universeId}/characters",
    async (string universeId, CreateCharacterRequest request, IAccountService accounts, CancellationToken token) =>
    {
        try
        {
            var character = await accounts.CreateCharacterAsync(universeId, request.Name, request.SpriteAssetId, token)
                .ConfigureAwait(false);
            return Results.Created($"/accounts/characters/{character.Id}", character);
        }
        catch (AccountOperationException ex) when (ex.Code == AccountErrorCodes.UniverseNotFound)
        {
            return Results.NotFound(new AccountError(ex.Code, ex.Message));
        }
        catch (AccountOperationException ex) when (ex.Code == AccountErrorCodes.CharacterLimit)
        {
            return Results.Conflict(new AccountError(ex.Code, ex.Message));
        }
    });

accountsApi.MapGet("/users/{userId}/wallets",
    async (string userId, IAccountService accounts, CancellationToken token) =>
    {
        try
        {
            var snapshot = await accounts.GetWalletSnapshotAsync(userId, token).ConfigureAwait(false);
            return Results.Ok(snapshot);
        }
        catch (AccountOperationException ex) when (ex.Code == AccountErrorCodes.UserNotFound)
        {
            return Results.NotFound(new AccountError(ex.Code, ex.Message));
        }
    });

accountsApi.MapPost("/users/{userId}/wallets/deposit",
    async (string userId, WalletDepositRequest request, IAccountService accounts, CancellationToken token) =>
    {
        if (request.BaseCurrency < 0 || request.PremiumCurrency < 0)
        {
            return Results.BadRequest(new AccountError(AccountErrorCodes.ValidationFailed,
                "Currency grants must be non-negative."));
        }

        try
        {
            var snapshot = await accounts
                .DepositCurrencyAsync(userId, request.UniverseId, request.CharacterId, request.BaseCurrency,
                    request.PremiumCurrency, token)
                .ConfigureAwait(false);
            return Results.Ok(snapshot);
        }
        catch (AccountOperationException ex) when (ex.Code is AccountErrorCodes.UserNotFound
                                                       or AccountErrorCodes.UniverseNotFound
                                                       or AccountErrorCodes.CharacterNotFound
                                                       or AccountErrorCodes.WalletUnavailable)
        {
            return ex.Code switch
            {
                AccountErrorCodes.UserNotFound => Results.NotFound(new AccountError(ex.Code, ex.Message)),
                AccountErrorCodes.UniverseNotFound => Results.NotFound(new AccountError(ex.Code, ex.Message)),
                AccountErrorCodes.CharacterNotFound => Results.NotFound(new AccountError(ex.Code, ex.Message)),
                AccountErrorCodes.WalletUnavailable => Results.Conflict(new AccountError(ex.Code, ex.Message)),
                _ => Results.BadRequest(new AccountError(ex.Code, ex.Message))
            };
        }
    });

var statisticsApi = app.MapGroup("/statistics");
statisticsApi.MapGet("/", (IStatisticsService statistics) =>
{
    var snapshot = statistics.SnapshotStatistics();
    return Results.Ok(StatisticsResponseFactory.CreateSnapshot(snapshot));
});

statisticsApi.MapPost("/skills/activate", (ActivateStatisticSkillRequest request, IStatisticsService statistics) =>
{
    if (string.IsNullOrWhiteSpace(request.SkillId))
    {
        return Results.BadRequest(new { error = "SkillId is required." });
    }

    try
    {
        statistics.ActivateSkill(request.SkillId);
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }

    var snapshot = statistics.SnapshotStatistics();
    return Results.Ok(StatisticsResponseFactory.CreateSnapshot(snapshot));
});

var assetsApi = app.MapGroup("/assets");
assetsApi.MapGet("/sprites", (SpriteLibrary sprites) =>
{
    var payload = sprites.List().Select(SpriteResponseFactory.Create);
    return Results.Ok(payload);
});

assetsApi.MapGet("/sprites/{*spriteId}", (string spriteId, SpriteLibrary sprites) =>
{
    return sprites.TryGet(spriteId, out var definition)
        ? Results.Ok(SpriteResponseFactory.Create(definition))
        : Results.NotFound();
});

app.MapPost("/telemetry/errors", (ClientErrorReport report, HttpContext context, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("ClientError");
    var agent = context.Request.Headers.UserAgent.ToString();
    var source = string.IsNullOrWhiteSpace(report.Source) ? "unknown" : report.Source;
    var message = string.IsNullOrWhiteSpace(report.Message) ? "Client error" : report.Message;
    var stack = string.IsNullOrWhiteSpace(report.StackTrace) ? "n/a" : report.StackTrace;
    logger.LogClientError(source, message, stack, agent);
    return Results.Accepted();
});

app.MapGet("/graphics/settings", (RenderSettingsStore store) => Results.Ok(store.Current));

app.MapPost("/graphics/settings", (RenderSettings settings, RenderSettingsStore store) =>
{
    var updated = store.Update(settings);
    return Results.Ok(updated);
});

app.MapGet("/leaderboard", (ILeaderboardService service) => Results.Ok(service.Snapshot()));

app.MapPost("/leaderboard", async (LeaderboardSubmission submission, ILeaderboardService service,
    IHubContext<SimulationHub> hub, CancellationToken token) =>
{
    var entry = new LeaderboardEntry(submission.PlayerId, submission.DisplayName, submission.Score,
        DateTimeOffset.UtcNow);
    await service.ReportAsync(entry, token).ConfigureAwait(false);
    await hub.Clients.All.SendAsync("LeaderboardUpdated", cancellationToken: token).ConfigureAwait(false);
    return Results.Accepted($"/leaderboard/{submission.PlayerId}");
});

app.MapGet("/sessions", async (IMultiplayerSessionService sessions, CancellationToken token) =>
{
    var descriptors = new List<SessionDescriptor>();
    await foreach (var descriptor in sessions.ListSessionsAsync(token).ConfigureAwait(false))
    {
        descriptors.Add(descriptor);
    }

    return Results.Ok(descriptors);
});

app.MapPost("/sessions",
    async (SessionCreateRequest request, IMultiplayerSessionService sessions, CancellationToken token) =>
    {
        var descriptor = await sessions.CreateSessionAsync(request.Name, token).ConfigureAwait(false);
        return Results.Created($"/sessions/{descriptor.Id}", descriptor);
    });

app.MapPost("/sessions/{sessionId}/join", async (string sessionId, SessionJoinRequest request,
    IMultiplayerSessionService sessions, CancellationToken token) =>
{
    var joined = await sessions.JoinSessionAsync(sessionId, request.PlayerId, token).ConfigureAwait(false);
    return joined ? Results.Ok() : Results.NotFound();
});

app.MapPost("/sessions/{sessionId}/leave", async (string sessionId, SessionJoinRequest request,
    IMultiplayerSessionService sessions, CancellationToken token) =>
{
    var left = await sessions.LeaveSessionAsync(sessionId, request.PlayerId, token).ConfigureAwait(false);
    return left ? Results.Ok() : Results.NotFound();
});

app.MapHub<SimulationHub>("/hubs/simulation");

using (var scope = app.Services.CreateScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<DeveloperModeBootstrapper>();
    await bootstrapper.InitializeAsync().ConfigureAwait(false);
}

await app.RunAsync().ConfigureAwait(false);
