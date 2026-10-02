namespace EdocsTestTask.Core.Commands
{
    /// <summary>
    /// Input for an action on an approval task. <see cref="ActorId"/> is the authenticated user, never the request body.
    /// </summary>
    public sealed record ExecuteActionCommand(string? ActorId, string? Action, string? Comment);
}
