using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace EMSApplicationLayer.Filters
{
    /// <summary>
    /// Applies idempotency to a POST action. Clients must send an Idempotency-Key header.
    /// Identical keys within 24 hours return the cached response without re-executing the action.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class IdempotentAttribute : Attribute, IFilterFactory
    {
        public bool IsReusable => false;

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
            serviceProvider.GetRequiredService<IdempotencyFilter>();
    }

    public sealed class IdempotencyFilter : IAsyncActionFilter
    {
        private const string HeaderName = "Idempotency-Key";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

        // Serializes concurrent requests that share a key so a near-simultaneous
        // double-submit can't both execute before either result is cached.
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

        private readonly IMemoryCache _cache;

        public IdempotencyFilter(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var key)
                || string.IsNullOrWhiteSpace(key))
            {
                await next();
                return;
            }

            var cacheKey = $"idempotency:{key}";
            var gate = Locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));

            await gate.WaitAsync(context.HttpContext.RequestAborted);
            try
            {
                if (TryReplay(context, cacheKey))
                {
                    return;
                }

                var executed = await next();

                if (executed.Result is ObjectResult { StatusCode: >= 200 and < 300 } objectResult)
                {
                    _cache.Set(cacheKey, new IdempotencyEntry
                    {
                        StatusCode = objectResult.StatusCode!.Value,
                        Body = JsonSerializer.Serialize(objectResult.Value)
                    }, CacheDuration);
                }
            }
            finally
            {
                gate.Release();
            }
        }

        private bool TryReplay(ActionExecutingContext context, string cacheKey)
        {
            if (!_cache.TryGetValue(cacheKey, out IdempotencyEntry? entry) || entry == null)
            {
                return false;
            }

            context.HttpContext.Response.Headers["X-Idempotent-Replayed"] = "true";
            context.Result = new ContentResult
            {
                StatusCode = entry.StatusCode,
                Content = entry.Body,
                ContentType = "application/json"
            };
            return true;
        }
    }

    internal sealed class IdempotencyEntry
    {
        public int StatusCode { get; init; }
        public string Body { get; init; } = string.Empty;
    }
}
