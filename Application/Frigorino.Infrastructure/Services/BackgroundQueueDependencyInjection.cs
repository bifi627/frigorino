using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    public static class BackgroundQueueDependencyInjection
    {
        public static IServiceCollection AddBackgroundTaskQueue(this IServiceCollection services)
        {
            // ONE singleton instance backs both the producer interface and the consumer.
            services.AddSingleton<BackgroundTaskQueue>();
            services.AddSingleton<IBackgroundTaskQueue>(sp => sp.GetRequiredService<BackgroundTaskQueue>());
            services.AddHostedService<QueuedHostedService>();

            return services;
        }
    }
}
