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
using decorativeplant_be.Infrastructure.Ghtk;
using decorativeplant_be.Infrastructure.Storage.S3;
using decorativeplant_be.Infrastructure.Auth;
using Amazon.S3;
using Amazon.Runtime;
using Amazon;
using decorativeplant_be.Application.Common.Settings;
using decorativeplant_be.Application.Common.Security;

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

        // Google OAuth / Frontend redirect settings (Infisical / env-driven)
        services.Configure<GoogleOAuthSettings>(configuration.GetSection(GoogleOAuthSettings.SectionName));
        services.Configure<FrontendSettings>(configuration.GetSection(FrontendSettings.SectionName));
        services.AddScoped<GoogleOAuthService>();
        services.AddScoped<GoogleCalendarService>();

        // Configure GHN (Giao Hang Nhanh) Settings
        services.Configure<GhnSettings>(configuration.GetSection(GhnSettings.SectionName));
        services.AddHttpClient<IShippingService, GhnService>();

        // Configure GHTK (Giao Hang Tiet Kiem) — parallel carrier. Docs: https://api.ghtk.vn/docs/
        services.Configure<GhtkSettings>(configuration.GetSection(GhtkSettings.SectionName));
        services.AddHttpClient<IGhtkShippingService, GhtkService>();

        // Register Branch Allocation Service (Chain Store model)
        services.AddScoped<decorativeplant_be.Application.Services.IBranchAllocationService,
                           decorativeplant_be.Application.Services.BranchAllocationService>();
        // Register Stock Service (shared lock/reserve/deduct logic)
        services.AddScoped<decorativeplant_be.Application.Services.IStockService,
                           decorativeplant_be.Application.Services.StockService>();
        // Register MQTT Service
        services.AddSingleton<MqttService>();
        services.AddHostedService<MqttService>(provider => provider.GetRequiredService<MqttService>());
        services.AddSingleton<IMqttService>(provider => provider.GetRequiredService<MqttService>());

        // Order assignment service (workload-based, scoped per request/job)
        services.AddScoped<decorativeplant_be.Application.Common.Interfaces.IOrderAssignmentService,
                           decorativeplant_be.Application.Services.OrderAssignmentService>();

        // Register Background Jobs
        services.AddHostedService<decorativeplant_be.Infrastructure.BackgroundJobs.MonthlyQuotaResetJob>();
        services.AddHostedService<decorativeplant_be.Infrastructure.BackgroundJobs.PendingOrderCleanupJob>();
        services.AddHostedService<decorativeplant_be.Infrastructure.BackgroundJobs.CareTaskReminderJob>();
        services.AddHostedService<decorativeplant_be.Infrastructure.BackgroundJobs.AutoCompleteDeliveredOrdersJob>();
        services.AddHostedService<decorativeplant_be.Infrastructure.BackgroundJobs.OrderAssignmentQueueJob>();
        services.AddHostedService<decorativeplant_be.Infrastructure.BackgroundJobs.IotHeartbeatMonitorJob>();

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
            redisConnectionString = NormalizeRedisConnectionStringForStackExchangeRedis(redisConnectionString);
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
        // AI Diagnosis: OpenAI-only or Gemini + Ollama (see AiDiagnosis:Provider)
        services.Configure<AiDiagnosisSettings>(configuration.GetSection(AiDiagnosisSettings.SectionName));
        services.AddHttpClient();
        services.AddScoped<OpenAiDiagnosisService>();
        services.AddScoped<GeminiPlantDiseaseDetectionService>();
        services.AddScoped<OllamaDiagnosisReasoningService>();
        services.AddScoped<GeminiOllamaDiagnosisService>();
        services.AddScoped<IPlantDiagnosisFromBase64Service>(sp =>
            sp.GetRequiredService<GeminiOllamaDiagnosisService>());
        services.AddScoped<IChatDiagnosisPipelineSettings, ChatDiagnosisPipelineSettings>();
        services.Configure<RoomScanSettings>(configuration.GetSection(RoomScanSettings.SectionName));
        services.AddScoped<IRoomScanGeminiClient, GeminiRoomScanClient>();
        services.AddScoped<IAiDiagnosisService>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiDiagnosisSettings>>().Value;
            return string.Equals(opts.Provider, "GeminiOllama", StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<GeminiOllamaDiagnosisService>()
                : sp.GetRequiredService<OpenAiDiagnosisService>();
        });

        // Local AI (Ollama) or Gemini-only when AiRouting:UseGeminiOnly=true (see OllamaOrGeminiClient)
        services.Configure<AiRoutingSettings>(configuration.GetSection(AiRoutingSettings.SectionName));
        services.Configure<OllamaSettings>(configuration.GetSection(OllamaSettings.SectionName));
        services.AddScoped<OllamaClient>();
        services.AddScoped<GeminiGenerativeContentClient>();
        services.AddScoped<IOllamaClient, OllamaOrGeminiClient>();
        services.AddScoped<IChatImageIntentClassifier, OllamaChatImageIntentClassifier>();
        services.AddScoped<decorativeplant_be.Application.Common.Interfaces.IRoomScanChatSuggestionIntentDetector,
            OllamaRoomScanChatSuggestionIntentDetector>();
        services.AddScoped<decorativeplant_be.Application.Common.Interfaces.IAiChatProfileShopIntentDetector,
            OllamaAiChatProfileShopIntentDetector>();

        services.Configure<AiCareAdviceSettings>(configuration.GetSection(AiCareAdviceSettings.SectionName));

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
        services.AddScoped<IObjectStorageService, S3ObjectStorageService>();

        // AR preview viewer token signing
        services.AddScoped<IArPreviewTokenService, ArPreviewTokenService>();

        services.Configure<AiLiveSettings>(configuration.GetSection(AiLiveSettings.SectionName));
        services.AddHttpClient<IGeminiLiveEphemeralTokenService, GeminiLiveEphemeralTokenService>();

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

    /// <summary>
    /// Converts <c>redis://</c> / <c>rediss://</c> URLs (e.g. Redis Cloud) to StackExchange.Redis format.
    /// Passing URI-style strings directly to <see cref="StackExchange.Redis.ConfigurationOptions.Parse(string)"/>
    /// can produce invalid endpoints (e.g. <c>:13424:6379</c>).
    /// </summary>
    private static string NormalizeRedisConnectionStringForStackExchangeRedis(string connectionString)
    {
        var s = connectionString.Trim();
        if (!s.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) &&
            !s.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
        {
            return s;
        }

        var ssl = s.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase);
        var schemeEnd = s.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0)
            return connectionString;

        var rest = s.AsSpan(schemeEnd + 3);
        var at = rest.LastIndexOf('@');
        if (at < 0)
            return connectionString;

        var userInfo = rest[..at].ToString();
        var hostPort = rest[(at + 1)..].ToString().Trim();
        if (string.IsNullOrEmpty(hostPort))
            return connectionString;

        string user = "default";
        string password;
        var colonIdx = userInfo.IndexOf(':');
        if (colonIdx >= 0)
        {
            user = Uri.UnescapeDataString(userInfo[..colonIdx]);
            password = Uri.UnescapeDataString(userInfo[(colonIdx + 1)..]);
        }
        else
        {
            password = Uri.UnescapeDataString(userInfo);
        }

        // Match the URL scheme from Redis Cloud / Valkey: <c>redis://</c> = plain TCP, <c>rediss://</c> = TLS.
        // Forcing ssl=true for <c>redis://</c> breaks the handshake with "Cannot determine the frame size or a corrupted frame was received".

        return $"{hostPort},user={user},password={password},ssl={(ssl ? "true" : "false")},abortConnect=false";
    }
}
