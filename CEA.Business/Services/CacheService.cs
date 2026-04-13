using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CEA.Business.Services
{
    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<CacheService> _logger;
        private static readonly object _lock = new object();

        public CacheService(IMemoryCache memoryCache, ILogger<CacheService> logger)
        {
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public Task<T?> GetAsync<T>(string key)
        {
            if (_memoryCache.TryGetValue(key, out T? value))
            {
                _logger.LogDebug("Cache hit: {Key}", key);
                return Task.FromResult(value);
            }

            _logger.LogDebug("Cache miss: {Key}", key);
            return Task.FromResult(default(T?));
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            lock (_lock) // Thread-safe
            {
                var options = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(expiration ?? TimeSpan.FromMinutes(10));

                _memoryCache.Set(key, value, options);
            }

            _logger.LogDebug("Cache set: {Key}", key);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key)
        {
            lock (_lock) // Thread-safe
            {
                _memoryCache.Remove(key);
            }
            return Task.CompletedTask;
        }

        public Task RemoveByPrefixAsync(string prefix)
        {
            _logger.LogInformation("Cache prefix remove requested: {Prefix}", prefix);
            return Task.CompletedTask;
        }
    }
}