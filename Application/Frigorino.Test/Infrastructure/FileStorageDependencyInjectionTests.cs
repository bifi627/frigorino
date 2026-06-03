using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Test.Infrastructure
{
    public class FileStorageDependencyInjectionTests
    {
        [Fact]
        public void LocalProvider_RegistersSameInstance_ForBothInterfaces()
        {
            var localPath = Path.Combine(
                Path.GetTempPath(), "frigorino-di-test-" + Guid.NewGuid().ToString("N"));
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FileStorage:Provider"] = "Local",
                    ["FileStorage:LocalPath"] = localPath,
                })
                .Build();

            var services = new ServiceCollection();
            services.AddFileStorage(config);
            using var sp = services.BuildServiceProvider();

            var hotPath = sp.GetRequiredService<IFileStorage>();
            var maintenance = sp.GetRequiredService<IFileStorageMaintenance>();

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

            Assert.IsType<LocalFileStorage>(sp.GetRequiredService<IFileStorage>());
        }
    }
}
