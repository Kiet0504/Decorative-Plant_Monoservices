using Swashbuckle.AspNetCore.SwaggerGen;

namespace decorativeplant_be.API.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerServices(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new()
            {
                Title = "Decorative Plant API",
                Version = "v1",
                Description = "Decorative Plant Backend API"
            });
            
            // Note: JWT security configuration will be added once OpenApi types namespace issue is resolved
            // This is a known compatibility issue with .NET 10.0 and Swashbuckle 10.1.0
        });
        
        return services;
    }
}
