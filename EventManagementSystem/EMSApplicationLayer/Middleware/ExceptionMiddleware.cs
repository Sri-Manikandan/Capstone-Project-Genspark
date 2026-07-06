using System.Net;
using System.Text.Json;
using EMSModelLibrary.Exceptions;
using Stripe;

namespace EMSApplicationLayer.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var (statusCode, message) = exception switch
            {
                NotFoundException ex => (HttpStatusCode.NotFound, ex.Message),
                ValidationException ex => (HttpStatusCode.BadRequest, ex.Message),
                InvalidCredentialsException ex => (HttpStatusCode.Unauthorized, ex.Message),
                UnauthorizedException ex => (HttpStatusCode.Forbidden, ex.Message),
                DatabaseException => (HttpStatusCode.InternalServerError, "A database error occurred."),
                // Surface the real reason Stripe rejected the request (e.g. amount below the
                // minimum, unsupported currency) instead of an opaque 500. Pass through Stripe's
                // own 4xx status when it sent one; otherwise treat it as an upstream failure.
                StripeException ex => (
                    (int)ex.HttpStatusCode is >= 400 and < 500 ? ex.HttpStatusCode : HttpStatusCode.BadGateway,
                    ex.StripeError?.Message ?? ex.Message),
                _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
            };

            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            var body = JsonSerializer.Serialize(
                new { error = message },
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            await context.Response.WriteAsync(body);
        }
    }
}
