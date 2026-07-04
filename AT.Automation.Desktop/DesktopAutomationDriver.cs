using System.Runtime.InteropServices;
using AT.Core.Contracts;
using AT.Core.Models;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using System.IO;
using FlaUI.UIA3;

namespace AT.Automation.Desktop;

/// <summary>
/// FlaUI (UIA3) alapú megvalósítás — ez váltja a régi, 2016 óta nem karbantartott
/// Winium-ot, a régi WDMethods.cs teljes funkciókészletével. A StartAsync csak az
/// UI Automation motort inicializálja; a tényleges alkalmazás-indítás vagy egy futó
/// ablakhoz csatlakozás egy-egy lépés (LaunchApp / AttachToWindow) — ugyanúgy, ahogy
/// a Web modulban a Navigate lépés nyitja meg az oldalt a böngészőben.
/// </summary>
public sealed class DesktopAutomationDriver : IAutomationDriver, IDisposable
{
    private UIA3Automation? _automation;
    private Application? _app;
    private Window? _mainWindow;

    public string PlatformName => "Desktop";

    /// <summary>Igaz, ha van kezelt főablak — akár indított, akár csatolt alkalmazásból.</summary>
    public bool IsRunning => _mainWindow is not null;

    // ===================== ÉLETCIKLUS =====================

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_automation is not null)
            return Task.CompletedTask;

        return Task.Run(() => _automation = new UIA3Automation(), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        var app = _app;
        var automation = _automation;
        _app = null;
        _mainWindow = null;
        _automation = null;

        if (app is null && automation is null)
            return Task.CompletedTask;

        return Task.Run(() =>
        {
            try { app?.Close(); } catch { /* ha már bezárták, nem gond */ }
            app?.Dispose();
            automation?.Dispose();
        }, cancellationToken);
    }

    /// <summary>Új alkalmazás indítása .exe elérési út alapján — a "LaunchApp" lépés hívja.</summary>
    public Task NavigateAsync(string target, CancellationToken cancellationToken = default)
    {
        EnsureAutomationReady();
        return Task.Run(() =>
        {
            _app = Application.Launch(target);
            _mainWindow = _app.GetMainWindow(_automation!, TimeSpan.FromSeconds(15))
                ?? throw new TimeoutException("Az alkalmazás elindult, de a főablak nem jelent meg időben.");
        }, cancellationToken);
    }

    /// <summary>Csatlakozás egy már futó alkalmazás ablakához cím alapján — a régi StartServiceOnly megfelelője.</summary>
    public Task AttachToWindowAsync(string windowTitle, CancellationToken cancellationToken = default)
    {
        EnsureAutomationReady();
        return Task.Run(() =>
        {
            var handle = NativeMethods.FindWindow(null, windowTitle);
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException($"Nem található ablak ezzel a címmel: {windowTitle}");

            _app = null; // nem mi indítottuk, ezért nem is mi zárjuk a folyamatot
            _mainWindow = _automation!.FromHandle(handle).AsWindow();
        }, cancellationToken);
    }

    public Task ClickAsync(string locator, LocatorType locatorType, CancellationToken cancellationToken = default)
        => RunOnWindow(w => FindElement(w, locator, locatorType, DefaultTimeout).Click(), cancellationToken);

    public Task SendKeysAsync(string locator, LocatorType locatorType, string text, CancellationToken cancellationToken = default)
        => RunOnWindow(w => SetElementText(FindElement(w, locator, locatorType, DefaultTimeout), text), cancellationToken);

    public Task<byte[]> GetScreenshotAsync(CancellationToken cancellationToken = default)
        => RunOnWindow(w =>
        {
            using var bitmap = w.Capture();
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            return stream.ToArray();
        }, cancellationToken);

    // ===================== TELJES LÉPÉS-VÉGREHAJTÁS =====================

    /// <summary>
    /// Egy összeállított TestStep végrehajtása. A visszatérési érték csak a ReadAttribute
    /// lépésnél nem null — azt a ViewModel toast-üzenetben jeleníti meg (a régi
    /// ElementCheck MessageBox.Show-jának nem blokkoló megfelelője).
    /// </summary>
    public Task<string?> ExecuteStepAsync(TestStep step, CancellationToken cancellationToken = default)
    {
        var action = Enum.Parse<DesktopStepAction>(step.Action);

        if (action == DesktopStepAction.LaunchApp)
            return NavigateAsync(step.Value ?? string.Empty, cancellationToken).ContinueWith(_ => (string?)null, cancellationToken);

        if (action == DesktopStepAction.AttachToWindow)
            return AttachToWindowAsync(step.Value ?? string.Empty, cancellationToken).ContinueWith(_ => (string?)null, cancellationToken);

        EnsureAppRunning();
        return Task.Run(() => ExecuteStepCore(step, action), cancellationToken);
    }

    private string? ExecuteStepCore(TestStep step, DesktopStepAction action)
    {
        var window = _mainWindow!;
        var timeout = TimeSpan.FromSeconds(Math.Max(1, step.TimeoutSeconds));

        AutomationElement Element() => FindElement(window, RequireLocator(step.Locator), step.LocatorType, timeout);

        switch (action)
        {
            case DesktopStepAction.Click:
                Element().Click();
                return null;

            case DesktopStepAction.DoubleClick:
                Element().DoubleClick();
                return null;

            case DesktopStepAction.RightClick:
                Element().RightClick();
                return null;

            case DesktopStepAction.SetText:
                SetElementText(Element(), step.Value ?? string.Empty);
                return null;

            case DesktopStepAction.Clear:
                SetElementText(Element(), string.Empty);
                return null;

            case DesktopStepAction.Hover:
                Mouse.MoveTo(Element().GetClickablePoint());
                return null;

            case DesktopStepAction.SelectComboBoxItem:
                {
                    var combo = Element().AsComboBox();
                    combo.Expand();
                    combo.Select(step.Value ?? string.Empty);
                    return null;
                }

            case DesktopStepAction.DragAndDrop:
                {
                    var source = Element();
                    var target = FindElement(window, RequireLocator(step.TargetLocator), step.TargetLocatorType, timeout);
                    Mouse.Drag(source.GetClickablePoint(), target.GetClickablePoint());
                    return null;
                }

            case DesktopStepAction.ReadAttribute:
                return GetPropertyValue(Element(), RequireLocator(step.Value));

            case DesktopStepAction.Wait:
                Thread.Sleep(timeout);
                return null;

            case DesktopStepAction.WaitVisible:
                RetryOrThrow(() => TryFind(window, RequireLocator(step.Locator), step.LocatorType) is { IsOffscreen: false },
                    timeout, $"Az elem nem lett látható időben: {step.Locator}");
                return null;

            case DesktopStepAction.WaitEnabled:
                RetryOrThrow(() => TryFind(window, RequireLocator(step.Locator), step.LocatorType) is { IsEnabled: true },
                    timeout, $"Az elem nem lett elérhető időben: {step.Locator}");
                return null;

            case DesktopStepAction.WaitClickable:
                RetryOrThrow(() => TryFind(window, RequireLocator(step.Locator), step.LocatorType) is { IsEnabled: true, IsOffscreen: false },
                    timeout, $"Az elem nem lett kattintható időben: {step.Locator}");
                return null;

            case DesktopStepAction.WaitPresent:
                RetryOrThrow(() => TryFind(window, RequireLocator(step.Locator), step.LocatorType) is not null,
                    timeout, $"Az elem nem jelent meg időben: {step.Locator}");
                return null;

            case DesktopStepAction.WaitAbsent:
                RetryOrThrow(() => TryFind(window, RequireLocator(step.Locator), step.LocatorType) is null,
                    timeout, $"Az elem nem tűnt el időben: {step.Locator}");
                return null;

            case DesktopStepAction.WaitSelected:
                RetryOrThrow(() => IsSelected(TryFind(window, RequireLocator(step.Locator), step.LocatorType)),
                    timeout, $"Az elem nem lett kiválasztva időben: {step.Locator}");
                return null;

            case DesktopStepAction.WaitHasText:
                RetryOrThrow(() => (GetPropertyValue(TryFind(window, RequireLocator(step.Locator), step.LocatorType), "Text") ?? string.Empty)
                        .Contains(step.Value ?? string.Empty),
                    timeout, $"Az elem nem kapta meg a várt szöveget: {step.Locator}");
                return null;

            case DesktopStepAction.WaitHasValue:
                RetryOrThrow(() => (GetPropertyValue(TryFind(window, RequireLocator(step.Locator), step.LocatorType), "Value") ?? string.Empty)
                        .Contains(step.Value ?? string.Empty),
                    timeout, $"Az elem nem kapta meg a várt értéket: {step.Locator}");
                return null;

            case DesktopStepAction.WaitHasClass:
                RetryOrThrow(() => (GetPropertyValue(TryFind(window, RequireLocator(step.Locator), step.LocatorType), "ClassName") ?? string.Empty)
                        .Contains(step.Value ?? string.Empty),
                    timeout, $"Az elem class-a nem egyezett időben: {step.Locator}");
                return null;

            case DesktopStepAction.WaitHasAttribute:
                {
                    var (attr, val) = ParseKeyValue(step.Value);
                    RetryOrThrow(() => (GetPropertyValue(TryFind(window, RequireLocator(step.Locator), step.LocatorType), attr) ?? string.Empty)
                            .Contains(val),
                        timeout, $"Az attribútum nem egyezett időben: {step.Locator} ({attr})");
                    return null;
                }

            case DesktopStepAction.Close:
                if (_app is not null)
                    _app.Close();
                else
                    window.Close();
                return null;

            default:
                throw new NotSupportedException($"Ismeretlen művelet: {action}");
        }
    }

    private static void RetryOrThrow(Func<bool> condition, TimeSpan timeout, string errorMessage)
    {
        var result = Retry.WhileFalse(condition, timeout, TimeSpan.FromMilliseconds(250));
        if (!result.Success)
            throw new TimeoutException(errorMessage);
    }

    private static bool IsSelected(AutomationElement? element)
        => element is not null && element.Patterns.SelectionItem.IsSupported && element.Patterns.SelectionItem.Pattern.IsSelected.Value;

    private static void SetElementText(AutomationElement element, string text)
    {
        if (element.Patterns.Value.IsSupported)
        {
            element.Patterns.Value.Pattern.SetValue(text);
            return;
        }

        element.Focus();
        Keyboard.Type(text);
    }

    /// <summary>
    /// Az UIA típusos tulajdonságait térképezi le egy szabad szöveges névre — ez a
    /// Selenium GetAttribute(string)-jének a megfelelője, mert FlaUI-ban nincs
    /// univerzális, string-alapú attribútum-lekérdezés.
    /// </summary>
    private static string GetPropertyValue(AutomationElement? element, string propertyName)
    {
        if (element is null)
            throw new InvalidOperationException("Az elem nem található.");

        string ValueOrName() => element.Patterns.Value.IsSupported
            ? element.Patterns.Value.Pattern.Value.Value
            : element.Name;

        return propertyName.Trim().ToLowerInvariant() switch
        {
            "name" => element.Name,
            "automationid" => element.AutomationId,
            "classname" => element.ClassName,
            "helptext" => element.HelpText,
            "isenabled" => element.IsEnabled.ToString(),
            "isoffscreen" => element.IsOffscreen.ToString(),
            "controltype" => element.ControlType.ToString(),
            "value" or "text" => ValueOrName(),
            "isselected" => element.Patterns.SelectionItem.IsSupported
                ? element.Patterns.SelectionItem.Pattern.IsSelected.Value.ToString()
                : throw new NotSupportedException("Az elem nem támogatja a kiválasztás (SelectionItem) mintát."),
            "istoggled" => element.Patterns.Toggle.IsSupported
                ? element.Patterns.Toggle.Pattern.ToggleState.Value.ToString()
                : throw new NotSupportedException("Az elem nem támogatja a Toggle mintát."),
            _ => throw new NotSupportedException(
                $"Ismeretlen attribútum: '{propertyName}'. Támogatott: Name, AutomationId, ClassName, HelpText, IsEnabled, IsOffscreen, ControlType, Value/Text, IsSelected, IsToggled.")
        };
    }

    // ===================== SEGÉDMETÓDUSOK =====================

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private Task RunOnWindow(Action<Window> action, CancellationToken cancellationToken)
    {
        EnsureAppRunning();
        return Task.Run(() => action(_mainWindow!), cancellationToken);
    }

    private Task<T> RunOnWindow<T>(Func<Window, T> func, CancellationToken cancellationToken)
    {
        EnsureAppRunning();
        return Task.Run(() => func(_mainWindow!), cancellationToken);
    }

    private void EnsureAutomationReady()
    {
        if (_automation is null)
            throw new InvalidOperationException("Az UI Automation motor nincs elindítva — hívd meg előbb a StartAsync-et.");
    }

    private void EnsureAppRunning()
    {
        EnsureAutomationReady();
        if (_mainWindow is null)
            throw new InvalidOperationException("Nincs kezelt ablak — vegyél fel előbb egy 'LaunchApp' vagy 'AttachToWindow' lépést.");
    }

    private static string RequireLocator(string? locator)
    {
        if (string.IsNullOrWhiteSpace(locator))
            throw new ArgumentException("A lokátor (vagy érték) mező kötelező ehhez a lépéstípushoz.");
        return locator;
    }

    private static (string Key, string Value) ParseKeyValue(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !raw.Contains('='))
            throw new ArgumentException("Az Érték mezőben 'attribútum=érték' formátum szükséges, pl. IsEnabled=True.");

        var idx = raw.IndexOf('=');
        return (raw[..idx].Trim(), raw[(idx + 1)..].Trim());
    }

    private static AutomationElement FindElement(Window window, string locator, LocatorType type, TimeSpan timeout)
    {
        var result = Retry.WhileNull(() => TryFind(window, locator, type), timeout, TimeSpan.FromMilliseconds(250));
        return result.Result ?? throw new TimeoutException($"Az elem nem található: {locator}");
    }

    private static AutomationElement? TryFind(Window window, string locator, LocatorType type) => type switch
    {
        LocatorType.Id => window.FindFirstDescendant(cf => cf.ByAutomationId(locator)),
        LocatorType.Name => window.FindFirstDescendant(cf => cf.ByName(locator)),
        LocatorType.ClassName => window.FindFirstDescendant(cf => cf.ByClassName(locator)),
        LocatorType.XPath => window.FindFirstByXPath(locator),
        _ => throw new NotSupportedException($"'{type}' lokátor-típus nem támogatott desktop automatizálásnál (Id, Name, ClassName, XPath közül választhatsz).")
    };

    public void Dispose()
    {
        try { _app?.Close(); } catch { /* ignore */ }
        _app?.Dispose();
        _automation?.Dispose();
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);
    }

    /// <summary>
    /// A képernyő adott pontján lévő elemet adja vissza — ez a mechanizmus a teljes
    /// képernyőn működik, NEM csak a mi elindított/csatolt ablakunk fáján belül, ezért
    /// felugró menükben, context menu-kben, tooltipekben is használható, kattintás nélkül.
    /// </summary>
    public Task<DesktopElementNode?> GetElementAtPointAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        if (_automation is null)
            return Task.FromResult<DesktopElementNode?>(null);

        return Task.Run(() =>
        {
            try
            {
                var element = _automation.FromPoint(new System.Drawing.Point(x, y));
                if (element is null)
                    return null;

                return new DesktopElementNode
                {
                    AutomationId = SafeGet(() => element.AutomationId),
                    Name = SafeGet(() => element.Name),
                    ClassName = SafeGet(() => element.ClassName),
                    ControlType = SafeGet(() => element.ControlType.ToString())
                };
            }
            catch
            {
                return null;
            }
        }, cancellationToken);
    }

    private static string SafeGet(Func<string?> getter)
    {
        try { return getter() ?? ""; }
        catch { return ""; }
    }

}