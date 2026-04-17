using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using decorativeplant_be.Application.Common.Behaviors;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.RoomScan.Services;
using decorativeplant_be.Application.Services.ContentSafety;
using decorativeplant_be.Application.Services.PlantAssistantScope;
using decorativeplant_be.Application.Services.Recommendations;

namespace decorativeplant_be.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        // Register AutoMapper
        services.AddAutoMapper(typeof(ApplicationServiceRegistration));

        // Register FluentValidation
        services.AddValidatorsFromAssembly(typeof(ApplicationServiceRegistration).Assembly);

        // Register MediatR Pipeline Behaviors
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

        // Recommendations
        services.AddScoped<IRecommendationEngine, RuleBasedRecommendationEngine>();

        services.AddScoped<IRoomScanCatalogRankingService, RoomScanCatalogRankingService>();

        services.AddSingleton<IUserContentSafetyService, UserContentSafetyService>();
        services.AddSingleton<IPlantAssistantScopeService, PlantAssistantScopeService>();

        return services;
    }
}
