using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AT.App.ViewModels;

namespace AT.App.Views;

public partial class MobileTestView : UserControl
{
    public MobileTestView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// A kép Stretch="Uniform", ezért letterbox-sávok lehetnek — a nyers kattintási
    /// koordinátát a ténylegesen kirajzolt kép-területhez kell igazítani, mielőtt
    /// relatív (0..1) koordinátává alakítjuk.
    /// </summary>
    private void ScreenImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MobileTestViewModel viewModel || !viewModel.IsPicking)
            return;

        if (sender is not Image { Source: BitmapSource bitmap } image)
            return;

        var containerWidth = image.ActualWidth;
        var containerHeight = image.ActualHeight;
        if (containerWidth <= 0 || containerHeight <= 0)
            return;

        var scale = Math.Min(containerWidth / bitmap.PixelWidth, containerHeight / bitmap.PixelHeight);
        var renderedWidth = bitmap.PixelWidth * scale;
        var renderedHeight = bitmap.PixelHeight * scale;
        var offsetX = (containerWidth - renderedWidth) / 2;
        var offsetY = (containerHeight - renderedHeight) / 2;

        var pos = e.GetPosition(image);
        var relativeX = (pos.X - offsetX) / renderedWidth;
        var relativeY = (pos.Y - offsetY) / renderedHeight;

        if (relativeX is < 0 or > 1 || relativeY is < 0 or > 1)
            return;

        _ = viewModel.CaptureElementAtAsync(relativeX, relativeY);
    }

    /// <summary>A lépéssor egy sorára kattintva kijelöli azt — ez adja meg a "kijelölt lépést"
    /// a Delete/Ctrl+D/Ctrl+↑/↓ billentyűparancsokhoz, és vizuálisan is kiemeli a sort.</summary>
    private void StepRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MobileTestViewModel viewModel)
            return;

        if (sender is FrameworkElement { DataContext: AT.App.Models.TestStepRow row })
            viewModel.SelectStepCommand.Execute(row);

        // A fókuszt is a nézetre visszük, hogy az utána lenyomott billentyűparancsok
        // (pl. Delete) rögtön működjenek, anélkül hogy külön a UserControl-ra kellene
        // kattintani a sor kijelölése után.
        Focus();
    }

    /// <summary>
    /// A "⋮⋮" fogó ikonra kattintva-húzva indítja el a WPF natív drag&amp;drop műveletét.
    /// Az egész sor helyett szándékosan csak ez a dedikált ikon indítja a húzást, hogy
    /// ne ütközzön a sorban lévő gombokkal (Törlés, Szerkesztés, stb.) vagy a sorra
    /// kattintva történő kijelöléssel.
    /// </summary>
    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: AT.App.Models.TestStepRow row } element)
            return;

        DragDrop.DoDragDrop(element, row, DragDropEffects.Move);
        e.Handled = true;
    }

    /// <summary>Engedélyezi az eldobást, ha a húzott adat egy lépéssor — máskülönben "tiltott" kurzort mutat.</summary>
    private void StepRow_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(AT.App.Models.TestStepRow))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// Az eldobás pillanatában kiszámítja, hogy a húzott lépés a cél-sor elé vagy mögé
    /// kerüljön-e (a kurzor függőleges pozíciója alapján a sormagasság felén belül/kívül),
    /// majd a ViewModel MoveStepTo-jával ténylegesen áthelyezi a listában.
    /// </summary>
    private void StepRow_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MobileTestViewModel viewModel)
            return;

        if (e.Data.GetData(typeof(AT.App.Models.TestStepRow)) is not AT.App.Models.TestStepRow draggedRow)
            return;

        if (sender is not FrameworkElement { DataContext: AT.App.Models.TestStepRow targetRow } targetElement)
            return;

        if (ReferenceEquals(draggedRow, targetRow))
            return;

        var targetIndex = viewModel.Steps.IndexOf(targetRow);

        // Ha a kurzor a cél-sor alsó felén van, a lépés a cél MÖGÉ kerüljön, ne elé —
        // enélkül a lista alsó fele felé húzva mindig eggyel "rövidebbre" esne a mozgatás.
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
    private void MobileTestView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MobileTestViewModel viewModel)
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
