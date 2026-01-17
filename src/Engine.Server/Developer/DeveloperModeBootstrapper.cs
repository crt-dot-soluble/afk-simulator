using System.Diagnostics.CodeAnalysis;
using Engine.Core.Accounts;
using Engine.Server.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Engine.Server.Developer;

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated via dependency injection")]
internal sealed partial class DeveloperModeBootstrapper
{
    private readonly IAccountService _accounts;
    private readonly ILogger<DeveloperModeBootstrapper> _logger;
    private readonly DeveloperModeOptions _options;

    public DeveloperModeBootstrapper(
        IAccountService accounts,
        IOptions<DeveloperModeOptions> options,
        ILogger<DeveloperModeBootstrapper> logger)
    {
        _accounts = accounts;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.AutoLogin)
        {
            DeveloperModeBootstrapperLog.AutoLoginDisabled(_logger);
            return;
        }

        DeveloperModeBootstrapperLog.EnsuringDeveloperAccount(_logger, _options.Email);
        var user = await _accounts.AuthenticateAsync(_options.Email, _options.Password, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            user = await _accounts.RegisterUserAsync(_options.Email, _options.Password, _options.DisplayName,
                    cancellationToken)
                .ConfigureAwait(false);
            DeveloperModeBootstrapperLog.ProvisionedDeveloperUser(_logger, user.Id);
        }

        var universes = await _accounts.ListUniversesAsync(user.Id, cancellationToken).ConfigureAwait(false);
        UniverseRecord? primaryUniverse = universes.Count > 0 ? universes[0] : null;
        if (primaryUniverse is null)
        {
            primaryUniverse = await _accounts.CreateUniverseAsync(user.Id, _options.PrimaryUniverseName,
                    cancellationToken)
                .ConfigureAwait(false);
            DeveloperModeBootstrapperLog.SeededDeveloperUniverse(_logger, primaryUniverse.Name);
        }

        var characters = await _accounts.ListCharactersAsync(primaryUniverse.Id, cancellationToken)
            .ConfigureAwait(false);
        if (characters.Count == 0)
        {
            var seeded = await _accounts.CreateCharacterAsync(primaryUniverse.Id, _options.PrimaryCharacterName,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            characters = new List<CharacterRecord> { seeded };
            DeveloperModeBootstrapperLog.SeededDeveloperCharacter(_logger, seeded.Name);
        }

        await _accounts.TopUpUserWalletAsync(user.Id, _options.BaseCurrency, _options.PremiumCurrency,
            cancellationToken).ConfigureAwait(false);
        DeveloperModeBootstrapperLog.DeveloperWalletsBoosted(_logger, _options.BaseCurrency,
            _options.PremiumCurrency);
    }
}

internal static partial class DeveloperModeBootstrapperLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Developer auto-login is disabled. Skipping bootstrap.")]
    public static partial void AutoLoginDisabled(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Ensuring developer account '{Email}' exists.")]
    public static partial void EnsuringDeveloperAccount(ILogger logger, string email);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "Provisioned developer user {UserId}.")]
    public static partial void ProvisionedDeveloperUser(ILogger logger, string userId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information,
        Message = "Seeded developer universe '{UniverseName}'.")]
    public static partial void SeededDeveloperUniverse(ILogger logger, string universeName);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information,
        Message = "Seeded developer character '{CharacterName}'.")]
    public static partial void SeededDeveloperCharacter(ILogger logger, string characterName);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information,
        Message = "Developer wallets boosted to Base={baseCurrency} Premium={premiumCurrency}.")]
    public static partial void DeveloperWalletsBoosted(ILogger logger, long baseCurrency, long premiumCurrency);
}
