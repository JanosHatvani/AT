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

    public MainViewModel(INavigationService navigationService, INotificationService notificationService)
    {
        _navigationService = navigationService;
        _notificationService = notificationService;

        _navigationService.CurrentViewModelChanged += OnCurrentViewModelChanged;
        NavigateCommand = new RelayCommand<Type>(t =>
        {
            if (t is not null)
                _navigationService.NavigateTo(t);
        });

        _navigationService.NavigateTo<DashboardViewModel>();
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
    }
}
