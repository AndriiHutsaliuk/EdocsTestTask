using EdocsTestTask.Core.Entities;

namespace EdocsTestTask.Core.Interfaces.Repositories
{
    /// <summary>
    /// Storage for approval tasks. Implementations store and return copies,
    /// so callers never hold a reference to stored state.
    /// </summary>
    public interface IApprovalTaskRepository
    {
        #region Methods

        /// <summary>
        /// Returns a copy of the task, or null when it does not exist.
        /// </summary>
        ApprovalTask? GetById(Guid id);

        /// <exception cref="InvalidOperationException">A task with the same id already exists.</exception>
        void Add(ApprovalTask task);

        /// <summary>
        /// Replaces the stored task with a copy of <paramref name="task"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">No task with this id exists.</exception>
        void Update(ApprovalTask task);

        #endregion
    }
}
