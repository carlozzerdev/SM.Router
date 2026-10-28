# SM.ChatRouter

Servicio de routing de prompts de un web chat a categorías mediante un modelo de IA (.NET 10, Clean Architecture).

## Estructura

| Proyecto | Responsabilidad |
|---|---|
| `src/SM.ChatRouter.Domain` | Entidades (`Category`, `CategoryParameter`, `Conversation`) y `RoutingResult`. Sin dependencias. |
| `src/SM.ChatRouter.Application` | Interfaces de repositorios y del cliente IA, `RoutingService` (núcleo), fallback y opciones. |
| `src/SM.ChatRouter.Infrastructure` | `JsonCategoryRepository`, `InMemoryConversationRepository` y los clientes de IA (`Ai/OpenAI`). |
| `src/SM.ChatRouter.Api` | Minimal API (`POST /api/chat`, `DELETE /api/chat/{id}`, `GET /api/categories`) y `Data/categories.json`. |
| `src/SM.ChatRouter.Web` | Web de chat para pruebas manuales (React + Vite, `.esproj`). Llama a la API por HTTP. |
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

## Web de pruebas

La solución incluye un proyecto web, `src/SM.ChatRouter.Web` (`SM.ChatRouter.Web.esproj`): un chat React + Vite + TypeScript para probar el router a mano contra la API.

### Dependencias

| Dependencia | Versión | Para qué |
|---|---|---|
| [Node.js](https://nodejs.org/) | 22 LTS o superior (incluye npm) | Ejecutar Vite y gestionar los paquetes |
| .NET SDK | 10 | La API (`SM.ChatRouter.Api`), que tiene que estar arrancada |
| Visual Studio (opcional) | 2022 17.x / 2026, con la carga de trabajo "Desarrollo de Node.js" o "ASP.NET y desarrollo web" | Abrir el `.esproj` y usar el perfil **Api + Web** |

Paquetes npm (en `package.json`, se instalan con `npm install`): `react`, `react-dom`, `vite`, `@vitejs/plugin-react`, `typescript` y los tipos `@types/*`. No usa librerías de UI.

### Ejecución

1. Instalar las dependencias (solo la primera vez o cuando cambie `package.json`):

   ```bash
   cd src/SM.ChatRouter.Web
   npm install
   ```

2. Arrancar la API (desde la raíz del repositorio):

   ```bash
   dotnet run --project src/SM.ChatRouter.Api --launch-profile http
   ```

3. Arrancar la web (desde `src/SM.ChatRouter.Web`) y abrir `http://localhost:5173`:

   ```bash
   npm run dev
   ```

El dev server hace de proxy de `/api` a la API, así que no hace falta CORS. Al arrancar, va a `https://localhost:7256` si ese puerto escucha (perfil `https`) y, si no, a `http://localhost:5022` (perfil `http`). Con la variable `API_URL` se fuerza otro destino.

En Visual Studio, el perfil de arranque **Api + Web** (`SM.ChatRouter.slnLaunch`) lanza los dos a la vez. `dotnet build` de la solución no compila la web: su build es `npm run build`.

Comportamiento del chat según `outcome`:

- `NeedsMoreInformation`: muestra `question` y sigue la conversación.
- `Routed`: propone el formulario `<VITE_CATEGORY_BASE_URL>/<category.id>` y cierra la conversación.
- `Fallback`: con `NoMatch`, `LowConfidence` o `UnknownCategory` pide más detalle y ofrece atajos con las categorías de nivel 1, hasta `VITE_MAX_REDIRECTS` veces. Con `AiError` ofrece **Reintentar**. En el resto de casos, o al agotar los intentos, da el formulario de consulta general. Como el servidor cierra la conversación tras un fallback, la web reenvía los mensajes anteriores del usuario junto con la aclaración (`src/fallbackPolicy.ts`).

La configuración está en `src/SM.ChatRouter.Web/.env`; los cambios locales van en `.env.local`. El panel derecho muestra el diagnóstico de cada turno (pincha en un mensaje del bot para verlo): categoría, parámetros, campos que faltan, motivo del fallback, texto enviado y JSON.

## Batería de evaluación con IA real

`tests/SM.ChatRouter.Tests/Evaluation/RoutingEvaluationTests.cs` ejecuta los 500 casos de `TestData/Evaluation/sm_500_casos_prueba.csv` contra el modelo real. Como llama a la API de OpenAI (coste y ~30 min), por defecto se omiten: un `dotnet test` normal solo ejecuta los tests unitarios y los de consistencia del CSV con el catálogo.

1. La clave de OpenAI es un secreto: se guarda con user-secrets, nunca en `appsettings.json`.

   ```bash
   dotnet user-secrets --project src/SM.ChatRouter.Api set "OpenAI:ApiKey" "sk-proj-xxxxxx"
   ```

2. Activa la evaluación con la variable de entorno `SMROUTER_RUN_EVALUATION` y lanza los tests (PowerShell). `--report-xunit-html` genera un informe HTML en `tests/SM.ChatRouter.Tests/bin/Debug/net10.0/TestResults/`:

   ```powershell
   $env:SMROUTER_RUN_EVALUATION = "true"
   dotnet test --solution SM.ChatRouter.slnx --report-xunit-html
   dotnet test --solution SM.ChatRouter.slnx --filter-trait "Category=Evaluation" --report-xunit-html #solo evalua los 500 prompts
   ```

   Cada caso aparece en el informe como `[id] Estado: mensaje`, con el `id` del CSV, y los fallos indican la categoría esperada y lo que devolvió el router.

Sin la variable, o sin clave configurada, los casos aparecen como omitidos.
