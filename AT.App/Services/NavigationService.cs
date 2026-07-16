using CommunityToolkit.Mvvm.ComponentModel;

namespace AT.App.Services;


// ViewModel-first navigáció: a MainWindow nem tud semmit az egyes oldalakról,
// csak azt figyeli, hogy melyik ViewModel az aktuális — a DataTemplate-ek
// (App.xaml) döntik el, melyik View tartozik hozzá.

public interface INavigationService
{
    event EventHandler<ObservableObject>? CurrentViewModelChanged;

    void NavigateTo<TViewModel>() where TViewModel : ObservableObject;
    void NavigateTo(Type viewModelType);
}

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private ObservableObject? _current;

    public event EventHandler<ObservableObject>? CurrentViewModelChanged;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void NavigateTo<TViewModel>() where TViewModel : ObservableObject
        => NavigateTo(typeof(TViewModel));

    public void NavigateTo(Type viewModelType)
    {
        if (_serviceProvider.GetService(viewModelType) is not ObservableObject viewModel)
            return;

        // Mielőtt lecserélnénk, hagyjuk, hogy a régi ViewModel takarítson maga után
        // (pl. timerek leállítása, külön ablakok elrejtése). A ViewModel-first
        // navigáció miatt minden váltás új példányt hoz létre — enélkül a hook
        // nélkül a régi példány referenciái (DispatcherTimer, Window) élve
        // maradnának a háttérben, feleslegesen futva/látszódva.
        if (_current is INavigationAware previousAware)
            previousAware.OnNavigatedFrom();

        _current = viewModel;
        CurrentViewModelChanged?.Invoke(this, viewModel);
    }
}
