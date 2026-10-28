# SM.ChatRouter

Servicio de routing de prompts de un web chat a categorías mediante un modelo de IA (.NET 10, Clean Architecture).

## Estructura

| Proyecto | Responsabilidad |
|---|---|
| `src/SM.ChatRouter.Domain` | Entidades (`Category`, `CategoryParameter`, `Conversation`) y `RoutingResult`. Sin dependencias. |
| `src/SM.ChatRouter.Application` | Interfaces de repositorios y del cliente IA, `RoutingService` (núcleo), fallback y opciones. |
| `src/SM.ChatRouter.Infrastructure` | `JsonCategoryRepository`, `InMemoryConversationRepository` y los clientes de IA (`Ai/OpenAI`). |
| `src/SM.ChatRouter.Api` | Minimal API (`POST /api/chat`, `DELETE /api/chat/{id}`, `GET /api/categories`) y `Data/categories.json`. |
| `tests/SM.ChatRouter.Tests` | xUnit v3 + NSubstitute: dominio, servicio, repositorios y API. |

## Flujo del `RoutingService`

1. Valida el mensaje (vacío o demasiado largo → 400).
2. Recupera o crea la conversación (si ya estaba enrutada, empieza un routing nuevo).
3. Envía al modelo el prompt, el historial, las categorías (sin la de fallback) y los parámetros ya recogidos.
4. Valida la respuesta sin fiarse del modelo:
   - `NoMatch`, categoría desconocida, confianza < `MinConfidence`, error/timeout de IA → **fallback**.
   - Faltan parámetros obligatorios (aunque el modelo diga que no) → **pregunta** al usuario.
   - Todos los obligatorios presentes → **Routed** con categoría y parámetros.
5. Tras `MaxClarificationTurns` preguntas sin resolver → fallback.

El fallback usa `Routing:FallbackCategoryId`; si no existe en el JSON, la primera categoría con `isFallback: true`; si tampoco, una categoría por defecto en memoria.

## Proveedores de IA

`Ai:Provider` en `appsettings.json` elige la implementación de `IAiRoutingClient` (hoy solo `OpenAI`). La parte común está en `Infrastructure/Ai/`: el prompt de sistema se lee del fichero `Ai:SystemPromptPath` (por defecto `Prompts/routing-system.md`, editable sin recompilar; si no se configura se usa `AiPromptTemplates.DefaultSystemPrompt`), el contexto dinámico en `AiPromptTemplates.BuildContext` y el esquema en `RoutingResponseSchema`; cada proveedor tiene su carpeta (`Infrastructure/Ai/OpenAI/`). Para añadir Claude, Qwen, etc.: crear `Infrastructure/Ai/<Proveedor>/<Proveedor>Client.cs` y añadir su `case` en `Infrastructure/DependencyInjection.cs`.

### OpenAI (Responses API)

Configuración en la sección `OpenAI` (`OpenAIOptions`): `Model`, `BaseUrl`, `Organization`, `Project`, `Timeout`, `MaxFunctionCallIterations` y `Defaults` (reasoning effort, verbosity, max output tokens, store…), que solo se aplican a los campos que la petición deja a `null`. La clave **no** va en `appsettings.json`:

```bash
dotnet user-secrets --project src/SM.ChatRouter.Api set "OpenAI:ApiKey" "<tu-clave>"
```

Sin clave, la API arranca igual y las peticiones devuelven fallback con motivo `AiError`.

Piezas:

- `IOpenAIResponsesClient`: `POST /v1/responses` con control total del payload (`ResponseRequest`): `Reasoning`, `Text.Format` (`json_schema`…), `Tools` (`FunctionTool`, `WebSearchTool`, `FileSearchTool`, `CodeInterpreterTool`, `McpTool`, `RawTool`), `ToolChoice`, `Include`, `PreviousResponseId`, `Background`… `StreamAsync` devuelve los eventos SSE, `CreateRawAsync` envía un `JsonObject` tal cual y todos los modelos tienen `AdditionalProperties` para campos que aún no estén modelados.
- `IOpenAIConversationRunner`: ejecuta automáticamente las funciones registradas (`IOpenAIFunctionHandler`) hasta obtener la respuesta final.
- `OpenAIClient`: implementación del routing con structured outputs (strict).

```csharp
var response = await responses.CreateAsync(new ResponseRequest
{
    Model = "gpt-5",
    Input = [InputMessage.User("¿Qué tiempo hace en Madrid?")],
    Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Medium, Summary = "auto" },
    Tools = [new WebSearchTool(), new RawTool(JsonNode.Parse("""{ "type": "image_generation" }""")!)],
    ToolChoice = ToolChoice.Auto,
    AdditionalProperties = new Dictionary<string, JsonElement>
    {
        ["campo_nuevo"] = JsonSerializer.SerializeToElement(true)
    }
});
Console.WriteLine(response.OutputText);
```

Función invocable por el modelo:

```csharp
public sealed class ConsultarFacturaFunction : IOpenAIFunctionHandler
{
    public string Name => "consultar_factura";
    public string? Description => "Devuelve el estado de una factura";
    public bool Strict => true;
    public JsonNode ParametersSchema => JsonNode.Parse("""
        { "type": "object", "additionalProperties": false,
          "required": ["numero"], "properties": { "numero": { "type": "string" } } }
        """)!;

    public Task<string> InvokeAsync(JsonElement arguments, CancellationToken ct) =>
        Task.FromResult($$"""{ "numero": "{{arguments.GetProperty("numero").GetString()}}", "estado": "pagada" }""");
}

// Program.cs
builder.Services.AddOpenAIFunction<ConsultarFacturaFunction>();
```

Si usas un modelo de razonamiento con `Store = false` y funciones, añade `Include = ["reasoning.encrypted_content"]` para que el razonamiento se conserve entre vueltas.

## Comandos

```bash
dotnet build SM.ChatRouter.slnx
```

```bash
dotnet test --solution SM.ChatRouter.slnx
```

```bash
dotnet run --project src/SM.ChatRouter.Api
```

## Batería de evaluación con IA real

`tests/SM.ChatRouter.Tests/Evaluation/RoutingEvaluationTests.cs` ejecuta los 500 casos de `TestData/Evaluation/sm_500_casos_prueba.csv` contra el modelo real. Como llama a la API de OpenAI (coste y ~30 min), por defecto se omiten: un `dotnet test` normal solo ejecuta los tests unitarios y los de consistencia del CSV con el catálogo.

1. La clave de OpenAI es un secreto: se guarda con user-secrets, nunca en `appsettings.json`.

   ```bash
   dotnet user-secrets --project src/SM.ChatRouter.Api set "OpenAI:ApiKey" "sk-proj-xxxxxx"
   ```

2. Activa la evaluación con la variable de entorno `SMROUTER_RUN_EVALUATION` y lanza los tests (PowerShell):

   ```powershell
   $env:SMROUTER_RUN_EVALUATION = "true"
   dotnet test --solution SM.ChatRouter.slnx
   ```

Sin la variable, o sin clave configurada, los casos aparecen como omitidos.
