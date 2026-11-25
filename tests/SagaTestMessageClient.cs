using src.Messaging;

public class SagaTestMessageClient : IMessageClient
{
    public List<object> PublishedMessages { get; } = [];

    public Task PublishAsync<T>(
        T message,
        CancellationToken cancellationToken = default)
    {
        PublishedMessages.Add(message!);

        return Task.CompletedTask;
    }

    public Task SubscribeAsync<T>(
        string subscriptionId,
        Func<T, Task> handler,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}