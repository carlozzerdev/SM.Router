using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SM.ChatRouter.Infrastructure.Ai.OpenAI.Models;

/// <summary>Elemento del array <c>input</c>.</summary>
public abstract record ResponseInputItem
{
    [JsonPropertyOrder(-1)]
    public abstract string Type { get; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

public sealed record InputMessage : ResponseInputItem
{
    public override string Type => "message";

    /// <summary><c>user</c>, <c>assistant</c>, <c>system</c> o <c>developer</c>.</summary>
    public required string Role { get; init; }

    public required IList<InputContentPart> Content { get; init; }

    public static InputMessage User(string text) => new() { Role = "user", Content = [new InputTextPart(text)] };

    public static InputMessage Developer(string text) => new() { Role = "developer", Content = [new InputTextPart(text)] };

    public static InputMessage System(string text) => new() { Role = "system", Content = [new InputTextPart(text)] };

    /// <summary>Mensaje previo del asistente (el contenido debe ser <c>output_text</c>).</summary>
    public static InputMessage Assistant(string text) => new() { Role = "assistant", Content = [new OutputTextPart(text)] };
}

public sealed record InputFunctionCallOutput(string CallId, string Output) : ResponseInputItem
{
    public override string Type => "function_call_output";
}

public sealed record InputItemReference(string Id) : ResponseInputItem
{
    public override string Type => "item_reference";
}

/// <summary>Cualquier item de entrada en JSON crudo (p. ej. un item del <c>output</c> previo reenviado).</summary>
public sealed record InputRawItem(JsonNode Json) : ResponseInputItem, IRawJson
{
    public override string Type => Json["type"]?.GetValue<string>() ?? string.Empty;
}

/// <summary>Parte del contenido de un mensaje.</summary>
public abstract record InputContentPart
{
    [JsonPropertyOrder(-1)]
    public abstract string Type { get; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

public sealed record InputTextPart(string Text) : InputContentPart
{
    public override string Type => "input_text";
}

public sealed record OutputTextPart(string Text) : InputContentPart
{
    public override string Type => "output_text";
}

public sealed record InputImagePart : InputContentPart
{
    public override string Type => "input_image";

    /// <summary>URL o data URL en base64.</summary>
    public string? ImageUrl { get; init; }

    public string? FileId { get; init; }

    /// <summary><c>low</c>, <c>high</c> o <c>auto</c>.</summary>
    public string? Detail { get; init; } = "auto";
}

public sealed record InputFilePart : InputContentPart
{
    public override string Type => "input_file";

    public string? FileId { get; init; }

    public string? FileUrl { get; init; }

    /// <summary>Contenido en data URL base64.</summary>
    public string? FileData { get; init; }

    public string? Filename { get; init; }
}

public sealed record InputRawPart(JsonNode Json) : InputContentPart, IRawJson
{
    public override string Type => Json["type"]?.GetValue<string>() ?? string.Empty;
}
