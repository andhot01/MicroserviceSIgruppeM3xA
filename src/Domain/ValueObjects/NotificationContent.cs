namespace src.Domain.ValueObjects;

public sealed record NotificationContent
{
    public string Title { get; }
    public string Message { get; }

    public NotificationContent(string title, string message)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.");

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be empty.");

        Title = title;
        Message = message;
    }
}