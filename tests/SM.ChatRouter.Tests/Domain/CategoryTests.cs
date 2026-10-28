using SM.ChatRouter.Domain.Entities;
using SM.ChatRouter.Domain.Enums;
using SM.ChatRouter.Tests.TestData;

namespace SM.ChatRouter.Tests.Domain;

public sealed class CategoryTests
{
    [Fact]
    public void GetMissingRequired_ReturnsAllRequired_WhenNothingCollected()
    {
        var missing = TestCategories.Vacaciones.GetMissingRequired(new Dictionary<string, string>());

        Assert.Equal(["fechaInicio", "fechaFin"], missing.Select(p => p.Name));
    }

    [Fact]
    public void GetMissingRequired_IgnoresOptionalAndTreatsBlankAsMissing()
    {
        var collected = new Dictionary<string, string> { ["fechaInicio"] = "2026-08-01", ["fechaFin"] = " " };

        var missing = TestCategories.Vacaciones.GetMissingRequired(collected);

        Assert.Equal(["fechaFin"], missing.Select(p => p.Name));
    }

    [Fact]
    public void GetMissingRequired_IsEmpty_ForCategoryWithoutParameters()
    {
        Assert.Empty(TestCategories.SinParametros.GetMissingRequired(new Dictionary<string, string>()));
    }

    [Fact]
    public void FindParameter_IsCaseInsensitive()
    {
        Assert.NotNull(TestCategories.Vacaciones.FindParameter("FECHAINICIO"));
        Assert.Null(TestCategories.Vacaciones.FindParameter("otro"));
    }

    [Theory]
    [InlineData(ParameterType.String, "  hola ", true, "hola")]
    [InlineData(ParameterType.Number, "12.50", true, "12.50")]
    [InlineData(ParameterType.Number, "doce", false, "")]
    [InlineData(ParameterType.Date, "2026-02-28", true, "2026-02-28")]
    [InlineData(ParameterType.Date, "2026-02-30", false, "")]
    [InlineData(ParameterType.Boolean, "TRUE", true, "true")]
    [InlineData(ParameterType.Boolean, "sí", false, "")]
    [InlineData(ParameterType.String, "", false, "")]
    [InlineData(ParameterType.String, null, false, "")]
    public void TryNormalize_ValidatesByType(ParameterType type, string? value, bool expectedOk, string expected)
    {
        var parameter = new CategoryParameter { Name = "p", Type = type };

        var ok = parameter.TryNormalize(value, out var normalized);

        Assert.Equal(expectedOk, ok);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("Media", true, "media")]
    [InlineData("urgente", false, "")]
    public void TryNormalize_Enum_UsesAllowedValues(string value, bool expectedOk, string expected)
    {
        var parameter = TestCategories.Incidencia.FindParameter("prioridad")!;

        Assert.Equal(expectedOk, parameter.TryNormalize(value, out var normalized));
        Assert.Equal(expected, normalized);
    }
}
