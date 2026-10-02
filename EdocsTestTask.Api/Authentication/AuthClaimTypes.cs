namespace EdocsTestTask.Api.Authentication
{
    /// <summary>
    /// JWT claim names used by the API. Inbound claim mapping is disabled, so these are the raw JWT names.
    /// </summary>
    public static class AuthClaimTypes
    {
        #region Claims

        public const string Subject = "sub";

        public const string Role = "role";

        #endregion
    }
}
