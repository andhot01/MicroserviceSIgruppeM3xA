using Microsoft.AspNetCore.Mvc;
using src.Application.Interfaces;
using src.Shared;

namespace src.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _service;

    public NotificationsController(INotificationService service)
    {
        _service = service;
    }

    [HttpGet]
    public ActionResult<IEnumerable<NotificationDto>> GetAll()
    {
        var notifications = _service.GetAll()
            .Select(ToDto);

        return Ok(notifications);
    }

    [HttpGet("{id:guid}")]
    public ActionResult<NotificationDto> GetById(Guid id)
    {
        var notification = _service.GetById(id);

        if (notification is null)
            return NotFound();

        return Ok(ToDto(notification));
    }

    [HttpPost]
    public ActionResult<NotificationDto> Create(
        CreateNotificationRequest request)
    {
        var notification = _service.Create(
            request.RecipientId,
            request.Title,
            request.Message,
            request.Type);

        var dto = ToDto(notification);

        return CreatedAtAction(
            nameof(GetById),
            new { id = notification.Id },
            dto);
    }

    private static NotificationDto ToDto(
        Domain.Entities.Notification notification)
    {
        return new NotificationDto(
            notification.Id,
            notification.RecipientId,
            notification.Content.Title,
            notification.Content.Message,
            notification.Type.ToString(),
            notification.Status.ToString(),
            notification.CreatedAt,
            notification.ReadAt);
    }

    [HttpPut("{id:guid}")]
    public IActionResult Update(
        Guid id,
        UpdateNotificationRequest request)
    {
        if (!request.IsRead)
            return BadRequest("A notification can only be marked as read.");

        var updated = _service.MarkAsRead(id);

        if (!updated)
            return NotFound();

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        var deleted = _service.Delete(id);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
}