using EdocsTestTask.Core.Enums;

namespace EdocsTestTask.Core.Entities
{
    /// <summary>
    /// A document waiting for a decision by its assignee. Allowed status transitions are defined by the approval task service's transition table.
    /// </summary>
    public sealed class ApprovalTask
    {
        #region Properties

        public Guid Id { get; init; }

        public required string DocumentNumber { get; init; }

        public required string Title { get; init; }

        public required string AssigneeId { get; init; }

        public ApprovalStatus Status { get; set; }

        public DateTime CreatedAtUtc { get; init; }

        public DateTime UpdatedAtUtc { get; set; }

        public List<ApprovalHistoryEntry> History { get; init; } = [];

        #endregion

        #region Methods

        /// <summary>
        /// Returns an independent copy. History entries are immutable, so copying the list is enough.
        /// </summary>
        public ApprovalTask Clone() => new()
        {
            Id = Id,
            DocumentNumber = DocumentNumber,
            Title = Title,
            AssigneeId = AssigneeId,
            Status = Status,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = UpdatedAtUtc,
            History = [.. History]
        };

        #endregion
    }
}
