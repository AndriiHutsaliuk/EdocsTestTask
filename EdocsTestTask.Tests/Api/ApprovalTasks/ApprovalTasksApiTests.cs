using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EdocsTestTask.Api.Authentication;
using EdocsTestTask.Api.Contracts;
using EdocsTestTask.Api.Contracts.ApprovalTasks;
using EdocsTestTask.Core.Constants;
using EdocsTestTask.Tests.Api.Auth;

namespace EdocsTestTask.Tests.Api.ApprovalTasks
{
    public class ApprovalTasksApiTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
    {
        #region Constants

        private const string Route = "api/approval-tasks";

        #endregion

        #region Fields

        private readonly HttpClient _client = factory.CreateClient();

        private readonly string _authorToken = CreateToken(factory, "author-1", UserRoles.Author);

        private readonly string _approverToken = CreateToken(factory, "approver-1", UserRoles.Approver);

        private readonly string _otherApproverToken = CreateToken(factory, "approver-2", UserRoles.Approver);

        #endregion

        #region Helpers

        private static string CreateToken(AuthApiFactory fixture, string userId, string role) =>
            new TestJwtFactory(fixture.JwtOptions).Create(userId, role);

        private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, string? token, object? body = null)
        {
            using var request = new HttpRequestMessage(method, route);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return await _client.SendAsync(request);
        }

        private static async Task<ApprovalTaskResponse> ReadTaskAsync(HttpResponseMessage response)
        {
            var envelope = await response.Content.ReadFromJsonAsync<ServiceResponse<ApprovalTaskResponse>>();
            Assert.NotNull(envelope);
            Assert.True(envelope.Success);
            return envelope.Data!;
        }

        private async Task<ApprovalTaskResponse> CreateTaskAsync()
        {
            var response = await SendAsync(HttpMethod.Post, Route, _authorToken,
                new { documentNumber = "DOC-001", title = "Supply contract", assigneeId = "approver-1" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return await ReadTaskAsync(response);
        }

        private Task<HttpResponseMessage> ActAsync(Guid id, string token, object body) =>
            SendAsync(HttpMethod.Post, $"{Route}/{id}/actions", token, body);

        #endregion

        #region Tests

        [Fact]
        public async Task FullPath_CreateStartApproveGet_ReturnsConsistentTask()
        {
            var created = await CreateTaskAsync();
            Assert.Equal("Assigned", created.Status);
            Assert.Equal(created.CreatedAtUtc, created.UpdatedAtUtc);
            Assert.Empty(created.History);

            var started = await ActAsync(created.Id, _approverToken, new { action = "Start" });
            Assert.Equal(HttpStatusCode.OK, started.StatusCode);
            Assert.Equal("InProgress", (await ReadTaskAsync(started)).Status);

            var approved = await ActAsync(created.Id, _approverToken, new { action = "Approve", comment = "OK" });
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
            Assert.Equal("Approved", (await ReadTaskAsync(approved)).Status);

            var fetched = await SendAsync(HttpMethod.Get, $"{Route}/{created.Id}", _authorToken);
            Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
            var task = await ReadTaskAsync(fetched);
            Assert.Equal("Approved", task.Status);
            Assert.Collection(task.History,
                e => Assert.Equal(("Start", "approver-1", (string?)null), (e.Action, e.ActorId, e.Comment)),
                e => Assert.Equal(("Approve", "approver-1", (string?)"OK"), (e.Action, e.ActorId, e.Comment)));
        }

        [Fact]
        public async Task Action_ActorIdInBody_IsIgnored()
        {
            var created = await CreateTaskAsync();

            var response = await ActAsync(created.Id, _otherApproverToken, new { action = "Start", actorId = "approver-1" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            await AuthTestHelpers.AssertErrorAsync(response, ApprovalTaskMessages.Codes.NotAssignee);
        }

        [Fact]
        public async Task Create_ByApprover_Returns403()
        {
            var response = await SendAsync(HttpMethod.Post, Route, _approverToken,
                new { documentNumber = "DOC-001", title = "Supply contract", assigneeId = "approver-1" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            await AuthTestHelpers.AssertErrorAsync(response, AuthErrorCodes.Forbidden);
        }

        [Fact]
        public async Task Action_ByAuthor_Returns403()
        {
            var created = await CreateTaskAsync();

            var response = await ActAsync(created.Id, _authorToken, new { action = "Start" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            await AuthTestHelpers.AssertErrorAsync(response, AuthErrorCodes.Forbidden);
        }

        [Fact]
        public async Task Create_InvalidTitle_Returns400()
        {
            var response = await SendAsync(HttpMethod.Post, Route, _authorToken,
                new { documentNumber = "DOC-001", title = "  ", assigneeId = "approver-1" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AuthTestHelpers.AssertErrorAsync(response, ApprovalTaskMessages.Codes.InvalidTitle);
        }

        [Fact]
        public async Task Action_Unknown_Returns400()
        {
            var created = await CreateTaskAsync();

            var response = await ActAsync(created.Id, _approverToken, new { action = "Delete" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AuthTestHelpers.AssertErrorAsync(response, ApprovalTaskMessages.Codes.UnknownAction);
        }

        [Fact]
        public async Task Action_MissingTask_Returns404()
        {
            var response = await ActAsync(Guid.NewGuid(), _approverToken, new { action = "Start" });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            await AuthTestHelpers.AssertErrorAsync(response, ApprovalTaskMessages.Codes.NotFound);
        }

        [Fact]
        public async Task Action_ApproveWhenAssigned_Returns409()
        {
            var created = await CreateTaskAsync();

            var response = await ActAsync(created.Id, _approverToken, new { action = "Approve" });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await AuthTestHelpers.AssertErrorAsync(response, ApprovalTaskMessages.Codes.InvalidTransition);
        }

        [Fact]
        public async Task Action_RejectWithoutComment_Returns400AndKeepsStatus()
        {
            var created = await CreateTaskAsync();
            Assert.Equal(HttpStatusCode.OK, (await ActAsync(created.Id, _approverToken, new { action = "Start" })).StatusCode);

            var response = await ActAsync(created.Id, _approverToken, new { action = "Reject" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AuthTestHelpers.AssertErrorAsync(response, ApprovalTaskMessages.Codes.RejectCommentRequired);
            var fetched = await SendAsync(HttpMethod.Get, $"{Route}/{created.Id}", _authorToken);
            Assert.Equal("InProgress", (await ReadTaskAsync(fetched)).Status);
        }

        [Fact]
        public async Task Action_OnApprovedTask_Returns409()
        {
            var created = await CreateTaskAsync();
            Assert.Equal(HttpStatusCode.OK, (await ActAsync(created.Id, _approverToken, new { action = "Start" })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await ActAsync(created.Id, _approverToken, new { action = "Approve" })).StatusCode);

            var response = await ActAsync(created.Id, _approverToken, new { action = "Start" });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await AuthTestHelpers.AssertErrorAsync(response, ApprovalTaskMessages.Codes.AlreadyFinalized);
        }

        [Fact]
        public async Task Get_WithoutToken_Returns401()
        {
            var response = await SendAsync(HttpMethod.Get, $"{Route}/{Guid.NewGuid()}", token: null);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        #endregion
    }
}
