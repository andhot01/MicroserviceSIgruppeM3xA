using Shared.Contracts.Events;
using src.Application.Interfaces;
using src.Messaging;

namespace src.Application.Handlers;

public class MessagePostedHandler : IMessageHandler<MessagePostedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly IMessageClient _messageClient;
    
    public MessagePostedHandler(INotificationService notificationService, IMessageClient messageClient)
    {
        _notificationService = notificationService;
        _messageClient = messageClient;
    }
    
    public async Task HandleAsync(
        MessagePostedEvent message,
        CancellationToken cancellationToken = default)
    {
        var mentionedUserId = message.Content
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.StartsWith('@'))
            .Select(word => word[1..])
            .Select(word => Guid.TryParse(word, out var id) ? id : (Guid?)null)
            .FirstOrDefault();

        if (mentionedUserId is null)
            return;

        _notificationService.Create(
            mentionedUserId.Value,
            "Du er blevet nævnt",
            "Du er blevet nævnt i en besked.",
            NotificationType.Message);
        
        await _messageClient.PublishAsync(
            new MentionDetectedEvent
            {
                MessageId = message.MessageId,
                MentionedUserId = mentionedUserId.Value
            },
            cancellationToken);
        
    }
    
}

