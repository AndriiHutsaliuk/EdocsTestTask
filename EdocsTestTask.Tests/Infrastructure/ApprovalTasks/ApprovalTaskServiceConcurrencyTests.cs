using EdocsTestTask.Core.Commands;
using EdocsTestTask.Core.Constants;
using EdocsTestTask.Core.Entities;
using EdocsTestTask.Core.Enums;
using EdocsTestTask.Infrastructure.Repositories;
using EdocsTestTask.Infrastructure.Services.ApprovalTasks;
using EdocsTestTask.Shared.Results;
using EdocsTestTask.Tests.TestData;

namespace EdocsTestTask.Tests.Infrastructure.ApprovalTasks
{
    public class ApprovalTaskServiceConcurrencyTests
    {
        #region Constants

        private const int Iterations = 200;

        private const string Assignee = ApprovalTaskTestData.AssigneeId;

        #endregion

        #region Fields

        private readonly InMemoryApprovalTaskRepository _repository = new();

        private readonly ApprovalTaskService _service;

        #endregion

        #region Constructors

        public ApprovalTaskServiceConcurrencyTests()
        {
            var time = new TestTimeProvider(new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero));
            _service = new ApprovalTaskService(_repository, time);
        }

        #endregion

        #region Tests

        [Theory]
        [InlineData("Approve", "Reject")]
        [InlineData("Approve", "Approve")]
        [InlineData("Reject", "Reject")]
        public async Task ExecuteAction_TwoConcurrentFinalDecisions_ExactlyOneSucceeds(string first, string second)
        {
            for (var i = 0; i < Iterations; i++)
            {
                var task = _service.Create(new CreateApprovalTaskCommand("DOC-001", "Supply contract", Assignee)).Value;
                Assert.True(_service.ExecuteAction(task.Id, new ExecuteActionCommand(Assignee, "Start", null)).IsSuccess);

                using var barrier = new Barrier(2);
                Result<ApprovalTask> Decide(string action)
                {
                    barrier.SignalAndWait();
                    return _service.ExecuteAction(task.Id, new ExecuteActionCommand(Assignee, action, "Decision comment"));
                }

                var results = await Task.WhenAll(Task.Run(() => Decide(first)), Task.Run(() => Decide(second)));

                var winner = Assert.Single(results, r => r.IsSuccess);
                var loser = Assert.Single(results, r => r.IsFailure);
                Assert.Equal(ErrorType.Conflict, loser.Error!.Type);
                Assert.Equal(ApprovalTaskMessages.Codes.AlreadyFinalized, loser.Error.Code);

                var stored = _repository.GetById(task.Id)!;
                Assert.Equal(winner.Value.Status, stored.Status);
                Assert.Equal(2, stored.History.Count);
                Assert.Single(stored.History, h => h.Action is ApprovalAction.Approve or ApprovalAction.Reject);
            }
        }

        #endregion
    }
}
