using Frigorino.Domain.Interfaces;
using ImageMagick;
using ImageMagick.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Infrastructure.Services
{
    public static class ImageProcessingDependencyInjection
    {
        // Magick.NET processor is stateless → singleton. Swap the implementation here if the library
        // is ever replaced (the IImageProcessor port keeps callers unchanged).
        public static IServiceCollection AddImageProcessing(this IServiceCollection services)
        {
            // We decode UNTRUSTED uploads. ImageMagick otherwise boots with 100+ coders and external
            // delegates enabled (the ImageTragick CVE class — MSL/MVG/URL/SSRF pseudo-coders), and our
            // app-level format allowlist only runs AFTER the native header parse. So lock the surface
            // down at the native layer first: deny every delegate and coder, then re-allow read/write
            // for only the three formats this feature handles (mirrors MagickImageProcessor's allowlist).
            var configFiles = ConfigurationFiles.Default;
            configFiles.Policy.Data = """
                <policymap>
                  <policy domain="delegate" rights="none" pattern="*" />
                  <policy domain="coder" rights="none" pattern="*" />
                  <policy domain="coder" rights="read|write" pattern="{JPEG,PNG,WEBP}" />
                </policymap>
                """;
            MagickNET.Initialize(configFiles);

            // Defense-in-depth resource bounds behind MagickImageProcessor's per-request 64 MP header
            // guard: cap geometry, total pixel area, frame count, and memory so a tiny-but-huge-declared
            // or many-frame file can't drive a native over-allocation (OOM-kill on Railway's constrained
            // tier). Thread=1 keeps ImageMagick's OpenMP from fanning out threads per request.
            ResourceLimits.Thread = 1;
            ResourceLimits.Width = 65536;
            ResourceLimits.Height = 65536;
            ResourceLimits.Area = 268_435_456;    // 256 MP backstop (per-request guard rejects > 64 MP)
            ResourceLimits.ListLength = 16;        // max frames/scenes in a single file
            ResourceLimits.Memory = 268_435_456;   // 256 MB

            services.AddSingleton<IImageProcessor, MagickImageProcessor>();
            return services;
        }
    }
}
