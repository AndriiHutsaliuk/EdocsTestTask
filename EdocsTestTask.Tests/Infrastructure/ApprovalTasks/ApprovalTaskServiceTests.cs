using EdocsTestTask.Core.Commands;
using EdocsTestTask.Core.Constants;
using EdocsTestTask.Core.Entities;
using EdocsTestTask.Core.Enums;
using EdocsTestTask.Infrastructure.Repositories;
using EdocsTestTask.Infrastructure.Services.ApprovalTasks;
using EdocsTestTask.Shared.Results;
using EdocsTestTask.Tests.TestData;

namespace EdocsTestTask.Tests.Infrastructure.ApprovalTasks
{
    public class ApprovalTaskServiceTests
    {
        #region Constants

        private const string Assignee = ApprovalTaskTestData.AssigneeId;

        #endregion

        #region Fields

        private static readonly DateTime StartUtc = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

        private readonly TestTimeProvider _time = new(new DateTimeOffset(StartUtc));

        private readonly InMemoryApprovalTaskRepository _repository = new();

        private readonly ApprovalTaskService _service;

        #endregion

        #region Constructors

        public ApprovalTaskServiceTests()
        {
            _service = new ApprovalTaskService(_repository, _time);
        }

        #endregion

        #region Helpers

        private ApprovalTask CreateTask() =>
            _service.Create(new CreateApprovalTaskCommand("DOC-001", "Supply contract", Assignee)).Value;

        /// <summary>
        /// Advances the clock by one minute before each action, so every change gets a distinct timestamp.
        /// </summary>
        private Result<ApprovalTask> Act(Guid id, string? action, string? comment = null, string? actor = Assignee)
        {
            _time.Advance(TimeSpan.FromMinutes(1));
            return _service.ExecuteAction(id, new ExecuteActionCommand(actor, action, comment));
        }

        private ApprovalTask MoveTo(ApprovalStatus status)
        {
            var task = CreateTask();
            if (status != ApprovalStatus.Assigned)
            {
                Assert.True(Act(task.Id, "Start").IsSuccess);
            }

            if (status == ApprovalStatus.Approved)
            {
                Assert.True(Act(task.Id, "Approve").IsSuccess);
            }
            else if (status == ApprovalStatus.Rejected)
            {
                Assert.True(Act(task.Id, "Reject", "Missing signature").IsSuccess);
            }

            return _repository.GetById(task.Id)!;
        }

        private void AssertUnchanged(ApprovalTask before)
        {
            var after = _repository.GetById(before.Id)!;
            Assert.Equal(before.Status, after.Status);
            Assert.Equal(before.UpdatedAtUtc, after.UpdatedAtUtc);
            Assert.Equal(before.History, after.History);
        }

        private static void AssertError(Result result, ErrorType type, string code)
        {
            Assert.True(result.IsFailure);
            Assert.Equal(type, result.Error!.Type);
            Assert.Equal(code, result.Error.Code);
        }

        #endregion

        #region Tests

        [Fact]
        public void Create_ValidCommand_ReturnsAssignedTaskWithEqualUtcDatesAndEmptyHistory()
        {
            var result = _service.Create(new CreateApprovalTaskCommand("  DOC-001 ", " Supply contract ", " approver-1 "));

            Assert.True(result.IsSuccess);
            var task = result.Value;
            Assert.NotEqual(Guid.Empty, task.Id);
            Assert.Equal("DOC-001", task.DocumentNumber);
            Assert.Equal("Supply contract", task.Title);
            Assert.Equal("approver-1", task.AssigneeId);
            Assert.Equal(ApprovalStatus.Assigned, task.Status);
            Assert.Equal(StartUtc, task.CreatedAtUtc);
            Assert.Equal(DateTimeKind.Utc, task.CreatedAtUtc.Kind);
            Assert.Equal(task.CreatedAtUtc, task.UpdatedAtUtc);
            Assert.Empty(task.History);
            Assert.Equal(ApprovalStatus.Assigned, _service.GetById(task.Id).Value.Status);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("ab")]
        public void Create_InvalidTitle_ReturnsValidationError(string? title)
        {
            var result = _service.Create(new CreateApprovalTaskCommand("DOC-001", title, Assignee));

            AssertError(result, ErrorType.Validation, ApprovalTaskMessages.Codes.InvalidTitle);
        }

        [Fact]
        public void Create_TooLongTitle_ReturnsValidationError()
        {
            var result = _service.Create(new CreateApprovalTaskCommand("DOC-001", new string('a', ValidationConstants.TitleMaxLength + 1), Assignee));

            AssertError(result, ErrorType.Validation, ApprovalTaskMessages.Codes.InvalidTitle);
        }

        [Fact]
        public void Create_TooLongDocumentNumber_ReturnsValidationError()
        {
            var documentNumber = new string('7', ValidationConstants.ShortTextMaxLength + 1);

            var result = _service.Create(new CreateApprovalTaskCommand(documentNumber, "Supply contract", Assignee));

            AssertError(result, ErrorType.Validation, ApprovalTaskMessages.Codes.InvalidDocumentNumber);
            Assert.Equal("DocumentNumber must be 128 characters or fewer.", result.Error!.Message);
        }

        [Fact]
        public void Create_DocumentNumberAtLimit_Succeeds()
        {
            var documentNumber = new string('7', ValidationConstants.ShortTextMaxLength);

            var result = _service.Create(new CreateApprovalTaskCommand(documentNumber, "Supply contract", Assignee));

            Assert.True(result.IsSuccess);
            Assert.Equal(documentNumber, result.Value.DocumentNumber);
        }

        [Fact]
        public void Create_AssigneeIdAtLimit_Succeeds()
        {
            var assigneeId = new string('a', ValidationConstants.ShortTextMaxLength);

            var result = _service.Create(new CreateApprovalTaskCommand("DOC-001", "Supply contract", assigneeId));

            Assert.True(result.IsSuccess);
            Assert.Equal(assigneeId, result.Value.AssigneeId);
        }

        [Fact]
        public void Create_TooLongAssigneeId_ReturnsValidationError()
        {
            var assigneeId = new string('a', ValidationConstants.ShortTextMaxLength + 1);

            var result = _service.Create(new CreateApprovalTaskCommand("DOC-001", "Supply contract", assigneeId));

            AssertError(result, ErrorType.Validation, ApprovalTaskMessages.Codes.InvalidAssignee);
            Assert.Equal("AssigneeId must be 128 characters or fewer.", result.Error!.Message);
        }

        [Fact]
        public void Create_AllFieldsMissing_ReportsEveryField()
        {
            var result = _service.Create(new CreateApprovalTaskCommand(" ", null, ""));

            Assert.True(result.IsFailure);
            Assert.Equal(
                new[] { ApprovalTaskMessages.Codes.InvalidTitle, ApprovalTaskMessages.Codes.InvalidDocumentNumber, ApprovalTaskMessages.Codes.InvalidAssignee },
                result.Errors.Select(e => e.Code));
            Assert.All(result.Errors, e => Assert.Equal(ErrorType.Validation, e.Type));
        }

        [Fact]
        public void ExecuteAction_StartThenApprove_CompletesWithHistory()
        {
            var created = CreateTask();

            Assert.True(Act(created.Id, "Start").IsSuccess);
            var result = Act(created.Id, "Approve", "  Looks good  ");

            Assert.True(result.IsSuccess);
            var task = result.Value;
            Assert.Equal(ApprovalStatus.Approved, task.Status);
            Assert.Equal(StartUtc, task.CreatedAtUtc);
            Assert.Equal(StartUtc.AddMinutes(2), task.UpdatedAtUtc);
            Assert.Collection(task.History,
                e => Assert.Equal(new ApprovalHistoryEntry(ApprovalAction.Start, Assignee, null, StartUtc.AddMinutes(1)), e),
                e => Assert.Equal(new ApprovalHistoryEntry(ApprovalAction.Approve, Assignee, "Looks good", StartUtc.AddMinutes(2)), e));

            var stored = _service.GetById(created.Id).Value;
            Assert.Equal(task.Status, stored.Status);
            Assert.Equal(task.UpdatedAtUtc, stored.UpdatedAtUtc);
            Assert.Equal(task.History, stored.History);
        }

        [Fact]
        public void ExecuteAction_StartThenReject_CompletesWithHistory()
        {
            var created = CreateTask();

            Assert.True(Act(created.Id, "Start").IsSuccess);
            var result = Act(created.Id, "Reject", "Missing signature");

            Assert.True(result.IsSuccess);
            Assert.Equal(ApprovalStatus.Rejected, result.Value.Status);
            Assert.Collection(result.Value.History,
                e => Assert.Equal(ApprovalAction.Start, e.Action),
                e => Assert.Equal(new ApprovalHistoryEntry(ApprovalAction.Reject, Assignee, "Missing signature", StartUtc.AddMinutes(2)), e));
            Assert.Equal(ApprovalStatus.Rejected, _service.GetById(created.Id).Value.Status);
        }

        [Fact]
        public void ExecuteAction_WhitespaceComment_IsStoredAsNull()
        {
            var created = CreateTask();
            Assert.True(Act(created.Id, "Start").IsSuccess);

            var result = Act(created.Id, "Approve", "   ");

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.History[^1].Comment);
        }

        [Theory]
        [InlineData(ApprovalStatus.Assigned, "Approve", ApprovalTaskMessages.Codes.InvalidTransition)]
        [InlineData(ApprovalStatus.Assigned, "Reject", ApprovalTaskMessages.Codes.InvalidTransition)]
        [InlineData(ApprovalStatus.InProgress, "Start", ApprovalTaskMessages.Codes.InvalidTransition)]
        [InlineData(ApprovalStatus.Approved, "Start", ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Approved, "Approve", ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Approved, "Reject", ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Rejected, "Start", ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Rejected, "Approve", ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Rejected, "Reject", ApprovalTaskMessages.Codes.AlreadyFinalized)]
        public void ExecuteAction_NotAllowedTransition_ReturnsConflictAndKeepsState(ApprovalStatus status, string action, string code)
        {
            var before = MoveTo(status);

            var result = Act(before.Id, action, "Some comment");

            AssertError(result, ErrorType.Conflict, code);
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData(ApprovalStatus.Assigned, "Start")]
        [InlineData(ApprovalStatus.InProgress, "Approve")]
        [InlineData(ApprovalStatus.InProgress, "Reject")]
        [InlineData(ApprovalStatus.Approved, "Start")]
        public void ExecuteAction_OtherActor_ReturnsForbiddenAndKeepsState(ApprovalStatus status, string action)
        {
            var before = MoveTo(status);

            var result = Act(before.Id, action, "Some comment", ApprovalTaskTestData.OtherUserId);

            AssertError(result, ErrorType.Forbidden, ApprovalTaskMessages.Codes.NotAssignee);
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ExecuteAction_RejectWithoutComment_ReturnsValidationAndKeepsState(string? comment)
        {
            var before = MoveTo(ApprovalStatus.InProgress);

            var result = Act(before.Id, "Reject", comment);

            AssertError(result, ErrorType.Validation, ApprovalTaskMessages.Codes.RejectCommentRequired);
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData(null, ApprovalTaskMessages.Codes.ActionRequired)]
        [InlineData("", ApprovalTaskMessages.Codes.ActionRequired)]
        [InlineData("  ", ApprovalTaskMessages.Codes.ActionRequired)]
        [InlineData("Delete", ApprovalTaskMessages.Codes.UnknownAction)]
        [InlineData("1", ApprovalTaskMessages.Codes.UnknownAction)]
        [InlineData("Start,Approve", ApprovalTaskMessages.Codes.UnknownAction)]
        public void ExecuteAction_MissingOrUnknownAction_ReturnsValidationAndKeepsState(string? action, string code)
        {
            var before = MoveTo(ApprovalStatus.Assigned);

            var result = Act(before.Id, action);

            AssertError(result, ErrorType.Validation, code);
            AssertUnchanged(before);
        }

        [Fact]
        public void ExecuteAction_UnknownAction_MessageNamesAction()
        {
            var task = CreateTask();

            var result = Act(task.Id, "Delete");

            Assert.Equal("Unknown action 'Delete'. Allowed: Start, Approve, Reject.", result.Error!.Message);
        }

        [Fact]
        public void ExecuteAction_ActionNameIsCaseInsensitive()
        {
            var task = CreateTask();

            var result = Act(task.Id, "start");

            Assert.True(result.IsSuccess);
            Assert.Equal(ApprovalStatus.InProgress, result.Value.Status);
        }

        [Fact]
        public void ExecuteAction_TooLongComment_ReturnsValidationAndKeepsState()
        {
            var before = MoveTo(ApprovalStatus.InProgress);

            var result = Act(before.Id, "Reject", new string('c', ValidationConstants.CommentMaxLength + 1));

            AssertError(result, ErrorType.Validation, ApprovalTaskMessages.Codes.InvalidComment);
            Assert.Equal("Comment must be 4096 characters or fewer.", result.Error!.Message);
            AssertUnchanged(before);
        }

        [Fact]
        public void ExecuteAction_CommentAtLimit_Succeeds()
        {
            var before = MoveTo(ApprovalStatus.InProgress);
            var comment = new string('c', ValidationConstants.CommentMaxLength);

            var result = Act(before.Id, "Reject", "  " + comment + "  ");

            Assert.True(result.IsSuccess);
            Assert.Equal(comment, result.Value.History[^1].Comment);
        }

        [Fact]
        public void ExecuteAction_BlankActor_ReturnsValidation()
        {
            var before = MoveTo(ApprovalStatus.Assigned);

            var result = Act(before.Id, "Start", actor: " ");

            AssertError(result, ErrorType.Validation, ApprovalTaskMessages.Codes.InvalidActor);
            AssertUnchanged(before);
        }

        [Fact]
        public void ExecuteAction_MissingTask_ReturnsNotFound()
        {
            var result = Act(Guid.NewGuid(), "Start");

            AssertError(result, ErrorType.NotFound, ApprovalTaskMessages.Codes.NotFound);
        }

        [Fact]
        public void ExecuteAction_InvalidCommandOnMissingTask_ReturnsValidationFirst()
        {
            var result = Act(Guid.NewGuid(), "Delete");

            AssertError(result, ErrorType.Validation, ApprovalTaskMessages.Codes.UnknownAction);
        }

        [Fact]
        public void GetById_MissingTask_ReturnsNotFound()
        {
            var id = Guid.NewGuid();

            var result = _service.GetById(id);

            AssertError(result, ErrorType.NotFound, ApprovalTaskMessages.Codes.NotFound);
            Assert.Equal($"Approval task '{id}' was not found.", result.Error!.Message);
        }

        #endregion
    }
}
