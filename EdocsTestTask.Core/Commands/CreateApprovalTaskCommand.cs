namespace EdocsTestTask.Core.Commands
{
    /// <summary>
    /// Input for creating an approval task. Fields are nullable: the service validates them.
    /// </summary>
    public sealed record CreateApprovalTaskCommand(string? DocumentNumber, string? Title, string? AssigneeId);
}
