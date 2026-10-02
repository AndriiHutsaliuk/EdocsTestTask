namespace EdocsTestTask.Api.Authentication
{
    /// <summary>
    /// JWT validation settings bound from the "Jwt" configuration section. Required values are enforced by <see cref="JwtOptionsValidator"/>.
    /// </summary>
    public sealed class JwtOptions
    {
        #region Constants

        public const string SectionName = "Jwt";

        /// <summary>
        /// HS256 needs a key of at least 256 bits.
        /// </summary>
        public const int MinSigningKeyBytes = 32;

        #endregion

        #region Properties

        public string Issuer { get; set; } = string.Empty;

        public string Audience { get; set; } = string.Empty;

        public string SigningKey { get; set; } = string.Empty;

        #endregion
    }
}
