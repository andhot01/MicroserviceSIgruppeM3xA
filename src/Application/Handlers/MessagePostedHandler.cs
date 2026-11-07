
using Shared.Contracts.Events;
using src.Application.Interfaces;
using src.Messaging;

namespace src.Application.Handlers;

public class MessagePostedHandler : IMessageHandler<MessagePostedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly IMessageClient _messageClient;

    private readonly HashSet<Guid> _processedMessages = [];
    private readonly SemaphoreSlim _lock = new(1, 1);

    public MessagePostedHandler(
        INotificationService notificationService,
        IMessageClient messageClient)
    {
        _notificationService = notificationService;
        _messageClient = messageClient;
    }

    public async Task HandleAsync(
        MessagePostedEvent message,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            // Ignore messages that have already been processed
            if (_processedMessages.Contains(message.MessageId))
                return;

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

            // Mark as processed only after successful publishing
            _processedMessages.Add(message.MessageId);
        }
        finally
        {
            _lock.Release();
        }
    }
}
