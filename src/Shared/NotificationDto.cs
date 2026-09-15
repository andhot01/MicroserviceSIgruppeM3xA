namespace src.Shared;

public record NotificationDto(
    Guid Id,
    Guid RecipientId,
    string Title,
    string Message,
    string Type,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt
);