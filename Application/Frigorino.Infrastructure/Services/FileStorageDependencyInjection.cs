using Frigorino.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Frigorino.Infrastructure.Services
{
    public static class FileStorageDependencyInjection
    {
        // v1 always binds LocalFileStorage. Sub-feature #4 introduces a FileStorage:Provider switch
        // behind the same IFileStorage port. When FileStorage:LocalPath is unset we fall back to a
        // "blobs" directory under the content root — fine for dev/test. NOTE: in a container this
        // path is ephemeral (lost on restart) unless it points at a mounted volume; production should
        // set FileStorage:LocalPath explicitly (or use the dedicated prod backend from sub-feature #4).
        // The factory lambda defers construction (and its Directory.CreateDirectory side-effect) to
        // first resolve, so DI build / build-time OpenAPI generation never touches the filesystem.
        public static IServiceCollection AddFileStorage(
            this IServiceCollection services, IConfiguration configuration)
        {
            var configured = configuration["FileStorage:LocalPath"];
            services.AddSingleton<IFileStorage>(sp =>
            {
                var root = string.IsNullOrWhiteSpace(configured)
                    ? Path.Combine(sp.GetRequiredService<IHostEnvironment>().ContentRootPath, "blobs")
                    : configured;
                return new LocalFileStorage(root);
            });
            return services;
        }
    }
}
