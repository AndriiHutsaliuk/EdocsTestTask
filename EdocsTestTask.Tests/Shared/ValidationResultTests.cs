using EdocsTestTask.Shared.Results;
using EdocsTestTask.Shared.Validation;

namespace EdocsTestTask.Tests.Shared
{
    public class ValidationResultTests
    {
        #region Tests

        [Fact]
        public void AddError_PrebuiltError_KeepsCodeMessageAndType()
        {
            var validation = new ValidationResult();
            var error = Error.Conflict("Test.Conflict", "Conflict happened");

            validation.AddError(error);

            Assert.False(validation.IsValid);
            Assert.Same(error, Assert.Single(validation.Errors));
            Assert.Equal(ErrorType.Conflict, validation.ToResult().Error!.Type);
        }

        [Fact]
        public void AddError_NullError_Throws()
        {
            var validation = new ValidationResult();

            Assert.Throws<ArgumentNullException>(() => validation.AddError((Error)null!));
        }

        #endregion
    }
}
