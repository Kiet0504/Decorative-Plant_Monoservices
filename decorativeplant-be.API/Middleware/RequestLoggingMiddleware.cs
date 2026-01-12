using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace decorativeplant_be.API.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestPath = context.Request.Path;
        var requestMethod = context.Request.Method;

        _logger.LogInformation(
            "Incoming {Method} request to {Path}",
            requestMethod,
            requestPath);

        try
        {
            await _next(context);
            stopwatch.Stop();

            _logger.LogInformation(
                "Completed {Method} request to {Path} in {ElapsedMilliseconds}ms with status {StatusCode}",
                requestMethod,
                requestPath,
                stopwatch.ElapsedMilliseconds,
                context.Response.StatusCode);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "Error processing {Method} request to {Path} in {ElapsedMilliseconds}ms",
                requestMethod,
                requestPath,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
