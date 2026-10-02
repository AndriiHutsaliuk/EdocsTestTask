namespace EdocsTestTask.Core.Enums
{
    /// <summary>
    /// Lifecycle status of an approval task. Approved and Rejected are final.
    /// </summary>
    public enum ApprovalStatus
    {
        Assigned = 1,
        InProgress = 2,
        Approved = 3,
        Rejected = 4
    }
}
