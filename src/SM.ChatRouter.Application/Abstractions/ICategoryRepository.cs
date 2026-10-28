using SM.ChatRouter.Domain.Entities;

namespace SM.ChatRouter.Application.Abstractions;

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Category?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
}
