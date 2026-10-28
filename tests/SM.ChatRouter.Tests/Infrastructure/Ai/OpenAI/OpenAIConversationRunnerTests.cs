using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SM.ChatRouter.Infrastructure.Ai.OpenAI;
using SM.ChatRouter.Infrastructure.Ai.OpenAI.Functions;
using SM.ChatRouter.Infrastructure.Ai.OpenAI.Models;
using SM.ChatRouter.Tests.TestData;

namespace SM.ChatRouter.Tests.Infrastructure.Ai.OpenAI;

public sealed class OpenAIConversationRunnerTests
{
    private readonly IOpenAIResponsesClient _client = Substitute.For<IOpenAIResponsesClient>();
    private readonly List<ResponseRequest> _sent = [];
    private readonly OpenAIOptions _options = new() { MaxFunctionCallIterations = 5 };

    public OpenAIConversationRunnerTests()
    {
        _client.CreateAsync(Arg.Do<ResponseRequest>(r => _sent.Add(r)), Arg.Any<CancellationToken>());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private OpenAIConversationRunner CreateSut(params IOpenAIFunctionHandler[] handlers) => new(
        _client, handlers, Microsoft.Extensions.Options.Options.Create(_options), NullLogger<OpenAIConversationRunner>.Instance);

    private void ClientReturns(params ResponseResult[] results) =>
        _client.CreateAsync(Arg.Any<ResponseRequest>(), Arg.Any<CancellationToken>()).Returns(results[0], results[1..]);

    [Fact]
    public async Task ReturnsDirectly_WhenNoFunctionCalls()
    {
        ClientReturns(OpenAIFakes.Result(OpenAIFakes.Message("hola")));

        var result = await CreateSut().RunAsync(new ResponseRequest { Input = [InputMessage.User("x")] }, Ct);

        Assert.Equal("hola", result.OutputText);
        Assert.Single(_sent);
        Assert.Null(_sent[0].Tools);
    }

    [Fact]
    public async Task AddsRegisteredFunctionsAsTools_WithoutDuplicatingDeclaredOnes()
    {
        ClientReturns(OpenAIFakes.Result(OpenAIFakes.Message("ok")));
        var declared = new FunctionTool { Name = "sumar", Description = "propia", Parameters = new JsonObject() };

        await CreateSut(new SumFunction(), new FakeFunction("otra"))
            .RunAsync(new ResponseRequest { Input = [InputMessage.User("x")], Tools = [new WebSearchTool(), declared] }, Ct);

        var tools = _sent[0].Tools!;
        Assert.Equal(3, tools.Count);
        Assert.Same(declared, tools.OfType<FunctionTool>().Single(t => t.Name == "sumar"));
        Assert.Contains(tools.OfType<FunctionTool>(), t => t.Name == "otra" && t.Strict == true);
    }

    [Fact]
    public async Task ExecutesFunctionAndSendsOutput_UntilFinalAnswer()
    {
        ClientReturns(
            OpenAIFakes.Result(OpenAIFakes.Reasoning("enc"), OpenAIFakes.FunctionCall("call_1", "sumar", """{"a":2,"b":3}""")),
            OpenAIFakes.Result(OpenAIFakes.Message("El resultado es 5")));

        var result = await CreateSut(new SumFunction()).RunAsync(new ResponseRequest { Input = [InputMessage.User("2+3")] }, Ct);

        Assert.Equal("El resultado es 5", result.OutputText);
        Assert.Equal(2, _sent.Count);
        var input = _sent[1].Input;
        Assert.Equal(["message", "reasoning", "function_call", "function_call_output"], input.Select(i => i.Type));
        var output = Assert.IsType<InputFunctionCallOutput>(input[3]);
        Assert.Equal(("call_1", "5"), (output.CallId, output.Output));
        Assert.Single(_sent[0].Input);
    }

    [Fact]
    public async Task DropsReasoningWithoutEncryptedContent_WhenNotStored()
    {
        ClientReturns(
            OpenAIFakes.Result(OpenAIFakes.Reasoning(), OpenAIFakes.FunctionCall("c", "sumar", """{"a":1,"b":1}""")),
            OpenAIFakes.Result(OpenAIFakes.Message("2")));

        await CreateSut(new SumFunction()).RunAsync(new ResponseRequest { Input = [InputMessage.User("x")], Store = false }, Ct);

        Assert.DoesNotContain(_sent[1].Input, i => i.Type == "reasoning");
    }

    [Fact]
    public async Task KeepsReasoningWithoutEncryptedContent_WhenStored()
    {
        ClientReturns(
            OpenAIFakes.Result(OpenAIFakes.Reasoning(), OpenAIFakes.FunctionCall("c", "sumar", """{"a":1,"b":1}""")),
            OpenAIFakes.Result(OpenAIFakes.Message("2")));

        await CreateSut(new SumFunction()).RunAsync(new ResponseRequest { Input = [InputMessage.User("x")], Store = true }, Ct);

        Assert.Contains(_sent[1].Input, i => i.Type == "reasoning");
    }

    [Fact]
    public async Task ExecutesSeveralCallsOfSameTurn()
    {
        ClientReturns(
            OpenAIFakes.Result(
                OpenAIFakes.FunctionCall("c1", "sumar", """{"a":1,"b":1}"""),
                OpenAIFakes.FunctionCall("c2", "sumar", """{"a":2,"b":2}""")),
            OpenAIFakes.Result(OpenAIFakes.Message("ok")));

        await CreateSut(new SumFunction()).RunAsync(new ResponseRequest { Input = [InputMessage.User("x")] }, Ct);

        var outputs = _sent[1].Input.OfType<InputFunctionCallOutput>().ToList();
        Assert.Equal([("c1", "2"), ("c2", "4")], outputs.Select(o => (o.CallId, o.Output)));
    }

    [Fact]
    public async Task ReturnsErrorToModel_WhenFunctionIsUnknownOrThrows()
    {
        ClientReturns(
            OpenAIFakes.Result(
                OpenAIFakes.FunctionCall("c1", "inventada", "{}"),
                OpenAIFakes.FunctionCall("c2", "fallar", "{}"),
                OpenAIFakes.FunctionCall("c3", "sumar", "esto no es json")),
            OpenAIFakes.Result(OpenAIFakes.Message("lo siento")));

        var result = await CreateSut(new SumFunction(), new FakeFunction("fallar", throws: true))
            .RunAsync(new ResponseRequest { Input = [InputMessage.User("x")] }, Ct);

        Assert.Equal("lo siento", result.OutputText);
        var outputs = _sent[1].Input.OfType<InputFunctionCallOutput>().ToList();
        Assert.All(outputs, o => Assert.NotNull(JsonNode.Parse(o.Output)!["error"]));
        Assert.Contains("inventada", outputs[0].Output);
        Assert.Contains("boom", outputs[1].Output);
    }

    [Fact]
    public async Task Throws_WhenMaxIterationsExceeded()
    {
        _options.MaxFunctionCallIterations = 2;
        _client.CreateAsync(Arg.Any<ResponseRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => OpenAIFakes.Result(OpenAIFakes.FunctionCall("c", "sumar", """{"a":1,"b":1}""")));

        var ex = await Assert.ThrowsAsync<OpenAIException>(() =>
            CreateSut(new SumFunction()).RunAsync(new ResponseRequest { Input = [InputMessage.User("x")] }, Ct));

        Assert.Contains("2", ex.Message);
        Assert.Equal(2, _sent.Count);
    }

    [Fact]
    public async Task DoesNotMutateOriginalRequest()
    {
        ClientReturns(
            OpenAIFakes.Result(OpenAIFakes.FunctionCall("c", "sumar", """{"a":1,"b":1}""")),
            OpenAIFakes.Result(OpenAIFakes.Message("2")));
        var request = new ResponseRequest { Input = [InputMessage.User("x")] };

        await CreateSut(new SumFunction()).RunAsync(request, Ct);

        Assert.Single(request.Input);
        Assert.Null(request.Tools);
    }

    [Fact]
    public async Task RunStreamingAsync_ExecutesFunctionsBetweenStreams()
    {
        _client.StreamAsync(Arg.Any<ResponseRequest>(), Arg.Any<CancellationToken>()).Returns(
            Events(Completed(OpenAIFakes.Result(OpenAIFakes.FunctionCall("c", "sumar", """{"a":1,"b":2}""")))),
            Events(
                new ResponseStreamEvent { Type = "response.output_text.delta", Delta = "3" },
                Completed(OpenAIFakes.Result(OpenAIFakes.Message("3")))));

        var events = new List<ResponseStreamEvent>();
        await foreach (var e in CreateSut(new SumFunction()).RunStreamingAsync(new ResponseRequest { Input = [InputMessage.User("x")] }, Ct))
        {
            events.Add(e);
        }

        Assert.Equal(["response.completed", "response.output_text.delta", "response.completed"], events.Select(e => e.Type));
        _client.Received(2).StreamAsync(Arg.Any<ResponseRequest>(), Arg.Any<CancellationToken>());
        _client.Received(1).StreamAsync(
            Arg.Is<ResponseRequest>(r => r.Input.OfType<InputFunctionCallOutput>().Any(o => o.Output == "3")),
            Arg.Any<CancellationToken>());
    }

    private static ResponseStreamEvent Completed(ResponseResult result) => new() { Type = "response.completed", Response = result };

    private static async IAsyncEnumerable<ResponseStreamEvent> Events(params ResponseStreamEvent[] events)
    {
        foreach (var e in events)
        {
            await Task.Yield();
            yield return e;
        }
    }

    private sealed class SumFunction : IOpenAIFunctionHandler
    {
        public string Name => "sumar";

        public string? Description => "Suma dos números";

        public JsonNode ParametersSchema => new JsonObject { ["type"] = "object" };

        public bool Strict => true;

        public Task<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken) =>
            Task.FromResult((arguments.GetProperty("a").GetInt32() + arguments.GetProperty("b").GetInt32()).ToString());
    }

    private sealed class FakeFunction(string name, bool throws = false) : IOpenAIFunctionHandler
    {
        public string Name => name;

        public string? Description => null;

        public JsonNode ParametersSchema => new JsonObject { ["type"] = "object" };

        public bool Strict => true;

        public Task<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken) =>
            throws ? throw new InvalidOperationException("boom") : Task.FromResult("{}");
    }
}
