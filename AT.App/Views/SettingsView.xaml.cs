using System.Windows.Controls;
using AT.App.ViewModels;

namespace AT.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();

        // A PasswordBox.Password NEM DependencyProperty (biztonsági okból, hogy ne lehessen
        // véletlenül pl. adatkötéssel egy logba vagy egy nem védett helyre kiírni), ezért nem
        // köthető közvetlenül Binding-gal a ViewModel SmtpPassword property-jéhez. Ehelyett:
        // - A DataContext beállításakor (DataContextChanged) betöltjük a mentett jelszót a
        //   dobozba, hogy a felhasználó lássa, van-e már elmentve valami.
        // - A PasswordChanged eseményben a code-behind írja vissza az értéket a ViewModel-be.
        DataContextChanged += (_, _) =>
        {
            if (DataContext is SettingsViewModel vm)
                SmtpPasswordBox.Password = vm.SmtpPassword ?? "";
        };
    }

    private void SmtpPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.SmtpPassword = SmtpPasswordBox.Password;
    }
}
