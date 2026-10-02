namespace EdocsTestTask.Shared.Results
{
    /// <summary>
    /// Structured description of an expected failure.
    /// </summary>
    /// <param name="Code">Stable machine-readable code, e.g. "Document.NotFound".</param>
    /// <param name="Message">Human-readable message safe to return to clients.</param>
    /// <param name="Type">Failure category.</param>
    public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
    {
        #region Factories

        public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

        public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

        public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

        public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

        public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

        public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

        #endregion
    }
}
