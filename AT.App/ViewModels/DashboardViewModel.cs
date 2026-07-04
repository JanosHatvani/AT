using AT.App.Models;
using AT.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AT.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly INotificationService _notificationService;
    private readonly INavigationService _navigationService;

    public DashboardViewModel(INotificationService notificationService, INavigationService navigationService)
    {
        _notificationService = notificationService;
        _navigationService = navigationService;
    }

    [RelayCommand]
    private void ShowInfoToast()
        => _notificationService.Show("Ez egy informatív értesítés — pl. \"Böngésző bezárva.\"", NotificationType.Info);

    [RelayCommand]
    private void ShowSuccessToast()
        => _notificationService.Show("Ez egy sikeres művelet visszajelzése — pl. \"Minden lépés lefutott.\"", NotificationType.Success);

    [RelayCommand]
    private void ShowWarningToast()
        => _notificationService.Show("Ez egy figyelmeztetés — pl. \"A lokátor mező kötelező.\"", NotificationType.Warning);

    [RelayCommand]
    private void ShowErrorToast()
        => _notificationService.Show("Ez egy hiba visszajelzése — pl. \"Lépés sikertelen.\"", NotificationType.Error);

    [RelayCommand]
    private void GoToWeb() => _navigationService.NavigateTo<WebTestViewModel>();

    [RelayCommand]
    private void GoToDesktop() => _navigationService.NavigateTo<DesktopTestViewModel>();

    [RelayCommand]
    private void GoToMobile() => _navigationService.NavigateTo<MobileTestViewModel>();
}