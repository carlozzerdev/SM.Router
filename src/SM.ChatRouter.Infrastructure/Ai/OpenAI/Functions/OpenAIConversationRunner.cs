using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SM.ChatRouter.Infrastructure.Ai.OpenAI.Models;

namespace SM.ChatRouter.Infrastructure.Ai.OpenAI.Functions;

public sealed class OpenAIConversationRunner(
    IOpenAIResponsesClient client,
    IEnumerable<IOpenAIFunctionHandler> handlers,
    IOptions<OpenAIOptions> options,
    ILogger<OpenAIConversationRunner> logger) : IOpenAIConversationRunner
{
    private readonly Dictionary<string, IOpenAIFunctionHandler> _handlers =
        handlers.ToDictionary(h => h.Name, StringComparer.Ordinal);

    private readonly int _maxIterations = options.Value.MaxFunctionCallIterations;
    private readonly bool? _defaultStore = options.Value.Defaults.Store;

    public async Task<ResponseResult> RunAsync(ResponseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (current, input) = Prepare(request);

        for (var iteration = 0; ; iteration++)
        {
            var response = await client.CreateAsync(current, cancellationToken);
            var calls = response.FunctionCalls;
            if (calls.Count == 0)
            {
                return response;
            }

            current = await ContinueAsync(current, input, response, calls, iteration, cancellationToken);
        }
    }

    public async IAsyncEnumerable<ResponseStreamEvent> RunStreamingAsync(
        ResponseRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (current, input) = Prepare(request);

        for (var iteration = 0; ; iteration++)
        {
            ResponseResult? final = null;
            await foreach (var streamEvent in client.StreamAsync(current, cancellationToken))
            {
                if (streamEvent.Type == "response.completed")
                {
                    final = streamEvent.Response;
                }
                yield return streamEvent;
            }

            var calls = final?.FunctionCalls ?? [];
            if (final is null || calls.Count == 0)
            {
                yield break;
            }

            current = await ContinueAsync(current, input, final, calls, iteration, cancellationToken);
        }
    }

    private (ResponseRequest Request, List<ResponseInputItem> Input) Prepare(ResponseRequest request)
    {
        var input = request.Input.ToList();
        var tools = request.Tools?.ToList() ?? [];

        var declared = tools.OfType<FunctionTool>().Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var handler in _handlers.Values.Where(h => !declared.Contains(h.Name)))
        {
            tools.Add(new FunctionTool
            {
                Name = handler.Name,
                Description = handler.Description,
                Parameters = handler.ParametersSchema.DeepClone(),
                Strict = handler.Strict
            });
        }

        // Cada petición lleva su propia copia del input: la lista acumulada se sigue modificando entre vueltas.
        return (request with { Input = input.ToList(), Tools = tools.Count > 0 ? tools : null }, input);
    }

    private async Task<ResponseRequest> ContinueAsync(
        ResponseRequest current,
        List<ResponseInputItem> input,
        ResponseResult response,
        IReadOnlyList<OutputFunctionCall> calls,
        int iteration,
        CancellationToken cancellationToken)
    {
        if (iteration + 1 >= _maxIterations)
        {
            throw new OpenAIException($"Se superó el máximo de {_maxIterations} vueltas de function calling.")
            {
                ResponseId = response.Id
            };
        }

        // Se reenvía el output (reasoning + function_call) para que el modelo tenga el contexto completo.
        // Sin store, los items de reasoning solo se pueden reenviar si traen encrypted_content
        // (pedirlo con Include = ["reasoning.encrypted_content"]).
        var stored = (current.Store ?? _defaultStore) == true;
        foreach (var item in response.Output)
        {
            if (item is OutputReasoning { EncryptedContent: null } && !stored)
            {
                continue;
            }
            input.Add(item.ToInputItem());
        }

        IEnumerable<InputFunctionCallOutput> outputs;
        if (current.ParallelToolCalls == false)
        {
            var sequential = new List<InputFunctionCallOutput>();
            foreach (var call in calls)
            {
                sequential.Add(await InvokeAsync(call, cancellationToken));
            }
            outputs = sequential;
        }
        else
        {
            outputs = await Task.WhenAll(calls.Select(call => InvokeAsync(call, cancellationToken)));
        }

        input.AddRange(outputs);
        return current with { Input = input.ToList() };
    }

    private async Task<InputFunctionCallOutput> InvokeAsync(OutputFunctionCall call, CancellationToken cancellationToken)
    {
        if (!_handlers.TryGetValue(call.Name, out var handler))
        {
            logger.LogWarning("El modelo llamó a una función no registrada: {Function}", call.Name);
            return new InputFunctionCallOutput(call.CallId, ErrorOutput($"La función '{call.Name}' no existe."));
        }

        try
        {
            using var arguments = JsonDocument.Parse(string.IsNullOrWhiteSpace(call.Arguments) ? "{}" : call.Arguments);
            var output = await handler.InvokeAsync(arguments.RootElement.Clone(), cancellationToken);
            return new InputFunctionCallOutput(call.CallId, output);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ejecutando la función {Function}", call.Name);
            return new InputFunctionCallOutput(call.CallId, ErrorOutput(ex.Message));
        }
    }

    private static string ErrorOutput(string message) => new JsonObject { ["error"] = message }.ToJsonString();
}
