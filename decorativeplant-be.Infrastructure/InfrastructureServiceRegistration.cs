using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Infrastructure.Data;
using decorativeplant_be.Infrastructure.Data.Repositories;
using decorativeplant_be.Infrastructure.Identity;
using decorativeplant_be.Infrastructure.Jwt;
using decorativeplant_be.Infrastructure.Cache;
using decorativeplant_be.Infrastructure.Email;
using decorativeplant_be.Infrastructure.Services;

namespace decorativeplant_be.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add DbContext with PostgreSQL
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                b =>
                {
                    b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    // Enable retry strategy for all connections (pure PostgreSQL supports it)
                    b.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                }));

        // Register UnitOfWork
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register RepositoryFactory
        services.AddScoped<IRepositoryFactory, RepositoryFactory>();

        // Register Garden Repository (for entities that do not inherit BaseEntity)
        services.AddScoped<IGardenRepository, GardenRepository>();

        // Register Custom Authentication Services
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<IUserAccountService, UserAccountService>();

        // Configure JWT Settings
        var jwtSettings = configuration.GetSection("JwtSettings").Get<JwtSettings>();
        if (jwtSettings == null)
        {
            throw new InvalidOperationException("JwtSettings not found in configuration.");
        }
        services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));

        // Register JWT Service
        services.AddScoped<IJwtService, JwtService>();

        // Configure Redis for refresh token storage
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "DecorativePlant:";
            });
            // Register Redis connection for key scan/revoke-all (e.g. on password reset)
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(_ =>
                StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString));
        }
        else
        {
            // Fallback to in-memory cache if Redis is not configured (for development)
            services.AddDistributedMemoryCache();
        }

        // Register Refresh Token Service (optional Redis connection for RevokeAll when Redis is configured)
        services.AddScoped<IRefreshTokenService>(sp =>
        {
            var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RedisRefreshTokenService>>();
            var connection = sp.GetService<StackExchange.Redis.IConnectionMultiplexer>();
            return new RedisRefreshTokenService(cache, logger, connection);
        });

        // PayOS Payment Service
        services.AddSingleton<IPayOSService, decorativeplant_be.Infrastructure.Services.PayOSService>();

        // Register IApplicationDbContext
        services.AddScoped<decorativeplant_be.Application.Common.Interfaces.IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());
        // AI Diagnosis (OpenAI)
        services.Configure<AiDiagnosisSettings>(configuration.GetSection(AiDiagnosisSettings.SectionName));
        services.AddHttpClient();
        services.AddScoped<IAiDiagnosisService, OpenAiDiagnosisService>();

        // Email and OTP
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<IOtpService, OtpService>();

        // Configure JWT Authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                ClockSkew = TimeSpan.Zero
            };
        });

        return services;
    }
}
