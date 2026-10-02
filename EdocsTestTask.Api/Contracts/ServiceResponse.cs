namespace EdocsTestTask.Api.Contracts
{
    /// <summary>
    /// Transport envelope for endpoints that return no data.
    /// </summary>
    public class ServiceResponse
    {
        #region Properties

        public bool Success { get; init; }

        public IReadOnlyList<ErrorResponse> Errors { get; init; } = [];

        #endregion

        #region Factories

        public static ServiceResponse Ok() => new() { Success = true };

        public static ServiceResponse Fail(IReadOnlyList<ErrorResponse> errors) => new() { Success = false, Errors = errors };

        #endregion
    }
}
