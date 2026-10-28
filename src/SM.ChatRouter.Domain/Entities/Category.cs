namespace SM.ChatRouter.Domain.Entities;

/// <summary>
/// Categoría seleccionable por el routing: un nodo de nivel 3 (N3) del árbol de categorías,
/// con la referencia a sus niveles superiores (N1 y N2).
/// </summary>
public sealed record Category
{
    /// <summary>Identificador estable: ruta de slugs <c>slugN1/slugN2/slugN3</c>.</summary>
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Slug del N3, generado a partir de su nombre.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Nivel 1 del árbol (p. ej. "Soporte para profesores"). Null en la categoría de fallback.</summary>
    public CategoryNode? Level1 { get; init; }

    /// <summary>Nivel 2 del árbol (p. ej. "Mis pedidos"). Null en la categoría de fallback.</summary>
    public CategoryNode? Level2 { get; init; }

    /// <summary>Rama del N1 ("Origen", "Camino"...).</summary>
    public string? Branch { get; init; }

    public string? Language { get; init; }

    public IReadOnlyList<string> Communities { get; init; } = [];

    public bool AllCommunities { get; init; }

    /// <summary>Fila del Excel del que procede la categoría (trazabilidad).</summary>
    public int? SourceRow { get; init; }

    public IReadOnlyList<CategoryParameter> Parameters { get; init; } = [];

    /// <summary>Ruta legible: <c>N1 &gt; N2 &gt; N3</c>.</summary>
    public string FullName =>
        string.Join(" > ", new[] { Level1?.Name, Level2?.Name, Name }.Where(n => !string.IsNullOrWhiteSpace(n)));

    public IEnumerable<CategoryParameter> RequiredParameters => Parameters.Where(p => p.IsRequired);

    public CategoryParameter? FindParameter(string name) =>
        Parameters.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<CategoryParameter> GetMissingRequired(IReadOnlyDictionary<string, string> collected) =>
        RequiredParameters
            .Where(p => !collected.TryGetValue(p.Name, out var value) || string.IsNullOrWhiteSpace(value))
            .ToList();
}

/// <summary>Nivel superior (N1 o N2) del árbol de categorías.</summary>
public sealed record CategoryNode(string Slug, string Name);
