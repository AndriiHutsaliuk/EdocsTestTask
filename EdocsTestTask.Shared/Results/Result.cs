namespace EdocsTestTask.Shared.Results
{
    /// <summary>
    /// Outcome of a service operation that returns no value.
    /// </summary>
    public class Result
    {
        #region Properties

        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// Errors describing the failure. Empty when <see cref="IsSuccess"/> is true.
        /// </summary>
        public IReadOnlyList<Error> Errors { get; }

        /// <summary>
        /// The first error, or null on success. Its type drives transport status mapping.
        /// </summary>
        public Error? Error => Errors.Count > 0 ? Errors[0] : null;

        #endregion

        #region Constructors

        protected Result(bool isSuccess, IReadOnlyList<Error> errors)
        {
            if (isSuccess && errors.Count > 0)
            {
                throw new ArgumentException("A successful result cannot contain errors.", nameof(errors));
            }

            if (!isSuccess && errors.Count == 0)
            {
                throw new ArgumentException("A failed result must contain at least one error.", nameof(errors));
            }

            IsSuccess = isSuccess;
            Errors = errors;
        }

        #endregion

        #region Factories

        public static Result Success() => new(true, []);

        public static Result Failure(Error error) => new(false, [error]);

        public static Result Failure(IEnumerable<Error> errors) => new(false, errors.ToArray());

        public static implicit operator Result(Error error) => Failure(error);

        #endregion
    }
}
