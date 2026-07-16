using System.Collections.ObjectModel;
using AT.App.Models;
using AT.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AT.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly INotificationService _notificationService;
    private readonly IAndroidSdkInstallerService _androidSdkInstallerService;

    [ObservableProperty]
    private ObservableObject? currentViewModel;

    public ObservableCollection<ToastMessage> Toasts => _notificationService.Toasts;

    public IRelayCommand<Type> NavigateCommand { get; }

    public bool IsDashboardActive => CurrentViewModel is DashboardViewModel;
    public bool IsWebActive => CurrentViewModel is WebTestViewModel;
    public bool IsDesktopActive => CurrentViewModel is DesktopTestViewModel;
    public bool IsMobileActive => CurrentViewModel is MobileTestViewModel;
    public bool IsHistoryActive => CurrentViewModel is HistoryViewModel;
    public bool IsScheduledTasksActive => CurrentViewModel is ScheduledTasksViewModel;
    public bool IsSettingsActive => CurrentViewModel is SettingsViewModel;

    public MainViewModel(
        INavigationService navigationService,
        INotificationService notificationService,
        IAndroidSdkInstallerService androidSdkInstallerService)
    {
        _navigationService = navigationService;
        _notificationService = notificationService;
        _androidSdkInstallerService = androidSdkInstallerService;

        _navigationService.CurrentViewModelChanged += OnCurrentViewModelChanged;
        NavigateCommand = new RelayCommand<Type>(OnNavigateRequested);

        _navigationService.NavigateTo<DashboardViewModel>();
    }

    /// <summary>
    /// A NavigateCommand belépési pontja — a Mobil (Android) nézetre navigálás ELŐTT
    /// ellenőrzi, van-e telepített Android SDK. Ha nincs, egy dialógusban megkérdezi a
    /// felhasználót, szeretné-e most telepíteni (lásd AndroidSdkSetupWindow). A navigáció
    /// MINDKÉT esetben megtörténik (a felhasználó "Nem"-et is választhat) — a Mobil nézet
    /// saját maga jelzi (lásd MobileTestViewModel.IsAndroidSdkMissing), ha SDK nélkül,
    /// korlátozott állapotban van.
    /// </summary>
    private void OnNavigateRequested(Type? targetViewModelType)
    {
        if (targetViewModelType is null)
            return;

        if (targetViewModelType == typeof(MobileTestViewModel) && !_androidSdkInstallerService.IsInstalled())
        {
            // A dialógus modális — a hívás itt blokkol, amíg a felhasználó dönt/végez a
            // telepítéssel (vagy elutasítja). Ez szándékos: a navigáció csak ezután történik,
            // hogy a Mobil nézet már a friss SDK-állapotot lássa (SdkRootOverride-frissítés).
            AT.App.Views.AndroidSdkSetupWindow.Show(System.Windows.Application.Current.MainWindow, _androidSdkInstallerService);
        }

        _navigationService.NavigateTo(targetViewModelType);
    }

    private void OnCurrentViewModelChanged(object? sender, ObservableObject viewModel)
    {
        CurrentViewModel = viewModel;

        OnPropertyChanged(nameof(IsDashboardActive));
        OnPropertyChanged(nameof(IsWebActive));
        OnPropertyChanged(nameof(IsDesktopActive));
        OnPropertyChanged(nameof(IsMobileActive));
        OnPropertyChanged(nameof(IsHistoryActive));
        OnPropertyChanged(nameof(IsScheduledTasksActive));
        OnPropertyChanged(nameof(IsSettingsActive));

        // Az "Ütemezett feladatok" nézet Singleton ViewModel-je csak a program indulásakor
        // épül fel egyszer — ha közben a Web/Desktop/Mobil nézeten új ütemezés jön létre,
        // azt csak egy explicit újratöltés láttatja. Ahelyett hogy a felhasználónak minden
        // alkalommal kézzel kellene a "Frissítés" gombra kattintania, a nézet minden
        // odalátogatáskor automatikusan frissül — a gomb így csak kényelmi kiegészítés
        // marad azoknak, akik az oldalon időznek, és onnan szeretnék frissíteni.
        if (viewModel is ScheduledTasksViewModel scheduledTasksViewModel)
            scheduledTasksViewModel.LoadRowsCommand.Execute(null);

        // A Mobil nézetre navigáláskor (akár telepítettük most az SDK-t, akár korábban
        // már megvolt, akár a felhasználó "Nem"-et választott) frissítjük az SDK-állapot
        // jelzést — enélkül a nézet a betöltéskori (esetleg elavult) állapotot mutatná.
        if (viewModel is MobileTestViewModel mobileTestViewModel)
            mobileTestViewModel.RefreshAndroidSdkStatusCommand.Execute(null);
    }
}
