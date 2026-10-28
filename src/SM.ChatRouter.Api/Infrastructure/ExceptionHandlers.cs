using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SM.ChatRouter.Application.Exceptions;

namespace SM.ChatRouter.Api.Infrastructure;

/// <summary>Traduce los errores de validación del mensaje a un 400 ProblemDetails.</summary>
public sealed class InvalidChatMessageExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not InvalidChatMessageException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Mensaje no válido",
                Detail = exception.Message
            }
        });
    }
}
