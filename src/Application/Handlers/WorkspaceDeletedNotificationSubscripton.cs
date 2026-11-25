using Shared.Contracts.WorkspaceDeletion;
using src.Messaging;

namespace src.Application.Handlers;

public class WorkspaceDeletedNotificationSubscription
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMessageClient _messageClient;

    public WorkspaceDeletedNotificationSubscription(
        IServiceScopeFactory scopeFactory,
        IMessageClient messageClient)
    {
        _scopeFactory = scopeFactory;
        _messageClient = messageClient;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await _messageClient
            .SubscribeAsync<SendWorkspaceDeletedNotification>(
                "notification-service",
                async message =>
                {
                    using var scope = _scopeFactory.CreateScope();

                    var handler = scope.ServiceProvider
                        .GetRequiredService<
                            IMessageHandler<
                                SendWorkspaceDeletedNotification>>();

                    await handler.HandleAsync(
                        message, stoppingToken);
                },
                stoppingToken);
    }
}