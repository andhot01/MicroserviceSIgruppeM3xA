using System.Collections.Concurrent;
using src.Application.Interfaces;
using src.Application.Sagas;

namespace src.Infrastructure.Repositories;

public class SagaStateRepository : ISagaStateRepository
{
    private readonly ConcurrentDictionary<Guid,
        WorkspaceDeletionSagaState> _sagas = new();

    public Task<WorkspaceDeletionSagaState?> GetById(Guid sagaId)
    {
        _sagas.TryGetValue(sagaId, out var state);

        return Task.FromResult(state);
    }

    public Task Save(WorkspaceDeletionSagaState state)
    {
        _sagas[state.SagaId] = state;

        return Task.CompletedTask;
    }
}