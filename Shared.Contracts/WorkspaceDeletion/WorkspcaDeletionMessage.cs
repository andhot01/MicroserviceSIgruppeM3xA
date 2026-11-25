namespace Shared.Contracts.WorkspaceDeletion;

public abstract class WorkspaceDeletionMessage
{
    public Guid SagaId { get; set; }
    public Guid WorkspaceId { get; set; }
}