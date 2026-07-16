using System.Windows;
using System.Windows.Threading;
using AT.App.Services;

namespace AT.App.Views;

/// <summary>
/// Két üzemmódú dialógus: (1) megerősítés — "Nincs Android SDK, szeretnéd telepíteni?",
/// (2) élő progress — %-os sáv (letöltésnél) vagy határozatlan sáv (a többi fázisnál),
/// fázis-szöveggel és hátralévő idővel. A tényleges letöltést/telepítést az
/// IAndroidSdkInstallerService végzi; ez az ablak csak a UI-t és az indítás/megszakítás
/// vezérlését adja.
///
/// A statikus ShowAsync metódus adja vissza, hogy a telepítés sikeresen befejeződött-e
/// (true), a felhasználó elutasította-e (false, azonnal), vagy megszakította/hibázott
/// (false, a folyamat közben). A hívó (MainViewModel navigáció) ez alapján dönt, hogy
/// engedi-e a Mobil nézetre navigálást teljes funkcionalitással, vagy csak "korlátozott",
/// SDK nélküli állapotban.
/// </summary>
public partial class AndroidSdkSetupWindow : Window
{
    private readonly IAndroidSdkInstallerService _installerService;
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>Igaz, ha a telepítés sikeresen lefutott (a Bezárás gomb ekkor jelenik meg
    /// "Kész" állapotban) — a ShowAsync ezt adja vissza eredményként.</summary>
    public bool InstallationSucceeded { get; private set; }

    private AndroidSdkSetupWindow(IAndroidSdkInstallerService installerService)
    {
        _installerService = installerService;
        InitializeComponent();
    }

    /// <summary>Megnyitja a dialógust modálisan, a megerősítő képernyővel indulva.
    /// Visszaadja, hogy a telepítés sikeresen befejeződött-e.</summary>
    public static bool Show(Window owner, IAndroidSdkInstallerService installerService)
    {
        var dialog = new AndroidSdkSetupWindow(installerService) { Owner = owner };
        dialog.ShowDialog();
        return dialog.InstallationSucceeded;
    }

    private void DeclineButton_Click(object sender, RoutedEventArgs e)
    {
        InstallationSucceeded = false;
        Close();
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        SwitchToProgressMode();
        await RunInstallationAsync();
    }

    private void SwitchToProgressMode()
    {
        ConfirmationPanel.Visibility = Visibility.Collapsed;
        ConfirmationButtons.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        ProgressButtons.Visibility = Visibility.Visible;
    }

    private async Task RunInstallationAsync()
    {
        _cancellationTokenSource = new CancellationTokenSource();

        // A Progress<T> callback-je garantáltan azon a szinkronizációs kontextuson fut,
        // ahol a Progress<T> példány létrejött — mivel ezt a UI-szálon (a gomb Click
        // eseménykezelőjében) hozzuk létre, a callback biztonságosan írhatja közvetlenül
        // a UI-elemeket, nincs szükség külön Dispatcher.Invoke-ra.
        var progress = new Progress<AndroidSdkInstallProgress>(OnProgressReported);

        try
        {
            await _installerService.InstallAsync(progress, _cancellationTokenSource.Token);

            InstallationSucceeded = true;
            PhaseLabelText.Text = "Az Android SDK sikeresen telepítve.";
            ProgressBarControl.IsIndeterminate = false;
            ProgressBarControl.Value = 100;
            PercentText.Text = "100%";
            EtaText.Text = "";
            CancelInstallButton.Visibility = Visibility.Collapsed;
            CloseAfterFinishButton.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException)
        {
            PhaseLabelText.Text = "Telepítés megszakítva.";
            ProgressBarControl.IsIndeterminate = false;
            ProgressBarControl.Value = 0;
            CancelInstallButton.Visibility = Visibility.Collapsed;
            CloseAfterFinishButton.Visibility = Visibility.Visible;
            CloseAfterFinishButton.Content = "Bezárás";
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"A telepítés sikertelen: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
            CancelInstallButton.Visibility = Visibility.Collapsed;
            CloseAfterFinishButton.Visibility = Visibility.Visible;
            CloseAfterFinishButton.Content = "Bezárás";
        }
    }

    private void OnProgressReported(AndroidSdkInstallProgress progress)
    {
        PhaseLabelText.Text = progress.PhaseLabel + "…";

        if (progress.PercentComplete.HasValue)
        {
            ProgressBarControl.IsIndeterminate = false;
            ProgressBarControl.Value = progress.PercentComplete.Value;
            PercentText.Text = $"{progress.PercentComplete.Value:0}%";
        }
        else
        {
            ProgressBarControl.IsIndeterminate = true;
            PercentText.Text = "";
        }

        EtaText.Text = progress.EstimatedTimeRemaining.HasValue
            ? FormatEta(progress.EstimatedTimeRemaining.Value)
            : "";
    }

    private static string FormatEta(TimeSpan eta)
    {
        if (eta.TotalSeconds < 1)
            return "";

        return eta.TotalMinutes >= 1
            ? $"kb. {(int)eta.TotalMinutes} perc {eta.Seconds} mp van hátra"
            : $"kb. {(int)eta.TotalSeconds} mp van hátra";
    }

    private void CancelInstallButton_Click(object sender, RoutedEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Ha a felhasználó a címsor ✕ gombjával zárja be a telepítés KÖZBEN futó
    /// ablakot, ez ugyanúgy megszakításnak számít, mint a "Megszakítás" gomb — enélkül
    /// egy háttérben tovább futó, de már nem látható telepítési folyamat maradna.</summary>
    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
    }
}
