using SM.ChatRouter.Api.Contracts;
using SM.ChatRouter.Application.Abstractions;
using SM.ChatRouter.Application.Services;

namespace SM.ChatRouter.Api.Endpoints;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Chat");

        group.MapPost("/chat", async (ChatRequest request, IRoutingService routing, CancellationToken ct) =>
            {
                var result = await routing.ProcessMessageAsync(request.ConversationId, request.Message, ct);
                return TypedResults.Ok(ChatResponse.From(result));
            })
            .WithName("SendChatMessage")
            .ProducesValidationProblem();

        group.MapDelete("/chat/{conversationId}", async (string conversationId, IRoutingService routing, CancellationToken ct) =>
                await routing.ResetConversationAsync(conversationId, ct) ? Results.NoContent() : Results.NotFound())
            .WithName("ResetConversation");

        group.MapGet("/categories", async (ICategoryRepository categories, CancellationToken ct) =>
                TypedResults.Ok(await categories.GetAllAsync(ct)))
            .WithName("GetCategories");

        return app;
    }
}
