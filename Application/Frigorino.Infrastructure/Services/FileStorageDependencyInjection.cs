using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Auth;
using Frigorino.Infrastructure.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Frigorino.Infrastructure.Services
{
    public static class FileStorageDependencyInjection
    {
        // Blob storage is split into per-feature "areas" (BlobAreas). Each area gets its OWN keyed
        // IFileStorage + IFileStorageMaintenance, prefixed {env}/{area} so the bucket groups by
        // environment then feature and the orphan sweep only ever touches one feature's folder.
        // Add an area = add a BlobAreas constant here and an IBlobReferenceSource for the sweep.
        private static readonly string[] Areas = [BlobAreas.ListItem, BlobAreas.RecipeAttachment];

        // Selects the blob backend by FileStorage:Provider ("Local" default for dev/test/CI, "Gcs"
        // for prod). Each area is registered as a keyed singleton under both IFileStorage (hot path)
        // and IFileStorageMaintenance (sweep listing). Construction is deferred in the factory lambdas
        // so DI build / build-time OpenAPI generation never touches the filesystem or a GCS client.
        public static IServiceCollection AddFileStorage(
            this IServiceCollection services, IConfiguration configuration)
        {
            // Environment token (e.g. "stage"/"prod") composed into every area's prefix so stage and
            // prod can share one bucket without colliding. Empty falls back to bare area names.
            var environment = configuration["FileStorage:Environment"];

            var provider = configuration["FileStorage:Provider"];
            if (string.Equals(provider, "Gcs", StringComparison.OrdinalIgnoreCase))
            {
                AddGcs(services, configuration, environment);
            }
            else
            {
                AddLocal(services, configuration, environment);
            }

            // The orphan sweep iterates these to learn which keys each area still references.
            services.AddScoped<IBlobReferenceSource, ListItemBlobReferences>();
            services.AddScoped<IBlobReferenceSource, RecipeAttachmentBlobReferences>();

            return services;
        }

        // Composes the GCS object-name prefix / Local subdir segment: {env}/{area}, env outermost so
        // the bucket groups by environment (GCS renders '/' as nested console folders). No env → bare area.
        public static string ComposePrefix(string? environment, string area)
        {
            return string.IsNullOrWhiteSpace(environment) ? area : $"{environment}/{area}";
        }

        private static void AddLocal(IServiceCollection services, IConfiguration configuration, string? environment)
        {
            // When FileStorage:LocalPath is unset we fall back to a "blobs" directory under the content
            // root — fine for dev/test. In a container this path is ephemeral (lost on restart) unless
            // it points at a mounted volume; production should use the Gcs provider.
            var configured = configuration["FileStorage:LocalPath"];

            foreach (var area in Areas)
            {
                // Each area is its own subtree {base}/{env}/{area}; keys stay flat GUIDs within it.
                var subPath = ComposePrefix(environment, area);
                services.AddKeyedSingleton<LocalFileStorage>(area, (sp, _) =>
                {
                    var baseRoot = string.IsNullOrWhiteSpace(configured)
                        ? Path.Combine(sp.GetRequiredService<IHostEnvironment>().ContentRootPath, "blobs")
                        : configured;
                    return new LocalFileStorage(Path.Combine(baseRoot, subPath));
                });
                services.AddKeyedSingleton<IFileStorage>(
                    area, (sp, key) => sp.GetRequiredKeyedService<LocalFileStorage>(key));
                services.AddKeyedSingleton<IFileStorageMaintenance>(
                    area, (sp, key) => sp.GetRequiredKeyedService<LocalFileStorage>(key));
            }
        }

        private static void AddGcs(IServiceCollection services, IConfiguration configuration, string? environment)
        {
            var bucket = configuration["FileStorage:Bucket"];
            var accessJson = configuration
                .GetSection(FirebaseSettings.SECTION_NAME)
                .Get<FirebaseSettings>()?.AccessJson;

            // One shared StorageClient (thread-safe) for every area; deferred so DI build / build-time
            // OpenAPI generation never constructs a real GCS client.
            services.AddSingleton<StorageClient>(_ =>
            {
                if (string.IsNullOrWhiteSpace(accessJson))
                {
                    throw new InvalidOperationException(
                        "FirebaseSettings:AccessJson is required for the GCS file storage backend.");
                }

                var credential = CredentialFactory
                    .FromJson<ServiceAccountCredential>(accessJson)
                    .ToGoogleCredential();
                return StorageClient.Create(credential);
            });

            foreach (var area in Areas)
            {
                var prefix = ComposePrefix(environment, area);
                services.AddKeyedSingleton<GcsFileStorage>(area, (sp, _) =>
                {
                    if (string.IsNullOrWhiteSpace(bucket))
                    {
                        throw new InvalidOperationException(
                            "FileStorage:Bucket is required when FileStorage:Provider is 'Gcs'.");
                    }

                    return new GcsFileStorage(sp.GetRequiredService<StorageClient>(), bucket, prefix);
                });
                services.AddKeyedSingleton<IFileStorage>(
                    area, (sp, key) => sp.GetRequiredKeyedService<GcsFileStorage>(key));
                services.AddKeyedSingleton<IFileStorageMaintenance>(
                    area, (sp, key) => sp.GetRequiredKeyedService<GcsFileStorage>(key));
            }
        }
    }
}
