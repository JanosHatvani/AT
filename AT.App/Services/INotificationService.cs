using System.Collections.ObjectModel;
using AT.App.Models;

namespace AT.App.Services;

/// <summary>
/// Toast-stílusú értesítések megjelenítése. A MainWindow a Toasts kollekciót
/// köti ki egy ItemsControl-lal (jobb alsó sarok overlay) — lásd MainWindow.xaml.
/// </summary>
public interface INotificationService
{
    /// <summary>A jelenleg látható toast-üzenetek. A MainViewModel.Toasts ezt adja tovább a View-nak.</summary>
    ObservableCollection<ToastMessage> Toasts { get; }

    /// <summary>Megjelenít egy toast-üzenetet, ami néhány másodperc után automatikusan eltűnik.</summary>
    void Show(string message, NotificationType type = NotificationType.Info);
}
