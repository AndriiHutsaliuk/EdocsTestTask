using EdocsTestTask.Core.Entities;
using EdocsTestTask.Core.Enums;

namespace EdocsTestTask.Tests.TestData
{
    /// <summary>
    /// Builds approval tasks in a given status without going through the service.
    /// </summary>
    public static class ApprovalTaskTestData
    {
        #region Constants

        public const string AssigneeId = "approver-1";

        public const string OtherUserId = "approver-2";

        #endregion

        #region Fields

        public static readonly DateTime CreatedAtUtc = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

        #endregion

        #region Methods

        public static ApprovalTask Create(ApprovalStatus status = ApprovalStatus.Assigned) => new()
        {
            Id = Guid.NewGuid(),
            DocumentNumber = "DOC-001",
            Title = "Supply contract",
            AssigneeId = AssigneeId,
            Status = status,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
            History = []
        };

        #endregion
    }
}
