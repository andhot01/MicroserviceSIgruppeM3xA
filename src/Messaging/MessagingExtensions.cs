using EasyNetQ;

namespace src.Messaging;

public static class MessagingExtensions
{
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("RabbitMQ")
            ?? throw new InvalidOperationException(
                "RabbitMQ connection string is missing.");

        services.AddEasyNetQ(connectionString);

        services.AddSingleton<IMessageClient, EasyNetQMessageClient>();

        return services;
    }
}