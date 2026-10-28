using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using SM.ChatRouter.Api.Contracts;
using SM.ChatRouter.Application.Abstractions;
using SM.ChatRouter.Application.Ai;
using SM.ChatRouter.Domain.Enums;
using SM.ChatRouter.Tests.TestData;

namespace SM.ChatRouter.Tests.Api;

public sealed class ChatEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly WebApplicationFactory<Program> _factory;

    public ChatEndpointsTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private HttpClient CreateClient(IAiRoutingClient? ai = null) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            if (ai is not null)
            {
                services.RemoveAll<IAiRoutingClient>();
                services.AddSingleton(ai);
            }
        })).CreateClient();

    private static IAiRoutingClient AiReturning(params AiRoutingResponse[] responses)
    {
        var ai = Substitute.For<IAiRoutingClient>();
        ai.RouteAsync(Arg.Any<AiRoutingRequest>(), Arg.Any<CancellationToken>()).Returns(responses[0], responses[1..]);
        return ai;
    }

    private static async Task<ChatResponse> PostAsync(HttpClient client, string? conversationId, string? message)
    {
        var response = await client.PostAsJsonAsync("/api/chat", new ChatRequest(conversationId, message), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChatResponse>(JsonOptions, Ct))!;
    }

    [Fact]
    public async Task Post_ReturnsQuestionThenRoutedCategory()
    {
        // Categoría N3 real del fichero categories_tree.json que se publica con la API.
        const string id = "librero-copia/comercial/solicitar-la-visitar-de-un-asesor-comercial";
        var client = CreateClient(AiReturning(
            Ai.NeedsInfo(id, "¿Cuál es tu email?",
                ("nombre-comercial-o-razon-social", "Librería Pérez"), ("nombre", "Ana"), ("apellidos", "Pérez"),
                ("descripcion-de-la-solicitud", "Quiero que me visite un comercial")),
            Ai.Selected(id, 0.9, ("email", "ana@example.com"))));

        var first = await PostAsync(client, null, "Soy Ana Pérez de Librería Pérez y quiero que me visite un comercial");
        var second = await PostAsync(client, first.ConversationId, "ana@example.com");

        Assert.Equal(RoutingOutcome.NeedsMoreInformation, first.Outcome);
        Assert.Equal("¿Cuál es tu email?", first.Question);
        Assert.Equal(["email"], first.MissingParameters);

        Assert.Equal(RoutingOutcome.Routed, second.Outcome);
        Assert.Equal(id, second.Category!.Id);
        Assert.Equal("Soporte para librerías o centros escolares > Atención Comercial > Solicitar la visitar de un asesor comercial", second.Category.Path);
        Assert.Equal("ana@example.com", second.Parameters["email"]);
        Assert.Equal("Ana", second.Parameters["nombre"]);
    }

    [Fact]
    public async Task Post_FallsBack_WithRealOpenAIClientWithoutApiKey()
    {
        // Sin sustituir el cliente y sin ApiKey (se anula cualquier user-secret): OpenAI no se llama y salta el fallback.
        var client = _factory.WithWebHostBuilder(builder => builder.UseSetting("OpenAI:ApiKey", "")).CreateClient();

        var result = await PostAsync(client, null, "Hola");

        Assert.Equal(RoutingOutcome.Fallback, result.Outcome);
        Assert.Equal(FallbackReason.AiError, result.FallbackReason);
        Assert.Equal("general", result.Category!.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Post_Returns400_WhenMessageIsEmpty(string? message)
    {
        var client = CreateClient(AiReturning(Ai.NoMatch()));

        var response = await client.PostAsJsonAsync("/api/chat", new ChatRequest(null, message), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Delete_ResetsConversation()
    {
        var client = CreateClient(AiReturning(Ai.NeedsInfo(null, "¿Eres profesor, familia o librería?")));
        var chat = await PostAsync(client, null, "Ayuda");

        var deleted = await client.DeleteAsync($"/api/chat/{chat.ConversationId}", Ct);
        var again = await client.DeleteAsync($"/api/chat/{chat.ConversationId}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task GetCategories_ReturnsLevel3CategoriesFromTree()
    {
        var response = await CreateClient().GetAsync("/api/categories", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.Contains("docentes/mis-pedidos/incidencia-en-la-entrega-de-un-pedido", body);
    }
}
