using System.Text.Json.Serialization;

namespace SM.ChatRouter.Infrastructure.Repositories;

// Forma del fichero de categorías tal como se exporta del Excel: un array de N1, cada uno con sus N2 (hijos)
// y cada N2 con sus N3 (hijos). El repositorio lo aplana a entidades de dominio Category (solo N3).

internal sealed record CategoryLevel1Dto
{
    [JsonPropertyName("nivel")]
    public int Level { get; init; }

    [JsonPropertyName("rama")]
    public string? Branch { get; init; }

    [JsonPropertyName("idioma")]
    public string? Language { get; init; }

    [JsonPropertyName("nombre")]
    public string? Name { get; init; }

    [JsonPropertyName("slug")]
    public string? Slug { get; init; }

    [JsonPropertyName("hijos")]
    public List<CategoryLevel2Dto>? Children { get; init; }
}

internal sealed record CategoryLevel2Dto
{
    [JsonPropertyName("nivel")]
    public int Level { get; init; }

    [JsonPropertyName("nombre")]
    public string? Name { get; init; }

    [JsonPropertyName("slug")]
    public string? Slug { get; init; }

    [JsonPropertyName("hijos")]
    public List<CategoryLevel3Dto>? Children { get; init; }
}

internal sealed record CategoryLevel3Dto
{
    [JsonPropertyName("nivel")]
    public int Level { get; init; }

    [JsonPropertyName("nombre")]
    public string? Name { get; init; }

    [JsonPropertyName("campos")]
    public List<CategoryFieldDto>? Fields { get; init; }

    [JsonPropertyName("comunidades")]
    public List<string>? Communities { get; init; }

    [JsonPropertyName("todas_las_comunidades")]
    public bool AllCommunities { get; init; }

    [JsonPropertyName("fila_excel")]
    public int? SourceRow { get; init; }

    [JsonPropertyName("valores_originales")]
    public CategoryOriginalValuesDto? OriginalValues { get; init; }
}

internal sealed record CategoryFieldDto
{
    [JsonPropertyName("nombre")]
    public string? Name { get; init; }

    [JsonPropertyName("obligatorio")]
    public bool Required { get; init; }
}

internal sealed record CategoryOriginalValuesDto
{
    [JsonPropertyName("detalle")]
    public string? Detail { get; init; }

    [JsonPropertyName("campos_formulario")]
    public string? FormFields { get; init; }

    [JsonPropertyName("comunidades")]
    public string? Communities { get; init; }
}
