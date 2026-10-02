using EdocsTestTask.Core.Commands;
using EdocsTestTask.Core.Entities;
using EdocsTestTask.Shared.Results;

namespace EdocsTestTask.Core.Interfaces.Services
{
    /// <summary>
    /// Creates approval tasks and moves them through their lifecycle.
    /// </summary>
    public interface IApprovalTaskService
    {
        #region Methods

        Result<ApprovalTask> Create(CreateApprovalTaskCommand command);

        Result<ApprovalTask> ExecuteAction(Guid id, ExecuteActionCommand command);

        Result<ApprovalTask> GetById(Guid id);

        #endregion
    }
}
