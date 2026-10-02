namespace EdocsTestTask.Api.Contracts
{
    /// <summary>
    /// Transport envelope for endpoints that return data.
    /// </summary>
    public class ServiceResponse<T> : ServiceResponse
    {
        #region Properties

        public T? Data { get; init; }

        #endregion

        #region Factories

        public static ServiceResponse<T> Ok(T data) => new() { Success = true, Data = data };

        public static new ServiceResponse<T> Fail(IReadOnlyList<ErrorResponse> errors) => new() { Success = false, Errors = errors };

        #endregion
    }
}
