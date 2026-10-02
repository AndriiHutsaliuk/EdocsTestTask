namespace EdocsTestTask.Core.Constants
{
    /// <summary>
    /// Default field rules used by service Helper validation unless a feature overrides them.
    /// </summary>
    public static class ValidationConstants
    {
        #region Title

        public const int TitleMinLength = 3;

        /// <summary>
        /// Raised from the 30-character default: approval task titles are document names.
        /// </summary>
        public const int TitleMaxLength = 256;

        #endregion

        #region ShortText

        /// <summary>
        /// Upper bound for short identifiers such as a document number.
        /// </summary>
        public const int ShortTextMaxLength = 128;

        #endregion

        #region Comment

        /// <summary>
        /// Upper bound for an optional action comment.
        /// </summary>
        public const int CommentMaxLength = 4096;

        #endregion
    }
}
