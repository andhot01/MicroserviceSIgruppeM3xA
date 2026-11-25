namespace Shared.Contracts.WorkspaceDeletion;

public abstract class WorkspaceDeletionFailure
    : WorkspaceDeletionMessage
{
    public string Reason { get; set; } = string.Empty;
}

// User Management

public class MembersWarnedEvent : WorkspaceDeletionMessage
{
    public List<Guid> MemberIds { get; set; } = [];
}

public class MemberWarningFailedEvent : WorkspaceDeletionFailure
{
}

// Streaming / VOIP

public class CallsTerminatedEvent : WorkspaceDeletionMessage
{
}

public class CallTerminationFailedEvent : WorkspaceDeletionFailure
{
}

// Channel

public class ChannelsArchivedEvent : WorkspaceDeletionMessage
{
}

public class ChannelArchivingFailedEvent : WorkspaceDeletionFailure
{
}

// File Sharing

public class FilePurgeStartedEvent : WorkspaceDeletionMessage
{
}

public class FilePurgeCompletedEvent : WorkspaceDeletionMessage
{
}

public class FilePurgeFailedEvent : WorkspaceDeletionFailure
{
}

// Notification

public class NotificationsSentEvent : WorkspaceDeletionMessage
{
}

public class NotificationFailedEvent : WorkspaceDeletionFailure
{
    public List<Guid> NotifiedMemberIds { get; set; } = [];
}