using Acman.Pdf.Abstractions.Storage;
using Acman.Pdf.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Acman.Pdf.Infrastructure.Caching;

public sealed class RedisRenderCache : IRenderCache, IDisposable
{
    private readonly IConnectionMultiplexer? _mux;
    private readonly bool _enabled;
    private readonly ILogger<RedisRenderCache> _logger;

    public RedisRenderCache(
        IOptions<RedisOptions> options,
        ILogger<RedisRenderCache> logger)
    {
        _logger = logger;
        _enabled = options.Value.Enabled;
        if (!_enabled)
        {
            return;
        }

        try
        {
            _mux = ConnectionMultiplexer.Connect(options.Value.ConnectionString);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable; render cache disabled for this process.");
            _enabled = false;
        }
    }

    public async Task<byte[]?> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (!_enabled || _mux is null)
        {
            return null;
        }

        var value = await _mux.GetDatabase().StringGetAsync(cacheKey);
        return value.HasValue ? (byte[]?)value : null;
    }

    public async Task SetAsync(string cacheKey, byte[] value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (!_enabled || _mux is null)
        {
            return;
        }

        await _mux.GetDatabase().StringSetAsync(cacheKey, value, ttl);
    }

    public void Dispose() => _mux?.Dispose();
}

public sealed class NullRenderCache : IRenderCache
{
    public Task<byte[]?> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
        => Task.FromResult<byte[]?>(null);

    public Task SetAsync(string cacheKey, byte[] value, TimeSpan ttl, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
