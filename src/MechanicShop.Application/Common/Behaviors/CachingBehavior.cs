
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results.Abstractions;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

public class CachingBehavior<TRequest, TResponse>(
    HybridCache cache,
    ILogger<CachingBehavior<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly HybridCache _cache = cache;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        if (request is not ICachedQuery cachedRequest)
            return await next(cancellationToken);

        _logger.LogInformation(
            "Checking cache for {RequestName}",
            typeof(TRequest).Name);

        var result = await _cache.GetOrCreateAsync<TResponse>(
            cachedRequest.CacheKey,
            static _ => ValueTask.FromResult<TResponse>(default!),
            new HybridCacheEntryOptions
            {
                Flags = HybridCacheEntryFlags.DisableUnderlyingData
            },
            cachedRequest.Tags,
            cancellationToken);

        // Cache Hit
        if (result is not null)
        {
            _logger.LogInformation(
                "Cache hit for {RequestName}",
                typeof(TRequest).Name);

            return result;
        }

        // Cache Miss
        _logger.LogInformation(
            "Cache miss for {RequestName}",
            typeof(TRequest).Name);

        result = await next(cancellationToken);

        if (result is IResult { IsSuccess: true })
        {
            _logger.LogInformation(
                "Caching result for {RequestName}",
                typeof(TRequest).Name);

            await _cache.SetAsync(
                cachedRequest.CacheKey,
                result,
                new HybridCacheEntryOptions
                {
                    Expiration = cachedRequest.Expiration
                },
                cachedRequest.Tags,
                cancellationToken);
        }

        return result;
    }
}
