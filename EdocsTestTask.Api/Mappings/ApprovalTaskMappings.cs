using EdocsTestTask.Api.Contracts.ApprovalTasks;
using EdocsTestTask.Core.Entities;

namespace EdocsTestTask.Api.Mappings
{
    /// <summary>
    /// Maps approval task entities to transport models.
    /// </summary>
    public static class ApprovalTaskMappings
    {
        #region Methods

        public static ApprovalTaskResponse ToResponse(this ApprovalTask task)
        {
            ArgumentNullException.ThrowIfNull(task);

            return new ApprovalTaskResponse(
                task.Id,
                task.DocumentNumber,
                task.Title,
                task.AssigneeId,
                task.Status.ToString(),
                task.CreatedAtUtc,
                task.UpdatedAtUtc,
                task.History
                    .Select(entry => new ApprovalHistoryEntryResponse(entry.Action.ToString(), entry.ActorId, entry.Comment, entry.AtUtc))
                    .ToArray());
        }

        #endregion
    }
}
