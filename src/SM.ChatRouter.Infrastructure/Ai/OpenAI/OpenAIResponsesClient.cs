using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using SM.ChatRouter.Infrastructure.Ai.OpenAI.Models;

namespace SM.ChatRouter.Infrastructure.Ai.OpenAI;

public sealed class OpenAIResponsesClient(HttpClient httpClient, IOptions<OpenAIOptions> options) : IOpenAIResponsesClient
{
    private const string ResponsesPath = "responses";
    private readonly OpenAIOptions _options = options.Value;

    public async Task<ResponseResult> CreateAsync(ResponseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var payload = ApplyDefaults(request) with { Stream = false };

        using var message = CreateRequest(HttpMethod.Post, ResponsesPath, Serialize(payload));
        return await SendAsync(message, cancellationToken);
    }

    public async IAsyncEnumerable<ResponseStreamEvent> StreamAsync(
        ResponseRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var payload = ApplyDefaults(request) with { Stream = true };

        using var message = CreateRequest(HttpMethod.Post, ResponsesPath, Serialize(payload));
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await foreach (var item in SseParser.Create(stream).EnumerateAsync(cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(item.Data) || item.Data == "[DONE]")
            {
                continue;
            }

            var streamEvent = ResponseStreamEvent.Parse(item.Data);
            yield return streamEvent;

            if (streamEvent.IsTerminal)
            {
                yield break;
            }
        }
    }

    public async Task<ResponseResult> CreateRawAsync(JsonObject payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        using var message = CreateRequest(HttpMethod.Post, ResponsesPath, payload.ToJsonString(OpenAIJson.Options));
        return await SendAsync(message, cancellationToken);
    }

    public async Task<ResponseResult> GetAsync(string responseId, CancellationToken cancellationToken = default)
    {
        using var message = CreateRequest(HttpMethod.Get, ResponsePath(responseId));
        return await SendAsync(message, cancellationToken);
    }

    public async Task<ResponseResult> CancelAsync(string responseId, CancellationToken cancellationToken = default)
    {
        using var message = CreateRequest(HttpMethod.Post, $"{ResponsePath(responseId)}/cancel");
        return await SendAsync(message, cancellationToken, validateStatus: false);
    }

    public async Task DeleteAsync(string responseId, CancellationToken cancellationToken = default)
    {
        using var message = CreateRequest(HttpMethod.Delete, ResponsePath(responseId));
        using var response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private ResponseRequest ApplyDefaults(ResponseRequest request)
    {
        var defaults = _options.Defaults;

        var reasoning = request.Reasoning;
        if (defaults.ReasoningEffort is not null || defaults.ReasoningSummary is not null)
        {
            reasoning = (reasoning ?? new ReasoningOptions()) with
            {
                Effort = reasoning?.Effort ?? defaults.ReasoningEffort,
                Summary = reasoning?.Summary ?? defaults.ReasoningSummary
            };
        }

        var text = request.Text;
        if (defaults.Verbosity is not null && text?.Verbosity is null)
        {
            text = (text ?? new ResponseTextOptions()) with { Verbosity = defaults.Verbosity };
        }

        return request with
        {
            Model = string.IsNullOrWhiteSpace(request.Model) ? _options.Model : request.Model,
            Reasoning = reasoning,
            Text = text,
            MaxOutputTokens = request.MaxOutputTokens ?? defaults.MaxOutputTokens,
            Temperature = request.Temperature ?? defaults.Temperature,
            TopP = request.TopP ?? defaults.TopP,
            Store = request.Store ?? defaults.Store,
            ServiceTier = request.ServiceTier ?? defaults.ServiceTier,
            Truncation = request.Truncation ?? defaults.Truncation
        };
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, string? json = null)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new OpenAIException("No se ha configurado 'OpenAI:ApiKey' (usa user-secrets o la variable de entorno OpenAI__ApiKey).");
        }

        var message = new HttpRequestMessage(method, path);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        if (!string.IsNullOrWhiteSpace(_options.Organization))
        {
            message.Headers.Add("OpenAI-Organization", _options.Organization);
        }
        if (!string.IsNullOrWhiteSpace(_options.Project))
        {
            message.Headers.Add("OpenAI-Project", _options.Project);
        }
        if (json is not null)
        {
            message.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        return message;
    }

    private async Task<ResponseResult> SendAsync(HttpRequestMessage message, CancellationToken cancellationToken, bool validateStatus = true)
    {
        using var response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        ResponseResult? result;
        try
        {
            result = await response.Content.ReadFromJsonAsync<ResponseResult>(OpenAIJson.Options, cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new OpenAIException("La respuesta de OpenAI no es un JSON válido.", ex) { RequestId = GetRequestId(response) };
        }

        if (result is null)
        {
            throw new OpenAIException("OpenAI devolvió una respuesta vacía.") { RequestId = GetRequestId(response) };
        }

        if (validateStatus && result.Status is "failed" or "incomplete" or "cancelled")
        {
            var detail = result.Error?.Message ?? result.IncompleteDetails?.Reason ?? result.Status;
            throw new OpenAIException($"La respuesta de OpenAI terminó con estado '{result.Status}': {detail}")
            {
                ErrorCode = result.Error?.Code ?? result.IncompleteDetails?.Reason,
                RequestId = GetRequestId(response),
                ResponseId = result.Id
            };
        }

        return result;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        string? type = null, code = null, detail = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                type = GetString(error, "type");
                code = GetString(error, "code");
                detail = GetString(error, "message");
            }
        }
        catch (JsonException)
        {
            // El cuerpo no es JSON: se usa tal cual en el mensaje.
        }

        throw new OpenAIException($"OpenAI devolvió {(int)response.StatusCode} {response.ReasonPhrase}: {detail ?? body}")
        {
            StatusCode = response.StatusCode,
            ErrorType = type,
            ErrorCode = code,
            RequestId = GetRequestId(response)
        };
    }

    private static string Serialize(ResponseRequest request) => JsonSerializer.Serialize(request, OpenAIJson.Options);

    private static string ResponsePath(string responseId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseId);
        return $"{ResponsesPath}/{Uri.EscapeDataString(responseId)}";
    }

    private static string? GetRequestId(HttpResponseMessage response) =>
        response.Headers.TryGetValues("x-request-id", out var values) ? values.FirstOrDefault() : null;

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
