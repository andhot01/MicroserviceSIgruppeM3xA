namespace src.Application.Handlers;

public interface IMessageHandler<T>
{
    Task HandleAsync(
        T message,
        CancellationToken cancellationToken = default);
}