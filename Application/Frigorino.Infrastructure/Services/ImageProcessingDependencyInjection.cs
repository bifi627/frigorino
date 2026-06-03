using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    public static class ImageProcessingDependencyInjection
    {
        // ImageSharp processor is stateless → singleton. Swap the implementation here if the library
        // is ever replaced (the IImageProcessor port keeps callers unchanged).
        public static IServiceCollection AddImageProcessing(this IServiceCollection services)
        {
            services.AddSingleton<IImageProcessor, ImageSharpImageProcessor>();
            return services;
        }
    }
}
