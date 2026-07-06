using System.Collections.ObjectModel;
using System.Windows.Threading;
using AT.App.Models;

namespace AT.App.Services;

/// <summary>
/// A Toasts kollekciót a MainWindow.xaml egy ItemsControl-lal köti ki (jobb alsó sarok
/// overlay). Minden Show hívás egy új ToastMessage-et ad hozzá, majd 3 másodperc múlva
/// automatikusan eltávolítja — a törlés egy DispatcherTimer-en keresztül, UI-szálon
/// történik, mert az ObservableCollection csak onnan módosítható biztonságosan.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(3);

    public ObservableCollection<ToastMessage> Toasts { get; } = new();

    public void Show(string message, NotificationType type = NotificationType.Info)
    {
        var toast = new ToastMessage { Message = message, Type = type };
        Toasts.Add(toast);

        var timer = new DispatcherTimer { Interval = DisplayDuration };
        timer.Tick += (sender, _) =>
        {
            Toasts.Remove(toast);
            ((DispatcherTimer)sender!).Stop();
        };
        timer.Start();
    }
}
