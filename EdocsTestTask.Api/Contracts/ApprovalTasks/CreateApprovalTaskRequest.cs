namespace EdocsTestTask.Api.Contracts.ApprovalTasks
{
    /// <summary>
    /// Body of POST api/approval-tasks. Nullable so the service, not model binding, reports missing fields.
    /// </summary>
    public sealed record CreateApprovalTaskRequest(string? DocumentNumber, string? Title, string? AssigneeId);
}
