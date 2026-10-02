using System.Collections.Concurrent;
using EdocsTestTask.Core.Entities;
using EdocsTestTask.Core.Interfaces.Repositories;

namespace EdocsTestTask.Infrastructure.Repositories
{
    /// <summary>
    /// Process-local storage. Register as a singleton; data is lost on restart.
    /// </summary>
    public sealed class InMemoryApprovalTaskRepository : IApprovalTaskRepository
    {
        #region Fields

        private readonly ConcurrentDictionary<Guid, ApprovalTask> _tasks = new();

        #endregion

        #region Methods

        public ApprovalTask? GetById(Guid id) =>
            _tasks.TryGetValue(id, out var task) ? task.Clone() : null;

        public void Add(ApprovalTask task)
        {
            ArgumentNullException.ThrowIfNull(task);

            if (!_tasks.TryAdd(task.Id, task.Clone()))
            {
                throw new InvalidOperationException($"Approval task '{task.Id}' already exists.");
            }
        }

        public void Update(ApprovalTask task)
        {
            ArgumentNullException.ThrowIfNull(task);

            // Tasks are never deleted, so an id seen here cannot disappear between the check and the write.
            if (!_tasks.ContainsKey(task.Id))
            {
                throw new InvalidOperationException($"Approval task '{task.Id}' does not exist.");
            }

            _tasks[task.Id] = task.Clone();
        }

        #endregion
    }
}
