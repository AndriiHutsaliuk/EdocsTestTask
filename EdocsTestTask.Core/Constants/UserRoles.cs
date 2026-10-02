namespace EdocsTestTask.Core.Constants
{
    /// <summary>
    /// Roles carried in the JWT "role" claim.
    /// </summary>
    public static class UserRoles
    {
        #region Roles

        /// <summary>
        /// Creates approval tasks.
        /// </summary>
        public const string Author = "Author";

        /// <summary>
        /// Performs actions on approval tasks assigned to them.
        /// </summary>
        public const string Approver = "Approver";

        #endregion

        #region Sets

        /// <summary>
        /// Every role a valid token may carry. Tokens with any other role are rejected.
        /// </summary>
        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Author, Approver };

        #endregion
    }
}
