namespace AT.App.Services;


// Opcionális interfész ViewModel-eknek, amik cleanup-ot akarnak végezni,
// amikor a NavigationService elnavigál róluk egy másik ViewModelre.
// A ViewModel-first navigáció miatt (lásd NavigationService) minden
// NavigateTo hívás új ViewModel-példányt hoz létre — a régi példány
// referenciái (timerek, event subscription-ök, külön ablakok) enélkül
// a hook nélkül élve maradnának a háttérben.

public interface INavigationAware
{
    // A NavigationService ezt hívja meg a ViewModel-en, mielőtt lecseréli egy másikra.
    void OnNavigatedFrom();
}
