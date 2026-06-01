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
            services.AddSingleton(FirebaseAdmin.Messaging.FirebaseMessaging.DefaultInstance);

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
                            var accessToken = context.Request.Query["access_token"];

                            // If the request is for our hub...
                            var path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrEmpty(accessToken) &&
                                (path.StartsWithSegments("/signalr")))
                            {
                                // Read the token out of the query string
                                context.Token = accessToken;
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
