using System.Net;

namespace SM.ChatRouter.Infrastructure.Ai.OpenAI;

public sealed class OpenAIException : Exception
{
    public OpenAIException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    public HttpStatusCode? StatusCode { get; init; }

    /// <summary>Campo <c>error.type</c> de la respuesta (p. ej. <c>invalid_request_error</c>).</summary>
    public string? ErrorType { get; init; }

    /// <summary>Campo <c>error.code</c> de la respuesta (p. ej. <c>rate_limit_exceeded</c>).</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Cabecera <c>x-request-id</c>, útil para soporte de OpenAI.</summary>
    public string? RequestId { get; init; }

    public string? ResponseId { get; init; }
}
