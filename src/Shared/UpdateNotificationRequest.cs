using System.ComponentModel.DataAnnotations;
using src.Domain.Entities;

namespace src.Shared;

public class UpdateNotificationRequest
{
    public bool IsRead { get; set; }
}