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
using decorativeplant_be.Infrastructure.Ghn;
using decorativeplant_be.Infrastructure.Storage.S3;
using Amazon.S3;
using Amazon.Runtime;
using Amazon;

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

        // Register IoT Repository
        services.AddScoped<IIotRepository, IotRepository>();

        // Register Custom Authentication Services
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<IUserAccountService, UserAccountService>();

        // Register Subscription Service
        services.AddScoped<ISubscriptionService, SubscriptionService>();

        // Register Quota Service
        services.AddScoped<IQuotaService, QuotaService>();

        // Register Analytics Service
        services.AddScoped<IAnalyticsService, AnalyticsService>();

        // Configure GHN (Giao Hang Nhanh) Settings
        services.Configure<GhnSettings>(configuration.GetSection(GhnSettings.SectionName));
        services.AddHttpClient<IShippingService, GhnService>();

        // Register Branch Allocation Service (Chain Store model)
        services.AddScoped<decorativeplant_be.Application.Services.IBranchAllocationService, 
                           decorativeplant_be.Application.Services.BranchAllocationService>();
        // Register MQTT Service
        services.AddSingleton<MqttService>();
        services.AddHostedService<MqttService>(provider => provider.GetRequiredService<MqttService>());
        services.AddSingleton<IMqttService>(provider => provider.GetRequiredService<MqttService>());

        // Register Background Jobs
        services.AddHostedService<decorativeplant_be.Infrastructure.BackgroundJobs.MonthlyQuotaResetJob>();
        services.AddHostedService<decorativeplant_be.Infrastructure.BackgroundJobs.PendingOrderCleanupJob>();

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
            {
                var options = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString);
                options.AbortOnConnectFail = false;
                return StackExchange.Redis.ConnectionMultiplexer.Connect(options);
            });
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

        // S3 media storage
        services.Configure<S3Settings>(configuration.GetSection(S3Settings.SectionName));
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var s3 = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<S3Settings>>().Value;
            var region = s3.Region;
            if (string.IsNullOrWhiteSpace(region))
            {
                region = Environment.GetEnvironmentVariable("S3__Region")
                    ?? Environment.GetEnvironmentVariable("S3_Region");
            }
            if (string.IsNullOrWhiteSpace(region))
            {
                region = Environment.GetEnvironmentVariable("AWS_REGION")
                    ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION");
            }
            if (string.IsNullOrWhiteSpace(region))
            {
                throw new InvalidOperationException(
                    "S3 region is not configured. Set S3:Region, S3__Region, or S3_Region, or AWS_REGION.");
            }
            if (string.IsNullOrWhiteSpace(s3.AccessKeyId) || string.IsNullOrWhiteSpace(s3.SecretAccessKey))
            {
                throw new InvalidOperationException("S3 credentials are not configured.");
            }
            var creds = new BasicAWSCredentials(s3.AccessKeyId, s3.SecretAccessKey);
            return new AmazonS3Client(creds, RegionEndpoint.GetBySystemName(region));
        });
        services.AddScoped<decorativeplant_be.Application.Common.Interfaces.IMediaStorageService, S3MediaStorageService>();

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
