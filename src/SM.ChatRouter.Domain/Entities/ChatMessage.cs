using SM.ChatRouter.Domain.Enums;

namespace SM.ChatRouter.Domain.Entities;

public sealed record ChatMessage(ChatRole Role, string Content, DateTimeOffset Timestamp);
