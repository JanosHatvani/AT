using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using AT.App.Models;

namespace AT.App.Services;

/// <summary>
/// Központi, nem blokkoló értesítési szolgáltatás. Ez váltja ki a jelenlegi
/// kódbázis 102 db MessageBox.Show hívását: a hívó csak annyit tud, hogy
/// "üzenetet akarok mutatni", a UI dönti el, hogyan (toast, jelenleg).
/// </summary>
public interface INotificationService
{
    ObservableCollection<ToastMessage> Toasts { get; }

    void Show(string message, NotificationType type = NotificationType.Info);
}

public sealed class NotificationService : INotificationService
{
    private const int DisplayDurationMs = 3500;

    public ObservableCollection<ToastMessage> Toasts { get; } = new();

    public void Show(string message, NotificationType type = NotificationType.Info)
    {
        var toast = new ToastMessage { Message = message, Type = type };

        void Add() => Toasts.Add(toast);
        void Remove() => Toasts.Remove(toast);

        if (Application.Current?.Dispatcher.CheckAccess() == true)
            Add();
        else
            Application.Current?.Dispatcher.Invoke(Add);

        _ = Task.Delay(DisplayDurationMs).ContinueWith(_ =>
        {
            Application.Current?.Dispatcher.Invoke(Remove);
        });
    }
}
