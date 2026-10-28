using System.Text;

namespace SM.ChatRouter.Tests.TestData.Evaluation;

/// <summary>Estado esperado de un caso de la batería de evaluación (columna <c>estado_esperado</c>).</summary>
public enum ExpectedState
{
    /// <summary>El mensaje encaja en una categoría N3 concreta del catálogo.</summary>
    Clasificada,

    /// <summary>El mensaje es ambiguo: hay que preguntar por el contexto antes de elegir categoría.</summary>
    Aclarar,

    /// <summary>El mensaje es ajeno al catálogo: no se debe forzar ninguna categoría.</summary>
    NoCategorizable
}

/// <summary>Caso de prueba del fichero <c>sm_500_casos_prueba.csv</c>.</summary>
public sealed record EvaluationCase
{
    public required int Id { get; init; }

    public required string Message { get; init; }

    public required ExpectedState State { get; init; }

    /// <summary>Fila del Excel de la categoría esperada (<see cref="SM.ChatRouter.Domain.Entities.Category.SourceRow"/>).</summary>
    public int? SourceRow { get; init; }

    public string? Level1 { get; init; }

    public string? Level2 { get; init; }

    public string? Level3 { get; init; }

    public string Criterion { get; init; } = string.Empty;
}

internal static class EvaluationCases
{
    public const string FileName = "sm_500_casos_prueba.csv";

    private static readonly Lazy<IReadOnlyDictionary<int, EvaluationCase>> Cases = new(Load);

    public static IReadOnlyCollection<EvaluationCase> All => (IReadOnlyCollection<EvaluationCase>)Cases.Value.Values;

    public static EvaluationCase Get(int id) => Cases.Value[id];

    private static IReadOnlyDictionary<int, EvaluationCase> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Evaluation", FileName);
        var lines = File.ReadAllLines(path, Encoding.UTF8);

        var header = ParseLine(lines[0]);
        int Column(string name) => Array.IndexOf(header, name) is var i and >= 0
            ? i
            : throw new InvalidOperationException($"Falta la columna '{name}' en {FileName}.");

        var id = Column("id");
        var message = Column("mensaje_usuario");
        var state = Column("estado_esperado");
        var row = Column("fila_excel_esperada");
        var level1 = Column("rama_esperada");
        var level2 = Column("subcategoria_esperada");
        var level3 = Column("categoria_esperada");
        var criterion = Column("criterio");

        return lines
            .Skip(1)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(ParseLine)
            .Select(f => new EvaluationCase
            {
                Id = int.Parse(f[id]),
                Message = f[message],
                State = ParseState(f[state]),
                SourceRow = string.IsNullOrWhiteSpace(f[row]) ? null : int.Parse(f[row]),
                Level1 = NullIfEmpty(f[level1]),
                Level2 = NullIfEmpty(f[level2]),
                Level3 = NullIfEmpty(f[level3]),
                Criterion = f[criterion]
            })
            .ToDictionary(c => c.Id);
    }

    private static ExpectedState ParseState(string value) => value.Trim() switch
    {
        "CLASIFICADA" => ExpectedState.Clasificada,
        "ACLARAR" => ExpectedState.Aclarar,
        "NO_CATEGORIZABLE" => ExpectedState.NoCategorizable,
        _ => throw new InvalidOperationException($"Estado esperado desconocido: '{value}'.")
    };

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // CSV con comillas dobles opcionales y "" como escape; ningún campo ocupa varias líneas.
    private static string[] ParseLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return [.. fields];
    }
}
