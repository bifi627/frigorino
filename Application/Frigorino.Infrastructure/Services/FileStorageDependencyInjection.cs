using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Auth;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Frigorino.Infrastructure.Services
{
    public static class FileStorageDependencyInjection
    {
        // Selects the blob backend by FileStorage:Provider ("Local" default for dev/test/CI, "Gcs"
        // for prod). Both backends register the same singleton instance under IFileStorage (hot path)
        // and IFileStorageMaintenance (sweep listing). Construction is deferred in the factory lambdas
        // so DI build / build-time OpenAPI generation never touches the filesystem or a GCS client.
        public static IServiceCollection AddFileStorage(
            this IServiceCollection services, IConfiguration configuration)
        {
            var provider = configuration["FileStorage:Provider"];
            if (string.Equals(provider, "Gcs", StringComparison.OrdinalIgnoreCase))
            {
                AddGcs(services, configuration);
            }
            else
            {
                AddLocal(services, configuration);
            }

            return services;
        }

        private static void AddLocal(IServiceCollection services, IConfiguration configuration)
        {
            // When FileStorage:LocalPath is unset we fall back to a "blobs" directory under the content
            // root — fine for dev/test. In a container this path is ephemeral (lost on restart) unless
            // it points at a mounted volume; production should use the Gcs provider.
            var configured = configuration["FileStorage:LocalPath"];
            services.AddSingleton<LocalFileStorage>(sp =>
            {
                var root = string.IsNullOrWhiteSpace(configured)
                    ? Path.Combine(sp.GetRequiredService<IHostEnvironment>().ContentRootPath, "blobs")
                    : configured;
                return new LocalFileStorage(root);
            });
            services.AddSingleton<IFileStorage>(sp => sp.GetRequiredService<LocalFileStorage>());
            services.AddSingleton<IFileStorageMaintenance>(sp => sp.GetRequiredService<LocalFileStorage>());
        }

        private static void AddGcs(IServiceCollection services, IConfiguration configuration)
        {
            var bucket = configuration["FileStorage:Bucket"];
            var prefix = configuration["FileStorage:KeyPrefix"];
            var accessJson = configuration
                .GetSection(FirebaseSettings.SECTION_NAME)
                .Get<FirebaseSettings>()?.AccessJson;

            services.AddSingleton<GcsFileStorage>(sp =>
            {
                if (string.IsNullOrWhiteSpace(bucket))
                {
                    throw new InvalidOperationException(
                        "FileStorage:Bucket is required when FileStorage:Provider is 'Gcs'.");
                }

                if (string.IsNullOrWhiteSpace(accessJson))
                {
                    throw new InvalidOperationException(
                        "FirebaseSettings:AccessJson is required for the GCS file storage backend.");
                }

                var credential = CredentialFactory
                    .FromJson<ServiceAccountCredential>(accessJson)
                    .ToGoogleCredential();
                var client = StorageClient.Create(credential);
                var effectivePrefix = string.IsNullOrWhiteSpace(prefix) ? "list-items" : prefix;
                return new GcsFileStorage(client, bucket, effectivePrefix);
            });
            services.AddSingleton<IFileStorage>(sp => sp.GetRequiredService<GcsFileStorage>());
            services.AddSingleton<IFileStorageMaintenance>(sp => sp.GetRequiredService<GcsFileStorage>());
        }
    }
}
