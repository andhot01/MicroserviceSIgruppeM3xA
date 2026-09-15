using src.Domain.Entities;

namespace src.Application.Interfaces;

public interface INotificationService
{
    IEnumerable<Notification> GetAll();
    Notification? GetById(Guid id);

    Notification Create(
        Guid recipientId,
        string title,
        string message,
        NotificationType type);

    bool MarkAsRead(Guid id);
    bool Delete(Guid id);
    bool Update(
        Guid id,
        string title,
        string message,
        NotificationType type);
}