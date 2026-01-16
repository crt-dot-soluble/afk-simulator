using System;

namespace Engine.Core.Accounts;

public static class AccountErrorCodes
{
    public const string UserNotFound = "user_not_found";
    public const string AccountNotFound = "account_not_found";
    public const string AccountLimit = "account_limit";
    public const string ProfileLimit = "profile_limit";
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

internal static class AccountErrors
{
    public static AccountOperationException UserNotFound(string userId) =>
        new(AccountErrorCodes.UserNotFound, $"User '{userId}' was not found.");

    public static AccountOperationException AccountNotFound(string accountId) =>
        new(AccountErrorCodes.AccountNotFound, $"Account '{accountId}' was not found.");

    public static AccountOperationException AccountLimit(string userId) =>
        new(AccountErrorCodes.AccountLimit, $"User '{userId}' cannot create additional accounts.");

    public static AccountOperationException ProfileLimit(string accountId) =>
        new(AccountErrorCodes.ProfileLimit, $"Account '{accountId}' reached the maximum number of profiles.");
}
