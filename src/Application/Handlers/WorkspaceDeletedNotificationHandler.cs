using Shared.Contracts.WorkspaceDeletion;
using src.Application.Interfaces;
using src.Messaging;

namespace src.Application.Handlers;

public class WorkspaceDeletedNotificationHandler :
    IMessageHandler<SendWorkspaceDeletedNotification>
{
    private readonly INotificationService _notificationService;
    private readonly IMessageClient _messageClient;

    public WorkspaceDeletedNotificationHandler(
        INotificationService notificationService,
        IMessageClient messageClient)
    {
        _notificationService = notificationService;
        _messageClient = messageClient;
    }

    public async Task HandleAsync(
        SendWorkspaceDeletedNotification message,
        CancellationToken ct = default)
    {
        var notifiedMemberIds = new List<Guid>();

        try
        {
            foreach (var memberId in message.MemberIds.Distinct())
            {
                _notificationService.Create(
                    memberId,
                    "Workspace deleted",
                    $"Workspace {message.WorkspaceId} has been deleted.",
                    NotificationType.General);

                notifiedMemberIds.Add(memberId);
            }
        }
        catch (Exception ex)
        {
            await _messageClient.PublishAsync(
                new NotificationFailedEvent
                {
                    SagaId = message.SagaId,
                    WorkspaceId = message.WorkspaceId,
                    Reason = ex.Message,
                    NotifiedMemberIds = notifiedMemberIds
                }, ct);

            return;
        }

        await _messageClient.PublishAsync(
            new NotificationsSentEvent
            {
                SagaId = message.SagaId,
                WorkspaceId = message.WorkspaceId
            }, ct);
    }
}