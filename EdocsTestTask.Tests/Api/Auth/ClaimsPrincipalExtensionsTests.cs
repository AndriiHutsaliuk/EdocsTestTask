using System.Security.Claims;
using EdocsTestTask.Api.Authentication;

namespace EdocsTestTask.Tests.Api.Auth
{
    public class ClaimsPrincipalExtensionsTests
    {
        #region Tests

        [Fact]
        public void GetUserId_ReturnsSubClaim()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AuthClaimTypes.Subject, "author-1")], "Bearer"));

            Assert.Equal("author-1", principal.GetUserId());
        }

        [Fact]
        public void GetUserId_WithoutSubClaim_Throws()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AuthClaimTypes.Role, "Author")], "Bearer"));

            Assert.Throws<InvalidOperationException>(() => principal.GetUserId());
        }

        #endregion
    }
}
