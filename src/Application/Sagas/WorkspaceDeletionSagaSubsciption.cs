using Shared.Contracts.WorkspaceDeletion;
using src.Application.Handlers;
using src.Messaging;

namespace src.Application.Sagas;

public class WorkspaceDeletionSagaSubscription : BackgroundService
{
    private readonly WorkspaceDeletionSagaOrchestrator _orchestrator;
    private readonly IMessageClient _messageClient;

    private readonly SemaphoreSlim _lock = new(1, 1);

    public WorkspaceDeletionSagaSubscription(
        WorkspaceDeletionSagaOrchestrator orchestrator,
        IMessageClient messageClient)
    {
        _orchestrator = orchestrator;
        _messageClient = messageClient;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        // Happy path
        await Subscribe<InitiateWorkspaceDeletion>(stoppingToken);
        await Subscribe<MembersWarnedEvent>(stoppingToken);
        await Subscribe<CallsTerminatedEvent>(stoppingToken);
        await Subscribe<ChannelsArchivedEvent>(stoppingToken);
        await Subscribe<FilePurgeStartedEvent>(stoppingToken);
        await Subscribe<FilePurgeCompletedEvent>(stoppingToken);
        await Subscribe<NotificationsSentEvent>(stoppingToken);

        // Failure events
        await Subscribe<MemberWarningFailedEvent>(stoppingToken);
        await Subscribe<CallTerminationFailedEvent>(stoppingToken);
        await Subscribe<ChannelArchivingFailedEvent>(stoppingToken);
        await Subscribe<FilePurgeFailedEvent>(stoppingToken);
        await Subscribe<NotificationFailedEvent>(stoppingToken);

        // Compensation events
        await Subscribe<FilePurgeCancelledEvent>(stoppingToken);
        await Subscribe<FilePurgeCancellationFailedEvent>(stoppingToken);
        await Subscribe<ChannelsRestoredEvent>(stoppingToken);
        await Subscribe<ChannelRestorationFailedEvent>(stoppingToken);
        await Subscribe<CancellationNotifiedEvent>(stoppingToken);
        await Subscribe<CancellationNotificationFailedEvent>(stoppingToken);
    }

    private async Task Subscribe<T>(CancellationToken ct)
    {
        var handler = (IMessageHandler<T>)_orchestrator;

        await _messageClient.SubscribeAsync<T>(
            "workspace-deletion-saga",
            async message =>
            {
                await _lock.WaitAsync(ct);

                try
                {
                    await handler.HandleAsync(message, ct);
                }
                finally
                {
                    _lock.Release();
                }
            },
            ct);
    }
}