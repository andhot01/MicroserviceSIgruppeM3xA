namespace Shared.Contracts.WorkspaceDeletion;

// User Management

public class NotifyDeletionCancelled : WorkspaceDeletionMessage
{
    public List<Guid> MemberIds { get; set; } = [];
}

public class CancellationNotifiedEvent : WorkspaceDeletionMessage
{
}

public class CancellationNotificationFailedEvent
    : WorkspaceDeletionFailure
{
}

// Channel

public class RestoreWorkspaceChannels : WorkspaceDeletionMessage
{
}

public class ChannelsRestoredEvent : WorkspaceDeletionMessage
{
}

public class ChannelRestorationFailedEvent
    : WorkspaceDeletionFailure
{
}

// File Sharing

public class CancelFilePurge : WorkspaceDeletionMessage
{
}

public class FilePurgeCancelledEvent : WorkspaceDeletionMessage
{
    public int PermanentlyDeletedFileCount { get; set; }
}

public class FilePurgeCancellationFailedEvent
    : WorkspaceDeletionFailure
{
}