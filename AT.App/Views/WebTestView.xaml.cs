using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AT.App.ViewModels;

namespace AT.App.Views;

public partial class WebTestView : UserControl
{
    public WebTestView()
    {
        InitializeComponent();
    }

    /// <summary>A lépéssor egy sorára kattintva kijelöli azt — ez adja meg a "kijelölt lépést"
    /// a Delete/Ctrl+D/Ctrl+↑/↓ billentyűparancsokhoz, és vizuálisan is kiemeli a sort.</summary>
    private void StepRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not WebTestViewModel viewModel)
            return;

        if (sender is FrameworkElement { DataContext: AT.App.Models.TestStepRow row })
            viewModel.SelectStepCommand.Execute(row);

        Focus();
    }

    /// <summary>
    /// Billentyűparancsok: F5 futtatás, Shift+F5 leállítás, Ctrl+S mentés, Ctrl+O
    /// betöltés, Ctrl+N fókusz az Új lépés form Művelet mezőjére, Delete a kijelölt
    /// lépés törlése, Ctrl+D duplikálás, Ctrl+↑/↓ mozgatás, Esc szerkesztés megszakítása.
    ///
    /// A Delete/Ctrl+D/Ctrl+↑/↓ szándékosan kimarad, ha a fókusz épp egy szövegbeviteli
    /// mezőben (TextBox/ComboBox) van — enélkül pl. egy TextBox-ban a Delete billentyű
    /// karakter törlése helyett véletlenül egy egész lépést törölne ki a listából.
    /// </summary>
    private void WebTestView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not WebTestViewModel viewModel)
            return;

        var isTextInputFocused = Keyboard.FocusedElement is TextBox or ComboBox or ComboBoxItem;
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        switch (e.Key)
        {
            case Key.F5 when shift:
                viewModel.HandleStopShortcut();
                e.Handled = true;
                break;

            case Key.F5:
                viewModel.HandleRunShortcut();
                e.Handled = true;
                break;

            case Key.S when ctrl:
                viewModel.HandleSaveShortcut();
                e.Handled = true;
                break;

            case Key.O when ctrl:
                viewModel.HandleLoadShortcut();
                e.Handled = true;
                break;

            case Key.N when ctrl:
                NewActionComboBox.Focus();
                e.Handled = true;
                break;

            case Key.Delete when !isTextInputFocused:
                viewModel.HandleDeleteShortcut();
                e.Handled = true;
                break;

            case Key.D when ctrl && !isTextInputFocused:
                viewModel.HandleDuplicateShortcut();
                e.Handled = true;
                break;

            case Key.Up when ctrl && !isTextInputFocused:
                viewModel.HandleMoveUpShortcut();
                e.Handled = true;
                break;

            case Key.Down when ctrl && !isTextInputFocused:
                viewModel.HandleMoveDownShortcut();
                e.Handled = true;
                break;

            case Key.Escape:
                viewModel.HandleEscapeShortcut();
                e.Handled = true;
                break;
        }
    }
}
