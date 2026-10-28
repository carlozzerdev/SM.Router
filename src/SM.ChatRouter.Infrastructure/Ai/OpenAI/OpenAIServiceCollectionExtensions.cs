using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SM.ChatRouter.Application.Abstractions;
using SM.ChatRouter.Infrastructure.Ai.OpenAI.Functions;

namespace SM.ChatRouter.Infrastructure.Ai.OpenAI;

public static class OpenAIServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAI(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpenAIOptions>()
            .Bind(configuration.GetSection(OpenAIOptions.SectionName))
            .Validate(o => o.IsValid(), "La configuración de 'OpenAI' no es válida (Model, BaseUrl, Timeout, MaxFunctionCallIterations).")
            .ValidateOnStart();

        services.AddHttpClient<IOpenAIResponsesClient, OpenAIResponsesClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;
            http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            http.Timeout = options.Timeout;
        });

        services.AddScoped<IOpenAIConversationRunner, OpenAIConversationRunner>();
        services.AddScoped<IAiRoutingClient, OpenAIClient>();

        return services;
    }

    /// <summary>Registra una función que el modelo podrá invocar en todas las peticiones del runner.</summary>
    public static IServiceCollection AddOpenAIFunction<THandler>(this IServiceCollection services)
        where THandler : class, IOpenAIFunctionHandler
    {
        services.AddScoped<IOpenAIFunctionHandler, THandler>();
        return services;
    }
}
