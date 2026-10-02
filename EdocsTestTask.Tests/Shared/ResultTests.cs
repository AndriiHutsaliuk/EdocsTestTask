using EdocsTestTask.Shared.Results;

namespace EdocsTestTask.Tests.Shared
{
    public class ResultTests
    {
        #region Tests

        [Fact]
        public void Success_HasNoErrors()
        {
            var result = Result.Success();

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Errors);
            Assert.Null(result.Error);
        }

        [Fact]
        public void Failure_CarriesError()
        {
            var error = Error.NotFound("Item.NotFound", "Item was not found");

            var result = Result.Failure(error);

            Assert.True(result.IsFailure);
            Assert.Equal(error, result.Error);
        }

        [Fact]
        public void Failure_WithNoErrors_Throws()
        {
            Assert.Throws<ArgumentException>(() => Result.Failure(Array.Empty<Error>()));
        }

        [Fact]
        public void GenericSuccess_ExposesValue()
        {
            Result<int> result = 42;

            Assert.True(result.IsSuccess);
            Assert.Equal(42, result.Value);
        }

        [Fact]
        public void GenericFailure_ValueAccessThrows()
        {
            Result<int> result = Error.Conflict("Item.Conflict", "Item already exists");

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorType.Conflict, result.Error!.Type);
            Assert.Throws<InvalidOperationException>(() => result.Value);
        }

        #endregion
    }
}
