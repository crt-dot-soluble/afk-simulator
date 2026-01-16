using Engine.Core;
using Engine.Core.Accounts;
using Engine.Core.Contracts;
using Engine.Core.DeveloperTools;
using Engine.Core.Multiplayer;
using Engine.Core.Rendering;
using Engine.Server.Hubs;
using Engine.Server.Models;
using Engine.Server.Security;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIncrementalEngine();
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
    IReadOnlyDictionary<string, object?>? parameters =
        request.Parameters?.ToDictionary(static kvp => kvp.Key, static kvp => (object?)kvp.Value);
    var response = await explorer.ExecuteAsync(moduleName, commandName, parameters, token).ConfigureAwait(false);
    return Results.Ok(new DeveloperCommandResult(response));
});
developer.MapGet("/autocomplete", (IModuleExplorer explorer) => Results.Ok(explorer.BuildAutocomplete()));
developer.MapGet("/profiles", (DeveloperProfileStore profiles) => Results.Ok(profiles.List()));
developer.MapPost("/profiles",
    (DeveloperProfileUpsertRequest request, DeveloperProfileStore profiles) =>
        Results.Ok(profiles.Upsert(request.Id, request.State)));

var accountsApi = app.MapGroup("/accounts");
accountsApi.MapPost("/users", async (CreateUserRequest request, IAccountService accounts, CancellationToken token) =>
{
    if (string.IsNullOrWhiteSpace(request.DisplayName))
    {
        return Results.BadRequest(new AccountError(AccountErrorCodes.ValidationFailed, "Display name is required."));
    }

    var user = await accounts.CreateUserAsync(request.DisplayName, token).ConfigureAwait(false);
    return Results.Created($"/accounts/users/{user.Id}", user);
});

accountsApi.MapGet("/users/{userId}", async (string userId, IAccountService accounts, CancellationToken token) =>
{
    var user = await accounts.GetUserAsync(userId, token).ConfigureAwait(false);
    return user is null
        ? Results.NotFound(new AccountError(AccountErrorCodes.UserNotFound, $"User '{userId}' was not found."))
        : Results.Ok(user);
});

accountsApi.MapGet("/users/{userId}/accounts",
    async (string userId, IAccountService accounts, CancellationToken token) =>
    {
        try
        {
            var snapshot = await accounts.ListAccountsAsync(userId, token).ConfigureAwait(false);
            return Results.Ok(snapshot);
        }
        catch (AccountOperationException ex) when (ex.Code == AccountErrorCodes.UserNotFound)
        {
            return Results.NotFound(new AccountError(ex.Code, ex.Message));
        }
    });

accountsApi.MapPost("/users/{userId}/accounts",
    async (string userId, CreateAccountRequest request, IAccountService accounts, CancellationToken token) =>
    {
        if (string.IsNullOrWhiteSpace(request.Label))
        {
            return Results.BadRequest(new AccountError(AccountErrorCodes.ValidationFailed, "Label is required."));
        }

        try
        {
            var account = await accounts.CreateAccountAsync(userId, request.Label, token).ConfigureAwait(false);
            return Results.Created($"/accounts/accounts/{account.Id}", account);
        }
        catch (AccountOperationException ex) when (ex.Code is AccountErrorCodes.UserNotFound)
        {
            return Results.NotFound(new AccountError(ex.Code, ex.Message));
        }
        catch (AccountOperationException ex) when (ex.Code is AccountErrorCodes.AccountLimit)
        {
            return Results.Conflict(new AccountError(ex.Code, ex.Message));
        }
    });

accountsApi.MapGet("/accounts/{accountId}/profiles",
    async (string accountId, IAccountService accounts, CancellationToken token) =>
    {
        var account = await accounts.GetAccountAsync(accountId, token).ConfigureAwait(false);
        if (account is null)
        {
            return Results.NotFound(new AccountError(AccountErrorCodes.AccountNotFound,
                $"Account '{accountId}' was not found."));
        }

        var profiles = await accounts.ListProfilesAsync(accountId, token).ConfigureAwait(false);
        return Results.Ok(profiles);
    });

accountsApi.MapPost("/accounts/{accountId}/profiles",
    async (string accountId, CreateProfileRequest request, IAccountService accounts, CancellationToken token) =>
    {
        try
        {
            var profile = await accounts.CreateProfileAsync(accountId, request.Name, request.SpriteAssetId, token)
                .ConfigureAwait(false);
            return Results.Created($"/accounts/profiles/{profile.Id}", profile);
        }
        catch (AccountOperationException ex) when (ex.Code == AccountErrorCodes.AccountNotFound)
        {
            return Results.NotFound(new AccountError(ex.Code, ex.Message));
        }
        catch (AccountOperationException ex) when (ex.Code == AccountErrorCodes.ProfileLimit)
        {
            return Results.Conflict(new AccountError(ex.Code, ex.Message));
        }
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
    await foreach (var descriptor in sessions.ListSessionsAsync(token))
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

app.Run();
