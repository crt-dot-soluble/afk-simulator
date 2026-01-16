using System;
using System.Diagnostics.CodeAnalysis;

namespace Engine.Server.Models;

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by minimal API binding.")]
internal sealed class ClientErrorReport
{
    public string? Source { get; init; }
    public string? Message { get; init; }
    public string? StackTrace { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}
