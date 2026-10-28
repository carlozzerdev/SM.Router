namespace SM.ChatRouter.Domain.Enums;

public enum ParameterType
{
    String,
    Number,
    Date,
    Boolean,
    Enum
}

public enum ChatRole
{
    User,
    Assistant
}

public enum ConversationStatus
{
    Open,
    AwaitingInformation,
    Routed,
    FallbackRouted
}

public enum RoutingOutcome
{
    /// <summary>Faltan parámetros obligatorios; se devuelve una pregunta al usuario.</summary>
    NeedsMoreInformation,

    /// <summary>Se ha seleccionado una categoría con toda la información obligatoria.</summary>
    Routed,

    /// <summary>No se ha podido determinar una categoría; se usa la categoría por defecto.</summary>
    Fallback
}

public enum FallbackReason
{
    NoMatch,
    UnknownCategory,
    LowConfidence,
    MaxClarificationsReached,
    AiError,
    NoCategoriesAvailable
}
