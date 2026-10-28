using Microsoft.Extensions.Options;
using SM.ChatRouter.Application.Abstractions;
using SM.ChatRouter.Application.Options;
using SM.ChatRouter.Domain.Entities;

namespace SM.ChatRouter.Application.Services;

/// <summary>
/// Resuelve la categoría de fallback: la del repositorio con <see cref="RoutingOptions.FallbackCategoryId"/>
/// si existe y, si no, una categoría virtual (fuera del árbol) con el id y nombre configurados.
/// </summary>
public sealed class FallbackCategoryProvider(ICategoryRepository categories, IOptions<RoutingOptions> options)
    : IFallbackCategoryProvider
{
    private readonly RoutingOptions _options = options.Value;

    public async Task<Category> GetFallbackAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Category> all;
        try
        {
            all = await categories.GetAllAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // El fallback no debe fallar nunca: si el repositorio no está disponible se usa el valor por defecto.
            return CreateDefault();
        }

        return all.FirstOrDefault(c => string.Equals(c.Id, _options.FallbackCategoryId, StringComparison.OrdinalIgnoreCase))
            ?? CreateDefault();
    }

    private Category CreateDefault() => new()
    {
        Id = _options.FallbackCategoryId,
        Name = _options.FallbackCategoryName
    };
}
