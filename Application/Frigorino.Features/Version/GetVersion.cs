using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Frigorino.Features.Version
{
    public sealed record VersionResponse(string Sha);

    public static class GetVersionEndpoint
    {
        public static IEndpointRouteBuilder MapGetVersion(this IEndpointRouteBuilder app)
        {
            app.MapGet("", Handle)
               .WithName("GetVersion")
               .Produces<VersionResponse>(StatusCodes.Status200OK);
            return app;
        }

        private static Ok<VersionResponse> Handle()
        {
            var sha = Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_SHA");
            return TypedResults.Ok(new VersionResponse(string.IsNullOrWhiteSpace(sha) ? "unknown" : sha));
        }
    }
}
