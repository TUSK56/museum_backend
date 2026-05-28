using System.Net;
using System.Text.Json;
using VirtualMuseum.API.DTOs;

namespace VirtualMuseum.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
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

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message) = GetStatusCodeAndMessage(ex);
        context.Response.StatusCode = (int)statusCode;

        var details = _env.IsDevelopment() ? ex.StackTrace : null;

        var response = new ApiResponse<object>(false, null, message, details);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }

    private static (HttpStatusCode statusCode, string message) GetStatusCodeAndMessage(Exception ex)
    {
        var typeName = ex.GetType().Name;
        if (typeName == "ArgumentException" || typeName == "ArgumentNullException")
            return (HttpStatusCode.BadRequest, ex.Message);
        if (typeName == "KeyNotFoundException")
            return (HttpStatusCode.NotFound, "Resource not found");
        if (typeName == "UnauthorizedAccessException")
            return (HttpStatusCode.Unauthorized, "Unauthorized");
        if (typeName == "InvalidOperationException")
            return (HttpStatusCode.BadRequest, ex.Message);
        return (HttpStatusCode.InternalServerError, "An error occurred while processing your request.");
    }
}
