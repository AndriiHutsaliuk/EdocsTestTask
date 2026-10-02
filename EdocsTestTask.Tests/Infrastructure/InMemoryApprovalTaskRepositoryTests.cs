using EdocsTestTask.Core.Enums;
using EdocsTestTask.Infrastructure.Repositories;
using EdocsTestTask.Tests.TestData;

namespace EdocsTestTask.Tests.Infrastructure
{
    public class InMemoryApprovalTaskRepositoryTests
    {
        #region Fields

        private readonly InMemoryApprovalTaskRepository _repository = new();

        #endregion

        #region Tests

        [Fact]
        public void GetById_Unknown_ReturnsNull()
        {
            Assert.Null(_repository.GetById(Guid.NewGuid()));
        }

        [Fact]
        public void Add_ThenGetById_ReturnsEqualCopy()
        {
            var task = ApprovalTaskTestData.Create();

            _repository.Add(task);
            var stored = _repository.GetById(task.Id);

            Assert.NotNull(stored);
            Assert.NotSame(task, stored);
            Assert.Equal(task.Title, stored.Title);
            Assert.Equal(task.Status, stored.Status);
        }

        [Fact]
        public void Add_MutatingSourceAfterAdd_DoesNotChangeStoredTask()
        {
            var task = ApprovalTaskTestData.Create();
            _repository.Add(task);

            task.Status = ApprovalStatus.Approved;

            Assert.Equal(ApprovalStatus.Assigned, _repository.GetById(task.Id)!.Status);
        }

        [Fact]
        public void GetById_MutatingReturnedTask_DoesNotChangeStoredTask()
        {
            var task = ApprovalTaskTestData.Create();
            _repository.Add(task);

            var copy = _repository.GetById(task.Id)!;
            copy.Status = ApprovalStatus.Rejected;
            copy.History.Add(new(ApprovalAction.Reject, "approver-1", "x", DateTime.UtcNow));

            var stored = _repository.GetById(task.Id)!;
            Assert.Equal(ApprovalStatus.Assigned, stored.Status);
            Assert.Empty(stored.History);
        }

        [Fact]
        public void Add_DuplicateId_Throws()
        {
            var task = ApprovalTaskTestData.Create();
            _repository.Add(task);

            Assert.Throws<InvalidOperationException>(() => _repository.Add(task));
        }

        [Fact]
        public void Update_ReplacesStoredTask()
        {
            var task = ApprovalTaskTestData.Create();
            _repository.Add(task);

            var changed = _repository.GetById(task.Id)!;
            changed.Status = ApprovalStatus.InProgress;
            _repository.Update(changed);

            Assert.Equal(ApprovalStatus.InProgress, _repository.GetById(task.Id)!.Status);
        }

        [Fact]
        public void Update_UnknownId_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _repository.Update(ApprovalTaskTestData.Create()));
        }

        [Fact]
        public void AddAndUpdate_Null_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => _repository.Add(null!));
            Assert.Throws<ArgumentNullException>(() => _repository.Update(null!));
        }

        #endregion
    }
}
