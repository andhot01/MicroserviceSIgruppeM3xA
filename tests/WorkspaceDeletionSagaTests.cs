using Shared.Contracts.WorkspaceDeletion;
using src.Application.Sagas;
using src.Infrastructure.Repositories;

public class WorkspaceDeletionSagaTests
{
    private readonly SagaTestMessageClient _messageClient = new();
    private readonly SagaStateRepository _repository = new();

    private readonly WorkspaceDeletionSagaOrchestrator _orchestrator;

    private readonly Guid _sagaId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();

    public WorkspaceDeletionSagaTests()
    {
        _orchestrator = new WorkspaceDeletionSagaOrchestrator(
            _messageClient,
            _repository);
    }

    [Fact]
    public async Task HappyPath_CompletesSaga()
    {
        await _orchestrator.HandleAsync(
            new InitiateWorkspaceDeletion
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId,
                AdminId = Guid.NewGuid()
            });

        Assert.IsType<WarnWorkspaceMembers>(
            _messageClient.PublishedMessages.Last());

        await _orchestrator.HandleAsync(
            new MembersWarnedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId,
                MemberIds = [Guid.NewGuid(), Guid.NewGuid()]
            });

        Assert.IsType<TerminateWorkspaceCalls>(
            _messageClient.PublishedMessages.Last());

        await _orchestrator.HandleAsync(
            new CallsTerminatedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId
            });

        Assert.IsType<ArchiveWorkspaceChannels>(
            _messageClient.PublishedMessages.Last());

        await _orchestrator.HandleAsync(
            new ChannelsArchivedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId
            });

        Assert.IsType<InitiateFilePurge>(
            _messageClient.PublishedMessages.Last());

        await _orchestrator.HandleAsync(
            new FilePurgeStartedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId
            });

        Assert.IsType<SendWorkspaceDeletedNotification>(
            _messageClient.PublishedMessages.Last());

        await _orchestrator.HandleAsync(
            new NotificationsSentEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId
            });

        var state = await _repository.GetById(_sagaId);

        Assert.NotNull(state);
        Assert.False(state.IsCompleted);

        await _orchestrator.HandleAsync(
            new FilePurgeCompletedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId
            });

        state = await _repository.GetById(_sagaId);

        Assert.NotNull(state);
        Assert.True(state.IsCompleted);
        Assert.False(state.IsFailed);
        Assert.False(state.IsCompensating);
        Assert.False(state.RequiresManualIntervention);
    }

    [Fact]
    public async Task ChannelArchivingFails_CompensatesSaga()
    {
        await _orchestrator.HandleAsync(
            new InitiateWorkspaceDeletion
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId,
                AdminId = Guid.NewGuid()
            });

        await _orchestrator.HandleAsync(
            new MembersWarnedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId,
                MemberIds = [Guid.NewGuid(), Guid.NewGuid()]
            });

        await _orchestrator.HandleAsync(
            new CallsTerminatedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId
            });

        await _orchestrator.HandleAsync(
            new ChannelArchivingFailedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId,
                Reason = "Channel service unavailable"
            });

        Assert.IsType<NotifyDeletionCancelled>(
            _messageClient.PublishedMessages.Last());

        var state = await _repository.GetById(_sagaId);

        Assert.NotNull(state);
        Assert.True(state.IsFailed);
        Assert.True(state.IsCompensating);

        await _orchestrator.HandleAsync(
            new CancellationNotifiedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId
            });

        state = await _repository.GetById(_sagaId);

        Assert.NotNull(state);
        Assert.True(state.IsFailed);
        Assert.False(state.IsCompleted);
        Assert.False(state.IsCompensating);
        Assert.True(state.CancellationNotified);
        Assert.True(state.RequiresManualIntervention);
    }

    [Fact]
    public async Task NotificationFails_CancelsPurgeAndCompensates()
    {
        var memberId = Guid.NewGuid();

        await _orchestrator.HandleAsync(
            new InitiateWorkspaceDeletion
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId,
                AdminId = Guid.NewGuid()
            });

        await _orchestrator.HandleAsync(
            new MembersWarnedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId,
                MemberIds = [memberId, Guid.NewGuid()]
            });

        await _orchestrator.HandleAsync(
            new CallsTerminatedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId
            });

        await _orchestrator.HandleAsync(
            new ChannelsArchivedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId
            });

        await _orchestrator.HandleAsync(
            new FilePurgeStartedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId
            });

        await _orchestrator.HandleAsync(
            new NotificationFailedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId,
                Reason = "Notification creation failed",
                NotifiedMemberIds = [memberId]
            });

        Assert.IsType<CancelFilePurge>(
            _messageClient.PublishedMessages.Last());

        await _orchestrator.HandleAsync(
            new FilePurgeCancelledEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId,
                PermanentlyDeletedFileCount = 3
            });

        Assert.IsType<RestoreWorkspaceChannels>(
            _messageClient.PublishedMessages.Last());

        await _orchestrator.HandleAsync(
            new ChannelsRestoredEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId
            });

        Assert.IsType<NotifyDeletionCancelled>(
            _messageClient.PublishedMessages.Last());

        await _orchestrator.HandleAsync(
            new CancellationNotifiedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId
            });

        var state = await _repository.GetById(_sagaId);

        Assert.NotNull(state);
        Assert.True(state.IsFailed);
        Assert.False(state.IsCompleted);
        Assert.False(state.IsCompensating);

        Assert.True(state.FilePurgeCancelled);
        Assert.True(state.ChannelsRestored);
        Assert.True(state.CancellationNotified);

        Assert.Equal(3, state.PermanentlyDeletedFileCount);
        Assert.Contains(memberId, state.NotifiedMemberIds);
        Assert.True(state.RequiresManualIntervention);
        Assert.Empty(state.CompensationErrors);
    }

    [Fact]
    public async Task FilePurgeCancellationFails_ContinuesCompensation()
    {
        await _orchestrator.HandleAsync(
            new InitiateWorkspaceDeletion
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId,
                AdminId = Guid.NewGuid()
            });

        await _orchestrator.HandleAsync(
            new MembersWarnedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId,
                MemberIds = [Guid.NewGuid(), Guid.NewGuid()]
            });

        await _orchestrator.HandleAsync(
            new CallsTerminatedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId
            });

        await _orchestrator.HandleAsync(
            new ChannelsArchivedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId
            });

        await _orchestrator.HandleAsync(
            new FilePurgeStartedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId
            });

        await _orchestrator.HandleAsync(
            new NotificationFailedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId,
                Reason = "Notification creation failed"
            });

        Assert.IsType<CancelFilePurge>(
            _messageClient.PublishedMessages.Last());

        await _orchestrator.HandleAsync(
            new FilePurgeCancellationFailedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId,
                Reason = "File Sharing Service unavailable"
            });

        Assert.IsType<RestoreWorkspaceChannels>(
            _messageClient.PublishedMessages.Last());

        await _orchestrator.HandleAsync(
            new ChannelsRestoredEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId
            });

        Assert.IsType<NotifyDeletionCancelled>(
            _messageClient.PublishedMessages.Last());

        await _orchestrator.HandleAsync(
            new CancellationNotifiedEvent
            {
                SagaId = _sagaId,
                WorkspaceId = _workspaceId
            });

        var state = await _repository.GetById(_sagaId);

        Assert.NotNull(state);
        Assert.True(state.IsFailed);
        Assert.False(state.IsCompleted);
        Assert.False(state.IsCompensating);

        Assert.False(state.FilePurgeCancelled);
        Assert.True(state.FilePurgeCancellationAttempted);
        Assert.True(state.ChannelsRestored);
        Assert.True(state.CancellationNotified);

        Assert.True(state.RequiresManualIntervention);
        Assert.Single(state.CompensationErrors);

        Assert.Contains(
            "File purge cancellation: File Sharing Service unavailable",
            state.CompensationErrors);
    }
}