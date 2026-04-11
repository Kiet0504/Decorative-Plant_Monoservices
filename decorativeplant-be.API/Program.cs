using Serilog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using DotNetEnv;
using decorativeplant_be.Application;
using decorativeplant_be.Infrastructure;
using decorativeplant_be.Infrastructure.Data;
using decorativeplant_be.API.Extensions;
using decorativeplant_be.API.Middleware;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

// Load environment variables from .env file (if it exists)
// TraversePath allows it to look up the directory tree to find the .env file in the root folder
try
{
    Env.TraversePath().Load();
}
catch (Exception)
{
    // .env file is optional - environment variables can be set directly
    Console.WriteLine("Warning: .env file not found. Using environment variables or appsettings.json.");
}

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .AddEnvironmentVariables()
        .Build())
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting web application");

    var builder = WebApplication.CreateBuilder(args);

    // Flat env vars like S3_Region do not bind to S3:Region; only S3__Region does.
    // Map common Infisical/Docker names so Region/Bucket/keys are picked up.
    ApplyFlatS3EnvironmentVariables(builder.Configuration);

    // Use Serilog for logging
    builder.Host.UseSerilog();

    // Add services to the container
    builder.Services.AddControllers();

    // Add CORS policy for React frontend and Flutter web
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5173",  // React Vite
                    "http://localhost:3000",  // React dev server
                    "http://localhost:4173"   // React preview
                );
            policy.SetIsOriginAllowed(origin =>
                    new Uri(origin).Host == "localhost")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });

        // Add policy for Flutter web (allows any localhost port for development)
        options.AddPolicy("AllowFlutterWeb", policy =>
        {
            policy.SetIsOriginAllowed(origin =>
                {
                    if (string.IsNullOrEmpty(origin)) return false;
                    var uri = new Uri(origin);
                    return uri.Host == "localhost" || uri.Host == "127.0.0.1";
                })
.AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    // Add API Versioning
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
    });

    // Add Swagger/OpenAPI
    builder.Services.AddSwaggerServices();

    // Add Application services (MediatR, AutoMapper, FluentValidation)
    builder.Services.AddApplicationServices();

    // Add Infrastructure services (DbContext, Identity, JWT, Repositories, etc.)
    builder.Services.AddInfrastructureServices(builder.Configuration);

    // Add Health Checks
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<ApplicationDbContext>("database");
        
    // Add Rate Limiting
    builder.Services.AddRateLimiter(options =>
    {
        options.AddFixedWindowLimiter("CartAndOrderPolicy", opt =>
        {
            opt.PermitLimit = 30; // 30 requests per minute
            opt.Window = TimeSpan.FromMinutes(1);
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            opt.QueueLimit = 0;
        });
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    var app = builder.Build();

    var applyMigrations = app.Environment.IsDevelopment()
        || app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false);
    if (applyMigrations)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            context.Database.Migrate();
            Log.Information("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while migrating the database");
            throw;
        }
    }

    // Configure the HTTP request pipeline
    app.UseSerilogRequestLogging();

    // Exception handling middleware (must be first)
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // Request logging middleware
    app.UseMiddleware<RequestLoggingMiddleware>();

    // Swagger: enable in Development, or when running in Docker (so you can test from host)
    if (app.Environment.IsDevelopment() || string.Equals(Environment.GetEnvironmentVariable("SWAGGER_ENABLED"), "true", StringComparison.OrdinalIgnoreCase))
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Decorative Plant API v1");
            c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
        });
    }

    app.UseHttpsRedirection();

    app.UseRouting();
    // Use the more permissive Flutter web policy in development
    app.UseCors(app.Environment.IsDevelopment() ? "AllowFlutterWeb" : "AllowFrontend");
app.UseAuthentication();
    app.UseMiddleware<BranchScopedAccessMiddleware>(); // Branch-scoped access control - after UseAuthentication, before UseAuthorization
    app.UseMiddleware<SoftPaywallMiddleware>();
    app.UseAuthorization();

    app.MapControllers();

    // Map health check endpoints
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready");
    app.MapHealthChecks("/health/live");

    Log.Information("Application started successfully");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

static void ApplyFlatS3EnvironmentVariables(ConfigurationManager configuration)
{
    void MapIfEmpty(string configKey, params string[] envVarNames)
    {
        if (!string.IsNullOrWhiteSpace(configuration[configKey]))
            return;
        foreach (var name in envVarNames)
        {
            var v = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(v))
            {
                configuration[configKey] = v.Trim();
                return;
            }
        }
    }

    MapIfEmpty("S3:Region", "S3_Region", "S3_REGION");
    MapIfEmpty("S3:Bucket", "S3_Bucket");
    MapIfEmpty("S3:AccessKeyId", "S3_AccessKeyId", "S3_AccesskeyID");
    MapIfEmpty("S3:SecretAccessKey", "S3_SecretAccessKey", "S3_SecreteAcessKey");
}