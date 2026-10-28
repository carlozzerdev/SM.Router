using SM.ChatRouter.Domain.Entities;
using SM.ChatRouter.Infrastructure.Repositories;

namespace SM.ChatRouter.Tests.Infrastructure;

public sealed class InMemoryConversationRepositoryTests
{
    private readonly InMemoryConversationRepository _repository = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveGetAndDelete()
    {
        var conversation = new Conversation("c1");

        await _repository.SaveAsync(conversation, Ct);

        Assert.Same(conversation, await _repository.GetAsync("c1", Ct));
        Assert.True(await _repository.DeleteAsync("c1", Ct));
        Assert.Null(await _repository.GetAsync("c1", Ct));
        Assert.False(await _repository.DeleteAsync("c1", Ct));
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_ForUnknownId()
    {
        Assert.Null(await _repository.GetAsync("desconocido", Ct));
    }

    [Fact]
    public async Task Ids_AreCaseSensitive()
    {
        await _repository.SaveAsync(new Conversation("Abc"), Ct);

        Assert.Null(await _repository.GetAsync("abc", Ct));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task RejectsInvalidIds(string? id)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.GetAsync(id!, Ct));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.DeleteAsync(id!, Ct));
    }

    [Fact]
    public async Task SupportsConcurrentWrites()
    {
        await Parallel.ForAsync(0, 500, Ct, async (i, ct) =>
            await _repository.SaveAsync(new Conversation($"c{i}"), ct));

        for (var i = 0; i < 500; i++)
        {
            Assert.NotNull(await _repository.GetAsync($"c{i}", Ct));
        }
    }
}
