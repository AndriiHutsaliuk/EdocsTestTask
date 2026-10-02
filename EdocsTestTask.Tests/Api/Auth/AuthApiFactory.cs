using EdocsTestTask.Api.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EdocsTestTask.Tests.Api.Auth
{
    /// <summary>
    /// Hosts the real API in Development (the committed test signing key) with <see cref="AuthProbeController"/> mounted.
    /// </summary>
    public sealed class AuthApiFactory : WebApplicationFactory<Program>
    {
        #region Properties

        /// <summary>
        /// The JWT settings the running app validates against.
        /// </summary>
        public JwtOptions JwtOptions => Services.GetRequiredService<IOptions<JwtOptions>>().Value;

        #endregion

        #region Methods

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);
            builder.ConfigureTestServices(services =>
                services.AddControllers().AddApplicationPart(typeof(AuthProbeController).Assembly));
        }

        #endregion
    }
}
