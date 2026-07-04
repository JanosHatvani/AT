namespace AT.App.Models;

public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}

public sealed class ToastMessage
{
    public required string Message { get; init; }
    public NotificationType Type { get; init; } = NotificationType.Info;
}
