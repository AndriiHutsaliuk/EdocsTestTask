namespace EdocsTestTask.Shared.Results
{
    /// <summary>
    /// Category of an expected failure. Entry points map it to a transport status (e.g. HTTP).
    /// </summary>
    public enum ErrorType
    {
        Failure = 0,
        Validation = 1,
        NotFound = 2,
        Conflict = 3,
        Unauthorized = 4,
        Forbidden = 5
    }
}
