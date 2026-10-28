using SM.ChatRouter.Application.Services;
using SM.ChatRouter.Domain.Entities;
using SM.ChatRouter.Domain.Enums;

namespace SM.ChatRouter.Tests.Application;

public sealed class ClarificationQuestionBuilderTests
{
    private static string Ask(params CategoryParameter[] missing) =>
        ClarificationQuestionBuilder.Build(missing, [], null, "¿Más detalles?");

    [Theory]
    [InlineData("Fecha del pedido", "¿Podrías indicarme fecha del pedido?")]
    [InlineData("DNI o NIE", "¿Podrías indicarme DNI o NIE?")]
    [InlineData("albarán o factura", "¿Podrías indicarme albarán o factura?")]
    public void UsesLabel_LowercasingFirstLetterExceptAcronyms(string label, string expected)
    {
        Assert.Equal(expected, Ask(new CategoryParameter { Name = "p", Label = label }));
    }

    [Fact]
    public void UsesLabelAsIs_WhenItIsAlreadyAQuestion()
    {
        var parameter = new CategoryParameter
        {
            Name = "como-realizaste-tu-pedido",
            Label = "¿Cómo realizaste tu pedido?",
            Type = ParameterType.Enum,
            AllowedValues = ["Web", "Asesor", "Otros"]
        };

        Assert.Equal("¿Cómo realizaste tu pedido? (Web, Asesor, Otros)", Ask(parameter));
    }

    [Fact]
    public void FallsBackToKey_WhenLabelIsEmpty()
    {
        Assert.Equal("¿Podrías indicarme email?", Ask(new CategoryParameter { Name = "email" }));
    }
}
