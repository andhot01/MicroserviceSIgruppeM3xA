namespace src.Application.Sagas;

public class WorkspaceDeletionSagaState
{
    public Guid SagaId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid AdminId { get; set; }

    public List<Guid> MemberIds { get; set; } = [];
    public List<Guid> NotifiedMemberIds { get; set; } = [];

    // Successful transactions
    public bool MembersWarned { get; set; }
    public bool CallsTerminated { get; set; }
    public bool ChannelsArchived { get; set; }
    public bool FilePurgeStarted { get; set; }
    public bool FilePurgeCompleted { get; set; }
    public bool NotificationsSent { get; set; }

    // Compensating transactions
    public bool FilePurgeCancelled { get; set; }
    public bool ChannelsRestored { get; set; }
    public bool CancellationNotified { get; set; }

    // Saga status
    public bool IsCompleted { get; set; }
    public bool IsFailed { get; set; }
    public bool IsCompensating { get; set; }
    public bool RequiresManualIntervention { get; set; }

    public int PermanentlyDeletedFileCount { get; set; }

    public string? FailureReason { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}