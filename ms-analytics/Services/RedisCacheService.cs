using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ms_analytics.Services;

public class RedisCacheService
{
    private readonly IDistributedCache? _cache;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IServiceProvider serviceProvider, ILogger<RedisCacheService> logger)
    {
        _logger = logger;
        _cache = serviceProvider.GetService(typeof(IDistributedCache)) as IDistributedCache;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        if (_cache == null) return default;
        try
        {
            var cachedData = await _cache.GetStringAsync(key);
            if (string.IsNullOrEmpty(cachedData))
                return default;

            return JsonSerializer.Deserialize<T>(cachedData);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Redis cache read error for key {Key}: {Message}. Falling back to database.", key, ex.Message);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration)
    {
        if (_cache == null || value == null) return;
        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };
            var json = JsonSerializer.Serialize(value);
            await _cache.SetStringAsync(key, json, options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Redis cache write error for key {Key}: {Message}", key, ex.Message);
        }
    }

    public async Task RemoveAsync(string key)
    {
        if (_cache == null) return;
        try
        {
            await _cache.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Redis cache remove error for key {Key}: {Message}", key, ex.Message);
        }
    }
}
