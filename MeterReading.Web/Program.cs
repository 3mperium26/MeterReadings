using Microsoft.EntityFrameworkCore;
using MeterReading.Domain.Repositories;
using MeterReading.Infrastructure.Persistence;
using MeterReading.Infrastructure.Persistence.Repositories;
using MeterReading.Application.Interfaces;
using MeterReading.Application.Services;
using MeterReading.Infrastructure.CSVParsing;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOpt => sqlOpt.EnableRetryOnFailure()));

builder.Services.AddScoped<IMeterReadingUploadService, MeterReadingUploadService>();
builder.Services.AddScoped<ICSVParseHelper, CSVParseHelper>();

builder.Services.AddScoped<IAccountRepository, AccountRepository>();

builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Meter Reading Upload API", Version = "v1", Description = "API endpoints for uploading meter readings." });
});

builder.Services.AddLogging(config =>
{
    config.AddConfiguration(builder.Configuration.GetSection("Logging"));
    config.AddConsole();
    config.AddDebug();
});


var app = builder.Build();

bool applyMigrationsOnStartup = app.Configuration.GetValue<bool>("ApplyMigrationsOnStartup", defaultValue: app.Environment.IsDevelopment());
if (applyMigrationsOnStartup)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            logger.LogInformation("{Count} pending database migrations...", pendingMigrations.Count());
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Database migrations completed.");
        }
        else
        {
            logger.LogInformation("No pending database migrations found. Ensuring database is created...");
            await dbContext.Database.EnsureCreatedAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "An error occurred while migrating or seeding the database.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Meter Reading Upload API V1");
        c.RoutePrefix = "swagger";
    });
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();
app.Run();