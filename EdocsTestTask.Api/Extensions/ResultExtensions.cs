using EdocsTestTask.Api.Contracts;
using EdocsTestTask.Shared.Results;
using Microsoft.AspNetCore.Mvc;

namespace EdocsTestTask.Api.Extensions
{
    /// <summary>
    /// Translates service Results into ServiceResponse envelopes with the matching HTTP status code.
    /// </summary>
    public static class ResultExtensions
    {
        #region Methods

        public static ActionResult<ServiceResponse> ToActionResult(this Result result, int successStatusCode = StatusCodes.Status200OK)
        {
            ArgumentNullException.ThrowIfNull(result);

            return result.IsSuccess
                ? new ObjectResult(ServiceResponse.Ok()) { StatusCode = successStatusCode }
                : new ObjectResult(ServiceResponse.Fail(ToErrorResponses(result))) { StatusCode = ToStatusCode(result.Error!.Type) };
        }

        public static ActionResult<ServiceResponse<T>> ToActionResult<T>(this Result<T> result, int successStatusCode = StatusCodes.Status200OK)
        {
            ArgumentNullException.ThrowIfNull(result);

            return result.IsSuccess
                ? new ObjectResult(ServiceResponse<T>.Ok(result.Value)) { StatusCode = successStatusCode }
                : new ObjectResult(ServiceResponse<T>.Fail(ToErrorResponses(result))) { StatusCode = ToStatusCode(result.Error!.Type) };
        }

        public static int ToStatusCode(ErrorType type) => type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        #endregion

        #region Private Methods

        private static IReadOnlyList<ErrorResponse> ToErrorResponses(Result result) =>
            result.Errors.Select(e => new ErrorResponse(e.Code, e.Message)).ToArray();

        #endregion
    }
}
