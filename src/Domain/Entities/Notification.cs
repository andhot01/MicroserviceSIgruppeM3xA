using src.Domain.ValueObjects;

namespace src.Domain.Entities;

public class Notification
{
    public Guid Id { get; private set; }
    public Guid RecipientId { get; private set; }
    public NotificationContent Content { get; private set; }
    public NotificationType Type { get; private set; }
    public NotificationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    public Notification(
        Guid recipientId,
        NotificationContent content,
        NotificationType type)
    {
        Id = Guid.NewGuid();
        RecipientId = recipientId;
        Content = content;
        Type = type;
        Status = NotificationStatus.Unread;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkAsRead()
    {
        if (Status == NotificationStatus.Read)
            return;

        Status = NotificationStatus.Read;
        ReadAt = DateTimeOffset.UtcNow;
    }

    public void UpdateContent(NotificationContent content, NotificationType type)
    {
        Content = content;
        Type = type;
    }
}