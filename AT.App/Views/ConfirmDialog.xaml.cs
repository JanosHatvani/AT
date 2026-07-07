using System.Windows;
using System.Windows.Input;

namespace AT.App.Views;

/// <summary>
/// Általános, a program témáját (világos/sötét) követő megerősítő dialógus a natív
/// MessageBox helyett — az utóbbi mindig a rendszer téma szerint jelenik meg, ami
/// sötét témában zavaróan világos/natív ablakként ütne el a program megjelenésétől.
///
/// Használat: ConfirmDialog.Show(ownerWindow, "Cím", "Üzenet") — igaz, ha a felhasználó
/// az "Igen" gombra kattintott, hamis egyébként (Mégse, Escape, vagy az ablak bezárása).
/// </summary>
public partial class ConfirmDialog : Window
{
    private bool _confirmed;

    private ConfirmDialog()
    {
        InitializeComponent();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
    }

    /// <summary>
    /// Megjeleníti a megerősítő dialógust, és megvárja, amíg a felhasználó bezárja.
    /// </summary>
    /// <param name="owner">A szülő ablak (a dialógus ehhez képest középre igazítva jelenik meg).</param>
    /// <param name="title">A dialógus címsora.</param>
    /// <param name="message">A megerősítendő kérdés szövege.</param>
    /// <param name="confirmButtonText">A megerősítő gomb felirata (alapértelmezetten "Igen").</param>
    /// <param name="cancelButtonText">A mégse gomb felirata (alapértelmezetten "Mégse").</param>
    /// <param name="isDestructive">Ha igaz, a megerősítő gomb piros (Danger) színt kap — visszavonhatatlan/törlő műveletekhez.</param>
    public static bool Show(
        Window? owner,
        string title,
        string message,
        string confirmButtonText = "Igen",
        string cancelButtonText = "Mégse",
        bool isDestructive = false)
    {
        var dialog = new ConfirmDialog
        {
            Owner = owner,
            Title = title
        };

        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.ConfirmButton.Content = confirmButtonText;
        dialog.CancelButton.Content = cancelButtonText;

        if (isDestructive)
        {
            dialog.ConfirmButton.Style = (Style)dialog.FindResource("PrimaryButtonStyle");
            dialog.ConfirmButton.Background = (System.Windows.Media.Brush)dialog.FindResource("Brush.Danger");
        }

        dialog.ShowDialog();
        return dialog._confirmed;
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        _confirmed = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _confirmed = false;
        Close();
    }
}
