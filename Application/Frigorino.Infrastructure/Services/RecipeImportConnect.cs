using System.Net;
using System.Net.Sockets;

namespace Frigorino.Infrastructure.Services
{
    internal static class RecipeImportConnect
    {
        // ponytail: ConnectCallback IP check is the load-bearing SSRF defense. It validates the actual
        // resolved IP and connects directly to it, so DNS-rebinding and redirect-to-private are both
        // covered (every connection, including each redirect hop, runs this).
        public static async ValueTask<Stream> ConnectAsync(
            SocketsHttpConnectionContext context, CancellationToken ct)
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
            if (addresses.Length == 0 || addresses.Any(a => !RecipeImportUrl.IsPublicIpAddress(a)))
            {
                // Reject the whole host if ANY record is non-public (defeats split-horizon rebinding).
                throw new IOException("Refused to connect to a non-public address.");
            }

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    }
}
