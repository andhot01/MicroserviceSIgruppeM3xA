using Shared.Contracts.Events;
using src.Messaging;

namespace src.Application.Handlers;

public class MessagePostedSubscription : BackgroundService
{
    private readonly IMessageHandler<MessagePostedEvent> _handler;
    private readonly IMessageClient _messageClient;

    public MessagePostedSubscription(
        IMessageHandler<MessagePostedEvent> handler,
        IMessageClient messageClient)
    {
        _handler = handler;
        _messageClient = messageClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _messageClient.SubscribeAsync<MessagePostedEvent>(
            "notification-service",
            message => _handler.HandleAsync(message, stoppingToken),
            stoppingToken);
    }
}