using System.Net;
using EdocsTestTask.Api.Authentication;
using EdocsTestTask.Core.Constants;

namespace EdocsTestTask.Tests.Api.Auth
{
    public class AuthorizationTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
    {
        #region Constants

        private const string FallbackRoute = $"{AuthProbeController.BaseRoute}/fallback";

        #endregion

        #region Fields

        private readonly HttpClient _client = factory.CreateClient();

        private readonly TestJwtFactory _tokens = new(factory.JwtOptions);

        #endregion

        #region Tests

        [Theory]
        [InlineData("author", "approver-1", UserRoles.Approver)]
        [InlineData("approver", "author-1", UserRoles.Author)]
        public async Task ValidTokenWithWrongRole_Returns403WithEnvelope(string action, string userId, string role)
        {
            var response = await _client.GetWithTokenAsync($"{AuthProbeController.BaseRoute}/{action}", _tokens.Create(userId, role));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            await AuthTestHelpers.AssertErrorAsync(response, AuthErrorCodes.Forbidden);
        }

        [Fact]
        public async Task EndpointWithoutAttributes_WithoutToken_Returns401()
        {
            var response = await _client.GetWithTokenAsync(FallbackRoute, null);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            await AuthTestHelpers.AssertErrorAsync(response, AuthErrorCodes.Unauthorized);
        }

        [Fact]
        public async Task EndpointWithoutAttributes_WithValidToken_Returns200()
        {
            var response = await _client.GetWithTokenAsync(FallbackRoute, _tokens.Create("author-1", UserRoles.Author));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Health_WithoutToken_Returns200()
        {
            var response = await _client.GetWithTokenAsync("/health", null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        #endregion
    }
}
