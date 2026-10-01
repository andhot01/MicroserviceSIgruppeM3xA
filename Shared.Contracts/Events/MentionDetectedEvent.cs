namespace Shared.Contracts.Events;

public class MentionDetectedEvent
{
    public Guid MessageId { get; set; }
    public Guid MentionedUserId { get; set; }
}