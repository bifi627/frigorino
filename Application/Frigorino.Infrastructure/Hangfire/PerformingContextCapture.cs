using Hangfire.Server;

namespace Frigorino.Infrastructure.Hangfire
{
    // Captures the running job's PerformingContext into an AsyncLocal so the ILogger bridge can
    // find it without jobs taking a PerformContext parameter (keeps job code Hangfire-free).
    internal sealed class PerformingContextCapture : IServerFilter
    {
        private static readonly AsyncLocal<PerformingContext?> Current = new();

        public static PerformingContext? Value => Current.Value;

        public void OnPerforming(PerformingContext filterContext)
        {
            Current.Value = filterContext;
        }

        public void OnPerformed(PerformedContext filterContext)
        {
            Current.Value = null;
        }
    }
}
