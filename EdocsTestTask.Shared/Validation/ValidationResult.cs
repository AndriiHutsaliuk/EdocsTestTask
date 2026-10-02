using EdocsTestTask.Shared.Results;

namespace EdocsTestTask.Shared.Validation
{
    /// <summary>
    /// Accumulates validation failures produced by service Helper validation methods.
    /// Build it with <see cref="AddError(string, string)"/> / <see cref="AddError(Error)"/>.
    /// </summary>
    public sealed class ValidationResult
    {
        #region Fields

        private readonly List<Error> _errors = [];

        #endregion

        #region Properties

        public bool IsValid => _errors.Count == 0;

        public IReadOnlyList<Error> Errors => _errors;

        #endregion

        #region Methods

        public void AddError(string message, string code = ValidationErrorCodes.Invalid)
        {
            _errors.Add(Error.Validation(code, message));
        }

        /// <summary>
        /// Adds a pre-built error, e.g. a Conflict raised by a business rule rather than an input check.
        /// </summary>
        public void AddError(Error error)
        {
            ArgumentNullException.ThrowIfNull(error);

            _errors.Add(error);
        }

        /// <summary>
        /// Converts the validation outcome to a <see cref="Result"/>.
        /// </summary>
        public Result ToResult() => IsValid ? Result.Success() : Result.Failure(_errors);

        /// <summary>
        /// Converts a failed validation outcome to a <see cref="Result{T}"/>.
        /// </summary>
        public Result<T> ToFailure<T>()
        {
            if (IsValid)
            {
                throw new InvalidOperationException("Cannot create a failure from a valid validation result.");
            }

            return Result<T>.Failure(_errors);
        }

        #endregion
    }
}
