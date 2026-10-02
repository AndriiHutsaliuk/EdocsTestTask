using EdocsTestTask.Core.Interfaces.Repositories;
using EdocsTestTask.Core.Interfaces.Services;
using EdocsTestTask.Infrastructure.Repositories;
using EdocsTestTask.Infrastructure.Services.ApprovalTasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EdocsTestTask.Infrastructure
{
    public static class DependencyInjection
    {
        #region Methods

        /// <summary>
        /// Registers Infrastructure implementations of Core service and repository interfaces.
        /// </summary>
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton(TimeProvider.System);

            // Singletons: the in-memory data and the service's per-task locks must be shared by all requests.
            services.AddSingleton<IApprovalTaskRepository, InMemoryApprovalTaskRepository>();
            services.AddSingleton<IApprovalTaskService, ApprovalTaskService>();

            return services;
        }

        #endregion
    }
}
