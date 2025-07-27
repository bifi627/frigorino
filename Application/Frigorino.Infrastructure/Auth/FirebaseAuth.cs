using FirebaseAdmin;
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
                Credential = GoogleCredential.FromJson(firebaseConfig.AccessJson),
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
                        }
                    };

                    options.Validate();
                });

            return services;
        }
    }
}
