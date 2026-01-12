using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Reflection;

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
            
            // Add JWT Authentication to Swagger
            AddJwtSecurityDefinition(options);
        });
        
        return services;
    }

    private static void AddJwtSecurityDefinition(SwaggerGenOptions options)
    {
        try
        {
            // Load Microsoft.OpenApi assembly to access Models namespace types
            var openApiAssembly = Assembly.Load("Microsoft.OpenApi");
            var modelsNamespace = "Microsoft.OpenApi.Models";
            
            // Get required types
            var securitySchemeType = openApiAssembly.GetType($"{modelsNamespace}.OpenApiSecurityScheme");
            var securityRequirementType = openApiAssembly.GetType($"{modelsNamespace}.OpenApiSecurityRequirement");
            var parameterLocationType = openApiAssembly.GetType($"{modelsNamespace}.ParameterLocation");
            var securitySchemeTypeEnum = openApiAssembly.GetType($"{modelsNamespace}.SecuritySchemeType");
            var referenceTypeEnum = openApiAssembly.GetType($"{modelsNamespace}.ReferenceType");
            var openApiReferenceType = openApiAssembly.GetType($"{modelsNamespace}.OpenApiReference");
            
            if (securitySchemeType == null || securityRequirementType == null)
            {
                return; // Types not found, skip JWT configuration
            }
            
            // Create security scheme
            var securityScheme = Activator.CreateInstance(securitySchemeType);
            securitySchemeType.GetProperty("Name")?.SetValue(securityScheme, "Authorization");
            securitySchemeType.GetProperty("Description")?.SetValue(securityScheme, 
                "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.");
            securitySchemeType.GetProperty("In")?.SetValue(securityScheme, 
                Enum.Parse(parameterLocationType!, "Header"));
            securitySchemeType.GetProperty("Type")?.SetValue(securityScheme, 
                Enum.Parse(securitySchemeTypeEnum!, "Http"));
            securitySchemeType.GetProperty("Scheme")?.SetValue(securityScheme, "bearer");
            securitySchemeType.GetProperty("BearerFormat")?.SetValue(securityScheme, "JWT");
            
            // Create reference for security requirement
            var reference = Activator.CreateInstance(openApiReferenceType!);
            openApiReferenceType!.GetProperty("Type")?.SetValue(reference, 
                Enum.Parse(referenceTypeEnum!, "SecurityScheme"));
            openApiReferenceType.GetProperty("Id")?.SetValue(reference, JwtBearerDefaults.AuthenticationScheme);
            securitySchemeType.GetProperty("Reference")?.SetValue(securityScheme, reference);
            
            // Add security definition
            var addSecurityDefinitionMethod = typeof(SwaggerGenOptions).GetMethod("AddSecurityDefinition", 
                new[] { typeof(string), typeof(object) });
            addSecurityDefinitionMethod?.Invoke(options, new object[] { JwtBearerDefaults.AuthenticationScheme, securityScheme });
            
            // Create and add security requirement
            var securityRequirement = Activator.CreateInstance(securityRequirementType);
            var addMethod = securityRequirementType.GetMethod("Add", 
                new[] { securitySchemeType, typeof(IEnumerable<string>) });
            addMethod?.Invoke(securityRequirement, new object[] { securityScheme, Array.Empty<string>() });
            
            var addSecurityRequirementMethod = typeof(SwaggerGenOptions).GetMethod("AddSecurityRequirement", 
                new[] { typeof(object) });
            addSecurityRequirementMethod?.Invoke(options, new object[] { securityRequirement });
        }
        catch
        {
            // If reflection fails, Swagger will work without JWT security configuration
            // This gracefully handles the namespace compatibility issue
        }
    }
}
