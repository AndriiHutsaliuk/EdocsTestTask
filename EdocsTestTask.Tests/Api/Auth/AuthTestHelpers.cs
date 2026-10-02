using System.Net.Http.Headers;
using System.Net.Http.Json;
using EdocsTestTask.Api.Contracts;

namespace EdocsTestTask.Tests.Api.Auth
{
    /// <summary>
    /// HTTP send and assert helpers shared by the auth integration tests.
    /// </summary>
    internal static class AuthTestHelpers
    {
        #region Methods

        public static async Task<HttpResponseMessage> GetWithTokenAsync(this HttpClient client, string route, string? token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, route);
            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return await client.SendAsync(request);
        }

        public static async Task AssertErrorAsync(HttpResponseMessage response, string expectedCode)
        {
            var body = await response.Content.ReadFromJsonAsync<ServiceResponse>();

            Assert.NotNull(body);
            Assert.False(body.Success);
            Assert.Equal(expectedCode, Assert.Single(body.Errors).Code);
        }

        public static async Task<string?> ReadUserIdAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadFromJsonAsync<ServiceResponse<string>>();

            Assert.NotNull(body);
            Assert.True(body.Success);
            return body.Data;
        }

        #endregion
    }
}
