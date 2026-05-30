using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PlatformWallet.TransactionIntake.Domain.Exceptions;

namespace PlatformWallet.TransactionIntake.Api.ExceptionHandlers;

internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            TransactionNotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            InvalidTransitionException   => (StatusCodes.Status409Conflict, "Conflict"),
            _                            => (StatusCodes.Status500InternalServerError, "Internal Server Error"),
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception");
        }

        var detail = statusCode < 500 ? exception.Message : "An unexpected error occurred.";

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title  = title,
            Detail = detail,
        };
        problem.Extensions["traceId"] = Activity.Current?.TraceId.ToString();

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}
