using System.Net;
using System.Text.Json;
using EdocsTestTask.Api.Authentication;
using EdocsTestTask.Api.Controllers;
using EdocsTestTask.Api.Extensions;
using EdocsTestTask.Core.Constants;
using EdocsTestTask.Tests.Api.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EdocsTestTask.Tests.Api.Swagger
{
    public class SwaggerTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
    {
        #region Constants

        private const string DocumentRoute = $"/swagger/{SwaggerExtensions.DocumentName}/swagger.json";

        private const string UiConfigRoute = "/swagger/index.js";

        private const string ProductionSigningKey = "production-test-signing-key-0123456789-abcdefghij";

        #endregion

        #region Tests

        [Fact]
        public async Task Development_SwaggerDocument_IsServedWithoutTokenAndDescribesBearerAuth()
        {
            var response = await factory.CreateClient().GetWithTokenAsync(DocumentRoute, null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            Assert.True(root.GetProperty("paths").TryGetProperty($"/{ApprovalTasksController.BaseRoute}", out var createPath));
            var createResponses = createPath.GetProperty("post").GetProperty("responses");
            Assert.True(createResponses.TryGetProperty("201", out _));
            Assert.False(createResponses.TryGetProperty("200", out _));
            Assert.True(root.GetProperty("components").GetProperty("securitySchemes")
                .TryGetProperty(SwaggerExtensions.BearerSchemeName, out var scheme));
            Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
            Assert.Contains(
                root.GetProperty("security").EnumerateArray(),
                requirement => requirement.TryGetProperty(SwaggerExtensions.BearerSchemeName, out _));
        }

        [Fact]
        public async Task Development_SwaggerUi_IsServedWithoutToken()
        {
            var response = await factory.CreateClient().GetWithTokenAsync("/swagger/index.html", null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Development_SwaggerUi_PointsAtTheSwaggerDocument()
        {
            var response = await factory.CreateClient().GetWithTokenAsync(UiConfigRoute, null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains($"{SwaggerExtensions.DocumentName}/swagger.json", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Production_SwaggerDocument_IsNotServed()
        {
            using var production = factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Production);
                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        [$"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}"] = ProductionSigningKey
                    }));
            });
            var jwtOptions = production.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
            var token = new TestJwtFactory(jwtOptions).Create("author-1", UserRoles.Author);

            // A valid token passes the fallback policy, so 404 means Swagger is not mounted (not merely unauthorized).
            var response = await production.CreateClient().GetWithTokenAsync(DocumentRoute, token);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        #endregion
    }
}
