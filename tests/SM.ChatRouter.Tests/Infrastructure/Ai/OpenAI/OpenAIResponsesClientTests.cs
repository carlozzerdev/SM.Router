using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using SM.ChatRouter.Infrastructure.Ai.OpenAI;
using SM.ChatRouter.Infrastructure.Ai.OpenAI.Models;
using SM.ChatRouter.Tests.TestData;

namespace SM.ChatRouter.Tests.Infrastructure.Ai.OpenAI;

public sealed class OpenAIResponsesClientTests
{
    private readonly StubHttpHandler _http = new();
    private readonly OpenAIOptions _options = new() { ApiKey = "sk-test", Model = "gpt-default", Project = "proj_1" };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private OpenAIResponsesClient CreateSut() => new(
        new HttpClient(_http) { BaseAddress = new Uri("https://api.test/v1/") },
        Microsoft.Extensions.Options.Options.Create(_options));

    private JsonNode SentBody(int index = 0) => JsonNode.Parse(_http.Requests[index].Body!)!;

    // ---------- Serialización del payload ----------

    [Fact]
    public async Task SerializesFullPayloadInSnakeCase_AndOmitsNulls()
    {
        _http.RespondJson(OpenAIFakes.ResponseJson(output: OpenAIFakes.Message("ok")));

        await CreateSut().CreateAsync(new ResponseRequest
        {
            Model = "gpt-5",
            Instructions = "Sé breve",
            Input = [InputMessage.Developer("dev"), InputMessage.User("hola"), InputMessage.Assistant("¿qué tal?")],
            Reasoning = new ReasoningOptions { Effort = ReasoningEffort.High, Summary = "auto" },
            Text = new ResponseTextOptions { Verbosity = "low" },
            Tools =
            [
                new FunctionTool { Name = "f", Description = "d", Parameters = new JsonObject { ["type"] = "object" }, Strict = true },
                new WebSearchTool { SearchContextSize = "low" },
                new FileSearchTool { VectorStoreIds = ["vs_1"] },
                new RawTool(new JsonObject { ["type"] = "image_generation", ["quality"] = "low" })
            ],
            ToolChoice = ToolChoice.Function("f"),
            ParallelToolCalls = false,
            MaxOutputTokens = 500,
            Include = ["reasoning.encrypted_content"],
            PreviousResponseId = "resp_0",
            Metadata = new Dictionary<string, string> { ["k"] = "v" }
        }, Ct);

        var body = SentBody();
        Assert.Equal("gpt-5", body["model"]!.GetValue<string>());
        Assert.False(body["stream"]!.GetValue<bool>());
        Assert.Equal("high", body["reasoning"]!["effort"]!.GetValue<string>());
        Assert.Equal("auto", body["reasoning"]!["summary"]!.GetValue<string>());
        Assert.Equal("low", body["text"]!["verbosity"]!.GetValue<string>());
        Assert.Equal(500, body["max_output_tokens"]!.GetValue<int>());
        Assert.False(body["parallel_tool_calls"]!.GetValue<bool>());
        Assert.Equal("resp_0", body["previous_response_id"]!.GetValue<string>());
        Assert.Equal("reasoning.encrypted_content", body["include"]![0]!.GetValue<string>());
        Assert.Equal("v", body["metadata"]!["k"]!.GetValue<string>());
        Assert.Equal("function", body["tool_choice"]!["type"]!.GetValue<string>());

        var input = body["input"]!.AsArray();
        Assert.Equal("developer", input[0]!["role"]!.GetValue<string>());
        Assert.Equal("input_text", input[1]!["content"]![0]!["type"]!.GetValue<string>());
        Assert.Equal("output_text", input[2]!["content"]![0]!["type"]!.GetValue<string>());

        var tools = body["tools"]!.AsArray();
        Assert.Equal(["function", "web_search", "file_search", "image_generation"], tools.Select(t => t!["type"]!.GetValue<string>()));
        Assert.True(tools[0]!["strict"]!.GetValue<bool>());
        Assert.Equal("low", tools[1]!["search_context_size"]!.GetValue<string>());
        Assert.Equal("vs_1", tools[2]!["vector_store_ids"]![0]!.GetValue<string>());
        Assert.Equal("low", tools[3]!["quality"]!.GetValue<string>());

        Assert.Null(body["temperature"]);
        Assert.Null(body["background"]);
        Assert.Null(body["truncation"]);
    }

    [Fact]
    public async Task SerializesJsonSchemaFormat()
    {
        _http.RespondJson(OpenAIFakes.ResponseJson(output: OpenAIFakes.Message("{}")));

        await CreateSut().CreateAsync(new ResponseRequest
        {
            Input = [InputMessage.User("x")],
            Text = new ResponseTextOptions { Format = new JsonSchemaFormat { Name = "s", Schema = new JsonObject { ["type"] = "object" } } }
        }, Ct);

        var format = SentBody()["text"]!["format"]!;
        Assert.Equal("json_schema", format["type"]!.GetValue<string>());
        Assert.Equal("s", format["name"]!.GetValue<string>());
        Assert.True(format["strict"]!.GetValue<bool>());
        Assert.Equal("object", format["schema"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public async Task SendsAdditionalPropertiesAtEveryLevel()
    {
        _http.RespondJson(OpenAIFakes.ResponseJson(output: OpenAIFakes.Message("ok")));

        await CreateSut().CreateAsync(new ResponseRequest
        {
            Input = [InputMessage.User("x") with { AdditionalProperties = Extra("item_extra", 1) }],
            Reasoning = new ReasoningOptions { Effort = "low", AdditionalProperties = Extra("reasoning_extra", 2) },
            AdditionalProperties = Extra("root_extra", 3)
        }, Ct);

        var body = SentBody();
        Assert.Equal(3, body["root_extra"]!.GetValue<int>());
        Assert.Equal(2, body["reasoning"]!["reasoning_extra"]!.GetValue<int>());
        Assert.Equal(1, body["input"]![0]!["item_extra"]!.GetValue<int>());
    }

    [Fact]
    public async Task AppliesDefaultsOnlyToNullFields()
    {
        _options.Defaults = new OpenAIRequestDefaults
        {
            ReasoningEffort = "low",
            ReasoningSummary = "concise",
            MaxOutputTokens = 100,
            Store = false,
            Temperature = 0.2
        };
        _http.RespondJson(OpenAIFakes.ResponseJson(output: OpenAIFakes.Message("ok")));

        await CreateSut().CreateAsync(new ResponseRequest
        {
            Input = [InputMessage.User("x")],
            Reasoning = new ReasoningOptions { Effort = "high" },
            MaxOutputTokens = 999
        }, Ct);

        var body = SentBody();
        Assert.Equal("gpt-default", body["model"]!.GetValue<string>());
        Assert.Equal("high", body["reasoning"]!["effort"]!.GetValue<string>());
        Assert.Equal("concise", body["reasoning"]!["summary"]!.GetValue<string>());
        Assert.Equal(999, body["max_output_tokens"]!.GetValue<int>());
        Assert.False(body["store"]!.GetValue<bool>());
        Assert.Equal(0.2, body["temperature"]!.GetValue<double>());
    }

    [Fact]
    public async Task SendsAuthAndProjectHeaders_ToResponsesEndpoint()
    {
        _http.RespondJson(OpenAIFakes.ResponseJson(output: OpenAIFakes.Message("ok")));

        await CreateSut().CreateAsync(new ResponseRequest { Input = [InputMessage.User("x")] }, Ct);

        var request = _http.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.test/v1/responses", request.Uri.ToString());
        Assert.Equal("Bearer sk-test", request.Headers["Authorization"]);
        Assert.Equal("proj_1", request.Headers["OpenAI-Project"]);
        Assert.False(request.Headers.ContainsKey("OpenAI-Organization"));
    }

    [Fact]
    public async Task Throws_WhenApiKeyIsMissing_WithoutCallingApi()
    {
        _options.ApiKey = "";

        var ex = await Assert.ThrowsAsync<OpenAIException>(() =>
            CreateSut().CreateAsync(new ResponseRequest { Input = [InputMessage.User("x")] }, Ct));

        Assert.Contains("OpenAI:ApiKey", ex.Message);
        Assert.Empty(_http.Requests);
    }

    // ---------- Lectura de la respuesta ----------

    [Fact]
    public async Task ParsesOutputTextUsageAndFunctionCalls()
    {
        _http.RespondJson(OpenAIFakes.ResponseJson("completed",
            OpenAIFakes.Reasoning("enc"),
            OpenAIFakes.Message("Hola "),
            OpenAIFakes.Message("mundo"),
            OpenAIFakes.FunctionCall("call_1", "f", """{"a":1}"""),
            new JsonObject { ["type"] = "web_search_call", ["id"] = "ws_1", ["status"] = "completed" }));

        var result = await CreateSut().CreateAsync(new ResponseRequest { Input = [InputMessage.User("x")] }, Ct);

        Assert.Equal("resp_1", result.Id);
        Assert.Equal("Hola mundo", result.OutputText);
        Assert.Equal(15, result.Usage!.TotalTokens);
        Assert.Equal(2, result.Usage.OutputTokensDetails!.ReasoningTokens);
        var call = Assert.Single(result.FunctionCalls);
        Assert.Equal(("call_1", "f", """{"a":1}"""), (call.CallId, call.Name, call.Arguments));
        Assert.Equal("enc", Assert.IsType<OutputReasoning>(result.Output[0]).EncryptedContent);
        var unknown = Assert.IsType<OutputUnknownItem>(result.Output[4]);
        Assert.Equal("web_search_call", unknown.Type);
        Assert.Equal("ws_1", unknown.Raw.GetProperty("id").GetString());
        Assert.Null(result.Refusal);
    }

    [Fact]
    public async Task ExposesRefusal()
    {
        _http.RespondJson(OpenAIFakes.ResponseJson(output: OpenAIFakes.Refusal("No puedo")));

        var result = await CreateSut().CreateAsync(new ResponseRequest { Input = [InputMessage.User("x")] }, Ct);

        Assert.Equal("No puedo", result.Refusal);
        Assert.Equal(string.Empty, result.OutputText);
    }

    [Fact]
    public async Task ReadsUnknownRootFieldsIntoAdditionalProperties()
    {
        var json = JsonNode.Parse(OpenAIFakes.ResponseJson(output: OpenAIFakes.Message("ok")))!;
        json["nuevo_campo"] = "valor";
        _http.RespondJson(json.ToJsonString());

        var result = await CreateSut().CreateAsync(new ResponseRequest { Input = [InputMessage.User("x")] }, Ct);

        Assert.Equal("valor", result.AdditionalProperties!["nuevo_campo"].GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "invalid_api_key")]
    [InlineData(HttpStatusCode.TooManyRequests, "rate_limit_exceeded")]
    [InlineData(HttpStatusCode.InternalServerError, "server_error")]
    public async Task ThrowsOpenAIException_OnHttpError(HttpStatusCode status, string code)
    {
        _http.Respond(status, $$"""{ "error": { "message": "mensaje", "type": "tipo", "code": "{{code}}" } }""", requestId: "req_123");

        var ex = await Assert.ThrowsAsync<OpenAIException>(() =>
            CreateSut().CreateAsync(new ResponseRequest { Input = [InputMessage.User("x")] }, Ct));

        Assert.Equal(status, ex.StatusCode);
        Assert.Equal(code, ex.ErrorCode);
        Assert.Equal("tipo", ex.ErrorType);
        Assert.Equal("req_123", ex.RequestId);
        Assert.Contains("mensaje", ex.Message);
    }

    [Fact]
    public async Task ThrowsOpenAIException_OnNonJsonErrorBody()
    {
        _http.Respond(HttpStatusCode.BadGateway, "<html>bad gateway</html>", "text/html");

        var ex = await Assert.ThrowsAsync<OpenAIException>(() =>
            CreateSut().CreateAsync(new ResponseRequest { Input = [InputMessage.User("x")] }, Ct));

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.Contains("bad gateway", ex.Message);
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("incomplete")]
    public async Task ThrowsOpenAIException_WhenResponseStatusIsNotCompleted(string status)
    {
        var json = JsonNode.Parse(OpenAIFakes.ResponseJson(status))!;
        json["incomplete_details"] = new JsonObject { ["reason"] = "max_output_tokens" };
        _http.RespondJson(json.ToJsonString());

        var ex = await Assert.ThrowsAsync<OpenAIException>(() =>
            CreateSut().CreateAsync(new ResponseRequest { Input = [InputMessage.User("x")] }, Ct));

        Assert.Equal("resp_1", ex.ResponseId);
        Assert.Contains(status, ex.Message);
    }

    [Fact]
    public async Task ThrowsOpenAIException_OnInvalidJsonBody()
    {
        _http.RespondJson("no es json");

        await Assert.ThrowsAsync<OpenAIException>(() =>
            CreateSut().CreateAsync(new ResponseRequest { Input = [InputMessage.User("x")] }, Ct));
    }

    // ---------- Raw, get, cancel, delete ----------

    [Fact]
    public async Task CreateRawAsync_SendsPayloadAsIs()
    {
        _http.RespondJson(OpenAIFakes.ResponseJson(output: OpenAIFakes.Message("ok")));
        var payload = new JsonObject { ["model"] = "o-raw", ["input"] = "hola", ["campo_libre"] = new JsonObject { ["x"] = 1 } };

        var result = await CreateSut().CreateRawAsync(payload, Ct);

        Assert.Equal("ok", result.OutputText);
        Assert.True(JsonNode.DeepEquals(payload, SentBody()));
    }

    [Fact]
    public async Task GetCancelAndDelete_UseResponseIdRoutes()
    {
        _http.RespondJson(OpenAIFakes.ResponseJson())
            .RespondJson(OpenAIFakes.ResponseJson("cancelled"))
            .Respond(HttpStatusCode.OK, """{ "id": "resp_1", "deleted": true }""");
        var sut = CreateSut();

        await sut.GetAsync("resp_1", Ct);
        var cancelled = await sut.CancelAsync("resp_1", Ct);
        await sut.DeleteAsync("resp_1", Ct);

        Assert.Equal("cancelled", cancelled.Status);
        Assert.Equal(
            [(HttpMethod.Get, "/v1/responses/resp_1"), (HttpMethod.Post, "/v1/responses/resp_1/cancel"), (HttpMethod.Delete, "/v1/responses/resp_1")],
            _http.Requests.Select(r => (r.Method, r.Uri.AbsolutePath)));
    }

    // ---------- Streaming ----------

    [Fact]
    public async Task StreamAsync_YieldsEventsUntilCompleted()
    {
        var completed = new JsonObject
        {
            ["type"] = "response.completed",
            ["sequence_number"] = 3,
            ["response"] = JsonNode.Parse(OpenAIFakes.ResponseJson(output: OpenAIFakes.Message("Hola mundo")))
        };
        _http.Respond(HttpStatusCode.OK, Sse(
            """{"type":"response.created","sequence_number":0,"response":{"id":"resp_1","status":"in_progress","output":[]}}""",
            """{"type":"response.output_text.delta","sequence_number":1,"item_id":"msg_1","output_index":0,"delta":"Hola"}""",
            """{"type":"response.output_text.delta","sequence_number":2,"item_id":"msg_1","output_index":0,"delta":" mundo"}""",
            completed.ToJsonString(),
            """{"type":"evento.posterior","sequence_number":4}"""), "text/event-stream");

        var events = new List<ResponseStreamEvent>();
        await foreach (var e in CreateSut().StreamAsync(new ResponseRequest { Input = [InputMessage.User("x")] }, Ct))
        {
            events.Add(e);
        }

        Assert.True(SentBody()["stream"]!.GetValue<bool>());
        Assert.Equal(["response.created", "response.output_text.delta", "response.output_text.delta", "response.completed"], events.Select(e => e.Type));
        Assert.Equal("Hola mundo", string.Concat(events.Select(e => e.Delta)));
        Assert.Equal("in_progress", events[0].Response!.Status);
        Assert.Equal("Hola mundo", events[^1].Response!.OutputText);
        Assert.Equal(0, events[1].OutputIndex);
    }

    [Fact]
    public async Task StreamAsync_StopsOnErrorEvent()
    {
        _http.Respond(HttpStatusCode.OK, Sse(
            """{"type":"error","code":"server_error","message":"boom","sequence_number":0}""",
            """{"type":"response.output_text.delta","delta":"no debería llegar"}"""), "text/event-stream");

        var events = new List<ResponseStreamEvent>();
        await foreach (var e in CreateSut().StreamAsync(new ResponseRequest { Input = [InputMessage.User("x")] }, Ct))
        {
            events.Add(e);
        }

        var error = Assert.Single(events);
        Assert.True(error.IsTerminal);
        Assert.Equal("boom", error.Raw.GetProperty("message").GetString());
    }

    [Fact]
    public async Task StreamAsync_ThrowsOnHttpError()
    {
        _http.Respond(HttpStatusCode.BadRequest, """{ "error": { "message": "malo", "code": "bad" } }""");

        await Assert.ThrowsAsync<OpenAIException>(async () =>
        {
            await foreach (var _ in CreateSut().StreamAsync(new ResponseRequest { Input = [InputMessage.User("x")] }, Ct))
            {
            }
        });
    }

    [Fact]
    public async Task StreamAsync_HonoursCancellation()
    {
        _http.Respond(HttpStatusCode.OK, Sse("""{"type":"response.created","response":{"id":"r","output":[]}}"""), "text/event-stream");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in CreateSut().StreamAsync(new ResponseRequest { Input = [InputMessage.User("x")] }, cts.Token))
            {
            }
        });
    }

    private static string Sse(params string[] events) =>
        string.Concat(events.Select(e => $"event: {JsonNode.Parse(e)!["type"]}\ndata: {e}\n\n"));

    private static Dictionary<string, JsonElement> Extra(string name, int value) =>
        new() { [name] = JsonSerializer.SerializeToElement(value) };
}
