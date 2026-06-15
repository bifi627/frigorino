using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Test.Infrastructure
{
    public class FileStorageDependencyInjectionTests
    {
        [Fact]
        public void LocalProvider_RegistersSameInstance_ForBothInterfaces_PerArea()
        {
            var localPath = Path.Combine(
                Path.GetTempPath(), "frigorino-di-test-" + Guid.NewGuid().ToString("N"));
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FileStorage:Provider"] = "Local",
                    ["FileStorage:LocalPath"] = localPath,
                    ["FileStorage:Environment"] = "test",
                })
                .Build();

            var services = new ServiceCollection();
            services.AddFileStorage(config);
            using var sp = services.BuildServiceProvider();

            var hotPath = sp.GetRequiredKeyedService<IFileStorage>(BlobAreas.ListItem);
            var maintenance = sp.GetRequiredKeyedService<IFileStorageMaintenance>(BlobAreas.ListItem);

            Assert.IsType<LocalFileStorage>(hotPath);
            Assert.Same(hotPath, maintenance);
        }

        [Fact]
        public void DefaultProvider_IsLocal_WhenUnset()
        {
            var localPath = Path.Combine(
                Path.GetTempPath(), "frigorino-di-test-" + Guid.NewGuid().ToString("N"));
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FileStorage:LocalPath"] = localPath,
                })
                .Build();

            var services = new ServiceCollection();
            services.AddFileStorage(config);
            using var sp = services.BuildServiceProvider();

            Assert.IsType<LocalFileStorage>(sp.GetRequiredKeyedService<IFileStorage>(BlobAreas.ListItem));
        }

        [Fact]
        public void ComposePrefix_NestsEnvironmentThenArea()
        {
            Assert.Equal("stage/list-item", FileStorageDependencyInjection.ComposePrefix("stage", BlobAreas.ListItem));
        }

        [Fact]
        public void ComposePrefix_OmitsEnvironment_WhenBlank()
        {
            Assert.Equal("list-item", FileStorageDependencyInjection.ComposePrefix("", BlobAreas.ListItem));
            Assert.Equal("list-item", FileStorageDependencyInjection.ComposePrefix(null, BlobAreas.ListItem));
        }
    }
}
