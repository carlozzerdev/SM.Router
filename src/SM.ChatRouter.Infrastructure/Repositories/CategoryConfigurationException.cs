namespace SM.ChatRouter.Infrastructure.Repositories;

public sealed class CategoryConfigurationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
