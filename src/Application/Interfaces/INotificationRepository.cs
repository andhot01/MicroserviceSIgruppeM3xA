using src.Domain.Entities;

namespace src.Application.Interfaces;

public interface INotificationRepository
{
    IEnumerable<Notification> GetAll();
    Notification? GetById(Guid id);
    void Add(Notification notification);
    void Update(Notification notification);
    void Delete(Notification notification);
}