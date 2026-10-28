using System.Text.Json;
using System.Text.Json.Nodes;
using NSubstitute;
using SM.ChatRouter.Application.Ai;
using SM.ChatRouter.Domain.Entities;
using SM.ChatRouter.Domain.Enums;
using SM.ChatRouter.Infrastructure.Ai;
using SM.ChatRouter.Infrastructure.Ai.OpenAI;
using SM.ChatRouter.Infrastructure.Ai.OpenAI.Functions;
using SM.ChatRouter.Infrastructure.Ai.OpenAI.Models;
using SM.ChatRouter.Tests.TestData;

namespace SM.ChatRouter.Tests.Infrastructure.Ai.OpenAI;

public sealed class OpenAIClientTests
{
    private readonly IOpenAIConversationRunner _runner = Substitute.For<IOpenAIConversationRunner>();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private OpenAIClient CreateSut() => new(_runner, Substitute.For<IAiPromptProvider>());

    private static AiRoutingRequest Request(params ChatMessage[] history) => new()
    {
        UserPrompt = "Me falla Outlook",
        History = history,
        Categories = [TestCategories.Incidencia, TestCategories.Vacaciones],
        CandidateCategoryId = "incidencia_tecnica",
        CollectedParameters = new Dictionary<string, string> { ["prioridad"] = "alta" }
    };

    private void RunnerReturns(params JsonObject[] output) =>
        _runner.RunAsync(Arg.Any<ResponseRequest>(), Arg.Any<CancellationToken>()).Returns(OpenAIFakes.Result(output));

    [Fact]
    public async Task MapsStructuredOutputToRoutingResponse()
    {
        RunnerReturns(OpenAIFakes.Message("""
            {
              "decision": "CategorySelected",
              "categoryId": "incidencia_tecnica",
              "confidence": 0.92,
              "extractedParameters": [ { "name": "producto", "value": "Outlook" }, { "name": "prioridad", "value": null }, { "name": " ", "value": "x" } ],
              "followUpQuestion": null
            }
            """));

        var result = await CreateSut().RouteAsync(Request(), Ct);

        Assert.Equal(AiDecision.CategorySelected, result.Decision);
        Assert.Equal("incidencia_tecnica", result.CategoryId);
        Assert.Equal(0.92, result.Confidence);
        Assert.Equal("Outlook", result.ExtractedParameters["PRODUCTO"]);
        Assert.Null(result.ExtractedParameters["prioridad"]);
        Assert.Equal(2, result.ExtractedParameters.Count);
        Assert.Null(result.FollowUpQuestion);
    }

    [Fact]
    public async Task Throws_WhenModelRefuses()
    {
        RunnerReturns(OpenAIFakes.Refusal("No puedo ayudar con eso"));

        var ex = await Assert.ThrowsAsync<OpenAIException>(() => CreateSut().RouteAsync(Request(), Ct));
        Assert.Contains("No puedo ayudar", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no es json")]
    [InlineData("""{ "categoryId": "x" }""")]
    [InlineData("""{ "decision": "Inventada" }""")]
    [InlineData("null")]
    public async Task Throws_WhenOutputIsNotValidRoutingJson(string text)
    {
        RunnerReturns(OpenAIFakes.Message(text));

        await Assert.ThrowsAnyAsync<JsonException>(() => CreateSut().RouteAsync(Request(), Ct));
    }

    [Fact]
    public async Task PropagatesRunnerErrors()
    {
        _runner.RunAsync(Arg.Any<ResponseRequest>(), Arg.Any<CancellationToken>())
            .Returns<ResponseResult>(_ => throw new OpenAIException("429"));

        await Assert.ThrowsAsync<OpenAIException>(() => CreateSut().RouteAsync(Request(), Ct));
    }

    [Fact]
    public void BuildRequest_MapsHistoryInstructionsAndStrictSchema()
    {
        var now = DateTimeOffset.UtcNow;

        var request = OpenAIClient.BuildRequest(Request(
            new ChatMessage(ChatRole.User, "Tengo un problema", now),
            new ChatMessage(ChatRole.Assistant, "¿Qué producto?", now),
            new ChatMessage(ChatRole.User, "Me falla Outlook", now)), "PROMPT DE SISTEMA");

        Assert.Equal(["user", "assistant", "user"], request.Input.Cast<InputMessage>().Select(m => m.Role));
        Assert.IsType<OutputTextPart>(((InputMessage)request.Input[1]).Content[0]);
        Assert.StartsWith("PROMPT DE SISTEMA\n\n", request.Instructions);
        Assert.Contains("incidencia_tecnica", request.Instructions);
        Assert.Contains("\"prioridad\":\"alta\"", request.Instructions);
        Assert.Contains("Categoría candidata de turnos anteriores: incidencia_tecnica", request.Instructions);
        Assert.Null(request.Model);

        var format = Assert.IsType<JsonSchemaFormat>(request.Text!.Format);
        Assert.True(format.Strict);
        Assert.Equal(RoutingResponseSchema.Name, format.Name);
        var ids = format.Schema["properties"]!["categoryId"]!["enum"]!.AsArray();
        Assert.Equal(["incidencia_tecnica", "solicitud_vacaciones", null], ids.Select(n => n?.GetValue<string>()));
    }

    [Fact]
    public void BuildRequest_UsesUserPrompt_WhenHistoryIsEmpty()
    {
        var request = OpenAIClient.BuildRequest(Request(), "prompt");

        var message = Assert.IsType<InputMessage>(Assert.Single(request.Input));
        Assert.Equal("Me falla Outlook", ((InputTextPart)message.Content[0]).Text);
    }

    [Fact]
    public async Task SendsSystemPromptFromProvider()
    {
        var prompts = Substitute.For<IAiPromptProvider>();
        prompts.SystemPrompt.Returns("Prompt desde fichero");
        RunnerReturns(OpenAIFakes.Message("""{ "decision": "NoMatch", "categoryId": null, "confidence": 0, "extractedParameters": [], "followUpQuestion": null }"""));

        await new OpenAIClient(_runner, prompts).RouteAsync(Request(), Ct);

        await _runner.Received(1).RunAsync(
            Arg.Is<ResponseRequest>(r => r.Instructions!.StartsWith("Prompt desde fichero\n\n")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Schema_IsStrictCompatible()
    {
        var schema = RoutingResponseSchema.Create(["a"]);

        AssertStrict(schema);
        Assert.Equal(
            Enum.GetNames<AiDecision>(),
            schema["properties"]!["decision"]!["enum"]!.AsArray().Select(n => n!.GetValue<string>()));
    }

    // Strict exige additionalProperties:false y todas las propiedades en required, en todos los niveles.
    private static void AssertStrict(JsonNode node)
    {
        if (node["type"]?.ToString() == "object")
        {
            Assert.False(node["additionalProperties"]!.GetValue<bool>());
            var properties = node["properties"]!.AsObject().Select(p => p.Key).Order();
            var required = node["required"]!.AsArray().Select(r => r!.GetValue<string>()).Order();
            Assert.Equal(properties, required);
            foreach (var (_, child) in node["properties"]!.AsObject())
            {
                AssertStrict(child!);
            }
        }
        else if (node["items"] is { } items)
        {
            AssertStrict(items);
        }
    }
}
