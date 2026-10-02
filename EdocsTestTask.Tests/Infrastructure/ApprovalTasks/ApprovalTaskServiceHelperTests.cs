using EdocsTestTask.Core.Constants;
using EdocsTestTask.Core.Enums;
using EdocsTestTask.Infrastructure.Services.ApprovalTasks;
using EdocsTestTask.Shared.Results;

namespace EdocsTestTask.Tests.Infrastructure.ApprovalTasks
{
    public class ApprovalTaskServiceHelperTests
    {
        #region Tests

        [Theory]
        [InlineData(ApprovalStatus.Assigned, ApprovalAction.Start, null, ApprovalStatus.InProgress)]
        [InlineData(ApprovalStatus.InProgress, ApprovalAction.Approve, null, ApprovalStatus.Approved)]
        [InlineData(ApprovalStatus.InProgress, ApprovalAction.Reject, "Missing signature", ApprovalStatus.Rejected)]
        public void ResolveNextStatus_AllowedTransition_ReturnsNextStatus(
            ApprovalStatus status, ApprovalAction action, string? comment, ApprovalStatus expected)
        {
            var result = ApprovalTaskServiceHelper.ResolveNextStatus(status, action, comment);

            Assert.True(result.IsSuccess);
            Assert.Equal(expected, result.Value);
        }

        [Theory]
        [InlineData(ApprovalStatus.Assigned, ApprovalAction.Approve, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.InvalidTransition)]
        [InlineData(ApprovalStatus.Assigned, ApprovalAction.Reject, "Bad", ErrorType.Conflict, ApprovalTaskMessages.Codes.InvalidTransition)]
        [InlineData(ApprovalStatus.Assigned, ApprovalAction.Reject, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.InvalidTransition)]
        [InlineData(ApprovalStatus.InProgress, ApprovalAction.Start, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.InvalidTransition)]
        [InlineData(ApprovalStatus.InProgress, ApprovalAction.Reject, null, ErrorType.Validation, ApprovalTaskMessages.Codes.RejectCommentRequired)]
        [InlineData(ApprovalStatus.InProgress, ApprovalAction.Reject, "", ErrorType.Validation, ApprovalTaskMessages.Codes.RejectCommentRequired)]
        [InlineData(ApprovalStatus.InProgress, ApprovalAction.Reject, "   ", ErrorType.Validation, ApprovalTaskMessages.Codes.RejectCommentRequired)]
        [InlineData(ApprovalStatus.Approved, ApprovalAction.Start, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Approved, ApprovalAction.Approve, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Approved, ApprovalAction.Reject, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Rejected, ApprovalAction.Start, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Rejected, ApprovalAction.Approve, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Rejected, ApprovalAction.Reject, "Bad", ErrorType.Conflict, ApprovalTaskMessages.Codes.AlreadyFinalized)]
        public void ResolveNextStatus_NotAllowed_ReturnsSingleError(
            ApprovalStatus status, ApprovalAction action, string? comment, ErrorType type, string code)
        {
            var result = ApprovalTaskServiceHelper.ResolveNextStatus(status, action, comment);

            Assert.True(result.IsFailure);
            var error = Assert.Single(result.Errors);
            Assert.Equal(type, error.Type);
            Assert.Equal(code, error.Code);
        }

        [Fact]
        public void ResolveNextStatus_InvalidTransition_MessageNamesActionAndStatus()
        {
            var result = ApprovalTaskServiceHelper.ResolveNextStatus(ApprovalStatus.Assigned, ApprovalAction.Approve, null);

            Assert.Equal("Action 'Approve' is not allowed when task is in status 'Assigned'.", result.Error!.Message);
        }

        [Fact]
        public void ResolveNextStatus_Finalized_MessageNamesStatus()
        {
            var result = ApprovalTaskServiceHelper.ResolveNextStatus(ApprovalStatus.Rejected, ApprovalAction.Start, null);

            Assert.Equal("Task is already Rejected; no further actions are allowed.", result.Error!.Message);
        }

        [Fact]
        public void ResolveNextStatus_AcrossAllStatusesAndActions_AllowsExactlyThreeTransitions()
        {
            var allowed =
                from status in Enum.GetValues<ApprovalStatus>()
                from action in Enum.GetValues<ApprovalAction>()
                where ApprovalTaskServiceHelper.ResolveNextStatus(status, action, "comment").IsSuccess
                select (status, action);

            Assert.Equal(
                new[]
                {
                    (ApprovalStatus.Assigned, ApprovalAction.Start),
                    (ApprovalStatus.InProgress, ApprovalAction.Approve),
                    (ApprovalStatus.InProgress, ApprovalAction.Reject)
                },
                allowed.ToArray());
        }

        #endregion
    }
}
