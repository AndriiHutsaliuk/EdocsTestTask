using System.Security.Claims;

namespace EdocsTestTask.Api.Authentication
{
    /// <summary>
    /// Reads the authenticated caller's identity from JWT claims.
    /// </summary>
    public static class ClaimsPrincipalExtensions
    {
        #region Methods

        /// <summary>
        /// Returns the "sub" claim. Token validation guarantees it on endpoints protected by [Authorize].
        /// </summary>
        /// <exception cref="InvalidOperationException">The principal has no "sub" claim, i.e. the endpoint is not protected.</exception>
        public static string GetUserId(this ClaimsPrincipal principal)
        {
            ArgumentNullException.ThrowIfNull(principal);

            var userId = principal.FindFirstValue(AuthClaimTypes.Subject);

            return string.IsNullOrWhiteSpace(userId)
                ? throw new InvalidOperationException($"Authenticated user has no '{AuthClaimTypes.Subject}' claim. Is the endpoint protected with [Authorize]?")
                : userId;
        }

        #endregion
    }
}
