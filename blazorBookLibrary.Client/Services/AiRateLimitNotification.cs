namespace blazorBookLibrary.Client.Services;

public record AiRateLimitNotification(bool IsGuest, int RetryAfterSeconds, string Message);
