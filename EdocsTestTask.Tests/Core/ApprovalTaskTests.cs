using EdocsTestTask.Core.Entities;
using EdocsTestTask.Core.Enums;
using EdocsTestTask.Tests.TestData;

namespace EdocsTestTask.Tests.Core
{
    public class ApprovalTaskTests
    {
        #region Tests

        [Fact]
        public void Clone_CopiesAllProperties()
        {
            var original = ApprovalTaskTestData.Create(ApprovalStatus.InProgress);
            original.History.Add(new ApprovalHistoryEntry(ApprovalAction.Start, "approver-1", null, original.CreatedAtUtc));

            var clone = original.Clone();

            Assert.NotSame(original, clone);
            Assert.Equal(original.Id, clone.Id);
            Assert.Equal(original.DocumentNumber, clone.DocumentNumber);
            Assert.Equal(original.Title, clone.Title);
            Assert.Equal(original.AssigneeId, clone.AssigneeId);
            Assert.Equal(original.Status, clone.Status);
            Assert.Equal(original.CreatedAtUtc, clone.CreatedAtUtc);
            Assert.Equal(original.UpdatedAtUtc, clone.UpdatedAtUtc);
            Assert.NotSame(original.History, clone.History);
            Assert.Equal(original.History, clone.History);
        }

        [Fact]
        public void Clone_MutatingCopy_LeavesOriginalUntouched()
        {
            var original = ApprovalTaskTestData.Create();

            var clone = original.Clone();
            clone.Status = ApprovalStatus.InProgress;
            clone.UpdatedAtUtc = clone.UpdatedAtUtc.AddMinutes(1);
            clone.History.Add(new ApprovalHistoryEntry(ApprovalAction.Start, "approver-1", null, clone.UpdatedAtUtc));

            Assert.Equal(ApprovalStatus.Assigned, original.Status);
            Assert.Equal(ApprovalTaskTestData.CreatedAtUtc, original.UpdatedAtUtc);
            Assert.Empty(original.History);
        }

        #endregion
    }
}
