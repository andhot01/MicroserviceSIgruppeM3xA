using EasyNetQ;

namespace src.Messaging;

public static class MessagingExtensions
{
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var rabbitMqHost =
            configuration["RABBITMQ_HOST"] ?? "localhost";

        var connectionString = $"host={rabbitMqHost}";

        services.AddEasyNetQ(connectionString);

        services.AddSingleton<IMessageClient, EasyNetQMessageClient>();

        return services;
    }
}