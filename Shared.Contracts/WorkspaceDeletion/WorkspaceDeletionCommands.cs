namespace Shared.Contracts.WorkspaceDeletion;

public class InitiateWorkspaceDeletion : WorkspaceDeletionMessage
{
    public Guid AdminId { get; set; }
}

public class WarnWorkspaceMembers : WorkspaceDeletionMessage
{
}

public class TerminateWorkspaceCalls : WorkspaceDeletionMessage
{
}

public class ArchiveWorkspaceChannels : WorkspaceDeletionMessage
{
}

public class InitiateFilePurge : WorkspaceDeletionMessage
{
}

public class SendWorkspaceDeletedNotification : WorkspaceDeletionMessage
{
    public List<Guid> MemberIds { get; set; } = [];
}