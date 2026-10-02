namespace EdocsTestTask.Core.Constants
{
    /// <summary>
    /// Client-facing messages and error codes for approval tasks.
    /// Templates take arguments via <c>string.Format(CultureInfo.InvariantCulture, ...)</c>.
    /// </summary>
    public static class ApprovalTaskMessages
    {
        #region Messages

        public const string TitleRequired = "Title is required and cannot be empty or whitespace.";

        /// <summary>{0} = minimum length, {1} = maximum length.</summary>
        public const string TitleLength = "Title must be between {0} and {1} characters.";

        /// <summary>{0} = field name.</summary>
        public const string FieldRequired = "{0} is required and cannot be empty or whitespace.";

        /// <summary>{0} = field name, {1} = maximum length.</summary>
        public const string FieldTooLong = "{0} must be {1} characters or fewer.";

        public const string ActionRequired = "Action is required.";

        /// <summary>{0} = action as sent by the client.</summary>
        public const string UnknownAction = "Unknown action '{0}'. Allowed: Start, Approve, Reject.";

        /// <summary>{0} = task id.</summary>
        public const string TaskNotFound = "Approval task '{0}' was not found.";

        public const string NotAssignee = "Only the assigned user can perform actions on this task.";

        /// <summary>{0} = action, {1} = current status.</summary>
        public const string InvalidTransition = "Action '{0}' is not allowed when task is in status '{1}'.";

        public const string RejectCommentRequired = "A non-empty comment is required to reject a task.";

        /// <summary>{0} = final status.</summary>
        public const string TaskAlreadyFinalized = "Task is already {0}; no further actions are allowed.";

        #endregion

        #region Nested Types

        /// <summary>
        /// Stable machine-readable error codes.
        /// </summary>
        public static class Codes
        {
            public const string InvalidTitle = "ApprovalTask.InvalidTitle";

            public const string InvalidDocumentNumber = "ApprovalTask.InvalidDocumentNumber";

            public const string InvalidAssignee = "ApprovalTask.InvalidAssignee";

            public const string InvalidActor = "ApprovalTask.InvalidActor";

            public const string ActionRequired = "ApprovalTask.ActionRequired";

            public const string UnknownAction = "ApprovalTask.UnknownAction";

            public const string NotFound = "ApprovalTask.NotFound";

            public const string NotAssignee = "ApprovalTask.NotAssignee";

            public const string InvalidTransition = "ApprovalTask.InvalidTransition";

            public const string InvalidComment = "ApprovalTask.InvalidComment";

            public const string RejectCommentRequired = "ApprovalTask.RejectCommentRequired";

            public const string AlreadyFinalized = "ApprovalTask.AlreadyFinalized";
        }

        #endregion
    }
}
