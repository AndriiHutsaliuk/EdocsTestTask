using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using EdocsTestTask.Core.Constants;

namespace EdocsTestTask.Core.Helpers
{
    /// <summary>
    /// Reusable checks for text fields (title, short text, comment). Each returns false with a client-facing message.
    /// </summary>
    public static class TextValidationHelper
    {
        #region Methods

        /// <summary>
        /// Title must not be null or whitespace, and its trimmed length must be within
        /// <see cref="ValidationConstants.TitleMinLength"/>..<see cref="ValidationConstants.TitleMaxLength"/>.
        /// </summary>
        public static bool IsTitleValid(string? title, [NotNullWhen(false)] out string? error)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                error = ApprovalTaskMessages.TitleRequired;
                return false;
            }

            var length = title.Trim().Length;
            if (length < ValidationConstants.TitleMinLength || length > ValidationConstants.TitleMaxLength)
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    ApprovalTaskMessages.TitleLength,
                    ValidationConstants.TitleMinLength,
                    ValidationConstants.TitleMaxLength);
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Value must not be null, empty or whitespace.
        /// </summary>
        public static bool IsRequiredTextValid([NotNullWhen(true)] string? value, string fieldName, [NotNullWhen(false)] out string? error)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

            if (string.IsNullOrWhiteSpace(value))
            {
                error = string.Format(CultureInfo.InvariantCulture, ApprovalTaskMessages.FieldRequired, fieldName);
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Value must not be null, empty or whitespace, and its trimmed length must not exceed
        /// <see cref="ValidationConstants.ShortTextMaxLength"/>.
        /// </summary>
        public static bool IsShortTextValid([NotNullWhen(true)] string? value, string fieldName, [NotNullWhen(false)] out string? error)
        {
            if (!IsRequiredTextValid(value, fieldName, out error))
            {
                return false;
            }

            if (value.Trim().Length > ValidationConstants.ShortTextMaxLength)
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    ApprovalTaskMessages.FieldTooLong,
                    fieldName,
                    ValidationConstants.ShortTextMaxLength);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Comment is optional; when present, its trimmed length must not exceed
        /// <see cref="ValidationConstants.CommentMaxLength"/>.
        /// </summary>
        public static bool IsCommentValid(string? comment, [NotNullWhen(false)] out string? error)
        {
            if (comment is not null && comment.Trim().Length > ValidationConstants.CommentMaxLength)
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    ApprovalTaskMessages.FieldTooLong,
                    "Comment",
                    ValidationConstants.CommentMaxLength);
                return false;
            }

            error = null;
            return true;
        }

        #endregion
    }
}
