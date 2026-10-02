namespace EdocsTestTask.Api.Contracts.ApprovalTasks
{
    /// <summary>
    /// Body of POST api/approval-tasks/{id}/actions. The actor is the authenticated user ("sub"), not a body field.
    /// </summary>
    public sealed record ExecuteActionRequest(string? Action, string? Comment);
}
