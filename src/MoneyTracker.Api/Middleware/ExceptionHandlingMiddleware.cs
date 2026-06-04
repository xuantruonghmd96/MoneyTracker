using System.Text.Json;
using MoneyTracker.Api.Common;
using MoneyTracker.Api.Services.Exceptions;
using MoneyTracker.Domain.Common;

namespace MoneyTracker.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (DomainException ex)
        {
            var status = ex switch
            {
                NotFoundException    => 404,
                ConflictException   => 409,
                ValidationException => 400,
                ForbiddenException  => 403,
                ServiceBusyException => 503,
                _                   => 500
            };
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new ApiError(ex.ErrorCode, ex.Fields)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            ctx.Response.StatusCode = 500;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new ApiError(ErrorCodes.InternalError)));
        }
    }
}
