using System.Text;
using EdocsTestTask.Api.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace EdocsTestTask.Tests.Api.Auth
{
    /// <summary>
    /// Mints JWTs for tests: valid ones from the app's own <see cref="JwtOptions"/>, broken ones via overrides.
    /// </summary>
    public sealed class TestJwtFactory(JwtOptions options)
    {
        #region Fields

        private readonly JsonWebTokenHandler _handler = new();

        #endregion

        #region Methods

        /// <summary>
        /// Creates an HS256 token. A null <paramref name="subject"/> or <paramref name="role"/> omits that claim;
        /// a null <paramref name="issuer"/>, <paramref name="audience"/> or <paramref name="signingKey"/> uses the app's settings;
        /// a null <paramref name="expires"/> means one hour from now.
        /// </summary>
        public string Create(
            string? subject,
            string? role,
            DateTime? expires = null,
            string? issuer = null,
            string? audience = null,
            string? signingKey = null)
        {
            var now = DateTime.UtcNow;
            var expiresUtc = expires ?? now.AddHours(1);

            // An already-expired token still needs nbf/iat before exp.
            var issuedAt = expiresUtc < now ? expiresUtc.AddHours(-1) : now;

            var claims = new Dictionary<string, object>();
            if (subject is not null)
            {
                claims[AuthClaimTypes.Subject] = subject;
            }

            if (role is not null)
            {
                claims[AuthClaimTypes.Role] = role;
            }

            return _handler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = issuer ?? options.Issuer,
                Audience = audience ?? options.Audience,
                IssuedAt = issuedAt,
                NotBefore = issuedAt,
                Expires = expiresUtc,
                Claims = claims,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? options.SigningKey)),
                    SecurityAlgorithms.HmacSha256)
            });
        }

        /// <summary>
        /// Creates an unsigned token (header alg "none") with otherwise valid claims.
        /// </summary>
        public string CreateUnsigned(string subject, string role)
        {
            var expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
            var header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""");
            var payload = Base64UrlEncoder.Encode(
                $$"""{"sub":"{{subject}}","role":"{{role}}","iss":"{{options.Issuer}}","aud":"{{options.Audience}}","exp":{{expires}}}""");

            return $"{header}.{payload}.";
        }

        #endregion
    }
}
