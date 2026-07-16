using System.Collections.ObjectModel;
using AT.App.Models;

namespace AT.App.Services;


// Toast-stílusú értesítések megjelenítése. A MainWindow a Toasts kollekciót
// köti ki egy ItemsControl-lal (jobb alsó sarok overlay) — lásd MainWindow.xaml.

public interface INotificationService
{
    // A jelenleg látható toast-üzenetek. A MainViewModel.Toasts ezt adja tovább a View-nak.
    ObservableCollection<ToastMessage> Toasts { get; }

    // Megjelenít egy toast-üzenetet, ami néhány másodperc után automatikusan eltűnik.
    void Show(string message, NotificationType type = NotificationType.Info);
}
