using System.Text;
using Microsoft.Extensions.Options;

namespace EdocsTestTask.Api.Authentication
{
    /// <summary>
    /// Fails startup when JWT settings are missing or the signing key is too short for HS256.
    /// </summary>
    public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
    {
        #region Methods

        public ValidateOptionsResult Validate(string? name, JwtOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var failures = new List<string>();

            if (string.IsNullOrWhiteSpace(options.Issuer))
            {
                failures.Add($"{JwtOptions.SectionName}:{nameof(JwtOptions.Issuer)} is required.");
            }

            if (string.IsNullOrWhiteSpace(options.Audience))
            {
                failures.Add($"{JwtOptions.SectionName}:{nameof(JwtOptions.Audience)} is required.");
            }

            if (string.IsNullOrWhiteSpace(options.SigningKey))
            {
                failures.Add($"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)} is required. Outside Development set it via the Jwt__SigningKey environment variable.");
            }
            else if (Encoding.UTF8.GetByteCount(options.SigningKey) < JwtOptions.MinSigningKeyBytes)
            {
                failures.Add($"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)} must be at least {JwtOptions.MinSigningKeyBytes} bytes for HS256.");
            }

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }

        #endregion
    }
}
