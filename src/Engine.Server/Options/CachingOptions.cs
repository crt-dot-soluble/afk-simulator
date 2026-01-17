namespace Engine.Server.Options;

internal enum CacheProvider
{
    Memory,
    Redis
}

internal sealed class CachingOptions
{
    public CacheProvider Provider { get; set; } = CacheProvider.Memory;

    public string? RedisConnectionString { get; set; }
}
