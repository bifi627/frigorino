using System.Net;
using System.Net.Sockets;

namespace Frigorino.Infrastructure.Services
{
    // Pure SSRF guards for the server-side recipe fetch.
    public static class RecipeImportUrl
    {
        public static bool TryParseHttpUrl(string? raw, out Uri uri)
        {
            uri = null!;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }
            if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var parsed))
            {
                return false;
            }
            if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }
            uri = parsed;
            return true;
        }

        // True only for globally-routable addresses. Rejects loopback / private / CGNAT / link-local
        // (incl. 169.254.169.254 metadata) / unique-local / unspecified, and IPv4-mapped variants.
        public static bool IsPublicIpAddress(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }
            if (IPAddress.IsLoopback(address)
                || address.Equals(IPAddress.Any)
                || address.Equals(IPAddress.IPv6Any))
            {
                return false;
            }

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = address.GetAddressBytes();
                if (b[0] == 10)
                {
                    return false;
                }
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                {
                    return false;
                }
                if (b[0] == 192 && b[1] == 168)
                {
                    return false;
                }
                if (b[0] == 169 && b[1] == 254)
                {
                    return false;
                }
                if (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                {
                    return false;
                }
                return true;
            }

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
                {
                    return false;
                }
                var b = address.GetAddressBytes();
                if ((b[0] & 0xFE) == 0xFC)
                {
                    return false;
                }
                return true;
            }

            return false;
        }
    }
}
