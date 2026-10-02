using System.Globalization;
using EdocsTestTask.Core.Commands;
using EdocsTestTask.Core.Constants;
using EdocsTestTask.Core.Entities;
using EdocsTestTask.Core.Enums;
using EdocsTestTask.Core.Helpers;
using EdocsTestTask.Shared.Results;
using EdocsTestTask.Shared.Validation;

namespace EdocsTestTask.Infrastructure.Services.ApprovalTasks
{
    /// <summary>
    /// Input validation, new-task creation and the status transition table for <see cref="ApprovalTaskService"/>.
    /// </summary>
    public static class ApprovalTaskServiceHelper
    {
        #region Methods

        /// <summary>
        /// Title via <see cref="TextValidationHelper.IsTitleValid"/> (3..256 trimmed);
        /// DocumentNumber and AssigneeId via <see cref="TextValidationHelper.IsShortTextValid"/> (required, up to 128 trimmed).
        /// Reports every failing field.
        /// </summary>
        public static ValidationResult Validate(CreateApprovalTaskCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);

            var validation = new ValidationResult();

            if (!TextValidationHelper.IsTitleValid(command.Title, out var titleError))
            {
                validation.AddError(titleError, ApprovalTaskMessages.Codes.InvalidTitle);
            }

            if (!TextValidationHelper.IsShortTextValid(command.DocumentNumber, nameof(command.DocumentNumber), out var documentNumberError))
            {
                validation.AddError(documentNumberError, ApprovalTaskMessages.Codes.InvalidDocumentNumber);
            }

            if (!TextValidationHelper.IsShortTextValid(command.AssigneeId, nameof(command.AssigneeId), out var assigneeError))
            {
                validation.AddError(assigneeError, ApprovalTaskMessages.Codes.InvalidAssignee);
            }

            return validation;
        }

        /// <summary>
        /// Creates a task in <see cref="ApprovalStatus.Assigned"/> from an already validated command:
        /// trimmed text fields, <c>CreatedAtUtc == UpdatedAtUtc == nowUtc</c> and an empty history.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is null.</exception>
        /// <exception cref="ArgumentException">A text field is null, empty or whitespace: the command was not validated.</exception>
        public static ApprovalTask Create(CreateApprovalTaskCommand command, DateTime nowUtc)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentException.ThrowIfNullOrWhiteSpace(command.DocumentNumber);
            ArgumentException.ThrowIfNullOrWhiteSpace(command.Title);
            ArgumentException.ThrowIfNullOrWhiteSpace(command.AssigneeId);

            return new ApprovalTask
            {
                Id = Guid.NewGuid(),
                DocumentNumber = command.DocumentNumber.Trim(),
                Title = command.Title.Trim(),
                AssigneeId = command.AssigneeId.Trim(),
                Status = ApprovalStatus.Assigned,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
                History = []
            };
        }

        /// <summary>
        /// ActorId required; Action required and one of <see cref="ApprovalAction"/> names (case-insensitive);
        /// Comment optional, up to 4096 trimmed via <see cref="TextValidationHelper.IsCommentValid"/>.
        /// Whether the action is allowed for the task's status is decided later by the transition table.
        /// </summary>
        public static ValidationResult Validate(ExecuteActionCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);

            var validation = new ValidationResult();

            if (!TextValidationHelper.IsRequiredTextValid(command.ActorId, nameof(command.ActorId), out var actorError))
            {
                validation.AddError(actorError, ApprovalTaskMessages.Codes.InvalidActor);
            }

            if (string.IsNullOrWhiteSpace(command.Action))
            {
                validation.AddError(ApprovalTaskMessages.ActionRequired, ApprovalTaskMessages.Codes.ActionRequired);
            }
            else if (!TryParseAction(command.Action, out _))
            {
                validation.AddError(
                    string.Format(CultureInfo.InvariantCulture, ApprovalTaskMessages.UnknownAction, command.Action.Trim()),
                    ApprovalTaskMessages.Codes.UnknownAction);
            }

            if (!TextValidationHelper.IsCommentValid(command.Comment, out var commentError))
            {
                validation.AddError(commentError, ApprovalTaskMessages.Codes.InvalidComment);
            }

            return validation;
        }

        /// <summary>
        /// The whole workflow in one table:
        /// Assigned + Start → InProgress; InProgress + Approve → Approved; InProgress + Reject (non-empty comment) → Rejected.
        /// Checked in this order: a final status → Conflict AlreadyFinalized; a pair not in the table → Conflict InvalidTransition;
        /// Reject without a comment → Validation RejectCommentRequired.
        /// </summary>
        public static Result<ApprovalStatus> ResolveNextStatus(ApprovalStatus status, ApprovalAction action, string? comment)
        {
            if (status is ApprovalStatus.Approved or ApprovalStatus.Rejected)
            {
                return Error.Conflict(
                    ApprovalTaskMessages.Codes.AlreadyFinalized,
                    string.Format(CultureInfo.InvariantCulture, ApprovalTaskMessages.TaskAlreadyFinalized, status));
            }

            ApprovalStatus? nextStatus = (status, action) switch
            {
                (ApprovalStatus.Assigned, ApprovalAction.Start) => ApprovalStatus.InProgress,
                (ApprovalStatus.InProgress, ApprovalAction.Approve) => ApprovalStatus.Approved,
                (ApprovalStatus.InProgress, ApprovalAction.Reject) => ApprovalStatus.Rejected,
                _ => null
            };

            if (nextStatus is null)
            {
                return Error.Conflict(
                    ApprovalTaskMessages.Codes.InvalidTransition,
                    string.Format(CultureInfo.InvariantCulture, ApprovalTaskMessages.InvalidTransition, action, status));
            }

            if (action == ApprovalAction.Reject && string.IsNullOrWhiteSpace(comment))
            {
                return Error.Validation(ApprovalTaskMessages.Codes.RejectCommentRequired, ApprovalTaskMessages.RejectCommentRequired);
            }

            return nextStatus.Value;
        }

        /// <summary>
        /// Matches defined names only. Unlike <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/>,
        /// numbers ("1") and flag lists ("Start,Approve") are rejected.
        /// </summary>
        public static bool TryParseAction(string? value, out ApprovalAction action)
        {
            action = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            var name = Enum.GetNames<ApprovalAction>()
                .FirstOrDefault(n => string.Equals(n, trimmed, StringComparison.OrdinalIgnoreCase));

            return name is not null && Enum.TryParse(name, out action);
        }

        #endregion
    }
}
