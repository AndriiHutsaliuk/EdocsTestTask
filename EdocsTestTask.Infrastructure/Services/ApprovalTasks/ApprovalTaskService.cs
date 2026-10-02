using System.Collections.Concurrent;
using System.Globalization;
using EdocsTestTask.Core.Commands;
using EdocsTestTask.Core.Constants;
using EdocsTestTask.Core.Entities;
using EdocsTestTask.Core.Enums;
using EdocsTestTask.Core.Interfaces.Repositories;
using EdocsTestTask.Core.Interfaces.Services;
using EdocsTestTask.Shared.Results;
using Helper = EdocsTestTask.Infrastructure.Services.ApprovalTasks.ApprovalTaskServiceHelper;

namespace EdocsTestTask.Infrastructure.Services.ApprovalTasks;

    /// <summary>
    /// Orchestrates approval task operations: validates the command through <see cref="ApprovalTaskServiceHelper"/>,
    /// resolves the next status from its transition table, and applies the change under a per-task lock.
    /// </summary>
    public sealed class ApprovalTaskService(IApprovalTaskRepository repository, TimeProvider timeProvider) : IApprovalTaskService
    {
        #region Fields

        /// <summary>
        /// One lock per existing task: actions on the same task run one at a time, different tasks run in parallel.
        /// Process-local only (see README, known limitations).
        /// </summary>
        private readonly ConcurrentDictionary<Guid, Lock> _taskLocks = new();

        #endregion

        #region Methods

        public Result<ApprovalTask> Create(CreateApprovalTaskCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);

            var validation = Helper.Validate(command);
            if (!validation.IsValid)
            {
                return validation.ToFailure<ApprovalTask>();
            }

            var task = Helper.Create(command, timeProvider.GetUtcNow().UtcDateTime);

            repository.Add(task);
            return task;
        }

        public Result<ApprovalTask> ExecuteAction(Guid id, ExecuteActionCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);

            var validation = Helper.Validate(command);
            if (!validation.IsValid)
            {
                return validation.ToFailure<ApprovalTask>();
            }

            Helper.TryParseAction(command.Action, out var action);
            var actorId = command.ActorId!.Trim();

            // Check existence before taking a lock, so unknown ids from requests never grow the lock map.
            // Tasks are never deleted, so a task seen here still exists inside the lock.
            if (repository.GetById(id) is null)
            {
                return NotFound(id);
            }

            lock (_taskLocks.GetOrAdd(id, _ => new Lock()))
            {
                return ExecuteValidatedAction(id, action, actorId, command.Comment);
            }
        }

        public Result<ApprovalTask> GetById(Guid id)
        {
            var task = repository.GetById(id);
            if (task is null)
            {
                return NotFound(id);
            }

            return task;
        }

        #endregion

        #region Private Methods

        private Result<ApprovalTask> ExecuteValidatedAction(Guid id, ApprovalAction action, string actorId, string? comment)
        {
            // Runs under the task's lock: read, check, change and save form one atomic step.
            // The repository returns a copy, so a failure below leaves stored data untouched.
            var task = repository.GetById(id);
            if (task is null)
            {
                return NotFound(id);
            }

            if (!string.Equals(task.AssigneeId, actorId, StringComparison.Ordinal))
            {
                return Error.Forbidden(ApprovalTaskMessages.Codes.NotAssignee, ApprovalTaskMessages.NotAssignee);
            }

            var nextStatus = Helper.ResolveNextStatus(task.Status, action, comment);
            if (nextStatus.IsFailure)
            {
                return Result<ApprovalTask>.Failure(nextStatus.Errors);
            }

            var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
            task.Status = nextStatus.Value;
            task.UpdatedAtUtc = nowUtc;
            task.History.Add(new ApprovalHistoryEntry(action, actorId, NormalizeComment(comment), nowUtc));

            repository.Update(task);
            return task;
        }

        private static string? NormalizeComment(string? comment) =>
            string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

        private static Error NotFound(Guid id) =>
            Error.NotFound(
                ApprovalTaskMessages.Codes.NotFound,
                string.Format(CultureInfo.InvariantCulture, ApprovalTaskMessages.TaskNotFound, id));

        #endregion
    }

