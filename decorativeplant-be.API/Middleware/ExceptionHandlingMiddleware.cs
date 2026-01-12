using System.Net;
using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace decorativeplant_be.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        var response = context.Response;

        var errorResponse = exception switch
        {
            ValidationException validationException => ApiResponse<object>.ErrorResponse(
                "Validation failed",
                validationException.Errors,
                (int)HttpStatusCode.BadRequest),

            NotFoundException => ApiResponse<object>.ErrorResponse(
                exception.Message,
                null,
                (int)HttpStatusCode.NotFound),

            UnauthorizedException => ApiResponse<object>.ErrorResponse(
                exception.Message,
                null,
                (int)HttpStatusCode.Unauthorized),

            _ => ApiResponse<object>.ErrorResponse(
                "An error occurred while processing your request.",
                null,
                (int)HttpStatusCode.InternalServerError)
        };

        response.StatusCode = errorResponse.StatusCode;

        _logger.LogError(exception, "An error occurred: {Message}", exception.Message);

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(errorResponse, options);
        await response.WriteAsync(json);
    }
}
