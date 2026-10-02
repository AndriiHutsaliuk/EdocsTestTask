using System.Net;
using EdocsTestTask.Api.Authentication;
using EdocsTestTask.Core.Constants;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace EdocsTestTask.Tests.Api.Auth
{
    public class AuthenticationTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
    {
        #region Constants

        private const string AnyRoute = $"{AuthProbeController.BaseRoute}/any";

        private const string ForeignKey = "a-completely-different-signing-key-0123456789";

        #endregion

        #region Properties

        /// <summary>
        /// Computed from the fixture: <c>factory</c> is also used inside tests, and a captured primary-constructor parameter must not also initialize a field (CS9124).
        /// </summary>
        private HttpClient Client => factory.CreateClient();

        private TestJwtFactory Tokens => new(factory.JwtOptions);

        #endregion

        #region Tests

        [Theory]
        [InlineData("missing")]
        [InlineData("garbage")]
        [InlineData("foreign-key")]
        [InlineData("expired")]
        [InlineData("wrong-issuer")]
        [InlineData("wrong-audience")]
        [InlineData("alg-none")]
        [InlineData("no-sub")]
        [InlineData("no-role")]
        [InlineData("unknown-role")]
        public async Task InvalidOrMissingToken_Returns401WithEnvelope(string tokenCase)
        {
            var token = tokenCase switch
            {
                "missing" => null,
                "garbage" => "not-a-jwt",
                "foreign-key" => Tokens.Create("author-1", UserRoles.Author, signingKey: ForeignKey),
                "expired" => Tokens.Create("author-1", UserRoles.Author, expires: DateTime.UtcNow.AddHours(-1)),
                "wrong-issuer" => Tokens.Create("author-1", UserRoles.Author, issuer: "someone-else"),
                "wrong-audience" => Tokens.Create("author-1", UserRoles.Author, audience: "someone-else"),
                "alg-none" => Tokens.CreateUnsigned("author-1", UserRoles.Author),
                "no-sub" => Tokens.Create(null, UserRoles.Author),
                "no-role" => Tokens.Create("author-1", null),
                "unknown-role" => Tokens.Create("author-1", "Admin"),
                _ => throw new ArgumentOutOfRangeException(nameof(tokenCase), tokenCase, null)
            };

            var response = await Client.GetWithTokenAsync(AnyRoute, token);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
            await AuthTestHelpers.AssertErrorAsync(response, AuthErrorCodes.Unauthorized);
        }

        [Theory]
        [InlineData("author", "author-1", UserRoles.Author)]
        [InlineData("approver", "approver-1", UserRoles.Approver)]
        [InlineData("any", "author-1", UserRoles.Author)]
        [InlineData("any", "approver-2", UserRoles.Approver)]
        public async Task ValidTokenWithAllowedRole_Returns200WithUserIdFromSub(string action, string userId, string role)
        {
            var response = await Client.GetWithTokenAsync($"{AuthProbeController.BaseRoute}/{action}", Tokens.Create(userId, role));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(userId, await AuthTestHelpers.ReadUserIdAsync(response));
        }

        [Theory]
        [InlineData("")]
        [InlineData("too-short-key")]
        public void MissingOrShortSigningKey_FailsStartup(string signingKey)
        {
            using var misconfiguredFactory = factory.WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        [$"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}"] = signingKey
                    })));

            var exception = Record.Exception(() => misconfiguredFactory.CreateClient());

            Assert.NotNull(exception);
            Assert.Contains(nameof(JwtOptions.SigningKey), exception.ToString());
        }

        #endregion
    }
}
