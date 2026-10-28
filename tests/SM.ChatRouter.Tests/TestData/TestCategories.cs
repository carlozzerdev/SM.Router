using SM.ChatRouter.Domain.Entities;
using SM.ChatRouter.Domain.Enums;

namespace SM.ChatRouter.Tests.TestData;

internal static class TestCategories
{
    public static Category Incidencia { get; } = new()
    {
        Id = "incidencia_tecnica",
        Name = "Incidencia técnica",
        Parameters =
        [
            new CategoryParameter { Name = "producto", Label = "El producto afectado", IsRequired = true, Question = "¿Qué producto falla?" },
            new CategoryParameter { Name = "prioridad", Type = ParameterType.Enum, AllowedValues = ["baja", "media", "alta"] }
        ]
    };

    public static Category Vacaciones { get; } = new()
    {
        Id = "solicitud_vacaciones",
        Name = "Solicitud de vacaciones",
        Parameters =
        [
            new CategoryParameter { Name = "fechaInicio", Label = "La fecha de inicio", Type = ParameterType.Date, IsRequired = true },
            new CategoryParameter { Name = "fechaFin", Label = "La fecha de fin", Type = ParameterType.Date, IsRequired = true },
            new CategoryParameter { Name = "comentario" }
        ]
    };

    public static Category SinParametros { get; } = new()
    {
        Id = "saludo",
        Name = "Saludo"
    };

    public static Category General { get; } = new()
    {
        Id = "general",
        Name = "Consulta general"
    };

    public static IReadOnlyList<Category> All { get; } = [Incidencia, Vacaciones, SinParametros, General];
}
