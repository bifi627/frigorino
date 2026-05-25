using Hangfire.Server;

namespace Frigorino.Infrastructure.Hangfire
{
    public interface IPerformingContextAccessor
    {
        PerformingContext? Get();
    }

    internal sealed class AsyncLocalPerformingContextAccessor : IPerformingContextAccessor
    {
        public PerformingContext? Get()
        {
            return PerformingContextCapture.Value;
        }
    }
}
