namespace EdocsTestTask.Api.Authentication
{
    /// <summary>
    /// Error codes returned in the ServiceResponse envelope for authentication and role failures.
    /// </summary>
    public static class AuthErrorCodes
    {
        #region Codes

        public const string Unauthorized = "Auth.Unauthorized";

        public const string Forbidden = "Auth.Forbidden";

        #endregion
    }
}
