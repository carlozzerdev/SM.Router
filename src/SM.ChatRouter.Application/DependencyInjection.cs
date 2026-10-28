using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SM.ChatRouter.Application.Options;
using SM.ChatRouter.Application.Services;

namespace SM.ChatRouter.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RoutingOptions>()
            .Bind(configuration.GetSection(RoutingOptions.SectionName))
            .Validate(o => o.IsValid(), "La configuración de 'Routing' no es válida.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IFallbackCategoryProvider, FallbackCategoryProvider>();
        services.AddScoped<IRoutingService, RoutingService>();

        return services;
    }
}
