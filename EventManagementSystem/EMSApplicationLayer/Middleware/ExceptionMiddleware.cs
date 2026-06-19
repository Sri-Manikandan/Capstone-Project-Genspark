using System.Net;
using System.Text.Json;
using EMSModelLibrary.Exceptions;

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
