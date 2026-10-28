using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SM.ChatRouter.Application.Abstractions;
using SM.ChatRouter.Application.Services;
using SM.ChatRouter.Domain.Enums;
using SM.ChatRouter.Domain.ValueObjects;
using SM.ChatRouter.Tests.TestData.Evaluation;

namespace SM.ChatRouter.Tests.Evaluation;

/// <summary>
/// Batería de evaluación del routing con el modelo real (<c>TestData/Evaluation/sm_500_casos_prueba.csv</c>).
/// Cada caso es un primer mensaje de una conversación nueva. Llama a la API de IA, así que solo se ejecuta
/// si <see cref="EnabledVariable"/> vale <c>true</c> y hay clave configurada (user-secrets de la API).
/// </summary>
[Trait("Category", "Evaluation")]
public sealed class RoutingEvaluationTests : IClassFixture<WebApplicationFactory<Program>>
{
    public const string EnabledVariable = "SMROUTER_RUN_EVALUATION";

    private readonly WebApplicationFactory<Program> _factory;

    public RoutingEvaluationTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static IEnumerable<TheoryDataRow<int>> Cases =>
        EvaluationCases.All.Select(c => new TheoryDataRow<int>(c.Id)
            {
                TestDisplayName = $"[{c.Id:000}] {c.State}: {c.Message}"
            }
            .WithTrait("Estado", c.State.ToString()));

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Case_IsRoutedAsExpected(int id)
    {
        Assert.SkipUnless(
            string.Equals(Environment.GetEnvironmentVariable(EnabledVariable), "true", StringComparison.OrdinalIgnoreCase),
            $"Evaluación con IA real desactivada. Define {EnabledVariable}=true para ejecutarla.");

        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        Assert.SkipWhen(string.IsNullOrWhiteSpace(configuration["OpenAI:ApiKey"]), "No hay OpenAI:ApiKey configurada.");

        var testCase = EvaluationCases.Get(id);

        using var scope = _factory.Services.CreateScope();
        var routing = scope.ServiceProvider.GetRequiredService<IRoutingService>();
        var result = await routing.ProcessMessageAsync(null, testCase.Message, Ct);

        var actual = Describe(result);
        Assert.NotEqual(FallbackReason.AiError, result.FallbackReason);

        switch (testCase.State)
        {
            case ExpectedState.Clasificada:
                // Se acepta Routed o NeedsMoreInformation: lo que importa es la categoría (los obligatorios se piden después).
                Assert.True(result.Outcome != RoutingOutcome.Fallback && result.Category is not null,
                    $"Se esperaba la fila {testCase.SourceRow} ({testCase.Level1} > {testCase.Level2} > {testCase.Level3}). {actual}");
                Assert.True(result.Category.SourceRow == testCase.SourceRow,
                    $"Se esperaba la fila {testCase.SourceRow} ({testCase.Level1} > {testCase.Level2} > {testCase.Level3}). {actual}");

                // "Pedir solo campos obligatorios que falten": si pregunta, es porque falta algún obligatorio.
                if (result.Outcome == RoutingOutcome.NeedsMoreInformation)
                {
                    Assert.True(result.MissingParameters.Count > 0, $"Pregunta sin que falten obligatorios. {actual}");
                }

                break;

            case ExpectedState.Aclarar:
                Assert.True(result.Outcome == RoutingOutcome.NeedsMoreInformation && result.Category is null,
                    $"Se esperaba una pregunta de aclaración sin categoría candidata. {actual}");
                Assert.True(result.MissingParameters.Count == 0, $"La aclaración no debe pedir datos del formulario. {actual}");
                Assert.False(string.IsNullOrWhiteSpace(result.Question), $"Falta la pregunta de aclaración. {actual}");
                break;

            case ExpectedState.NoCategorizable:
                Assert.True(result.Outcome == RoutingOutcome.Fallback && result.FallbackReason == FallbackReason.NoMatch,
                    $"Se esperaba fallback por NoMatch. {actual}");
                break;
        }
    }

    private static string Describe(RoutingResult result) =>
        $"Obtenido: {result.Outcome}"
        + (result.FallbackReason is { } reason ? $" ({reason})" : string.Empty)
        + (result.Category is { } category ? $", fila {category.SourceRow}: {category.FullName}" : ", sin categoría")
        + (result.MissingParameters.Count > 0 ? $", faltan [{string.Join(", ", result.MissingParameters)}]" : string.Empty)
        + (result.Question is { } question ? $", pregunta: \"{question}\"" : string.Empty);
}

/// <summary>Comprueba sin IA que la batería de evaluación es coherente con el catálogo publicado.</summary>
public sealed class EvaluationCasesConsistencyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EvaluationCasesConsistencyTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Cases_HaveUniqueIdsAndExpectedShape()
    {
        var cases = EvaluationCases.All;

        Assert.Equal(500, cases.Count);
        Assert.All(cases, c => Assert.False(string.IsNullOrWhiteSpace(c.Message), $"Caso {c.Id} sin mensaje"));
        Assert.All(cases.Where(c => c.State == ExpectedState.Clasificada),
            c => Assert.True(c.SourceRow is not null && c.Level1 is not null && c.Level2 is not null && c.Level3 is not null,
                $"Caso {c.Id} clasificado sin categoría esperada completa"));
        Assert.All(cases.Where(c => c.State != ExpectedState.Clasificada),
            c => Assert.True(c.SourceRow is null, $"Caso {c.Id} no clasificado con fila esperada"));
    }

    [Fact]
    public async Task ClassifiedCases_MatchCategoryTree()
    {
        using var scope = _factory.Services.CreateScope();
        var categories = await scope.ServiceProvider.GetRequiredService<ICategoryRepository>().GetAllAsync(Ct);
        var byRow = categories.Where(c => c.SourceRow is not null).ToDictionary(c => c.SourceRow!.Value);

        var classified = EvaluationCases.All.Where(c => c.State == ExpectedState.Clasificada).ToList();

        Assert.All(classified, c =>
        {
            Assert.True(byRow.TryGetValue(c.SourceRow!.Value, out var category), $"Caso {c.Id}: no existe la fila {c.SourceRow}");
            Assert.Equal((c.Level1, c.Level2, c.Level3), (category.Level1?.Name, category.Level2?.Name, category.Name));
        });

        // Todas las categorías del catálogo tienen al menos un caso.
        Assert.Empty(byRow.Keys.Except(classified.Select(c => c.SourceRow!.Value)));
    }
}
