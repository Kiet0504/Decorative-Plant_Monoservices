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
                // Supports both formats:
                // 1. Simple: host:port (for local Redis without auth)
                // 2. Full: redis://username:password@host:port (for Redis Labs/cloud with auth)
                options.Configuration = redisConnectionString;
                options.InstanceName = "DecorativePlant:";
            });
        }
        else
        {
            // Fallback to in-memory cache if Redis is not configured (for development)
            services.AddDistributedMemoryCache();
        }

        // Register Refresh Token Service
        services.AddScoped<IRefreshTokenService, RedisRefreshTokenService>();

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
