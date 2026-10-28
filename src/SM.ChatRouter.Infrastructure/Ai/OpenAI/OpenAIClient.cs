using SM.ChatRouter.Application.Abstractions;
using SM.ChatRouter.Application.Ai;
using SM.ChatRouter.Domain.Enums;
using SM.ChatRouter.Infrastructure.Ai.OpenAI.Functions;
using SM.ChatRouter.Infrastructure.Ai.OpenAI.Models;

namespace SM.ChatRouter.Infrastructure.Ai.OpenAI;

/// <summary>
/// Implementación de <see cref="IAiRoutingClient"/> con la OpenAI Responses API y structured outputs.
/// Cualquier error (HTTP, rechazo, JSON inválido) se propaga y el <c>RoutingService</c> aplica el fallback.
/// </summary>
public sealed class OpenAIClient(IOpenAIConversationRunner runner, IAiPromptProvider prompts) : IAiRoutingClient
{
    public async Task<AiRoutingResponse> RouteAsync(AiRoutingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await runner.RunAsync(BuildRequest(request, prompts.SystemPrompt), cancellationToken);

        if (response.Refusal is { } refusal)
        {
            throw new OpenAIException($"El modelo rechazó la petición: {refusal}") { ResponseId = response.Id };
        }

        return RoutingResponseSchema.Parse(response.OutputText);
    }

    internal static ResponseRequest BuildRequest(AiRoutingRequest request, string systemPrompt)
    {
        var input = request.History
            .Select(m => m.Role == ChatRole.User ? InputMessage.User(m.Content) : (ResponseInputItem)InputMessage.Assistant(m.Content))
            .ToList();

        if (input.Count == 0)
        {
            input.Add(InputMessage.User(request.UserPrompt));
        }

        return new ResponseRequest
        {
            Instructions = $"{systemPrompt}\n\n{AiPromptTemplates.BuildContext(request)}",
            Input = input,
            Text = new ResponseTextOptions
            {
                Format = new JsonSchemaFormat
                {
                    Name = RoutingResponseSchema.Name,
                    Schema = RoutingResponseSchema.Create(request.Categories.Select(c => c.Id)),
                    Strict = true
                }
            }
        };
    }
}
