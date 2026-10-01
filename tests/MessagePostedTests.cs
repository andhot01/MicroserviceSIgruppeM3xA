using Shared.Contracts.Events;
using src.Application.Handlers;
using src.Application.Interfaces;
using src.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using src.Messaging;

namespace Tests;

public class MessagePostedTests
{
    private class FakeMessageClient : IMessageClient
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
    
    private class FakeNotificationRepository : INotificationRepository
    {
        private readonly List<Notification> _notifications = [];

        public IEnumerable<Notification> GetAll()
        {
            return _notifications;
        }

        public Notification? GetById(Guid id)
        {
            return _notifications.FirstOrDefault(n => n.Id == id);
        }

        public void Add(Notification notification)
        {
            _notifications.Add(notification);
        }

        public void Update(Notification notification)
        {
        }

        public void Delete(Notification notification)
        {
            _notifications.Remove(notification);
        }
    }
    
    [Fact]
    public async Task HandleAsync_WhenMessageContainsMention_PublishesMentionDetectedEvent()
    {
        var repository = new FakeNotificationRepository();
        var notificationService = new src.Application.Services.NotificationService(repository);
        var messageClient = new FakeMessageClient();

        var handler = new MessagePostedHandler(
            notificationService,
            messageClient);

        var messageId = Guid.NewGuid();
        var mentionedUserId = Guid.NewGuid();

        var message = new MessagePostedEvent
        {
            MessageId = messageId,
            ChannelId = Guid.NewGuid(),
            AuthorId = Guid.NewGuid(),
            Content = $"Hej @{mentionedUserId}",
            PostedAt = DateTime.UtcNow
        };

        await handler.HandleAsync(message);

        var publishedEvent =
            Assert.Single(messageClient.PublishedMessages)
                as MentionDetectedEvent;

        Assert.NotNull(publishedEvent);
        Assert.Equal(messageId, publishedEvent.MessageId);
        Assert.Equal(mentionedUserId, publishedEvent.MentionedUserId);
    }

    [Fact]
    public async Task HandleAsync_WithValidMessage_PublishesOneMentionDetectedEvent()
    {
        var repository = new FakeNotificationRepository();
        var notificationService = new src.Application.Services.NotificationService(repository);
        var messageClient = new FakeMessageClient();

        var handler = new MessagePostedHandler(
            notificationService,
            messageClient);

        var mentionedUserId = Guid.NewGuid();

        var message = new MessagePostedEvent
        {
            MessageId = Guid.NewGuid(),
            ChannelId = Guid.NewGuid(),
            AuthorId = Guid.NewGuid(),
            Content = $"Hej @{mentionedUserId}",
            PostedAt = DateTime.UtcNow
        };
        
        await handler.HandleAsync(message);

        Assert.Single(messageClient.PublishedMessages);
        Assert.IsType<MentionDetectedEvent>(messageClient.PublishedMessages[0]);
    }
    
    [Fact]
    public async Task Integration_MessagePosted_PublishesMentionDetectedEvent()
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RABBITMQ_HOST"] = "localhost"
            })
            .Build();

        services.AddMessaging(configuration);

        var provider = services.BuildServiceProvider();

        var messageClient = provider.GetRequiredService<IMessageClient>();
        
        var repository = new FakeNotificationRepository();
        
        var notificationService = new src.Application.Services.NotificationService(repository);
        
        var handler = new MessagePostedHandler(notificationService, messageClient);
        
        var messageId = Guid.NewGuid();
        var mentionedUserId = Guid.NewGuid();

        var message = new MessagePostedEvent
        {
            MessageId = messageId,
            ChannelId = Guid.NewGuid(),
            AuthorId = Guid.NewGuid(),
            Content = $"Hej @{mentionedUserId}",
            PostedAt = DateTime.UtcNow
        };

        var result = new TaskCompletionSource<MentionDetectedEvent>();

        await messageClient.SubscribeAsync<MentionDetectedEvent>(
            "integration-test",
            eventMessage =>
            {
                result.TrySetResult(eventMessage);
                return Task.CompletedTask;
            });

        await messageClient.SubscribeAsync<MessagePostedEvent>(
            "integration-test-handler",
            eventMessage => handler.HandleAsync(eventMessage));
        
        await messageClient.PublishAsync(message);
        
        var publishedEvent = await result.Task.WaitAsync(TimeSpan.FromSeconds(5));
        
        Assert.Equal(messageId, publishedEvent.MessageId);
        Assert.Equal(mentionedUserId, publishedEvent.MentionedUserId);
    }
    
}