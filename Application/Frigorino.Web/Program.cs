using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Auth;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Frigorino.Web.Middlewares;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddEntityFramework(builder.Configuration);
builder.Services.AddFirebaseAuth(builder.Configuration);

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<InitialConnectionMiddleware>();

builder.Services.AddSpaStaticFiles(configuration => { configuration.RootPath = "ClientApp/build"; });

var app = builder.Build();

app.UseRouting();
app.MapDefaultControllerRoute();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<InitialConnectionMiddleware>();

app.MapControllers();

app.UseSwagger();
app.UseSwaggerUI();

//app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseSpaStaticFiles();

// Configure the HTTP request pipeline.
app.UseSpa(spa => spa.Options.SourcePath = "ClientApp");

app.MapFallbackToFile("index.html");

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var context = services.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();
}

app.Run();
