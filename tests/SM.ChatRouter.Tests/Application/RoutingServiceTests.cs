using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SM.ChatRouter.Application.Abstractions;
using SM.ChatRouter.Application.Ai;
using SM.ChatRouter.Application.Exceptions;
using SM.ChatRouter.Application.Options;
using SM.ChatRouter.Application.Services;
using SM.ChatRouter.Domain.Entities;
using SM.ChatRouter.Domain.Enums;
using SM.ChatRouter.Infrastructure.Repositories;
using SM.ChatRouter.Tests.TestData;

namespace SM.ChatRouter.Tests.Application;

public sealed class RoutingServiceTests
{
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly InMemoryConversationRepository _conversations = new();
    private readonly IAiRoutingClient _ai = Substitute.For<IAiRoutingClient>();
    private readonly RoutingOptions _options = new() { AiTimeout = TimeSpan.FromSeconds(5) };

    public RoutingServiceTests()
    {
        _categories.GetAllAsync(Arg.Any<CancellationToken>()).Returns(TestCategories.All);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoutingService CreateSut()
    {
        var options = Microsoft.Extensions.Options.Options.Create(_options);
        return new RoutingService(
            _categories,
            _conversations,
            _ai,
            new FallbackCategoryProvider(_categories, options),
            options,
            TimeProvider.System,
            NullLogger<RoutingService>.Instance);
    }

    private void AiReturns(params AiRoutingResponse[] responses) =>
        _ai.RouteAsync(Arg.Any<AiRoutingRequest>(), Arg.Any<CancellationToken>())
            .Returns(responses[0], responses[1..]);

    // ---------- Casos normales ----------

    [Fact]
    public async Task Routes_WhenModelSelectsCategoryWithAllRequiredParameters()
    {
        AiReturns(Ai.Selected("incidencia_tecnica", 0.95, ("producto", "Outlook"), ("prioridad", "ALTA")));

        var result = await CreateSut().ProcessMessageAsync(null, "Outlook no abre, es urgente", Ct);

        Assert.Equal(RoutingOutcome.Routed, result.Outcome);
        Assert.Equal("incidencia_tecnica", result.Category!.Id);
        Assert.Equal("Outlook", result.Parameters["producto"]);
        Assert.Equal("alta", result.Parameters["prioridad"]);
        Assert.Empty(result.MissingParameters);
        Assert.Null(result.FallbackReason);
    }

    [Fact]
    public async Task AsksForMissingRequiredParameter_UsingParameterQuestion()
    {
        AiReturns(Ai.NeedsInfo("incidencia_tecnica"));

        var result = await CreateSut().ProcessMessageAsync(null, "Tengo un problema técnico", Ct);

        Assert.Equal(RoutingOutcome.NeedsMoreInformation, result.Outcome);
        Assert.Equal("incidencia_tecnica", result.Category!.Id);
        Assert.Equal(["producto"], result.MissingParameters);
        Assert.Equal("¿Qué producto falla?", result.Question);
    }

    [Fact]
    public async Task UsesModelFollowUpQuestion_WhenProvided()
    {
        AiReturns(Ai.NeedsInfo("incidencia_tecnica", "¿Qué aplicación te falla?"));

        var result = await CreateSut().ProcessMessageAsync(null, "Algo no va", Ct);

        Assert.Equal("¿Qué aplicación te falla?", result.Question);
    }

    [Fact]
    public async Task CompletesParametersAcrossTurns_AndRoutes()
    {
        AiReturns(
            Ai.NeedsInfo("solicitud_vacaciones", null, ("fechaInicio", "2026-08-01")),
            Ai.Selected("solicitud_vacaciones", 0.9, ("fechaFin", "2026-08-15")));
        var sut = CreateSut();

        var first = await sut.ProcessMessageAsync(null, "Quiero vacaciones desde el 1 de agosto", Ct);
        var second = await sut.ProcessMessageAsync(first.ConversationId, "Hasta el 15", Ct);

        Assert.Equal(RoutingOutcome.NeedsMoreInformation, first.Outcome);
        Assert.Equal(["fechaFin"], first.MissingParameters);
        Assert.Equal(RoutingOutcome.Routed, second.Outcome);
        Assert.Equal(first.ConversationId, second.ConversationId);
        Assert.Equal("2026-08-01", second.Parameters["fechaInicio"]);
        Assert.Equal("2026-08-15", second.Parameters["fechaFin"]);
    }

    [Fact]
    public async Task SendsHistoryCandidateAndCollectedParametersToModel_OnSecondTurn()
    {
        AiReturns(
            Ai.NeedsInfo("solicitud_vacaciones", null, ("fechaInicio", "2026-08-01")),
            Ai.Selected("solicitud_vacaciones", 0.9, ("fechaFin", "2026-08-15")));
        var sut = CreateSut();

        var first = await sut.ProcessMessageAsync(null, "Vacaciones desde el 1 de agosto", Ct);
        await sut.ProcessMessageAsync(first.ConversationId, "Hasta el 15", Ct);

        await _ai.Received(1).RouteAsync(
            Arg.Is<AiRoutingRequest>(r =>
                r.UserPrompt == "Hasta el 15"
                && r.CandidateCategoryId == "solicitud_vacaciones"
                && r.CollectedParameters["fechaInicio"] == "2026-08-01"
                && r.History.Count == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OptionalParametersDoNotBlockRouting()
    {
        AiReturns(Ai.Selected("incidencia_tecnica", 0.9, ("producto", "VPN")));

        var result = await CreateSut().ProcessMessageAsync(null, "Falla la VPN", Ct);

        Assert.Equal(RoutingOutcome.Routed, result.Outcome);
        Assert.False(result.Parameters.ContainsKey("prioridad"));
    }

    [Fact]
    public async Task RoutesCategoryWithoutParameters()
    {
        AiReturns(Ai.Selected("saludo"));

        var result = await CreateSut().ProcessMessageAsync(null, "Hola", Ct);

        Assert.Equal(RoutingOutcome.Routed, result.Outcome);
        Assert.Empty(result.Parameters);
    }

    [Fact]
    public async Task ModelCategoriesExcludeFallbackCategory()
    {
        AiReturns(Ai.Selected("saludo"));

        await CreateSut().ProcessMessageAsync(null, "Hola", Ct);

        await _ai.Received(1).RouteAsync(
            Arg.Is<AiRoutingRequest>(r => r.Categories.All(c => c.Id != "general") && r.Categories.Count == 3),
            Arg.Any<CancellationToken>());
    }

    // ---------- Validación de entrada ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task Throws_WhenMessageIsEmpty(string? message)
    {
        await Assert.ThrowsAsync<InvalidChatMessageException>(() => CreateSut().ProcessMessageAsync(null, message, Ct));
        Assert.Empty(_ai.ReceivedCalls());
    }

    [Fact]
    public async Task Throws_WhenMessageExceedsMaxLength()
    {
        _options.MaxPromptLength = 10;

        await Assert.ThrowsAsync<InvalidChatMessageException>(() =>
            CreateSut().ProcessMessageAsync(null, new string('a', 11), Ct));
    }

    [Fact]
    public async Task AcceptsMessageExactlyAtMaxLength_AfterTrimming()
    {
        _options.MaxPromptLength = 10;
        AiReturns(Ai.Selected("saludo"));

        var result = await CreateSut().ProcessMessageAsync(null, "  " + new string('a', 10) + "  ", Ct);

        Assert.Equal(RoutingOutcome.Routed, result.Outcome);
    }

    // ---------- Fallback ----------

    [Fact]
    public async Task FallsBack_WhenModelReturnsNoMatch()
    {
        AiReturns(Ai.NoMatch());

        var result = await CreateSut().ProcessMessageAsync(null, "¿Cuál es el sentido de la vida?", Ct);

        Assert.Equal(RoutingOutcome.Fallback, result.Outcome);
        Assert.Equal(FallbackReason.NoMatch, result.FallbackReason);
        Assert.Equal("general", result.Category!.Id);
    }

    [Theory]
    [InlineData("no_existe")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("general")]
    public async Task FallsBack_WhenModelSelectsUnknownOrFallbackCategory(string? categoryId)
    {
        AiReturns(new AiRoutingResponse { Decision = AiDecision.CategorySelected, CategoryId = categoryId, Confidence = 1 });

        var result = await CreateSut().ProcessMessageAsync(null, "Algo", Ct);

        Assert.Equal(RoutingOutcome.Fallback, result.Outcome);
        Assert.Equal(FallbackReason.UnknownCategory, result.FallbackReason);
    }

    [Fact]
    public async Task MatchesCategoryIdIgnoringCaseAndWhitespace()
    {
        AiReturns(Ai.Selected("  SALUDO "));

        var result = await CreateSut().ProcessMessageAsync(null, "Hola", Ct);

        Assert.Equal(RoutingOutcome.Routed, result.Outcome);
        Assert.Equal("saludo", result.Category!.Id);
    }

    [Theory]
    [InlineData(0.59, RoutingOutcome.Fallback)]
    [InlineData(0.6, RoutingOutcome.Routed)]
    [InlineData(0.0, RoutingOutcome.Fallback)]
    [InlineData(1.0, RoutingOutcome.Routed)]
    public async Task AppliesConfidenceThreshold(double confidence, RoutingOutcome expected)
    {
        AiReturns(Ai.Selected("saludo", confidence));

        var result = await CreateSut().ProcessMessageAsync(null, "Hola", Ct);

        Assert.Equal(expected, result.Outcome);
        if (expected == RoutingOutcome.Fallback)
        {
            Assert.Equal(FallbackReason.LowConfidence, result.FallbackReason);
        }
    }

    [Fact]
    public async Task FallsBack_WhenAiThrows()
    {
        _ai.RouteAsync(Arg.Any<AiRoutingRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("boom"));

        var result = await CreateSut().ProcessMessageAsync(null, "Hola", Ct);

        Assert.Equal(RoutingOutcome.Fallback, result.Outcome);
        Assert.Equal(FallbackReason.AiError, result.FallbackReason);
        Assert.Null(result.Explanation);
    }

    // ---------- Explicación del modelo ----------

    [Fact]
    public async Task PropagatesModelExplanation_WhenRouted()
    {
        AiReturns(Ai.Selected("saludo") with { Explanation = "  El usuario solo saluda.  " });

        var result = await CreateSut().ProcessMessageAsync(null, "Hola", Ct);

        Assert.Equal(RoutingOutcome.Routed, result.Outcome);
        Assert.Equal("El usuario solo saluda.", result.Explanation);
    }

    [Fact]
    public async Task PropagatesModelExplanation_WhenAskingForInformation()
    {
        AiReturns(Ai.NeedsInfo("incidencia_tecnica") with { Explanation = "Falta el producto." });

        var result = await CreateSut().ProcessMessageAsync(null, "Tengo un problema", Ct);

        Assert.Equal(RoutingOutcome.NeedsMoreInformation, result.Outcome);
        Assert.Equal("Falta el producto.", result.Explanation);
    }

    [Fact]
    public async Task PropagatesModelExplanation_WhenFallingBackOnModelDecision()
    {
        AiReturns(Ai.NoMatch() with { Explanation = "No es una consulta de SM." });

        var result = await CreateSut().ProcessMessageAsync(null, "¿Qué tiempo hace?", Ct);

        Assert.Equal(FallbackReason.NoMatch, result.FallbackReason);
        Assert.Equal("No es una consulta de SM.", result.Explanation);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExplanationIsNull_WhenModelReturnsBlank(string explanation)
    {
        AiReturns(Ai.Selected("saludo") with { Explanation = explanation });

        var result = await CreateSut().ProcessMessageAsync(null, "Hola", Ct);

        Assert.Null(result.Explanation);
    }

    [Fact]
    public async Task FallsBack_WhenAiIsNotImplemented()
    {
        _ai.RouteAsync(Arg.Any<AiRoutingRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new NotImplementedException());

        var result = await CreateSut().ProcessMessageAsync(null, "Hola", Ct);

        Assert.Equal(FallbackReason.AiError, result.FallbackReason);
    }

    [Fact]
    public async Task FallsBack_WhenAiReturnsNull()
    {
        _ai.RouteAsync(Arg.Any<AiRoutingRequest>(), Arg.Any<CancellationToken>()).Returns((AiRoutingResponse)null!);

        var result = await CreateSut().ProcessMessageAsync(null, "Hola", Ct);

        Assert.Equal(FallbackReason.AiError, result.FallbackReason);
    }

    [Fact]
    public async Task FallsBack_WhenAiExceedsTimeout()
    {
        _options.AiTimeout = TimeSpan.FromMilliseconds(50);
        _ai.RouteAsync(Arg.Any<AiRoutingRequest>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                await Task.Delay(Timeout.Infinite, ci.Arg<CancellationToken>());
                return Ai.NoMatch();
            });

        var result = await CreateSut().ProcessMessageAsync(null, "Hola", Ct);

        Assert.Equal(RoutingOutcome.Fallback, result.Outcome);
        Assert.Equal(FallbackReason.AiError, result.FallbackReason);
    }

    [Fact]
    public async Task PropagatesCancellation_WhenCallerCancels()
    {
        using var cts = new CancellationTokenSource();
        _ai.RouteAsync(Arg.Any<AiRoutingRequest>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                await cts.CancelAsync();
                await Task.Delay(Timeout.Infinite, ci.Arg<CancellationToken>());
                return Ai.NoMatch();
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateSut().ProcessMessageAsync(null, "Hola", cts.Token));
    }

    [Fact]
    public async Task FallsBack_WhenNoCategoriesAvailable()
    {
        _categories.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);

        var result = await CreateSut().ProcessMessageAsync(null, "Hola", Ct);

        Assert.Equal(FallbackReason.NoCategoriesAvailable, result.FallbackReason);
        Assert.Equal("general", result.Category!.Id);
        Assert.Empty(_ai.ReceivedCalls());
    }

    [Fact]
    public async Task FallsBack_WhenOnlyFallbackCategoryExists()
    {
        _categories.GetAllAsync(Arg.Any<CancellationToken>()).Returns([TestCategories.General]);

        var result = await CreateSut().ProcessMessageAsync(null, "Hola", Ct);

        Assert.Equal(FallbackReason.NoCategoriesAvailable, result.FallbackReason);
    }

    [Fact]
    public async Task UsesDefaultFallbackCategory_WhenConfiguredOneDoesNotExist()
    {
        _options.FallbackCategoryId = "otro";
        _options.FallbackCategoryName = "Otro";
        _categories.GetAllAsync(Arg.Any<CancellationToken>()).Returns([TestCategories.Incidencia]);
        AiReturns(Ai.NoMatch());

        var result = await CreateSut().ProcessMessageAsync(null, "Hola", Ct);

        Assert.Equal("otro", result.Category!.Id);
        Assert.Equal("Otro", result.Category.Name);
    }


    // ---------- Desconfianza en el modelo y parámetros ----------

    [Fact]
    public async Task AsksForMissingParameters_EvenIfModelClaimsCategorySelected()
    {
        AiReturns(Ai.Selected("solicitud_vacaciones", 0.99, ("fechaInicio", "2026-08-01")));

        var result = await CreateSut().ProcessMessageAsync(null, "Vacaciones el 1 de agosto", Ct);

        Assert.Equal(RoutingOutcome.NeedsMoreInformation, result.Outcome);
        Assert.Equal(["fechaFin"], result.MissingParameters);
        Assert.Equal("¿Podrías indicarme la fecha de fin?", result.Question);
    }

    [Fact]
    public async Task ListsAllMissingParameters_InQuestion()
    {
        AiReturns(Ai.NeedsInfo("solicitud_vacaciones"));

        var result = await CreateSut().ProcessMessageAsync(null, "Quiero vacaciones", Ct);

        Assert.Equal(["fechaInicio", "fechaFin"], result.MissingParameters);
        Assert.Contains("fecha de inicio", result.Question);
        Assert.Contains("fecha de fin", result.Question);
    }

    [Fact]
    public async Task IgnoresParametersNotDefinedByCategory()
    {
        AiReturns(Ai.Selected("incidencia_tecnica", 0.9, ("producto", "Teams"), ("inventado", "x")));

        var result = await CreateSut().ProcessMessageAsync(null, "Falla Teams", Ct);

        Assert.Equal(RoutingOutcome.Routed, result.Outcome);
        Assert.False(result.Parameters.ContainsKey("inventado"));
    }

    [Fact]
    public async Task MatchesParameterNamesIgnoringCase()
    {
        AiReturns(Ai.Selected("incidencia_tecnica", 0.9, ("PRODUCTO", "Teams")));

        var result = await CreateSut().ProcessMessageAsync(null, "Falla Teams", Ct);

        Assert.Equal(RoutingOutcome.Routed, result.Outcome);
        Assert.Equal("Teams", result.Parameters["producto"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task TreatsEmptyRequiredValuesAsMissing(string? value)
    {
        AiReturns(Ai.Selected("incidencia_tecnica", 0.9, ("producto", value)));

        var result = await CreateSut().ProcessMessageAsync(null, "Algo falla", Ct);

        Assert.Equal(RoutingOutcome.NeedsMoreInformation, result.Outcome);
        Assert.Equal(["producto"], result.MissingParameters);
    }

    [Fact]
    public async Task RejectsValueOutsideAllowedValues_AndExplainsIt()
    {
        AiReturns(Ai.NeedsInfo("incidencia_tecnica", null, ("prioridad", "urgentísima")));

        var result = await CreateSut().ProcessMessageAsync(null, "Es urgentísimo", Ct);

        Assert.False(result.Parameters.ContainsKey("prioridad"));
        Assert.Contains("'prioridad' no es válido", result.Question);
        Assert.Contains("baja, media, alta", result.Question);
    }

    [Fact]
    public async Task RejectsInvalidDate_AndAsksAgain()
    {
        AiReturns(Ai.Selected("solicitud_vacaciones", 0.9, ("fechaInicio", "mañana"), ("fechaFin", "2026-08-15")));

        var result = await CreateSut().ProcessMessageAsync(null, "Vacaciones mañana", Ct);

        Assert.Equal(RoutingOutcome.NeedsMoreInformation, result.Outcome);
        Assert.Equal(["fechaInicio"], result.MissingParameters);
    }

    [Fact]
    public async Task DropsParametersFromPreviousCandidate_WhenCategoryChanges()
    {
        AiReturns(
            Ai.NeedsInfo("solicitud_vacaciones", null, ("fechaInicio", "2026-08-01"), ("comentario", "verano")),
            Ai.NeedsInfo("incidencia_tecnica"));
        var sut = CreateSut();

        var first = await sut.ProcessMessageAsync(null, "Vacaciones", Ct);
        var second = await sut.ProcessMessageAsync(first.ConversationId, "Perdón, en realidad me falla el portátil", Ct);

        Assert.Equal("incidencia_tecnica", second.Category!.Id);
        Assert.Empty(second.Parameters);
    }

    [Fact]
    public async Task AsksGenericQuestion_WhenModelIsUnsureAboutCategory()
    {
        AiReturns(Ai.NeedsInfo(null));

        var result = await CreateSut().ProcessMessageAsync(null, "Necesito ayuda", Ct);

        Assert.Equal(RoutingOutcome.NeedsMoreInformation, result.Outcome);
        Assert.Null(result.Category);
        Assert.Equal(_options.DefaultFollowUpQuestion, result.Question);
    }

    [Fact]
    public async Task AsksModelQuestion_WhenNeedsInfoWithUnknownCategory()
    {
        AiReturns(Ai.NeedsInfo("no_existe", "¿Es un tema técnico o de RR.HH.?"));

        var result = await CreateSut().ProcessMessageAsync(null, "Necesito ayuda", Ct);

        Assert.Null(result.Category);
        Assert.Equal("¿Es un tema técnico o de RR.HH.?", result.Question);
    }

    [Fact]
    public async Task Routes_WhenModelNeedsInfoWithoutQuestionButRequiredAreComplete()
    {
        AiReturns(Ai.NeedsInfo("incidencia_tecnica", null, ("producto", "Teams")));

        var result = await CreateSut().ProcessMessageAsync(null, "Falla Teams", Ct);

        Assert.Equal(RoutingOutcome.Routed, result.Outcome);
    }

    [Fact]
    public async Task AsksOptionalInfo_WhenModelNeedsInfoWithQuestionAndRequiredAreComplete()
    {
        AiReturns(Ai.NeedsInfo("incidencia_tecnica", "¿Qué prioridad tiene?", ("producto", "Teams")));

        var result = await CreateSut().ProcessMessageAsync(null, "Falla Teams", Ct);

        Assert.Equal(RoutingOutcome.NeedsMoreInformation, result.Outcome);
        Assert.Empty(result.MissingParameters);
    }

    // ---------- Límite de aclaraciones ----------

    [Fact]
    public async Task FallsBack_WhenMaxClarificationTurnsReached()
    {
        _options.MaxClarificationTurns = 2;
        _ai.RouteAsync(Arg.Any<AiRoutingRequest>(), Arg.Any<CancellationToken>()).Returns(Ai.NeedsInfo("incidencia_tecnica"));
        var sut = CreateSut();

        var r1 = await sut.ProcessMessageAsync("c1", "Ayuda", Ct);
        var r2 = await sut.ProcessMessageAsync("c1", "No sé", Ct);
        var r3 = await sut.ProcessMessageAsync("c1", "Pues eso", Ct);

        Assert.Equal(RoutingOutcome.NeedsMoreInformation, r1.Outcome);
        Assert.Equal(RoutingOutcome.NeedsMoreInformation, r2.Outcome);
        Assert.Equal(RoutingOutcome.Fallback, r3.Outcome);
        Assert.Equal(FallbackReason.MaxClarificationsReached, r3.FallbackReason);
    }

    [Fact]
    public async Task FallsBackImmediately_WhenMaxClarificationTurnsIsZero()
    {
        _options.MaxClarificationTurns = 0;
        AiReturns(Ai.NeedsInfo("incidencia_tecnica"));

        var result = await CreateSut().ProcessMessageAsync(null, "Ayuda", Ct);

        Assert.Equal(FallbackReason.MaxClarificationsReached, result.FallbackReason);
    }

    // ---------- Ciclo de vida de la conversación ----------

    [Fact]
    public async Task CreatesConversationWithGivenId_WhenUnknown()
    {
        AiReturns(Ai.Selected("saludo"));

        var result = await CreateSut().ProcessMessageAsync("  mi-id  ", "Hola", Ct);

        Assert.Equal("mi-id", result.ConversationId);
        Assert.NotNull(await _conversations.GetAsync("mi-id", Ct));
    }

    [Fact]
    public async Task GeneratesConversationId_WhenNotProvided()
    {
        AiReturns(Ai.Selected("saludo"), Ai.Selected("saludo"));
        var sut = CreateSut();

        var a = await sut.ProcessMessageAsync(null, "Hola", Ct);
        var b = await sut.ProcessMessageAsync("", "Hola", Ct);

        Assert.False(string.IsNullOrWhiteSpace(a.ConversationId));
        Assert.NotEqual(a.ConversationId, b.ConversationId);
    }

    [Fact]
    public async Task StartsNewRouting_AfterConversationWasRouted()
    {
        AiReturns(
            Ai.Selected("incidencia_tecnica", 0.9, ("producto", "Teams")),
            Ai.NeedsInfo("solicitud_vacaciones"));
        var sut = CreateSut();

        var first = await sut.ProcessMessageAsync("c1", "Falla Teams", Ct);
        var second = await sut.ProcessMessageAsync("c1", "Ahora quiero vacaciones", Ct);

        Assert.Equal(RoutingOutcome.Routed, first.Outcome);
        Assert.Equal(RoutingOutcome.NeedsMoreInformation, second.Outcome);
        Assert.Empty(second.Parameters);

        var conversation = await _conversations.GetAsync("c1", Ct);
        Assert.Equal(ConversationStatus.AwaitingInformation, conversation!.Status);
        Assert.Equal(2, conversation.Messages.Count);
        Assert.Equal(1, conversation.ClarificationTurns);
    }

    [Fact]
    public async Task PersistsUserAndAssistantMessages()
    {
        AiReturns(Ai.NeedsInfo("incidencia_tecnica"));

        var result = await CreateSut().ProcessMessageAsync(null, "Ayuda", Ct);

        var conversation = await _conversations.GetAsync(result.ConversationId, Ct);
        Assert.Collection(conversation!.Messages,
            m => Assert.Equal((ChatRole.User, "Ayuda"), (m.Role, m.Content)),
            m => Assert.Equal((ChatRole.Assistant, result.Question), (m.Role, m.Content)));
    }

    [Fact]
    public async Task ResetConversation_RemovesIt()
    {
        AiReturns(Ai.Selected("saludo"));
        var sut = CreateSut();
        await sut.ProcessMessageAsync("c1", "Hola", Ct);

        Assert.True(await sut.ResetConversationAsync("c1", Ct));
        Assert.False(await sut.ResetConversationAsync("c1", Ct));
        Assert.Null(await _conversations.GetAsync("c1", Ct));
    }
}
