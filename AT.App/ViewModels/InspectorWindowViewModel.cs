using System.Collections.ObjectModel;
using AT.App.Interop;
using AT.Automation.Desktop;
using AT.Automation.Web;
using AT.Core.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AT.App.ViewModels;

public enum InspectorPlatform
{
    Web,
    Desktop
}


// Egy adott platformhoz (Web VAGY Desktop — a hívó dönti el, melyikhez, nincs váltás
// az ablakon belül) rögzített elem-kereső. 3 másodperce van a felhasználónak, hogy a
// kurzort/egeret a kívánt elem fölé vigye — kattintás vagy gyorsbillentyű nélkül.
// Az Android/iOS elem-keresés máshogy működik (a Mobil oldal élő kijelzőjére kattintva),
// ezért ez az ablak nem is ismeri azt a platformot.

public sealed partial class InspectorWindowViewModel : ObservableObject
{
    private const int CountdownSeconds = 3;

    private readonly InspectorPlatform _platform;
    private readonly DesktopAutomationDriver? _desktopDriver;
    private readonly WebAutomationDriver? _webDriver;
    private readonly Action<LocatorType, string, int?> _onChoose;

    [ObservableProperty]
    private bool isCountingDown;

    [ObservableProperty]
    private int secondsRemaining;

    [ObservableProperty]
    private bool isCaptured;

    [ObservableProperty]
    private string controlType = "";

    public ObservableCollection<LocatorCandidate> Candidates { get; } = new();

    public bool HasCandidates => Candidates.Count > 0;

    // Az ablak fejlécében megjelenő platform-név.
    public string PlatformTitle => _platform == InspectorPlatform.Web ? "Web" : "Windows desktop";

    // Nem "számoláson kívüli és nem rögzített" — ez az alap, indítás-előtti állapot.
    public bool IsIdle => !IsCountingDown && !IsCaptured;

    // Igaz, ha ehhez a platformhoz van csatlakoztatott driver.
    public bool IsPlatformReady => _platform switch
    {
        InspectorPlatform.Desktop => _desktopDriver is not null,
        InspectorPlatform.Web => _webDriver is not null,
        _ => false
    };

    public InspectorWindowViewModel(
        DesktopAutomationDriver? desktopDriver,
        WebAutomationDriver? webDriver,
        InspectorPlatform platform,
        Action<LocatorType, string, int?> onChoose)
    {
        _desktopDriver = desktopDriver;
        _webDriver = webDriver;
        _platform = platform;
        _onChoose = onChoose;
    }

    partial void OnIsCountingDownChanged(bool value)
    {
        OnPropertyChanged(nameof(IsIdle));
        StartInspectCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsCapturedChanged(bool value) => OnPropertyChanged(nameof(IsIdle));

    private bool CanStartInspect() => !IsCountingDown && IsPlatformReady;

    [RelayCommand(CanExecute = nameof(CanStartInspect))]
    private async Task StartInspectAsync()
    {
        ResetCapture();
        IsCountingDown = true;

        if (_platform == InspectorPlatform.Web && _webDriver is not null)
        {
            try { await _webDriver.StartHoverTrackingAsync(); }
            catch { /* ha ez elhasal, a végén úgyis jelezzük, hogy nincs találat */ }
        }

        for (var i = CountdownSeconds; i >= 1; i--)
        {
            SecondsRemaining = i;
            await Task.Delay(1000);
        }

        IsCountingDown = false;

        if (_platform == InspectorPlatform.Desktop)
            await CaptureDesktopAsync();
        else
            await CaptureWebAsync();
    }

    private async Task CaptureDesktopAsync()
    {
        if (_desktopDriver is null)
            return;

        var (x, y) = Win32.GetCursorScreenPosition();
        var node = await _desktopDriver.GetElementAtPointAsync(x, y);

        if (node is null)
        {
            ControlType = "Nem található elem ezen a ponton — próbáld újra.";
            return;
        }

        ControlType = string.IsNullOrWhiteSpace(node.ControlType) ? "(ismeretlen típus)" : node.ControlType;
        IsCaptured = true;

        AddCandidate(LocatorType.Name, "Name", node.Name, node.NameMatchIndex, node.NameMatchCount);
        AddCandidate(LocatorType.Id, "AutomationId", node.AutomationId, node.AutomationIdMatchIndex, node.AutomationIdMatchCount);
        AddCandidate(LocatorType.ClassName, "ClassName", node.ClassName, node.ClassNameMatchIndex, node.ClassNameMatchCount);

        var xpath = BuildDesktopXPath(node);
        if (xpath is not null)
            AddCandidate(LocatorType.XPath, "XPath", xpath);

        OnPropertyChanged(nameof(HasCandidates));
    }

    private async Task CaptureWebAsync()
    {
        if (_webDriver is null)
            return;

        WebInspectResult? result;
        try
        {
            result = await _webDriver.ReadLastHoveredElementAsync();
        }
        catch (Exception ex)
        {
            ControlType = $"Hiba az elem beolvasásakor: {ex.Message}";
            return;
        }

        if (result is null)
        {
            ControlType = "Nem sikerült elemet találni — mozgasd az egeret a böngésző fölött a visszaszámlálás alatt.";
            return;
        }

        ControlType = string.IsNullOrWhiteSpace(result.Tag) ? "(ismeretlen elem)" : $"<{result.Tag}>";
        IsCaptured = true;

        AddCandidate(LocatorType.Id, "Id", result.Id, result.IdMatchIndex, result.IdMatchCount);
        AddCandidate(LocatorType.Name, "Name", result.Name, result.NameMatchIndex, result.NameMatchCount);
        AddCandidate(LocatorType.ClassName, "ClassName", result.ClassName, result.ClassNameMatchIndex, result.ClassNameMatchCount);
        AddCandidate(LocatorType.XPath, "XPath", result.XPath);

        if (!string.IsNullOrWhiteSpace(result.Id))
            AddCandidate(LocatorType.CssSelector, "CSS", "#" + result.Id);

        OnPropertyChanged(nameof(HasCandidates));
    }

    private static string? BuildDesktopXPath(DesktopElementNode node)
    {
        if (string.IsNullOrWhiteSpace(node.ControlType))
            return null;

        if (!string.IsNullOrWhiteSpace(node.AutomationId))
            return $"//{node.ControlType}[@AutomationId=\"{node.AutomationId}\"]";

        if (!string.IsNullOrWhiteSpace(node.Name))
            return $"//{node.ControlType}[@Name=\"{node.Name}\"]";

        return $"//{node.ControlType}";
    }

    /// <summary>matchCount &gt; 1 esetén a Label-hez hozzáfűzi a "lokátor N. eleme (összesen M)"
    /// jelzést,
    /// és a jelöltbe belekerül a MatchIndex/MatchCount is — ez adja meg a felhasználónak
    /// (és a lépésbe automatikusan beillesztve az ElementIndex-et), hányadik egyező
    /// elemre kattintott, amikor a lokátor nem egyedi.</summary>
    private void AddCandidate(LocatorType type, string label, string? value, int matchIndex = 0, int matchCount = 0)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var suffix = matchCount > 1 ? $" — lokátor {matchIndex}. eleme (összesen {matchCount})" : "";
        Candidates.Add(new LocatorCandidate
        {
            Type = type,
            Label = label + suffix,
            Value = value,
            MatchIndex = matchCount > 1 ? matchIndex : null,
            MatchCount = matchCount > 1 ? matchCount : null
        });
    }

    private void ResetCapture()
    {
        IsCaptured = false;
        ControlType = "";
        Candidates.Clear();
        OnPropertyChanged(nameof(HasCandidates));
    }

    [RelayCommand]
    private void UseCandidate(LocatorCandidate? candidate)
    {
        if (candidate is null)
            return;

        _onChoose(candidate.Type, candidate.Value, candidate.MatchIndex);
        ResetCapture();
    }

    /// <summary>
    /// Az InspectorWindow hívja meg bezáráskor (Bezárás gomb VAGY a címsor ✕ gombja) —
    /// csak Web platformnál van hatása: leállítja a driver böngésző-session-jét, ami —
    /// a WebAutomationDriver.StopAsync belső logikája alapján — bezárja a böngészőt, HA
    /// azt kifejezetten az Elem-kereső miatt indítottuk (eldobható debug-példány), de
    /// érintetlenül hagyja, ha egy MÁR KORÁBBAN is futó, a felhasználó saját böngészőjéhez
    /// csatlakoztunk. Desktop platformnál nincs teendő — ott a driver session életciklusa
    /// (attach-elt alkalmazás) független az Elem-kereső ablaktól, azt a "Leállítás" gomb
    /// kezeli a fő nézeten.
    /// </summary>
    public async Task HandleWindowClosingAsync()
    {
        if (_platform == InspectorPlatform.Web && _webDriver is not null)
        {
            try { await _webDriver.StopAsync(); }
            catch { /* a felhasználó úgyis zárja az ablakot, egy hiba itt nem blokkolhatja */ }
        }
    }
}
