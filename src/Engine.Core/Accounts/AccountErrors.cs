using System;

namespace Engine.Core.Accounts;

public static class AccountErrorCodes
{
    public const string UserNotFound = "user_not_found";
    public const string UniverseNotFound = "universe_not_found";
    public const string UniverseLimit = "universe_limit";
    public const string CharacterLimit = "character_limit";
    public const string CharacterNotFound = "character_not_found";
    public const string WalletUnavailable = "wallet_unavailable";
    public const string ValidationFailed = "validation_error";
}

public sealed record AccountError(string Code, string Message);

public sealed class AccountOperationException : InvalidOperationException
{
    public AccountOperationException()
    {
        Code = AccountErrorCodes.ValidationFailed;
    }

    public AccountOperationException(string message)
        : base(message)
    {
        Code = AccountErrorCodes.ValidationFailed;
    }

    public AccountOperationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Code = AccountErrorCodes.ValidationFailed;
    }

    public AccountOperationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

public static class AccountErrors
{
    public static AccountOperationException UserNotFound(string userId) =>
        new(AccountErrorCodes.UserNotFound, $"User '{userId}' was not found.");

    public static AccountOperationException UniverseNotFound(string universeId) =>
        new(AccountErrorCodes.UniverseNotFound, $"Universe '{universeId}' was not found.");

    public static AccountOperationException UniverseLimit(string userId) =>
        new(AccountErrorCodes.UniverseLimit, $"User '{userId}' cannot create additional universes.");

    public static AccountOperationException CharacterLimit(string universeId) =>
        new(AccountErrorCodes.CharacterLimit, $"Universe '{universeId}' reached the maximum number of characters.");

    public static AccountOperationException CharacterNotFound(string characterId) =>
        new(AccountErrorCodes.CharacterNotFound, $"Character '{characterId}' was not found.");

    public static AccountOperationException WalletUnavailable(string scope) =>
        new(AccountErrorCodes.WalletUnavailable, $"Wallet data is unavailable for {scope}.");
}
