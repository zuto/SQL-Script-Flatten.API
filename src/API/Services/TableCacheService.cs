using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Data.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Services;

public class TableCacheService
{
    private readonly IScriptRepository _repository;
    private readonly ILogger<TableCacheService> _logger;
    private readonly TableCacheOptions _options;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    
    private HashSet<string> _cachedTableNames;
    private DateTime? _lastRefreshed;

    public TableCacheService(
        IScriptRepository repository,
        ILogger<TableCacheService> logger,
        IOptions<TableCacheOptions> options)
    {
        _repository = repository;
        _logger = logger;
        _options = options.Value;
    }

    public DateTime? LastRefreshed => _lastRefreshed;

    public async Task<HashSet<string>> GetTableNamesAsync()
    {
        if (!_options.EnableCache)
        {
            _logger.LogDebug("Cache is disabled, fetching fresh table names");
            return await _repository.GetAllTableNames();
        }

        // Check if cache is valid
        if (_cachedTableNames != null && _lastRefreshed.HasValue)
        {
            var cacheAge = DateTime.UtcNow - _lastRefreshed.Value;
            if (cacheAge.TotalMinutes < _options.ExpirationMinutes)
            {
                _logger.LogDebug("Returning cached table names (age: {CacheAgeMinutes:F2} minutes)", cacheAge.TotalMinutes);
                return _cachedTableNames;
            }

            _logger.LogInformation("Table name cache expired (age: {CacheAgeMinutes:F2} minutes), refreshing", cacheAge.TotalMinutes);
        }

        // Cache is invalid or doesn't exist, refresh it
        return await RefreshCacheAsync();
    }

    public async Task<HashSet<string>> RefreshCacheAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            // Double-check after acquiring lock (another thread might have refreshed)
            if (_cachedTableNames != null && _lastRefreshed.HasValue)
            {
                var cacheAge = DateTime.UtcNow - _lastRefreshed.Value;
                if (cacheAge.TotalMinutes < _options.ExpirationMinutes)
                {
                    _logger.LogDebug("Cache was refreshed by another thread, using existing cache");
                    return _cachedTableNames;
                }
            }

            _logger.LogInformation("Refreshing table name cache");
            
            var tableNames = await _repository.GetAllTableNames();
            _cachedTableNames = tableNames;
            _lastRefreshed = DateTime.UtcNow;
            
            _logger.LogInformation("Table name cache refreshed with {TableCount} tables", tableNames.Count);
            
            return _cachedTableNames;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void ClearCache()
    {
        _semaphore.Wait();
        try
        {
            _cachedTableNames = null;
            _lastRefreshed = null;
            _logger.LogInformation("Table name cache cleared");
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

public class TableCacheOptions
{
    public int ExpirationMinutes { get; set; } = 60;
    public bool EnableCache { get; set; } = true;
}
