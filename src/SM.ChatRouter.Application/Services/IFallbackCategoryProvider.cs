using SM.ChatRouter.Domain.Entities;

namespace SM.ChatRouter.Application.Services;

public interface IFallbackCategoryProvider
{
    Task<Category> GetFallbackAsync(CancellationToken cancellationToken = default);
}
