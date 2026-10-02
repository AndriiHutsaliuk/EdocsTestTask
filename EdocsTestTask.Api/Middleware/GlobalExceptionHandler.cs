using EdocsTestTask.Api.Contracts;
using Microsoft.AspNetCore.Diagnostics;

namespace EdocsTestTask.Api.Middleware
{
    /// <summary>
    /// Converts unexpected exceptions into a generic 500 ServiceResponse without leaking details to the client.
    /// </summary>
    public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
    {
        #region Methods

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}. TraceId: {TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier);

            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

            var response = ServiceResponse.Fail(
            [
                new ErrorResponse("Server.Unexpected", $"An unexpected error occurred. TraceId: {httpContext.TraceIdentifier}")
            ]);

            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
            return true;
        }

        #endregion
    }
}
