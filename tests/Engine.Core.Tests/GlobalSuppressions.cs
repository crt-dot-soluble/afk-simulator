using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "Unit tests execute on the default synchronization context.", Scope = "module")]
[assembly: SuppressMessage("Xunit.Analyzers", "xUnit1000:Test classes must be public", Justification = "Legacy tests remain internal while refactors are staged.", Scope = "module")]
[assembly: SuppressMessage("Usage", "CA1812:Avoid uninstantiated internal classes", Justification = "Test fixtures are discovered via reflection despite internal visibility.", Scope = "module")]
