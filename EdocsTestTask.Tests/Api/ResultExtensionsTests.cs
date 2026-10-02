using EdocsTestTask.Api.Contracts;
using EdocsTestTask.Api.Extensions;
using EdocsTestTask.Shared.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EdocsTestTask.Tests.Api
{
    public class ResultExtensionsTests
    {
        #region Tests

        [Fact]
        public void Success_MapsToOkWithData()
        {
            var actionResult = Result<string>.Success("value").ToActionResult();

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            var response = Assert.IsType<ServiceResponse<string>>(objectResult.Value);
            Assert.Equal(StatusCodes.Status200OK, objectResult.StatusCode);
            Assert.True(response.Success);
            Assert.Equal("value", response.Data);
        }

        [Fact]
        public void Success_UsesCustomStatusCode()
        {
            var actionResult = Result.Success().ToActionResult(StatusCodes.Status204NoContent);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status204NoContent, objectResult.StatusCode);
        }

        [Theory]
        [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest)]
        [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
        [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
        [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized)]
        [InlineData(ErrorType.Forbidden, StatusCodes.Status403Forbidden)]
        [InlineData(ErrorType.Failure, StatusCodes.Status500InternalServerError)]
        public void Failure_MapsErrorTypeToStatusCode(ErrorType type, int expectedStatus)
        {
            var actionResult = Result.Failure(new Error("Test.Code", "Test message", type)).ToActionResult();

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            var response = Assert.IsType<ServiceResponse>(objectResult.Value);
            Assert.Equal(expectedStatus, objectResult.StatusCode);
            Assert.False(response.Success);
            var error = Assert.Single(response.Errors);
            Assert.Equal("Test.Code", error.Code);
        }

        #endregion
    }
}
