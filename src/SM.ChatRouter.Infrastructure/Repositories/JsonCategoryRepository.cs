using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SM.ChatRouter.Application.Abstractions;
using SM.ChatRouter.Domain.Entities;
using SM.ChatRouter.Domain.Enums;

namespace SM.ChatRouter.Infrastructure.Repositories;

/// <summary>
/// Lee el árbol de categorías (N1 → N2 → N3) de un fichero JSON y lo aplana a las categorías seleccionables,
/// que son los N3. El contenido se carga una sola vez y se mantiene en memoria.
/// </summary>
public sealed partial class JsonCategoryRepository(IOptions<CategoryRepositoryOptions> options) : ICategoryRepository, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private IReadOnlyList<Category>? _cache;

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (_cache is not null)
        {
            return _cache;
        }

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            return _cache ??= await LoadAsync(cancellationToken);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public async Task<Category?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var all = await GetAllAsync(cancellationToken);
        return all.FirstOrDefault(c => string.Equals(c.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose() => _loadLock.Dispose();

    private async Task<IReadOnlyList<Category>> LoadAsync(CancellationToken cancellationToken)
    {
        var path = ResolvePath(options.Value.FilePath);
        if (!File.Exists(path))
        {
            throw new CategoryConfigurationException($"No se encuentra el fichero de categorías: '{path}'.");
        }

        List<CategoryLevel1Dto>? tree;
        try
        {
            await using var stream = File.OpenRead(path);
            tree = await JsonSerializer.DeserializeAsync<List<CategoryLevel1Dto>>(stream, SerializerOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new CategoryConfigurationException($"El fichero de categorías '{path}' no es un JSON válido: {ex.Message}", ex);
        }

        var categories = Flatten(tree ?? [], path);
        Validate(categories, path);
        return categories;
    }

    private static List<Category> Flatten(List<CategoryLevel1Dto> tree, string path)
    {
        var categories = new List<Category>();
        foreach (var level1 in tree)
        {
            var node1 = ToNode(level1.Name, level1.Slug, level1.Level, expectedLevel: 1, path);
            foreach (var level2 in level1.Children ?? [])
            {
                var node2 = ToNode(level2.Name, level2.Slug, level2.Level, expectedLevel: 2, path);
                foreach (var level3 in level2.Children ?? [])
                {
                    var name = level3.Name?.Trim();
                    var slug = SlugGenerator.Generate(name);
                    CheckLevel(level3.Level, expectedLevel: 3, name, path);
                    if (string.IsNullOrEmpty(name) || slug.Length == 0)
                    {
                        throw new CategoryConfigurationException(
                            $"Hay un nivel 3 sin 'nombre' válido en '{node1.Name} > {node2.Name}' ('{path}').");
                    }

                    categories.Add(new Category
                    {
                        Id = $"{node1.Slug}/{node2.Slug}/{slug}",
                        Name = name,
                        Slug = slug,
                        Level1 = node1,
                        Level2 = node2,
                        Branch = level1.Branch?.Trim(),
                        Language = level1.Language?.Trim(),
                        Communities = (level3.Communities ?? []).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList(),
                        AllCommunities = level3.AllCommunities,
                        SourceRow = level3.SourceRow,
                        Parameters = (level3.Fields ?? []).Select(f => ToParameter(f, name, path)).ToList()
                    });
                }
            }
        }
        return categories;
    }

    private static CategoryNode ToNode(string? name, string? slug, int level, int expectedLevel, string path)
    {
        var trimmedName = name?.Trim();
        CheckLevel(level, expectedLevel, trimmedName, path);
        if (string.IsNullOrEmpty(trimmedName))
        {
            throw new CategoryConfigurationException($"Hay un nivel {expectedLevel} sin 'nombre' en '{path}'.");
        }

        // Se respeta el slug del fichero, normalizado (minúsculas, sin tildes); si no viene, se genera del nombre.
        var normalizedSlug = SlugGenerator.Generate(string.IsNullOrWhiteSpace(slug) ? trimmedName : slug);
        if (normalizedSlug.Length == 0)
        {
            throw new CategoryConfigurationException($"No se puede generar el slug del nivel {expectedLevel} '{trimmedName}' en '{path}'.");
        }

        return new CategoryNode(normalizedSlug, trimmedName);
    }

    private static void CheckLevel(int level, int expectedLevel, string? name, string path)
    {
        if (level != 0 && level != expectedLevel)
        {
            throw new CategoryConfigurationException(
                $"'{name}' declara nivel {level} pero está en la posición de nivel {expectedLevel} en '{path}'.");
        }
    }

    /// <summary>
    /// Convierte un campo del formulario en parámetro. Si el nombre termina en una lista de opciones entre
    /// paréntesis separadas por '/', p. ej. "¿Cómo realizaste tu pedido? (Web / Asesor / Otros)", es un Enum.
    /// </summary>
    private static CategoryParameter ToParameter(CategoryFieldDto field, string categoryName, string path)
    {
        var text = field.Name?.Trim() ?? string.Empty;
        var label = text;
        IReadOnlyList<string> allowedValues = [];

        var match = OptionsPattern().Match(text);
        if (match.Success)
        {
            label = match.Groups["label"].Value.Trim();
            allowedValues = match.Groups["options"].Value
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        var key = SlugGenerator.Generate(label);
        if (key.Length == 0)
        {
            throw new CategoryConfigurationException($"La categoría '{categoryName}' tiene un campo sin nombre válido ('{path}').");
        }

        return new CategoryParameter
        {
            Name = key,
            Label = label,
            IsRequired = field.Required,
            Type = allowedValues.Count > 1 ? ParameterType.Enum : ParameterType.String,
            AllowedValues = allowedValues.Count > 1 ? allowedValues : []
        };
    }

    private static void Validate(IReadOnlyList<Category> categories, string path)
    {
        var duplicatedIds = categories
            .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicatedIds.Count > 0)
        {
            throw new CategoryConfigurationException(
                $"Ids de categoría duplicados en '{path}': {string.Join(", ", duplicatedIds)}.");
        }

        foreach (var category in categories)
        {
            var duplicatedParameters = category.Parameters
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicatedParameters.Count > 0)
            {
                throw new CategoryConfigurationException(
                    $"La categoría '{category.Id}' tiene campos duplicados: {string.Join(", ", duplicatedParameters)}.");
            }
        }
    }

    private static string ResolvePath(string filePath) =>
        Path.IsPathRooted(filePath) ? filePath : Path.Combine(AppContext.BaseDirectory, filePath);

    [GeneratedRegex(@"^(?<label>.*?)\s*\((?<options>[^()]*/[^()]*)\)\s*$")]
    private static partial Regex OptionsPattern();
}
