using EdocsTestTask.Api.Authentication;

namespace EdocsTestTask.Tests.Api.Auth
{
    public class JwtOptionsValidatorTests
    {
        #region Constants

        // 32 ASCII characters = 32 UTF-8 bytes, the HS256 minimum.
        private const string ValidKey = "0123456789abcdef0123456789abcdef";

        #endregion

        #region Fields

        private readonly JwtOptionsValidator _validator = new();

        #endregion

        #region Tests

        [Fact]
        public void Validate_AllSettingsPresent_Succeeds()
        {
            var result = _validator.Validate(null, Create());

            Assert.True(result.Succeeded);
        }

        [Fact]
        public void Validate_KeyLengthIsMeasuredInUtf8Bytes()
        {
            // 16 characters, but 32 bytes in UTF-8.
            var result = _validator.Validate(null, Create(signingKey: new string('ї', 16)));

            Assert.True(result.Succeeded);
        }

        [Theory]
        [InlineData("", "aud", ValidKey, nameof(JwtOptions.Issuer))]
        [InlineData("   ", "aud", ValidKey, nameof(JwtOptions.Issuer))]
        [InlineData("iss", "", ValidKey, nameof(JwtOptions.Audience))]
        [InlineData("iss", "aud", "", nameof(JwtOptions.SigningKey))]
        [InlineData("iss", "aud", "   ", nameof(JwtOptions.SigningKey))]
        [InlineData("iss", "aud", "0123456789abcdef0123456789abcde", nameof(JwtOptions.SigningKey))]
        public void Validate_MissingOrInvalidSetting_FailsNamingTheField(string issuer, string audience, string signingKey, string expectedField)
        {
            var result = _validator.Validate(null, Create(issuer, audience, signingKey));

            Assert.True(result.Failed);
            Assert.Contains($"{JwtOptions.SectionName}:{expectedField}", result.FailureMessage);
        }

        #endregion

        #region Private Methods

        private static JwtOptions Create(string issuer = "iss", string audience = "aud", string signingKey = ValidKey) =>
            new() { Issuer = issuer, Audience = audience, SigningKey = signingKey };

        #endregion
    }
}
