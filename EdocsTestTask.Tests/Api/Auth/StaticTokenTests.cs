using System.Net;
using System.Text.RegularExpressions;
using EdocsTestTask.Api.Authentication;
using EdocsTestTask.Core.Constants;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit.Abstractions;

namespace EdocsTestTask.Tests.Api.Auth
{
    /// <summary>
    /// Guards the committed static tokens in EdocsTestTask.Api.http against drift from the signing key, and regenerates them on demand.
    /// </summary>
    public class StaticTokenTests(AuthApiFactory factory, ITestOutputHelper output) : IClassFixture<AuthApiFactory>
    {
        #region Constants

        private static readonly DateTime StaticTokenExpiry = new(2027, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly string HttpFilePath = Path.Combine(AppContext.BaseDirectory, "TestData", "EdocsTestTask.Api.http");

        #endregion

        #region Properties

        public static TheoryData<string, string, string> StaticUsers => new()
        {
            { "authorToken", "author-1", UserRoles.Author },
            { "approverToken", "approver-1", UserRoles.Approver },
            { "otherApproverToken", "approver-2", UserRoles.Approver }
        };

        #endregion

        #region Tests

        [Theory]
        [MemberData(nameof(StaticUsers))]
        public async Task CommittedStaticToken_IsAcceptedAndCarriesExpectedClaims(string variable, string userId, string role)
        {
            var token = ReadHttpFileVariable(variable);

            var jwt = new JsonWebToken(token);
            Assert.Equal(userId, jwt.Subject);
            Assert.Equal(role, jwt.GetPayloadValue<string>(AuthClaimTypes.Role));
            Assert.Equal(StaticTokenExpiry, jwt.ValidTo);

            var response = await factory.CreateClient().GetWithTokenAsync($"{AuthProbeController.BaseRoute}/any", token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(userId, await AuthTestHelpers.ReadUserIdAsync(response));
        }

        [Theory(Skip = "Manual: remove Skip to print fresh static tokens, then paste them into EdocsTestTask.Api.http and README.md")]
        [MemberData(nameof(StaticUsers))]
        public void GenerateStaticToken(string variable, string userId, string role)
        {
            var token = new TestJwtFactory(factory.JwtOptions).Create(userId, role, expires: StaticTokenExpiry);

            output.WriteLine($"@{variable} = {token}");
        }

        #endregion

        #region Private Methods

        private static string ReadHttpFileVariable(string name)
        {
            var match = Regex.Match(
                File.ReadAllText(HttpFilePath),
                $@"^@{Regex.Escape(name)}\s*=\s*(?<value>\S+)\s*$",
                RegexOptions.Multiline);

            Assert.True(match.Success, $"Variable @{name} not found in {HttpFilePath}");
            return match.Groups["value"].Value;
        }

        #endregion
    }
}
