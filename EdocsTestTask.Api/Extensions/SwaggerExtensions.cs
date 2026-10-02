using Microsoft.OpenApi;

namespace EdocsTestTask.Api.Extensions
{
    /// <summary>
    /// Swagger (Swashbuckle) with a JWT Bearer scheme, so requests can be sent from the UI after "Authorize".
    /// </summary>
    public static class SwaggerExtensions
    {
        #region Constants

        public const string DocumentName = "v1";

        public const string BearerSchemeName = "bearer";

        private const string ApiTitle = "EdocsTestTask API";

        private const string BearerDescription =
            "Paste a JWT without the \"Bearer \" prefix (static tokens: EdocsTestTask.Api.http, README «Автентифікація»).";

        #endregion

        #region Methods

        /// <summary>
        /// Registers the OpenAPI document generator with a Bearer scheme required by every operation.
        /// </summary>
        public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc(DocumentName, new OpenApiInfo { Title = ApiTitle, Version = DocumentName });
                options.AddSecurityDefinition(BearerSchemeName, new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = BearerSchemeName,
                    BearerFormat = "JWT",
                    Description = BearerDescription
                });
                options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(BearerSchemeName, document)] = []
                });
            });

            return services;
        }

        /// <summary>
        /// Serves /swagger/v1/swagger.json and the UI at /swagger. Call after UseHttpsRedirection (so the UI and "Try it out"
        /// share one origin) and before UseAuthentication/UseAuthorization: the fallback policy also applies to requests
        /// without an endpoint and would answer 401.
        /// </summary>
        public static IApplicationBuilder UseSwaggerWithUi(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);

            app.UseSwagger();
            app.UseSwaggerUI(options => options.SwaggerEndpoint($"{DocumentName}/swagger.json", ApiTitle));
            return app;
        }

        #endregion
    }
}
