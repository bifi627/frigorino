using System.Net;
using Frigorino.Infrastructure.Services;

namespace Frigorino.Test.Infrastructure
{
    public class RecipeImportUrlTests
    {
        [Theory]
        [InlineData("https://example.com/recipe", true)]
        [InlineData("http://example.com", true)]
        [InlineData("ftp://example.com/x", false)]
        [InlineData("file:///etc/passwd", false)]
        [InlineData("not a url", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void TryParseHttpUrl_accepts_only_http_and_https(string? raw, bool expected)
        {
            Assert.Equal(expected, RecipeImportUrl.TryParseHttpUrl(raw, out _));
        }

        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("10.1.2.3")]
        [InlineData("172.16.5.4")]
        [InlineData("172.31.255.255")]
        [InlineData("192.168.0.1")]
        [InlineData("169.254.169.254")] // cloud metadata
        [InlineData("100.64.0.1")]      // CGNAT
        [InlineData("0.0.0.0")]
        [InlineData("::1")]
        [InlineData("fe80::1")]         // link-local v6
        [InlineData("fc00::1")]         // unique-local v6
        [InlineData("::ffff:10.0.0.1")] // IPv4-mapped private
        public void IsPublicIpAddress_rejects_non_public(string ip)
        {
            Assert.False(RecipeImportUrl.IsPublicIpAddress(IPAddress.Parse(ip)));
        }

        [Theory]
        [InlineData("8.8.8.8")]
        [InlineData("1.1.1.1")]
        [InlineData("93.184.216.34")]
        [InlineData("2606:4700:4700::1111")]
        public void IsPublicIpAddress_allows_public(string ip)
        {
            Assert.True(RecipeImportUrl.IsPublicIpAddress(IPAddress.Parse(ip)));
        }
    }
}
