using SM.ChatRouter.Domain.Enums;
using SM.ChatRouter.Infrastructure.Repositories;

namespace SM.ChatRouter.Tests.Infrastructure;

public sealed class JsonCategoryRepositoryTests : IDisposable
{
    private const string Tree = """
        [
          // los comentarios están permitidos
          {
            "nivel": 1, "rama": "Origen", "idioma": "Espanol", "nombre": "Soporte para profesores", "slug": "Docentes",
            "hijos": [
              {
                "nivel": 2, "nombre": "Mis pedidos", "slug": "mis-pedidos",
                "hijos": [
                  {
                    "nivel": 3,
                    "nombre": "Incidencia en la entrega de un pedido",
                    "campos": [
                      { "nombre": "Número de pedido", "obligatorio": true },
                      { "nombre": "¿Cómo realizaste tu pedido? (Web / Asesor / Otros)", "obligatorio": true },
                      { "nombre": "Teléfono", "obligatorio": false },
                    ],
                    "comunidades": [ "Profesores", "Grupo SM" ],
                    "todas_las_comunidades": false,
                    "fila_excel": 7,
                    "valores_originales": { "detalle": "x", "campos_formulario": "y", "comunidades": "z" }
                  },
                  { "nivel": 3, "nombre": "Cambiar mi usuario", "campos": [] }
                ]
              }
            ]
          },
          {
            "nivel": 1, "nombre": "Soporte para familias", "slug": "familias",
            "hijos": [
              {
                "nivel": 2, "nombre": "Mis pedidos", "slug": "mis-pedidos",
                "hijos": [ { "nivel": 3, "nombre": "Incidencia en la entrega de un pedido" } ]
              }
            ]
          }
        ]
        """;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "SM.ChatRouter.Tests", Guid.NewGuid().ToString("N"));

    public JsonCategoryRepositoryTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private JsonCategoryRepository CreateRepository(string? json, string fileName = "categories.json")
    {
        var path = Path.Combine(_directory, fileName);
        if (json is not null)
        {
            File.WriteAllText(path, json);
        }
        return new JsonCategoryRepository(Microsoft.Extensions.Options.Options.Create(new CategoryRepositoryOptions { FilePath = path }));
    }

    [Fact]
    public async Task FlattensTreeToLevel3Categories_WithSlugPathIds()
    {
        using var repository = CreateRepository(Tree);

        var categories = await repository.GetAllAsync(Ct);

        Assert.Equal(
            [
                "docentes/mis-pedidos/incidencia-en-la-entrega-de-un-pedido",
                "docentes/mis-pedidos/cambiar-mi-usuario",
                "familias/mis-pedidos/incidencia-en-la-entrega-de-un-pedido"
            ],
            categories.Select(c => c.Id));

        var entrega = categories[0];
        Assert.Equal("Incidencia en la entrega de un pedido", entrega.Name);
        Assert.Equal("incidencia-en-la-entrega-de-un-pedido", entrega.Slug);
        Assert.Equal(new("docentes", "Soporte para profesores"), entrega.Level1);
        Assert.Equal(new("mis-pedidos", "Mis pedidos"), entrega.Level2);
        Assert.Equal("Soporte para profesores > Mis pedidos > Incidencia en la entrega de un pedido", entrega.FullName);
        Assert.Equal(("Origen", "Espanol", 7), (entrega.Branch, entrega.Language, entrega.SourceRow));
        Assert.Equal(["Profesores", "Grupo SM"], entrega.Communities);
        Assert.Empty(categories[1].Parameters);
        Assert.Empty(categories[2].Parameters);
    }

    [Fact]
    public async Task MapsFieldsToParameters_WithSlugKeysAndEnumOptions()
    {
        using var repository = CreateRepository(Tree);

        var parameters = (await repository.GetAllAsync(Ct))[0].Parameters;

        Assert.Equal(["numero-de-pedido", "como-realizaste-tu-pedido", "telefono"], parameters.Select(p => p.Name));
        Assert.Equal([true, true, false], parameters.Select(p => p.IsRequired));

        Assert.Equal("Número de pedido", parameters[0].Label);
        Assert.Equal(ParameterType.String, parameters[0].Type);

        Assert.Equal("¿Cómo realizaste tu pedido?", parameters[1].Label);
        Assert.Equal(ParameterType.Enum, parameters[1].Type);
        Assert.Equal(["Web", "Asesor", "Otros"], parameters[1].AllowedValues);
    }

    [Theory]
    [InlineData("Código postal (sin espacios)")]
    [InlineData("Licencia")]
    public async Task ParenthesesWithoutOptions_AreNotEnum(string field)
    {
        using var repository = CreateRepository(SingleLeaf($$"""[ { "nombre": "{{field}}", "obligatorio": true } ]"""));

        var parameter = Assert.Single((await repository.GetAllAsync(Ct))[0].Parameters);

        Assert.Equal(ParameterType.String, parameter.Type);
        Assert.Equal(field, parameter.Label);
    }

    [Fact]
    public async Task GetByIdAsync_IsCaseInsensitive_AndReturnsNullWhenMissing()
    {
        using var repository = CreateRepository(Tree);

        Assert.NotNull(await repository.GetByIdAsync(" DOCENTES/mis-pedidos/cambiar-mi-usuario ", Ct));
        Assert.Null(await repository.GetByIdAsync("docentes/mis-pedidos", Ct));
        Assert.Null(await repository.GetByIdAsync("", Ct));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("""[ { "nivel": 1, "nombre": "Sin hijos", "slug": "x" } ]""")]
    public async Task ReturnsEmptyList_WhenTreeHasNoLevel3(string json)
    {
        using var repository = CreateRepository(json);

        Assert.Empty(await repository.GetAllAsync(Ct));
    }

    [Fact]
    public async Task GeneratesSlugFromName_WhenUpperLevelHasNoSlug()
    {
        using var repository = CreateRepository("""
            [ { "nombre": "Protección de datos", "hijos": [ { "nombre": "Consultas", "slug": "", "hijos": [ { "nombre": "Baja" } ] } ] } ]
            """);

        Assert.Equal("proteccion-de-datos/consultas/baja", Assert.Single(await repository.GetAllAsync(Ct)).Id);
    }

    [Fact]
    public async Task CachesContent_AfterFirstLoad()
    {
        using var repository = CreateRepository(Tree);
        await repository.GetAllAsync(Ct);

        File.WriteAllText(Path.Combine(_directory, "categories.json"), "corrupto");

        Assert.Equal(3, (await repository.GetAllAsync(Ct)).Count);
    }

    [Fact]
    public async Task Throws_WhenFileDoesNotExist()
    {
        using var repository = CreateRepository(null, "no-existe.json");

        var ex = await Assert.ThrowsAsync<CategoryConfigurationException>(() => repository.GetAllAsync(Ct));
        Assert.Contains("no-existe.json", ex.Message);
    }

    [Theory]
    [InlineData("esto no es json")]
    [InlineData("""{ "categories": [] }""")]
    [InlineData("""[ { "nombre": "A", "hijos": [ ] ]""")]
    public async Task Throws_WhenJsonIsInvalid(string json)
    {
        using var repository = CreateRepository(json);

        await Assert.ThrowsAsync<CategoryConfigurationException>(() => repository.GetAllAsync(Ct));
    }

    [Theory]
    [InlineData("""[ { "nombre": "A", "hijos": [ { "nombre": "B", "hijos": [ { "nombre": "Baja" }, { "nombre": "BAJA" } ] } ] } ]""", "duplicados")]
    [InlineData("""[ { "nombre": "A", "hijos": [ { "nombre": "B", "hijos": [ { "nombre": "C", "campos": [ { "nombre": "Email" }, { "nombre": "email" } ] } ] } ] } ]""", "duplicados")]
    [InlineData("""[ { "nombre": "A", "hijos": [ { "nombre": "B", "hijos": [ { "nombre": " " } ] } ] } ]""", "sin 'nombre'")]
    [InlineData("""[ { "nombre": "A", "hijos": [ { "nombre": "B", "hijos": [ { "nombre": "¿?" } ] } ] } ]""", "sin 'nombre'")]
    [InlineData("""[ { "slug": "a", "hijos": [] } ]""", "nivel 1 sin 'nombre'")]
    [InlineData("""[ { "nombre": "A", "hijos": [ { "nombre": "B", "hijos": [ { "nombre": "C", "campos": [ { "nombre": "" } ] } ] } ] } ]""", "campo sin nombre")]
    [InlineData("""[ { "nombre": "A", "hijos": [ { "nivel": 3, "nombre": "B" } ] } ]""", "nivel 3")]
    public async Task Throws_WhenConfigurationIsInconsistent(string json, string expectedMessage)
    {
        using var repository = CreateRepository(json);

        var ex = await Assert.ThrowsAsync<CategoryConfigurationException>(() => repository.GetAllAsync(Ct));
        Assert.Contains(expectedMessage, ex.Message);
    }

    [Fact]
    public async Task LoadsSampleFileShippedWithApi()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "categories_tree.json");
        using var repository = new JsonCategoryRepository(Microsoft.Extensions.Options.Options.Create(new CategoryRepositoryOptions { FilePath = path }));

        var categories = await repository.GetAllAsync(Ct);

        Assert.Equal(54, categories.Count);
        Assert.All(categories, c => Assert.Equal(3, c.Id.Split('/').Length));
        Assert.All(categories, c => Assert.NotEmpty(c.RequiredParameters));
        Assert.Contains(categories, c => c.Parameters.Any(p => p.Type == ParameterType.Enum));
    }

    private static string SingleLeaf(string fields) =>
        $$"""[ { "nombre": "A", "hijos": [ { "nombre": "B", "hijos": [ { "nombre": "C", "campos": {{fields}} } ] } ] } ]""";
}
