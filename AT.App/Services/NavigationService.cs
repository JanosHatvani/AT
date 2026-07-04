using CommunityToolkit.Mvvm.ComponentModel;

namespace AT.App.Services;

/// <summary>
/// ViewModel-first navigáció: a MainWindow nem tud semmit az egyes oldalakról,
/// csak azt figyeli, hogy melyik ViewModel az aktuális — a DataTemplate-ek
/// (App.xaml) döntik el, melyik View tartozik hozzá.
/// </summary>
public interface INavigationService
{
    event EventHandler<ObservableObject>? CurrentViewModelChanged;

    void NavigateTo<TViewModel>() where TViewModel : ObservableObject;
    void NavigateTo(Type viewModelType);
}

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;

    public event EventHandler<ObservableObject>? CurrentViewModelChanged;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void NavigateTo<TViewModel>() where TViewModel : ObservableObject
        => NavigateTo(typeof(TViewModel));

    public void NavigateTo(Type viewModelType)
    {
        if (_serviceProvider.GetService(viewModelType) is ObservableObject viewModel)
            CurrentViewModelChanged?.Invoke(this, viewModel);
    }
}
