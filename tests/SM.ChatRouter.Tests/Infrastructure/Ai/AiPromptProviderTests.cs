using SM.ChatRouter.Infrastructure.Ai;

namespace SM.ChatRouter.Tests.Infrastructure.Ai;

public sealed class AiPromptProviderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "SM.ChatRouter.Tests", Guid.NewGuid().ToString("N"));

    public AiPromptProviderTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static FileAiPromptProvider CreateProvider(string? path) =>
        new(Microsoft.Extensions.Options.Options.Create(new AiOptions { SystemPromptPath = path }));

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void UsesDefaultPrompt_WhenNoPathConfigured(string? path)
    {
        Assert.Equal(AiPromptTemplates.DefaultSystemPrompt, CreateProvider(path).SystemPrompt);
    }

    [Fact]
    public void ReadsPromptFromFile_Trimmed()
    {
        var path = Path.Combine(_directory, "prompt.md");
        File.WriteAllText(path, "\n  Eres un clasificador.\n\n");

        Assert.Equal("Eres un clasificador.", CreateProvider(path).SystemPrompt);
    }

    [Fact]
    public void ReadsFileOnlyOnce()
    {
        var path = Path.Combine(_directory, "prompt.md");
        File.WriteAllText(path, "v1");
        var provider = CreateProvider(path);
        _ = provider.SystemPrompt;

        File.WriteAllText(path, "v2");

        Assert.Equal("v1", provider.SystemPrompt);
    }

    [Fact]
    public void Throws_WhenFileIsMissingOrEmpty()
    {
        var empty = Path.Combine(_directory, "vacio.md");
        File.WriteAllText(empty, "  \n");

        Assert.Contains("no-existe.md", Assert.Throws<InvalidOperationException>(() => CreateProvider(Path.Combine(_directory, "no-existe.md")).SystemPrompt).Message);
        Assert.Contains("vacío", Assert.Throws<InvalidOperationException>(() => CreateProvider(empty).SystemPrompt).Message);
    }

    [Fact]
    public void Options_AreInvalid_WhenConfiguredFileDoesNotExist()
    {
        Assert.True(new AiOptions().IsValid());
        Assert.False(new AiOptions { SystemPromptPath = Path.Combine(_directory, "no-existe.md") }.IsValid());
        Assert.False(new AiOptions { Provider = "" }.IsValid());
    }

    [Fact]
    public void ShippedPromptFile_IsCopiedWithApi()
    {
        var provider = CreateProvider(Path.Combine(AppContext.BaseDirectory, "Prompts", "routing-system.md"));

        Assert.Contains("nivel 3", provider.SystemPrompt);
    }
}
