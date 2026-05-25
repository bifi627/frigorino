using FirebaseAdmin;
using Frigorino.Infrastructure.EntityFramework;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Frigorino.Infrastructure.Auth
{
    public static class FirebaseAuth
    {
        public static IServiceCollection AddFirebaseAuth(this IServiceCollection services, IConfiguration configuration)
        {
            var firebaseSettingsSection = configuration.GetSection(FirebaseSettings.SECTION_NAME);

            var firebaseConfig = firebaseSettingsSection.Get<FirebaseSettings>();

            var app = FirebaseApp.Create(new AppOptions
            {
                Credential = CredentialFactory.FromJson<ServiceAccountCredential>(firebaseConfig!.AccessJson).ToGoogleCredential(),
            });

            services.AddSingleton(FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance);

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = firebaseConfig.ValidIssuer;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = firebaseConfig.ValidIssuer,
                        ValidAudience = firebaseConfig.ValidAudience
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var path = context.HttpContext.Request.Path;

                            // SignalR keeps its token in the query string (long-lived connection).
                            var accessToken = context.Request.Query["access_token"];
                            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/signalr"))
                            {
                                context.Token = accessToken;
                                return Task.CompletedTask;
                            }

                            // The Hangfire dashboard is browser-navigated and fires its own polling/asset
                            // sub-requests that can't carry a bearer header, so read the Firebase token from
                            // a path-scoped cookie. Strictly scoped to /hangfire AND only when no
                            // Authorization header is present, so the /api bearer flow is untouched.
                            if (path.StartsWithSegments("/hangfire")
                                && string.IsNullOrEmpty(context.Request.Headers.Authorization)
                                && context.Request.Cookies.TryGetValue("hf_dashboard_token", out var cookieToken)
                                && !string.IsNullOrEmpty(cookieToken))
                            {
                                context.Token = cookieToken;
                            }

                            return Task.CompletedTask;
                        },
                        OnTokenValidated = SyncUserOnLoginAsync,
                    };

                    options.Validate();
                });

            return services;
        }

        // Fires only on successful JWT validation (before UseAuthorization), so the DB
        // hit inside UserSync is gated by a fully-validated principal. Failed validations
        // never reach this handler.
        private static Task SyncUserOnLoginAsync(TokenValidatedContext context)
        {
            if (context.Principal is null)
            {
                return Task.CompletedTask;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            return UserSync.EnsureAsync(context.Principal, db, context.HttpContext.RequestAborted);
        }
    }
}
