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
// This should be done before any configuration is read
try
{
    Env.Load();
}
catch (FileNotFoundException)
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

    // Use Serilog for logging
    builder.Host.UseSerilog();

    // Add services to the container
    builder.Services.AddControllers();

    // Add CORS policy for React frontend
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5173",  // Vite dev server
                    "http://localhost:3000",  // Fallback dev port
                    "http://localhost:4173"   // Vite preview
                )
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

    // Add CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAllOrigins",
            policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
    });

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

    app.UseCors("AllowFrontend");

    app.UseRateLimiter(); // Must be after routing/cors, before auth ideally

    app.UseRouting();
    app.UseCors("AllowAll");
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
