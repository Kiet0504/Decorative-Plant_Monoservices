using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using decorativeplant_be.Application.Common.Behaviors;
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

        return services;
    }
}
