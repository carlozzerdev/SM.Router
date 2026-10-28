using System.Text.Json;
using System.Text.Json.Nodes;
using SM.ChatRouter.Infrastructure.Ai.OpenAI.Models;

namespace SM.ChatRouter.Tests.TestData;

/// <summary>Constructores de JSON de respuestas de la Responses API para los tests.</summary>
internal static class OpenAIFakes
{
    public static JsonObject Message(string text) => new()
    {
        ["type"] = "message",
        ["id"] = "msg_1",
        ["status"] = "completed",
        ["role"] = "assistant",
        ["content"] = new JsonArray(new JsonObject { ["type"] = "output_text", ["text"] = text, ["annotations"] = new JsonArray() })
    };

    public static JsonObject Refusal(string text) => new()
    {
        ["type"] = "message",
        ["id"] = "msg_1",
        ["role"] = "assistant",
        ["content"] = new JsonArray(new JsonObject { ["type"] = "refusal", ["refusal"] = text })
    };

    public static JsonObject FunctionCall(string callId, string name, string arguments) => new()
    {
        ["type"] = "function_call",
        ["id"] = $"fc_{callId}",
        ["call_id"] = callId,
        ["name"] = name,
        ["arguments"] = arguments,
        ["status"] = "completed"
    };

    public static JsonObject Reasoning(string? encryptedContent = null) => new()
    {
        ["type"] = "reasoning",
        ["id"] = "rs_1",
        ["summary"] = new JsonArray(),
        ["encrypted_content"] = encryptedContent
    };

    public static string ResponseJson(string status = "completed", params JsonObject[] output) => new JsonObject
    {
        ["id"] = "resp_1",
        ["object"] = "response",
        ["status"] = status,
        ["model"] = "gpt-test",
        ["output"] = new JsonArray(output.Select(o => (JsonNode)o).ToArray()),
        ["usage"] = new JsonObject
        {
            ["input_tokens"] = 10,
            ["output_tokens"] = 5,
            ["total_tokens"] = 15,
            ["output_tokens_details"] = new JsonObject { ["reasoning_tokens"] = 2 }
        }
    }.ToJsonString();

    public static ResponseResult Result(params JsonObject[] output) =>
        JsonSerializer.Deserialize<ResponseResult>(ResponseJson("completed", output), OpenAIJson.Options)!;
}
