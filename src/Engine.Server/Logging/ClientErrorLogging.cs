using Microsoft.Extensions.Logging;

namespace Engine.Server.Logging;

internal static partial class ClientErrorLogging
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Error,
        Message = "Client error from {Source}: {Message}\nStack: {Stack}\nAgent: {Agent}")]
    public static partial void LogClientError(this ILogger logger, string source, string message, string stack,
        string agent);
}
