using SM.ChatRouter.Infrastructure.Repositories;

namespace SM.ChatRouter.Tests.Infrastructure;

public sealed class SlugGeneratorTests
{
    [Theory]
    [InlineData("Incidencia en la entrega de un pedido", "incidencia-en-la-entrega-de-un-pedido")]
    [InlineData("¿Cómo realizaste tu pedido?", "como-realizaste-tu-pedido")]
    [InlineData("  Activación de Licencias  ", "activacion-de-licencias")]
    [InlineData("Botiga Cruïlla", "botiga-cruilla")]
    [InlineData("Año / Niño -- SMA+", "ano-nino-sma")]
    [InlineData("SMA", "sma")]
    [InlineData("99", "99")]
    [InlineData("¿?", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Generate_NormalizesText(string? text, string expected)
    {
        Assert.Equal(expected, SlugGenerator.Generate(text));
    }
}
