using EdocsTestTask.Shared.Results;

namespace EdocsTestTask.Tests.Shared
{
    public class ResultMapTests
    {
        #region Tests

        [Fact]
        public void Map_Success_ReturnsMappedValue()
        {
            var result = Result<int>.Success(21).Map(value => (value * 2).ToString());

            Assert.True(result.IsSuccess);
            Assert.Equal("42", result.Value);
        }

        [Fact]
        public void Map_Failure_KeepsErrorsAndSkipsMapper()
        {
            var error = Error.NotFound("Test.NotFound", "Missing");
            var called = false;

            var result = Result<int>.Failure(error).Map(value =>
            {
                called = true;
                return value.ToString();
            });

            Assert.True(result.IsFailure);
            Assert.Same(error, Assert.Single(result.Errors));
            Assert.False(called);
        }

        [Fact]
        public void Map_NullMapper_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Result<int>.Success(1).Map<string>(null!));
        }

        #endregion
    }
}
