namespace EdocsTestTask.Shared.Results
{
    /// <summary>
    /// Outcome of a service operation that returns a value on success.
    /// </summary>
    public sealed class Result<T> : Result
    {
        #region Fields

        private readonly T? _value;

        #endregion

        #region Properties

        /// <summary>
        /// The value. Accessing it on a failed result throws.
        /// </summary>
        public T Value => IsSuccess
            ? _value!
            : throw new InvalidOperationException("Cannot access the value of a failed result.");

        #endregion

        #region Constructors

        private Result(T? value, bool isSuccess, IReadOnlyList<Error> errors)
            : base(isSuccess, errors)
        {
            _value = value;
        }

        #endregion

        #region Factories

        public static Result<T> Success(T value) => new(value, true, []);

        public static new Result<T> Failure(Error error) => new(default, false, [error]);

        public static new Result<T> Failure(IEnumerable<Error> errors) => new(default, false, errors.ToArray());

        public static implicit operator Result<T>(T value) => Success(value);

        public static implicit operator Result<T>(Error error) => Failure(error);

        #endregion

        #region Methods

        /// <summary>
        /// Projects the value of a successful result; a failed result keeps its errors and the mapper is not called.
        /// </summary>
        public Result<TOut> Map<TOut>(Func<T, TOut> map)
        {
            ArgumentNullException.ThrowIfNull(map);

            return IsSuccess
                ? Result<TOut>.Success(map(_value!))
                : Result<TOut>.Failure(Errors);
        }

        #endregion
    }
}
