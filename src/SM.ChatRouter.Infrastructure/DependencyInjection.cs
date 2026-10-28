using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SM.ChatRouter.Application.Abstractions;
using SM.ChatRouter.Infrastructure.Ai;
using SM.ChatRouter.Infrastructure.Ai.OpenAI;
using SM.ChatRouter.Infrastructure.Repositories;

namespace SM.ChatRouter.Infrastructure;

public static class DependencyInjection
{
    public const string AiProviderKey = "Ai:Provider";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CategoryRepositoryOptions>(configuration.GetSection(CategoryRepositoryOptions.SectionName));

        services.AddSingleton<ICategoryRepository, JsonCategoryRepository>();
        services.AddSingleton<IConversationRepository, InMemoryConversationRepository>();

        services.AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.SectionName))
            .Validate(o => o.IsValid(), "La configuración de 'Ai' no es válida (Provider vacío o no existe el fichero de 'SystemPromptPath').")
            .ValidateOnStart();
        services.AddSingleton<IAiPromptProvider, FileAiPromptProvider>();

        var provider = configuration[AiProviderKey] ?? "OpenAI";
        switch (provider.Trim().ToUpperInvariant())
        {
            case "OPENAI":
                services.AddOpenAI(configuration);
                break;
            // Añadir aquí otros proveedores: case "CLAUDE": services.AddClaude(configuration); break;
            default:
                throw new InvalidOperationException($"Proveedor de IA no soportado en '{AiProviderKey}': '{provider}'.");
        }

        return services;
    }
}
