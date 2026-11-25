using Shared.Contracts.WorkspaceDeletion;
using src.Application.Handlers;
using src.Application.Interfaces;
using src.Messaging;

namespace src.Application.Sagas;

public class WorkspaceDeletionSagaOrchestrator :
    IMessageHandler<InitiateWorkspaceDeletion>,
    IMessageHandler<MembersWarnedEvent>,
    IMessageHandler<CallsTerminatedEvent>,
    IMessageHandler<ChannelsArchivedEvent>,
    IMessageHandler<FilePurgeStartedEvent>,
    IMessageHandler<FilePurgeCompletedEvent>,
    IMessageHandler<NotificationsSentEvent>,

    // Failure events
    IMessageHandler<MemberWarningFailedEvent>,
    IMessageHandler<CallTerminationFailedEvent>,
    IMessageHandler<ChannelArchivingFailedEvent>,
    IMessageHandler<FilePurgeFailedEvent>,
    IMessageHandler<NotificationFailedEvent>,

    // Compensation events
    IMessageHandler<FilePurgeCancelledEvent>,
    IMessageHandler<FilePurgeCancellationFailedEvent>,
    IMessageHandler<ChannelsRestoredEvent>,
    IMessageHandler<ChannelRestorationFailedEvent>,
    IMessageHandler<CancellationNotifiedEvent>,
    IMessageHandler<CancellationNotificationFailedEvent>
{
    private readonly IMessageClient _messageClient;
    private readonly ISagaStateRepository _sagaRepo;

    public WorkspaceDeletionSagaOrchestrator(
        IMessageClient messageClient,
        ISagaStateRepository sagaRepo)
    {
        _messageClient = messageClient;
        _sagaRepo = sagaRepo;
    }

    public async Task HandleAsync(
        InitiateWorkspaceDeletion message,
        CancellationToken ct = default)
    {
        if (message.SagaId == Guid.Empty)
            throw new ArgumentException("SagaId is required.");

        if (await _sagaRepo.GetById(message.SagaId) is not null)
            return;

        var state = new WorkspaceDeletionSagaState
        {
            SagaId = message.SagaId,
            WorkspaceId = message.WorkspaceId,
            AdminId = message.AdminId
        };

        await _sagaRepo.Save(state);

        await _messageClient.PublishAsync(
            new WarnWorkspaceMembers
            {
                SagaId = state.SagaId,
                WorkspaceId = state.WorkspaceId
            }, ct);
    }

    public async Task HandleAsync(
        MembersWarnedEvent message,
        CancellationToken ct = default)
    {
        var state = await _sagaRepo.GetById(message.SagaId);

        if (state is null || state.IsFailed || state.MembersWarned)
            return;

        state.MembersWarned = true;
        state.MemberIds = message.MemberIds;

        await _sagaRepo.Save(state);

        await Task.Delay(TimeSpan.FromSeconds(60), ct);

        if (state.IsFailed)
            return;

        await _messageClient.PublishAsync(
            new TerminateWorkspaceCalls
            {
                SagaId = state.SagaId,
                WorkspaceId = state.WorkspaceId
            }, ct);
    }

    public async Task HandleAsync(
        CallsTerminatedEvent message,
        CancellationToken ct = default)
    {
        var state = await _sagaRepo.GetById(message.SagaId);

        if (state is null || state.IsFailed || state.CallsTerminated)
            return;

        state.CallsTerminated = true;

        await _sagaRepo.Save(state);

        await _messageClient.PublishAsync(
            new ArchiveWorkspaceChannels
            {
                SagaId = state.SagaId,
                WorkspaceId = state.WorkspaceId
            }, ct);
    }

    public async Task HandleAsync(
        ChannelsArchivedEvent message,
        CancellationToken ct = default)
    {
        var state = await _sagaRepo.GetById(message.SagaId);

        if (state is null || state.IsFailed || state.ChannelsArchived)
            return;

        state.ChannelsArchived = true;

        await _sagaRepo.Save(state);

        await _messageClient.PublishAsync(
            new InitiateFilePurge
            {
                SagaId = state.SagaId,
                WorkspaceId = state.WorkspaceId
            }, ct);
    }

    public async Task HandleAsync(
        FilePurgeStartedEvent message,
        CancellationToken ct = default)
    {
        var state = await _sagaRepo.GetById(message.SagaId);

        if (state is null || state.IsFailed || state.FilePurgeStarted)
            return;

        state.FilePurgeStarted = true;

        await _sagaRepo.Save(state);

        await _messageClient.PublishAsync(
            new SendWorkspaceDeletedNotification
            {
                SagaId = state.SagaId,
                WorkspaceId = state.WorkspaceId,
                MemberIds = state.MemberIds
            }, ct);
    }

    public async Task HandleAsync(
        NotificationsSentEvent message,
        CancellationToken ct = default)
    {
        var state = await _sagaRepo.GetById(message.SagaId);

        if (state is null || state.NotificationsSent)
            return;

        state.NotificationsSent = true;
        state.NotifiedMemberIds = [..state.MemberIds];

        if (state.IsFailed)
            state.RequiresManualIntervention = true;

        await TryCompleteSaga(state);
    }

    private async Task TryCompleteSaga(
        WorkspaceDeletionSagaState state)
    {
        if (!state.IsFailed &&
            !state.IsCompleted &&
            state.FilePurgeCompleted &&
            state.NotificationsSent)
        {
            state.IsCompleted = true;
            state.CompletedAt = DateTime.UtcNow;
        }

        await _sagaRepo.Save(state);
    }

    public Task HandleAsync(
        MemberWarningFailedEvent message,
        CancellationToken ct = default)
    {
        return FailSaga(message, ct);
    }

    public Task HandleAsync(
        CallTerminationFailedEvent message,
        CancellationToken ct = default)
    {
        return FailSaga(message, ct);
    }

    public Task HandleAsync(
        ChannelArchivingFailedEvent message,
        CancellationToken ct = default)
    {
        return FailSaga(message, ct);
    }

    public Task HandleAsync(
        FilePurgeFailedEvent message,
        CancellationToken ct = default)
    {
        return FailSaga(message, ct);
    }

    public async Task HandleAsync(
        NotificationFailedEvent message,
        CancellationToken ct = default)
    {
        var state = await _sagaRepo.GetById(message.SagaId);

        if (state is null || state.IsCompleted)
            return;

        state.NotifiedMemberIds = message.NotifiedMemberIds;

        if (state.NotifiedMemberIds.Count > 0)
            state.RequiresManualIntervention = true;

        await _sagaRepo.Save(state);

        await FailSaga(message, ct);
    }

    private async Task FailSaga(
        WorkspaceDeletionFailure message,
        CancellationToken ct)
    {
        var state = await _sagaRepo.GetById(message.SagaId);

        if (state is null ||
            state.IsCompleted ||
            state.IsFailed)
            return;

        state.IsFailed = true;
        state.IsCompensating = true;
        state.FailureReason = message.Reason;

        if (state.CallsTerminated ||
            state.FilePurgeCompleted ||
            state.NotificationsSent ||
            state.NotifiedMemberIds.Count > 0)
        {
            state.RequiresManualIntervention = true;
        }

        await _sagaRepo.Save(state);

        await CompensateNext(state, ct);
    }

    private async Task CompensateNext(
        WorkspaceDeletionSagaState state,
        CancellationToken ct)
    {
        if (!state.IsCompensating)
            return;

        // 1. Cancel file purge
        if (state.FilePurgeStarted &&
            !state.FilePurgeCompleted &&
            !state.FilePurgeCancellationAttempted)
        {
            state.FilePurgeCancellationAttempted = true;

            await _sagaRepo.Save(state);

            await _messageClient.PublishAsync(
                new CancelFilePurge
                {
                    SagaId = state.SagaId,
                    WorkspaceId = state.WorkspaceId
                }, ct);

            return;
        }

        // 2. Restore channels
        if (state.ChannelsArchived &&
            !state.ChannelRestorationAttempted)
        {
            state.ChannelRestorationAttempted = true;

            await _sagaRepo.Save(state);

            await _messageClient.PublishAsync(
                new RestoreWorkspaceChannels
                {
                    SagaId = state.SagaId,
                    WorkspaceId = state.WorkspaceId
                }, ct);

            return;
        }

        // 3. Notify members of cancellation
        if (state.MembersWarned &&
            !state.CancellationNotificationAttempted)
        {
            state.CancellationNotificationAttempted = true;

            await _sagaRepo.Save(state);

            await _messageClient.PublishAsync(
                new NotifyDeletionCancelled
                {
                    SagaId = state.SagaId,
                    WorkspaceId = state.WorkspaceId,
                    MemberIds = state.MemberIds
                }, ct);

            return;
        }

        // 4. Finish compensation
        state.IsCompensating = false;
        state.CompletedAt = DateTime.UtcNow;

        await _sagaRepo.Save(state);
    }

    public async Task HandleAsync(
        FilePurgeCancelledEvent message,
        CancellationToken ct = default)
    {
        var state = await _sagaRepo.GetById(message.SagaId);

        if (state is null ||
            !state.IsCompensating ||
            !state.FilePurgeCancellationAttempted ||
            state.FilePurgeCancelled)
            return;

        state.FilePurgeCancelled = true;

        state.PermanentlyDeletedFileCount =
            message.PermanentlyDeletedFileCount;

        if (state.PermanentlyDeletedFileCount > 0)
            state.RequiresManualIntervention = true;

        await _sagaRepo.Save(state);

        await CompensateNext(state, ct);
    }

    public async Task HandleAsync(
        ChannelsRestoredEvent message,
        CancellationToken ct = default)
    {
        var state = await _sagaRepo.GetById(message.SagaId);

        if (state is null ||
            !state.IsCompensating ||
            !state.ChannelRestorationAttempted ||
            state.ChannelsRestored)
            return;

        state.ChannelsRestored = true;

        await _sagaRepo.Save(state);

        await CompensateNext(state, ct);
    }

    public async Task HandleAsync(
        CancellationNotifiedEvent message,
        CancellationToken ct = default)
    {
        var state = await _sagaRepo.GetById(message.SagaId);

        if (state is null ||
            !state.IsCompensating ||
            !state.CancellationNotificationAttempted ||
            state.CancellationNotified)
            return;

        state.CancellationNotified = true;

        await _sagaRepo.Save(state);

        await CompensateNext(state, ct);
    }

    private async Task CompensationFailed(
        WorkspaceDeletionFailure message,
        string compensation,
        CancellationToken ct)
    {
        var state = await _sagaRepo.GetById(message.SagaId);

        if (state is null || !state.IsCompensating)
            return;

        var error = $"{compensation}: {message.Reason}";

        if (state.CompensationErrors.Contains(error))
            return;

        state.RequiresManualIntervention = true;
        state.CompensationErrors.Add(error);

        await _sagaRepo.Save(state);

        await CompensateNext(state, ct);
    }

    public Task HandleAsync(
        FilePurgeCancellationFailedEvent message,
        CancellationToken ct = default)
    {
        return CompensationFailed(
            message, "File purge cancellation", ct);
    }

    public Task HandleAsync(
        ChannelRestorationFailedEvent message,
        CancellationToken ct = default)
    {
        return CompensationFailed(
            message, "Channel restoration", ct);
    }

    public Task HandleAsync(
        CancellationNotificationFailedEvent message,
        CancellationToken ct = default)
    {
        return CompensationFailed(
            message, "Cancellation notification", ct);
    }

    public async Task HandleAsync(
        FilePurgeCompletedEvent message,
        CancellationToken ct = default)
    {
        var state = await _sagaRepo.GetById(message.SagaId);

        if (state is null || state.FilePurgeCompleted)
            return;

        state.FilePurgeCompleted = true;

        if (state.IsFailed)
            state.RequiresManualIntervention = true;

        await TryCompleteSaga(state);
    }
}

