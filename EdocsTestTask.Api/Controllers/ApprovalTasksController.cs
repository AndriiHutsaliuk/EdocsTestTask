using EdocsTestTask.Api.Authentication;
using EdocsTestTask.Api.Contracts;
using EdocsTestTask.Api.Contracts.ApprovalTasks;
using EdocsTestTask.Api.Extensions;
using EdocsTestTask.Api.Mappings;
using EdocsTestTask.Core.Commands;
using EdocsTestTask.Core.Constants;
using EdocsTestTask.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EdocsTestTask.Api.Controllers
{
    /// <summary>
    /// Approval tasks: Authors create them, the assigned Approver moves them through Start → Approve/Reject.
    /// </summary>
    [ApiController]
    [Route(BaseRoute)]
    [Authorize]
    [ProducesResponseType<ServiceResponse>(StatusCodes.Status401Unauthorized)]
    public sealed class ApprovalTasksController(IApprovalTaskService service) : ControllerBase
    {
        #region Constants

        public const string BaseRoute = "api/approval-tasks";

        #endregion

        #region Methods

        [HttpPost]
        [Authorize(Roles = UserRoles.Author)]
        [ProducesResponseType<ServiceResponse<ApprovalTaskResponse>>(StatusCodes.Status201Created)]
        [ProducesResponseType<ServiceResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ServiceResponse>(StatusCodes.Status403Forbidden)]
        public ActionResult<ServiceResponse<ApprovalTaskResponse>> Create([FromBody] CreateApprovalTaskRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var command = new CreateApprovalTaskCommand(request.DocumentNumber, request.Title, request.AssigneeId);
            return service.Create(command)
                .Map(task => task.ToResponse())
                .ToActionResult(StatusCodes.Status201Created);
        }

        [HttpPost("{id:guid}/actions")]
        [Authorize(Roles = UserRoles.Approver)]
        [ProducesResponseType<ServiceResponse<ApprovalTaskResponse>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ServiceResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ServiceResponse>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ServiceResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ServiceResponse>(StatusCodes.Status409Conflict)]
        public ActionResult<ServiceResponse<ApprovalTaskResponse>> ExecuteAction(Guid id, [FromBody] ExecuteActionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var command = new ExecuteActionCommand(User.GetUserId(), request.Action, request.Comment);
            return service.ExecuteAction(id, command)
                .Map(task => task.ToResponse())
                .ToActionResult();
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType<ServiceResponse<ApprovalTaskResponse>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ServiceResponse>(StatusCodes.Status404NotFound)]
        public ActionResult<ServiceResponse<ApprovalTaskResponse>> GetById(Guid id) =>
            service.GetById(id)
                .Map(task => task.ToResponse())
                .ToActionResult();

        #endregion
    }
}
