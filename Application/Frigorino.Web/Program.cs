using Frigorino.Application;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Auth;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Frigorino.Web.Auth;
using Frigorino.Web.Middlewares;
using Frigorino.Web.Services;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddEntityFramework(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddFirebaseAuth(builder.Configuration);
builder.Services.AddHangfireServices(builder.Configuration);

builder.Services.AddHttpContextAccessor();

// Add session support for household context
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<InitialConnectionMiddleware>();

builder.Services.AddSpaStaticFiles(configuration => { configuration.RootPath = "ClientApp/build"; });

var app = builder.Build();

// Database migration
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();
}

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Add Hangfire Dashboard with basic authentication
var hangfireUsername = builder.Configuration["HangfireAuth:Username"];
var hangfirePassword = builder.Configuration["HangfireAuth:Password"];

if (string.IsNullOrWhiteSpace(hangfireUsername) || string.IsNullOrWhiteSpace(hangfirePassword))
{
    throw new Exception("Hangfire credentials are not set in configuration.");
}

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireAuthorizationFilter(hangfireUsername, hangfirePassword)]
});

app.UseHttpsRedirection();
app.UseRouting();

// Static files for SPA
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSpaStaticFiles();

// Session middleware (before authentication)
app.UseSession();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Custom middleware
app.UseMiddleware<InitialConnectionMiddleware>();

// API endpoints
app.MapControllers();

// SPA configuration
app.UseSpa(spa =>
{
    spa.Options.SourcePath = "ClientApp";

    if (app.Environment.IsDevelopment())
    {
        //spa.UseProxyToSpaDevelopmentServer("https://localhost:44375");
    }
});

app.MapFallbackToFile("index.html");

// Configure Hangfire recurring jobs
HangfireDependencyInjection.ConfigureHangfireJobs();

app.Run();
