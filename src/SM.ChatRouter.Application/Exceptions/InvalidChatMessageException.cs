namespace SM.ChatRouter.Application.Exceptions;

public sealed class InvalidChatMessageException(string message) : Exception(message);
