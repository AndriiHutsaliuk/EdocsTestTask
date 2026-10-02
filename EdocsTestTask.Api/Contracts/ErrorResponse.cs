namespace EdocsTestTask.Api.Contracts
{
    /// <summary>
    /// Client-facing error entry in a <see cref="ServiceResponse"/>.
    /// </summary>
    public sealed record ErrorResponse(string Code, string Message);
}
