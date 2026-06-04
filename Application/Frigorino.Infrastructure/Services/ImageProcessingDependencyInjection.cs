using Frigorino.Domain.Interfaces;
using ImageMagick;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    public static class ImageProcessingDependencyInjection
    {
        // Magick.NET processor is stateless → singleton. Swap the implementation here if the library
        // is ever replaced (the IImageProcessor port keeps callers unchanged). ResourceLimits.Thread
        // is global to ImageMagick; pin to 1 so OpenMP doesn't fan threads per request under Railway's
        // constrained CPU.
        public static IServiceCollection AddImageProcessing(this IServiceCollection services)
        {
            ResourceLimits.Thread = 1;
            services.AddSingleton<IImageProcessor, MagickImageProcessor>();
            return services;
        }
    }
}
