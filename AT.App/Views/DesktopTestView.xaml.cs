using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AT.App.ViewModels;

namespace AT.App.Views;

public partial class DesktopTestView : UserControl
{
    public DesktopTestView()
    {
        InitializeComponent();
    }

    /// <summary>A lépéssor egy sorára kattintva kijelöli azt — ez adja meg a "kijelölt lépést"
    /// a Delete/Ctrl+D/Ctrl+↑/↓ billentyűparancsokhoz, és vizuálisan is kiemeli a sort.</summary>
    private void StepRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not DesktopTestViewModel viewModel)
            return;

        if (sender is FrameworkElement { DataContext: AT.App.Models.TestStepRow row })
            viewModel.SelectStepCommand.Execute(row);

        Focus();
    }

    /// <summary>A "⋮⋮" fogó ikonra kattintva-húzva indítja el a WPF natív drag&amp;drop műveletét.</summary>
    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: AT.App.Models.TestStepRow row } element)
            return;

        DragDrop.DoDragDrop(element, row, DragDropEffects.Move);
        e.Handled = true;
    }

    /// <summary>Engedélyezi az eldobást, ha a húzott adat egy lépéssor.</summary>
    private void StepRow_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(AT.App.Models.TestStepRow))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>Kiszámítja, hova essen az áthelyezett lépés, majd elvégzi a ViewModel MoveStepTo-jával.</summary>
    private void StepRow_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not DesktopTestViewModel viewModel)
            return;

        if (e.Data.GetData(typeof(AT.App.Models.TestStepRow)) is not AT.App.Models.TestStepRow draggedRow)
            return;

        if (sender is not FrameworkElement { DataContext: AT.App.Models.TestStepRow targetRow } targetElement)
            return;

        if (ReferenceEquals(draggedRow, targetRow))
            return;

        var targetIndex = viewModel.Steps.IndexOf(targetRow);

        var position = e.GetPosition(targetElement);
        if (position.Y > targetElement.ActualHeight / 2)
            targetIndex++;

        viewModel.MoveStepTo(draggedRow, targetIndex);
        e.Handled = true;
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
    private void DesktopTestView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not DesktopTestViewModel viewModel)
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
