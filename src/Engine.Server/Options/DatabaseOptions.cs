using System;
using System.Diagnostics.CodeAnalysis;

namespace Engine.Server.Options;

internal enum DatabaseProvider
{
    Sqlite,
    PostgreSql,
    Supabase
}

[SuppressMessage("Performance", "CA1812",
    Justification = "Bound via configuration and instantiated by the options system.")]
internal sealed class DatabaseOptions
{
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;

    public string ConnectionString { get; set; } = string.Empty;

    public string? DataDirectory { get; set; }

    public string DatabaseName { get; set; } = "engine.db";

    public SupabaseOptions Supabase { get; set; } = new();
}

internal sealed class SupabaseOptions
{
    public Uri? Url { get; set; }

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional direct Postgres connection string emitted by Supabase. When provided, this value wins over <see cref="DatabaseOptions.ConnectionString"/>.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
}
