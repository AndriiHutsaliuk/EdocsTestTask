using EdocsTestTask.Core.Commands;
using EdocsTestTask.Core.Enums;
using EdocsTestTask.Infrastructure.Services.ApprovalTasks;

namespace EdocsTestTask.Tests.Infrastructure.ApprovalTasks
{
    public class ApprovalTaskServiceHelperCreateTests
    {
        #region Fields

        private static readonly DateTime NowUtc = new(2026, 10, 2, 9, 30, 0, DateTimeKind.Utc);

        #endregion

        #region Tests

        [Fact]
        public void Create_ValidCommand_ReturnsTrimmedAssignedTaskWithEqualDatesAndEmptyHistory()
        {
            var task = ApprovalTaskServiceHelper.Create(new CreateApprovalTaskCommand("  DOC-001 ", " Supply contract ", " approver-1 "), NowUtc);

            Assert.NotEqual(Guid.Empty, task.Id);
            Assert.Equal("DOC-001", task.DocumentNumber);
            Assert.Equal("Supply contract", task.Title);
            Assert.Equal("approver-1", task.AssigneeId);
            Assert.Equal(ApprovalStatus.Assigned, task.Status);
            Assert.Equal(NowUtc, task.CreatedAtUtc);
            Assert.Equal(NowUtc, task.UpdatedAtUtc);
            Assert.Empty(task.History);
        }

        [Fact]
        public void Create_CalledTwice_GivesDistinctIds()
        {
            var command = new CreateApprovalTaskCommand("DOC-001", "Supply contract", "approver-1");

            Assert.NotEqual(ApprovalTaskServiceHelper.Create(command, NowUtc).Id, ApprovalTaskServiceHelper.Create(command, NowUtc).Id);
        }

        [Fact]
        public void Create_NullCommand_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ApprovalTaskServiceHelper.Create(null!, NowUtc));
        }

        [Theory]
        [InlineData(null, "Supply contract", "approver-1")]
        [InlineData("DOC-001", " ", "approver-1")]
        [InlineData("DOC-001", "Supply contract", "")]
        public void Create_UnvalidatedBlankField_Throws(string? documentNumber, string? title, string? assigneeId)
        {
            Assert.ThrowsAny<ArgumentException>(() =>
                ApprovalTaskServiceHelper.Create(new CreateApprovalTaskCommand(documentNumber, title, assigneeId), NowUtc));
        }

        #endregion
    }
}
