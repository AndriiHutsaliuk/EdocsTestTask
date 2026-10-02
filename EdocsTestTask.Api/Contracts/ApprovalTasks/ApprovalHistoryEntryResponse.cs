namespace EdocsTestTask.Api.Contracts.ApprovalTasks
{
    /// <summary>
    /// One successful action in a task's history.
    /// </summary>
    public sealed record ApprovalHistoryEntryResponse(string Action, string ActorId, string? Comment, DateTime AtUtc);
}
