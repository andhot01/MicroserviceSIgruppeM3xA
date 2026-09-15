using src.Application.Interfaces;
using src.Domain.Entities;

namespace src.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly List<Notification> _notifications = [];

    public IEnumerable<Notification> GetAll()
    {
        return _notifications;
    }

    public Notification? GetById(Guid id)
    {
        return _notifications.FirstOrDefault(n => n.Id == id);
    }

    public void Add(Notification notification)
    {
        _notifications.Add(notification);
    }

    public void Update(Notification notification)
    {
        // The object is already tracked in our in-memory collection.
    }

    public void Delete(Notification notification)
    {
        _notifications.Remove(notification);
    }
}