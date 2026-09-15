using System.ComponentModel.DataAnnotations;
using src.Domain.Entities;

namespace src.Shared;

public class CreateNotificationRequest
{
    public Guid RecipientId { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Message { get; set; } = string.Empty;

    public NotificationType Type { get; set; }
}