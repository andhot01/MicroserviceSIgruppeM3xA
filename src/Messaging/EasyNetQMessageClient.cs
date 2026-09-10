using EasyNetQ;

namespace src.Messaging;

public class EasyNetQMessageClient : IMessageClient
{
    private readonly IBus _bus;

    public EasyNetQMessageClient(IBus bus)
    {
        _bus = bus;
    }

    public async Task PublishAsync<T>(
        T message,
        CancellationToken cancellationToken = default)
    {
        await _bus.PubSub.PublishAsync(message, cancellationToken);
    }

    public async Task SubscribeAsync<T>(
        string subscriptionId,
        Func<T, Task> handler,
        CancellationToken cancellationToken = default)
    {
        await _bus.PubSub.SubscribeAsync(
            subscriptionId,
            handler,
            cancellationToken);
    }
}