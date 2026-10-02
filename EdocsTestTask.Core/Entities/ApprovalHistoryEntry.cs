using EdocsTestTask.Core.Enums;

namespace EdocsTestTask.Core.Entities
{
    /// <summary>
    /// One successful action on an approval task. Immutable.
    /// </summary>
    public sealed record ApprovalHistoryEntry(ApprovalAction Action, string ActorId, string? Comment, DateTime AtUtc);
}
