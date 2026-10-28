using SM.ChatRouter.Domain.Entities;
using SM.ChatRouter.Domain.Enums;
using SM.ChatRouter.Tests.TestData;

namespace SM.ChatRouter.Tests.Domain;

public sealed class ConversationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_RejectsInvalidId(string? id)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Conversation(id!));
    }

    [Fact]
    public void AddUserMessage_RejectsEmptyContent()
    {
        var conversation = new Conversation("c1");

        Assert.ThrowsAny<ArgumentException>(() => conversation.AddUserMessage(" "));
    }

    [Fact]
    public void MergeParameters_KeepsDefinedValidValues_AndReportsInvalidOnes()
    {
        var conversation = new Conversation("c1");

        var rejected = conversation.MergeParameters(TestCategories.Incidencia, new Dictionary<string, string?>
        {
            ["Producto"] = " Teams ",
            ["prioridad"] = "máxima",
            ["desconocido"] = "x",
            ["otro"] = null
        });

        Assert.Equal("Teams", conversation.CollectedParameters["producto"]);
        Assert.False(conversation.CollectedParameters.ContainsKey("prioridad"));
        Assert.False(conversation.CollectedParameters.ContainsKey("desconocido"));
        Assert.Equal(["prioridad"], rejected);
    }

    [Fact]
    public void MergeParameters_OverwritesPreviousValue()
    {
        var conversation = new Conversation("c1");
        conversation.MergeParameters(TestCategories.Incidencia, new Dictionary<string, string?> { ["producto"] = "Teams" });

        conversation.MergeParameters(TestCategories.Incidencia, new Dictionary<string, string?> { ["producto"] = "Outlook" });

        Assert.Equal("Outlook", conversation.CollectedParameters["producto"]);
    }

    [Fact]
    public void MergeParameters_DoesNotOverwriteWithEmptyValue()
    {
        var conversation = new Conversation("c1");
        conversation.MergeParameters(TestCategories.Incidencia, new Dictionary<string, string?> { ["producto"] = "Teams" });

        conversation.MergeParameters(TestCategories.Incidencia, new Dictionary<string, string?> { ["producto"] = "" });

        Assert.Equal("Teams", conversation.CollectedParameters["producto"]);
    }

    [Fact]
    public void SetCandidate_DropsParametersNotDefinedByNewCategory()
    {
        var conversation = new Conversation("c1");
        conversation.SetCandidate(TestCategories.Vacaciones);
        conversation.MergeParameters(TestCategories.Vacaciones, new Dictionary<string, string?> { ["fechaInicio"] = "2026-08-01" });

        conversation.SetCandidate(TestCategories.Incidencia);

        Assert.Equal("incidencia_tecnica", conversation.CandidateCategoryId);
        Assert.Empty(conversation.CollectedParameters);
    }

    [Fact]
    public void Reset_ClearsRoutingState()
    {
        var conversation = new Conversation("c1");
        conversation.AddUserMessage("hola");
        conversation.SetCandidate(TestCategories.Incidencia);
        conversation.MergeParameters(TestCategories.Incidencia, new Dictionary<string, string?> { ["producto"] = "Teams" });
        conversation.MarkAwaitingInformation();
        conversation.MarkRouted(TestCategories.Incidencia);

        conversation.Reset();

        Assert.Equal("c1", conversation.Id);
        Assert.Empty(conversation.Messages);
        Assert.Empty(conversation.CollectedParameters);
        Assert.Null(conversation.CandidateCategoryId);
        Assert.Null(conversation.RoutedCategoryId);
        Assert.Equal(0, conversation.ClarificationTurns);
        Assert.Equal(ConversationStatus.Open, conversation.Status);
        Assert.False(conversation.IsClosed);
    }

    [Fact]
    public void MarkRoutedAndFallback_CloseConversation()
    {
        var routed = new Conversation("a");
        routed.MarkRouted(TestCategories.Incidencia);
        var fallback = new Conversation("b");
        fallback.MarkFallback(TestCategories.General);

        Assert.True(routed.IsClosed);
        Assert.Equal(ConversationStatus.Routed, routed.Status);
        Assert.True(fallback.IsClosed);
        Assert.Equal(ConversationStatus.FallbackRouted, fallback.Status);
    }
}
