using src.Application.Sagas;

namespace src.Application.Interfaces;

public interface ISagaStateRepository
{
    Task<WorkspaceDeletionSagaState?> GetById(Guid sagaId);

    Task Save(WorkspaceDeletionSagaState state);
}