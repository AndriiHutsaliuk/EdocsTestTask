using EdocsTestTask.Core.Constants;
using EdocsTestTask.Core.Helpers;

namespace EdocsTestTask.Tests.Core
{
    public class TextValidationHelperTests
    {
        #region Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void IsTitleValid_MissingTitle_ReturnsRequiredError(string? title)
        {
            var isValid = TextValidationHelper.IsTitleValid(title, out var error);

            Assert.False(isValid);
            Assert.Equal(ApprovalTaskMessages.TitleRequired, error);
        }

        [Theory]
        [InlineData(2, "")]
        [InlineData(2, "   ")]
        [InlineData(257, "")]
        public void IsTitleValid_LengthOutOfRange_ReturnsLengthError(int length, string padding)
        {
            var title = padding + new string('a', length) + padding;

            var isValid = TextValidationHelper.IsTitleValid(title, out var error);

            Assert.False(isValid);
            Assert.Equal("Title must be between 3 and 256 characters.", error);
        }

        [Theory]
        [InlineData(3, "")]
        [InlineData(256, "")]
        [InlineData(3, "  ")]
        public void IsTitleValid_LengthInRange_ReturnsTrue(int length, string padding)
        {
            var title = padding + new string('a', length) + padding;

            var isValid = TextValidationHelper.IsTitleValid(title, out var error);

            Assert.True(isValid);
            Assert.Null(error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" \t ")]
        public void IsRequiredTextValid_Missing_ReturnsFieldError(string? value)
        {
            var isValid = TextValidationHelper.IsRequiredTextValid(value, "DocumentNumber", out var error);

            Assert.False(isValid);
            Assert.Equal("DocumentNumber is required and cannot be empty or whitespace.", error);
        }

        [Fact]
        public void IsRequiredTextValid_Present_ReturnsTrue()
        {
            var isValid = TextValidationHelper.IsRequiredTextValid("DOC-1", "DocumentNumber", out var error);

            Assert.True(isValid);
            Assert.Null(error);
        }

        [Fact]
        public void IsRequiredTextValid_BlankFieldName_Throws()
        {
            Assert.ThrowsAny<ArgumentException>(() => TextValidationHelper.IsRequiredTextValid("x", " ", out _));
        }

        [Fact]
        public void TitleMaxLength_Is256()
        {
            Assert.Equal(256, ValidationConstants.TitleMaxLength);
        }

        [Fact]
        public void ShortTextMaxLength_Is128()
        {
            Assert.Equal(128, ValidationConstants.ShortTextMaxLength);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" \t ")]
        public void IsShortTextValid_Missing_ReturnsFieldError(string? value)
        {
            var isValid = TextValidationHelper.IsShortTextValid(value, "DocumentNumber", out var error);

            Assert.False(isValid);
            Assert.Equal("DocumentNumber is required and cannot be empty or whitespace.", error);
        }

        [Theory]
        [InlineData(1, "")]
        [InlineData(128, "")]
        [InlineData(128, "  ")]
        public void IsShortTextValid_WithinLimit_ReturnsTrue(int length, string padding)
        {
            var value = padding + new string('7', length) + padding;

            var isValid = TextValidationHelper.IsShortTextValid(value, "DocumentNumber", out var error);

            Assert.True(isValid);
            Assert.Null(error);
        }

        [Fact]
        public void IsShortTextValid_TooLong_ReturnsLengthError()
        {
            var isValid = TextValidationHelper.IsShortTextValid(new string('7', 129), "DocumentNumber", out var error);

            Assert.False(isValid);
            Assert.Equal("DocumentNumber must be 128 characters or fewer.", error);
        }

        [Fact]
        public void CommentMaxLength_Is4096()
        {
            Assert.Equal(4096, ValidationConstants.CommentMaxLength);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void IsCommentValid_Missing_ReturnsTrue(string? comment)
        {
            var isValid = TextValidationHelper.IsCommentValid(comment, out var error);

            Assert.True(isValid);
            Assert.Null(error);
        }

        [Theory]
        [InlineData(4096, "")]
        [InlineData(4096, "  ")]
        public void IsCommentValid_WithinLimit_ReturnsTrue(int length, string padding)
        {
            var comment = padding + new string('c', length) + padding;

            var isValid = TextValidationHelper.IsCommentValid(comment, out var error);

            Assert.True(isValid);
            Assert.Null(error);
        }

        [Fact]
        public void IsCommentValid_TooLong_ReturnsLengthError()
        {
            var isValid = TextValidationHelper.IsCommentValid(new string('c', 4097), out var error);

            Assert.False(isValid);
            Assert.Equal("Comment must be 4096 characters or fewer.", error);
        }

        [Fact]
        public void IsShortTextValid_BlankFieldName_Throws()
        {
            Assert.ThrowsAny<ArgumentException>(() => TextValidationHelper.IsShortTextValid("x", " ", out _));
        }

        #endregion
    }
}
