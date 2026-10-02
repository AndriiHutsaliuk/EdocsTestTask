namespace EdocsTestTask.Api.Contracts.ApprovalTasks
{
    /// <summary>
    /// Approval task as returned to clients. Status and actions are names, e.g. "InProgress".
    /// </summary>
    public sealed record ApprovalTaskResponse(
        Guid Id,
        string DocumentNumber,
        string Title,
        string AssigneeId,
        string Status,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc,
        IReadOnlyList<ApprovalHistoryEntryResponse> History);
}
