using EdocsTestTask.Api.Authentication;
using EdocsTestTask.Api.Contracts;
using EdocsTestTask.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EdocsTestTask.Tests.Api.Auth
{
    /// <summary>
    /// Test-only endpoints that exercise the real auth pipeline. It lives in the test assembly, so production never maps it.
    /// </summary>
    [ApiController]
    [Route(BaseRoute)]
    public sealed class AuthProbeController : ControllerBase
    {
        #region Constants

        public const string BaseRoute = "test/auth-probe";

        #endregion

        #region Actions

        [HttpGet("author")]
        [Authorize(Roles = UserRoles.Author)]
        public ActionResult<ServiceResponse<string>> AuthorOnly() => Ok(ServiceResponse<string>.Ok(User.GetUserId()));

        [HttpGet("approver")]
        [Authorize(Roles = UserRoles.Approver)]
        public ActionResult<ServiceResponse<string>> ApproverOnly() => Ok(ServiceResponse<string>.Ok(User.GetUserId()));

        [HttpGet("any")]
        [Authorize]
        public ActionResult<ServiceResponse<string>> AnyRole() => Ok(ServiceResponse<string>.Ok(User.GetUserId()));

        /// <summary>
        /// No authorization attributes: protected only by the fallback policy.
        /// </summary>
        [HttpGet("fallback")]
        public ActionResult<ServiceResponse> Fallback() => Ok(ServiceResponse.Ok());

        #endregion
    }
}
