using src.Application.Interfaces;
using src.Domain.Entities;
using src.Domain.ValueObjects;

namespace src.Application.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;

    public NotificationService(INotificationRepository repository)
    {
        _repository = repository;
    }

    public IEnumerable<Notification> GetAll()
    {
        return _repository.GetAll();
    }

    public Notification? GetById(Guid id)
    {
        return _repository.GetById(id);
    }

    public Notification Create(
        Guid recipientId,
        string title,
        string message,
        NotificationType type)
    {
        var content = new NotificationContent(title, message);

        var notification = new Notification(
            recipientId,
            content,
            type);

        _repository.Add(notification);

        return notification;
    }

    public bool MarkAsRead(Guid id)
    {
        var notification = _repository.GetById(id);

        if (notification is null)
            return false;

        notification.MarkAsRead();
        _repository.Update(notification);

        return true;
    }

    public bool Delete(Guid id)
    {
        var notification = _repository.GetById(id);

        if (notification is null)
            return false;

        _repository.Delete(notification);

        return true;
    }

    public bool Update(
        Guid id,
        string title,
        string message,
        NotificationType type)
    {
        var notification = _repository.GetById(id);

        if (notification is null)
            return false;

        var content = new NotificationContent(title, message);

        notification.UpdateContent(content, type);

        _repository.Update(notification);

        return true;
    }
}