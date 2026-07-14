using EMSApplicationLayer.Filters;
using EMSModelLibrary.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;

namespace EMSTests.Filters
{
    [TestFixture]
    public class IdempotencyFilterTests
    {
        private static ActionExecutingContext MakeContext(string idempotencyKey)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Idempotency-Key"] = idempotencyKey;
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object?>(), controller: new object());
        }

        private static ActionExecutionDelegate NextReturning(IActionResult result, ActionExecutingContext ctx, Action? onCalled = null)
        {
            return () =>
            {
                onCalled?.Invoke();
                var executed = new ActionExecutedContext(ctx, new List<IFilterMetadata>(), controller: new object())
                {
                    Result = result
                };
                return Task.FromResult(executed);
            };
        }

        [Test]
        public async Task Replay_SerializesBodyInCamelCase_MatchingNormalResponses()
        {
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var filter = new IdempotencyFilter(cache);
            var dto = new PaymentInitiateDto { Id = 1, BookingId = 7, ClientSecret = "pi_secret_abc", Currency = "inr" };

            // First call executes the action and caches its response.
            var first = MakeContext("payment-initiate-7");
            await filter.OnActionExecutionAsync(first, NextReturning(new ObjectResult(dto) { StatusCode = 200 }, first));

            // Second call with the same key replays the cached body without re-executing.
            var second = MakeContext("payment-initiate-7");
            var reExecuted = false;
            await filter.OnActionExecutionAsync(second,
                NextReturning(new ObjectResult(dto) { StatusCode = 200 }, second, onCalled: () => reExecuted = true));

            reExecuted.Should().BeFalse("the second identical request must be served from cache");
            var replay = second.Result.Should().BeOfType<ContentResult>().Subject;
            // The replayed JSON must use the same camelCase keys the client reads, or fields
            // like clientSecret come back undefined in the browser.
            replay.Content.Should().Contain("\"clientSecret\":\"pi_secret_abc\"");
            replay.Content.Should().NotContain("\"ClientSecret\"");
        }
    }
}
