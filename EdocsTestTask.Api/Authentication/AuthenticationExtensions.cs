using System.Text;
using EdocsTestTask.Api.Contracts;
using EdocsTestTask.Core.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EdocsTestTask.Api.Authentication
{
    /// <summary>
    /// Registers JWT bearer authentication, role authorization and a secure-by-default fallback policy, with ServiceResponse-shaped failures.
    /// </summary>
    public static class AuthenticationExtensions
    {
        #region Constants

        private const string LoggerCategory = "EdocsTestTask.Api.Authentication";

        private const string UnauthorizedMessage = "Authentication is required. Provide a valid Bearer token.";

        private const string ForbiddenMessage = "You do not have permission to perform this action.";

        #endregion

        #region Methods

        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
            services.AddOptions<JwtOptions>()
                .Bind(configuration.GetSection(JwtOptions.SectionName))
                .ValidateOnStart();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer();

            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<JwtOptions>>((bearer, jwt) => ConfigureBearer(bearer, jwt.Value));

            // Secure by default: an endpoint without [Authorize]/[AllowAnonymous] still requires a valid token.
            services.AddAuthorizationBuilder()
                .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build());

            return services;
        }

        #endregion

        #region Private Methods

        private static void ConfigureBearer(JwtBearerOptions bearer, JwtOptions jwt)
        {
            // Keep "sub" and "role" as-is instead of mapping them to long WS-* claim URIs.
            bearer.MapInboundClaims = false;

            bearer.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                NameClaimType = AuthClaimTypes.Subject,
                RoleClaimType = AuthClaimTypes.Role
            };

            bearer.Events = new JwtBearerEvents
            {
                OnTokenValidated = OnTokenValidated,
                OnChallenge = OnChallenge,
                OnForbidden = OnForbidden
            };
        }

        private static Task OnTokenValidated(TokenValidatedContext context)
        {
            var principal = context.Principal;
            var userId = principal?.FindFirst(AuthClaimTypes.Subject)?.Value;
            var roles = principal?.FindAll(AuthClaimTypes.Role).Select(claim => claim.Value).ToArray() ?? [];

            if (string.IsNullOrWhiteSpace(userId))
            {
                context.Fail($"Token has no '{AuthClaimTypes.Subject}' claim.");
            }
            else if (roles.Length == 0 || roles.Any(role => !UserRoles.All.Contains(role)))
            {
                context.Fail($"Token has no known '{AuthClaimTypes.Role}' claim.");
            }

            return Task.CompletedTask;
        }

        private static async Task OnChallenge(JwtBearerChallengeContext context)
        {
            // We write the response ourselves; skip the default empty 401.
            context.HandleResponse();

            var httpContext = context.HttpContext;
            var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory);
            logger.LogInformation(
                "Authentication challenge for {Method} {Path}. Reason: {Reason}. TraceId: {TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                context.AuthenticateFailure?.Message ?? "no bearer token",
                httpContext.TraceIdentifier);

            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            httpContext.Response.Headers.WWWAuthenticate = JwtBearerDefaults.AuthenticationScheme;
            await httpContext.Response.WriteAsJsonAsync(
                ServiceResponse.Fail([new ErrorResponse(AuthErrorCodes.Unauthorized, UnauthorizedMessage)]),
                httpContext.RequestAborted);
        }

        private static Task OnForbidden(ForbiddenContext context)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;

            return context.Response.WriteAsJsonAsync(
                ServiceResponse.Fail([new ErrorResponse(AuthErrorCodes.Forbidden, ForbiddenMessage)]),
                context.HttpContext.RequestAborted);
        }

        #endregion
    }
}
