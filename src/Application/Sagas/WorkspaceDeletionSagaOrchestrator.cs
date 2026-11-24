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
    IMessageHandler<NotificationsSentEvent>
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
}

