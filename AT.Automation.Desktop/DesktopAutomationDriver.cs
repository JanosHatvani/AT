using System.Diagnostics;
using System.Linq;
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

    /// <summary>Igaz, ha a jelenlegi alkalmazás-folyamatot MI MAGUNK indítottuk (a
    /// NavigateAsync/LaunchApp lépés indította el, mert a folyamat még nem futott) — ha
    /// hamis, egy MÁR KORÁBBAN futó, hozzá csatlakoztatott folyamatról van szó (akár az
    /// AttachOrLaunch csatlakozott hozzá, akár az AttachToWindowAsync). Ez dönti el, hogy
    /// a StopAsync/Dispose ténylegesen bezárja-e az alkalmazást — egy már korábban is
    /// futó, csatlakoztatott programot SOSEM zárunk be automatikusan, csak leválasztjuk
    /// róla a vezérlést, ugyanúgy, ahogy a Web driver teszi a böngészővel.</summary>
    private bool _weLaunchedApp;

    public string PlatformName => "Desktop";

    /// <summary>Igaz, ha van kezelt főablak — akár indított, akár csatolt alkalmazásból.</summary>
    public bool IsRunning => _mainWindow is not null;

    /// <summary>Igaz, ha a LEGUTÓBB végrehajtott lépésnél az elsődleges lokátor nem volt
    /// megtalálható, és a driver a tartalék (FallbackLocator) lokátorral tudta csak
    /// megtalálni az elemet — a ViewModel ezt ellenőrzi minden sikeres ExecuteStepAsync
    /// után, hogy figyelmeztető üzenetet mutasson. Minden ExecuteStepAsync hívás elején
    /// false-ra áll vissza.</summary>
    public bool LastStepUsedFallbackLocator { get; private set; }

    // ===================== FELVEVŐ MÓD (Recorder) =====================
    // FONTOS KORLÁTOZÁS: az alacsony szintű Win32 hook-ok (WH_MOUSE_LL/WH_KEYBOARD_LL)
    // a TELJES KÉPERNYŐN minden kattintást/billentyűleütést elkapnak, nem csak a
    // tesztelt alkalmazáson belülieket — amíg a felvétel aktív, kizárólag a tesztelt
    // alkalmazással érdemes dolgozni, mert MINDEN más ablakra (pl. magára az AT Studio-ra,
    // vagy a Visual Studio-ra) történő kattintás is rögzítésre kerül.

    /// <summary>Egy rögzített akció eseménye — a ViewModel iratkozik fel rá, és minden
    /// egyes tüzelésnél hozzáadja a kapott TestStep-et a lépéslistához. SZINKRON módon,
    /// ugyanazon a szálon tüzel, amelyiken a StartRecording() meghívódott (jellemzően a
    /// WPF UI-szál) — lásd RaiseActionRecorded doksi.</summary>
    public event Action<TestStep>? ActionRecorded;

    public bool IsRecording => _mouseHookHandle != IntPtr.Zero;

    private IntPtr _mouseHookHandle = IntPtr.Zero;
    private IntPtr _keyboardHookHandle = IntPtr.Zero;
    private NativeMethods.LowLevelMouseProc? _mouseProc;
    private NativeMethods.LowLevelKeyboardProc? _keyboardProc;
    private (string Locator, LocatorType Type, int? ElementIndex)? _lastClickLocatorInfo;
    private bool _lastClickWasTextInput;
    private readonly System.Text.StringBuilder _typedBuffer = new();

    /// <summary>Elindítja a felvételt — telepíti az egér- és billentyűzet-hook-okat.
    /// Idempotens: ha már fut, nem csinál semmit. A hook-oknak a hívó szálon (jellemzően
    /// a WPF UI-szálon) kell maradniuk életben, mivel a Win32 hook-üzenetek végfeldolgozása
    /// az adott szál üzenetszivattyúján (a WPF Dispatcher ezt biztosítja) történik.</summary>
    public void StartRecording()
    {
        if (IsRecording)
            return;

        _typedBuffer.Clear();
        _lastClickLocatorInfo = null;
        _lastClickWasTextInput = false;

        _mouseProc = MouseHookCallback;
        _keyboardProc = KeyboardHookCallback;

        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        var moduleHandle = NativeMethods.GetModuleHandle(curModule.ModuleName);

        _mouseHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
        _keyboardHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);

        if (_mouseHookHandle == IntPtr.Zero || _keyboardHookHandle == IntPtr.Zero)
        {
            StopRecording();
            throw new InvalidOperationException("Nem sikerült telepíteni a billentyűzet-/egér-figyelést (Win32 hook hiba).");
        }
    }

    /// <summary>Leállítja a felvételt — eltávolítja a hook-okat, és lezárja (rögzíti) az
    /// esetleg még folyamatban lévő, be nem fejezett szöveg-gépelést.</summary>
    public void StopRecording()
    {
        if (_mouseHookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHookHandle);
            _mouseHookHandle = IntPtr.Zero;
        }

        if (_keyboardHookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHookHandle);
            _keyboardHookHandle = IntPtr.Zero;
        }

        FlushTypedBuffer();
        _mouseProc = null;
        _keyboardProc = null;
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)NativeMethods.WM_LBUTTONDOWN)
        {
            try
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                // Az ELŐZŐ mezőbe gépelt (de még le nem zárt) szöveget lezárjuk, mielőtt
                // az új kattintást feldolgoznánk — enélkül egy gyors kattintás-gépelés-
                // kattintás sorozatnál a szöveg a rossz (a következő) elemhez kerülne.
                FlushTypedBuffer();
                TryRecordClick(hookStruct.pt.x, hookStruct.pt.y);
            }
            catch
            {
                // A felvétel folytatódjon akkor is, ha egy adott kattintás feldolgozása
                // hibázna — egyetlen kihagyott lépés jobb, mint az egész felvétel leállása.
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }

    private void TryRecordClick(int screenX, int screenY)
    {
        if (_automation is null)
            return;

        var element = _automation.FromPoint(new System.Drawing.Point(screenX, screenY));
        if (element is null)
            return;

        var automationId = SafeGet(() => element.AutomationId);
        var name = SafeGet(() => element.Name);
        var className = SafeGet(() => element.ClassName);
        var controlType = SafeGetControlType(element);

        string locator;
        LocatorType locatorType;
        int? elementIndex = null;

        if (!string.IsNullOrEmpty(automationId))
        {
            locator = automationId;
            locatorType = LocatorType.Id;
            var (idx, cnt) = ComputeMatchInfo(element, automationId, e => SafeGet(() => e.AutomationId));
            if (cnt > 1) elementIndex = idx;
        }
        else if (!string.IsNullOrEmpty(name))
        {
            locator = name;
            locatorType = LocatorType.Name;
            var (idx, cnt) = ComputeMatchInfo(element, name, e => SafeGet(() => e.Name));
            if (cnt > 1) elementIndex = idx;
        }
        else if (!string.IsNullOrEmpty(className))
        {
            locator = className;
            locatorType = LocatorType.ClassName;
            var (idx, cnt) = ComputeMatchInfo(element, className, e => SafeGet(() => e.ClassName));
            if (cnt > 1) elementIndex = idx;
        }
        else
        {
            // Nincs használható lokátor erre az elemre (se AutomationId, se Name, se
            // ClassName) — csendben kihagyjuk, nem tudnánk vele mit kezdeni futtatáskor
            // sem.
            return;
        }

        _lastClickLocatorInfo = (locator, locatorType, elementIndex);
        _lastClickWasTextInput = controlType == FlaUI.Core.Definitions.ControlType.Edit
            || controlType == FlaUI.Core.Definitions.ControlType.Document;

        var step = new TestStep
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = $"Kattintás → {locator}",
            Target = AutomationTarget.Desktop,
            Action = DesktopStepAction.Click.ToString(),
            Locator = locator,
            LocatorType = locatorType,
            ElementIndex = elementIndex,
            TimeoutSeconds = 10
        };

        RaiseActionRecorded(step);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)NativeMethods.WM_KEYDOWN && _lastClickWasTextInput)
        {
            try
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                var vk = hookStruct.vkCode;

                if (vk == NativeMethods.VK_RETURN || vk == NativeMethods.VK_TAB)
                {
                    FlushTypedBuffer();
                }
                else if (vk == NativeMethods.VK_BACK)
                {
                    if (_typedBuffer.Length > 0)
                        _typedBuffer.Length--;
                }
                else
                {
                    var ch = NativeMethods.VirtualKeyToChar(vk);
                    if (ch.HasValue)
                        _typedBuffer.Append(ch.Value);
                }
            }
            catch
            {
                // A felvétel folytatódjon akkor is, ha egy adott billentyű feldolgozása hibázna.
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    /// <summary>Az addig összegyűjtött, begépelt szöveget SendKeys lépésként lezárja és
    /// kiküldi (ha van mit) — az utolsó kattintás lokátorát célozva. Hívódik: Enter/Tab
    /// leütésekor, a következő kattintás előtt, és a felvétel leállításakor.</summary>
    private void FlushTypedBuffer()
    {
        if (_typedBuffer.Length == 0 || _lastClickLocatorInfo is null)
        {
            _typedBuffer.Clear();
            return;
        }

        var (locator, locatorType, elementIndex) = _lastClickLocatorInfo.Value;
        var text = _typedBuffer.ToString();
        _typedBuffer.Clear();

        var step = new TestStep
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = $"Szöveg beírása → {locator} → {text}",
            Target = AutomationTarget.Desktop,
            Action = DesktopStepAction.SetText.ToString(),
            Locator = locator,
            LocatorType = locatorType,
            ElementIndex = elementIndex,
            Value = text,
            TimeoutSeconds = 10
        };

        RaiseActionRecorded(step);
        _lastClickWasTextInput = false;
    }

    private void RaiseActionRecorded(TestStep step)
    {
        // A hook callback ugyanazon a szálon fut, amelyik a SetWindowsHookEx-et hívta
        // (StartRecording-ban) — vagyis a WPF UI-szálon, hiszen a ViewModel a UI-szálról
        // hívja a StartRecording()-ot. Emiatt NEM kell külön Dispatcher-en átküldeni az
        // eseményt (ráadásul ez a projekt egy sima osztálykönyvtár, nem WPF-projekt,
        // a System.Windows.Application itt nem is lenne elérhető) — egyszerű, szinkron
        // hívás biztonságos.
        ActionRecorded?.Invoke(step);
    }

    private static FlaUI.Core.Definitions.ControlType? SafeGetControlType(AutomationElement element)
    {
        try { return element.ControlType; }
        catch { return null; }
    }

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
        var shouldCloseApp = _weLaunchedApp;
        _app = null;
        _mainWindow = null;
        _automation = null;
        _weLaunchedApp = false;

        if (app is null && automation is null)
            return Task.CompletedTask;

        return Task.Run(() =>
        {
            if (shouldCloseApp)
            {
                // Csak akkor zárjuk be TÉNYLEGESEN az alkalmazást, ha mi magunk
                // indítottuk — egy már korábban is futó, csatlakoztatott programot
                // sosem zárunk be automatikusan, csak leválasztjuk róla a vezérlést.
                try { app?.Close(); } catch { /* ha már bezárták, nem gond */ }
            }

            app?.Dispose();
            automation?.Dispose();
        }, cancellationToken);
    }

    /// <summary>
    /// Alkalmazás elindítása VAGY egy már futó példányhoz csatlakozás .exe elérési út
    /// alapján — a "LaunchApp" lépés hívja. A FlaUI AttachOrLaunch metódusa dönti el,
    /// melyik történjen: ha már fut egy ugyanabból a futtatható fájlból induló folyamat,
    /// AHHOZ csatlakozik (nem indít egy második, párhuzamos példányt) — ha nem fut,
    /// elindítja. Ez teszi feleslegessé, hogy a felhasználónak külön kelljen
    /// eldöntenie/beállítania, hogy az alkalmazás fut-e már a gépen: a "LaunchApp" lépés
    /// mindkét esetben helyesen viselkedik, anélkül hogy külön "AttachToWindow" lépést
    /// kellene felvenni a lépéssor elejére.
    /// </summary>
    public Task NavigateAsync(string target, CancellationToken cancellationToken = default)
    {
        EnsureAutomationReady();
        return Task.Run(() =>
        {
            // Ezt az AttachOrLaunch HÍVÁS ELŐTT kell megnézni — ha itt már fut egy
            // ugyanilyen nevű folyamat, azt NEM mi indítottuk, tehát a StopAsync sosem
            // zárhatja be automatikusan.
            var processName = Path.GetFileNameWithoutExtension(target);
            var wasAlreadyRunning = !string.IsNullOrEmpty(processName)
                && Process.GetProcessesByName(processName).Length > 0;

            _app = Application.AttachOrLaunch(new ProcessStartInfo(target));
            _weLaunchedApp = !wasAlreadyRunning;

            _mainWindow = _app.GetMainWindow(_automation!, TimeSpan.FromSeconds(15))
                ?? throw new TimeoutException(
                    "Az alkalmazás fut, de a főablak nem jelent meg/nem található időben. " +
                    "Ha az alkalmazásnak több ablaka van, vagy a fő ablak nem azonnal jelenik meg " +
                    "(pl. betöltő/splash képernyő után), próbáld az 'AttachToWindow' lépést a pontos " +
                    "ablakcímmel.");
        }, cancellationToken);
    }

    /// <summary>Csatlakozás egy már futó alkalmazás ablakához cím alapján — a régi StartServiceOnly megfelelője.
    /// Ezt sosem zárjuk be automatikusan (lásd _weLaunchedApp), mert ez a lépés kifejezetten
    /// egy MÁR LÉTEZŐ ablakhoz való csatlakozásra szolgál.</summary>
    public Task AttachToWindowAsync(string windowTitle, CancellationToken cancellationToken = default)
    {
        EnsureAutomationReady();
        return Task.Run(() =>
        {
            var handle = NativeMethods.FindWindow(null, windowTitle);
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException($"Nem található ablak ezzel a címmel: {windowTitle}");

            _app = null; // nem mi indítottuk, ezért nem is mi zárjuk a folyamatot
            _weLaunchedApp = false;
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
        LastStepUsedFallbackLocator = false;

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

        // A TestStep.ElementIndex 1-alapú, emberi számozású (1 = első elem) — itt váltjuk
        // 0-alapú tömb-indexre, amit a TryFind/FindAllDescendants(...)[index] vár.
        var elementIndex = Math.Max(0, (step.ElementIndex ?? 1) - 1);

        // "Self-healing": ha az elsődleges lokátor a Timeout-on belül nem található (a
        // FindElement itt TimeoutException-t dob, nem WebDriverTimeoutException-t, mert
        // a FlaUI-alapú Retry.WhileNull manuálisan dobja), és van megadva tartalék
        // lokátor, azzal próbálkozunk újra, mielőtt hibásnak jelölnénk a lépést.
        AutomationElement Element()
        {
            try
            {
                return FindElement(window, RequireLocator(step.Locator), step.LocatorType, timeout, elementIndex);
            }
            catch (TimeoutException) when (!string.IsNullOrWhiteSpace(step.FallbackLocator))
            {
                var fallbackElement = FindElement(window, step.FallbackLocator!, step.FallbackLocatorType, timeout, elementIndex);
                LastStepUsedFallbackLocator = true;
                return fallbackElement;
            }
        }

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
                RetryOrThrow(() => TryFind(window, RequireLocator(step.Locator), step.LocatorType, elementIndex) is { IsOffscreen: false },
                    timeout, $"Az elem nem lett látható időben: {step.Locator}");
                return null;

            case DesktopStepAction.WaitEnabled:
                RetryOrThrow(() => TryFind(window, RequireLocator(step.Locator), step.LocatorType, elementIndex) is { IsEnabled: true },
                    timeout, $"Az elem nem lett elérhető időben: {step.Locator}");
                return null;

            case DesktopStepAction.WaitClickable:
                RetryOrThrow(() => TryFind(window, RequireLocator(step.Locator), step.LocatorType, elementIndex) is { IsEnabled: true, IsOffscreen: false },
                    timeout, $"Az elem nem lett kattintható időben: {step.Locator}");
                return null;

            case DesktopStepAction.WaitPresent:
                RetryOrThrow(() => TryFind(window, RequireLocator(step.Locator), step.LocatorType, elementIndex) is not null,
                    timeout, $"Az elem nem jelent meg időben: {step.Locator}");
                return null;

            case DesktopStepAction.WaitAbsent:
                RetryOrThrow(() => TryFind(window, RequireLocator(step.Locator), step.LocatorType, elementIndex) is null,
                    timeout, $"Az elem nem tűnt el időben: {step.Locator}");
                return null;

            case DesktopStepAction.WaitSelected:
                RetryOrThrow(() => IsSelected(TryFind(window, RequireLocator(step.Locator), step.LocatorType, elementIndex)),
                    timeout, $"Az elem nem lett kiválasztva időben: {step.Locator}");
                return null;

            case DesktopStepAction.WaitHasText:
                RetryOrThrow(() => (GetPropertyValue(TryFind(window, RequireLocator(step.Locator), step.LocatorType, elementIndex), "Text") ?? string.Empty)
                        .Contains(step.Value ?? string.Empty),
                    timeout, $"Az elem nem kapta meg a várt szöveget: {step.Locator}");
                return null;

            case DesktopStepAction.WaitHasValue:
                RetryOrThrow(() => (GetPropertyValue(TryFind(window, RequireLocator(step.Locator), step.LocatorType, elementIndex), "Value") ?? string.Empty)
                        .Contains(step.Value ?? string.Empty),
                    timeout, $"Az elem nem kapta meg a várt értéket: {step.Locator}");
                return null;

            case DesktopStepAction.WaitHasClass:
                RetryOrThrow(() => (GetPropertyValue(TryFind(window, RequireLocator(step.Locator), step.LocatorType, elementIndex), "ClassName") ?? string.Empty)
                        .Contains(step.Value ?? string.Empty),
                    timeout, $"Az elem class-a nem egyezett időben: {step.Locator}");
                return null;

            case DesktopStepAction.WaitHasAttribute:
                {
                    var (attr, val) = ParseKeyValue(step.Value);
                    RetryOrThrow(() => (GetPropertyValue(TryFind(window, RequireLocator(step.Locator), step.LocatorType, elementIndex), attr) ?? string.Empty)
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

    private static AutomationElement FindElement(Window window, string locator, LocatorType type, TimeSpan timeout, int elementIndex = 0)
    {
        var result = Retry.WhileNull(() => TryFind(window, locator, type, elementIndex), timeout, TimeSpan.FromMilliseconds(250));
        return result.Result ?? throw new TimeoutException(
            $"Az elem nem található: {locator}" + (elementIndex > 0 ? $" ({elementIndex + 1}. találat)" : ""));
    }

    /// <summary>elementIndex esetén az összes találatot lekéri (FindAllDescendants), és a
    /// megadott 0-alapú indexűt adja vissza — XPath-nál ez nem támogatott, mert az XPath
    /// eleve egyetlen, konkrét elemre mutat, nincs értelme "hányadik találat"-ot kérni rá.</summary>
    private static AutomationElement? TryFind(Window window, string locator, LocatorType type, int elementIndex = 0)
    {
        if (type == LocatorType.XPath)
        {
            if (elementIndex > 0)
                throw new NotSupportedException("XPath lokátornál nem támogatott az elem-index — az XPath eleve egyetlen elemre mutat.");
            return window.FindFirstByXPath(locator);
        }

        var matches = type switch
        {
            LocatorType.Id => window.FindAllDescendants(cf => cf.ByAutomationId(locator)),
            LocatorType.Name => window.FindAllDescendants(cf => cf.ByName(locator)),
            LocatorType.ClassName => window.FindAllDescendants(cf => cf.ByClassName(locator)),
            _ => throw new NotSupportedException($"'{type}' lokátor-típus nem támogatott desktop automatizálásnál (Id, Name, ClassName, XPath közül választhatsz).")
        };

        return elementIndex < matches.Length ? matches[elementIndex] : null;
    }

    public void Dispose()
    {
        if (_weLaunchedApp)
        {
            try { _app?.Close(); } catch { /* ignore */ }
        }

        _app?.Dispose();
        _automation?.Dispose();
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        // ===== Felvevő mód: alacsony szintű egér-/billentyűzet-hook =====

        public const int WH_MOUSE_LL = 14;
        public const int WH_KEYBOARD_LL = 13;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_KEYDOWN = 0x0100;
        public const uint VK_RETURN = 0x0D;
        public const uint VK_TAB = 0x09;
        public const uint VK_BACK = 0x08;

        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        public static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        public static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ToUnicodeEx(
            uint wVirtKey, uint wScanCode, byte[] lpKeyState,
            [Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pwszBuff,
            int cchBuff, uint wFlags, IntPtr dwhkl);

        /// <summary>Egy virtuális billentyűkódot ("VK_...") a jelenlegi billentyűzet-
        /// kiosztás és Shift/CapsLock állapot szerint tényleges karakterré alakít — pl.
        /// Shift+'a' VK-ból 'A'-t ad. Null-t ad vissza vezérlő-/funkcióbillentyűknél
        /// (F1-F12, nyilak, stb.), amikhez nincs értelmes karakter-megfelelő.</summary>
        public static char? VirtualKeyToChar(uint vkCode)
        {
            try
            {
                var keyboardState = new byte[256];
                if (!GetKeyboardState(keyboardState))
                    return null;

                var scanCode = MapVirtualKey(vkCode, 0);
                var sb = new System.Text.StringBuilder(4);
                var result = ToUnicodeEx(vkCode, scanCode, keyboardState, sb, sb.Capacity, 0, GetKeyboardLayout(0));

                return result > 0 && sb.Length > 0 ? sb[0] : (char?)null;
            }
            catch
            {
                return null;
            }
        }
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

                var automationId = SafeGet(() => element.AutomationId);
                var name = SafeGet(() => element.Name);
                var className = SafeGet(() => element.ClassName);
                var controlType = SafeGet(() => element.ControlType.ToString());

                var (automationIdIndex, automationIdCount) = ComputeMatchInfo(element, automationId, e => SafeGet(() => e.AutomationId));
                var (nameIndex, nameCount) = ComputeMatchInfo(element, name, e => SafeGet(() => e.Name));
                var (classNameIndex, classNameCount) = ComputeMatchInfo(element, className, e => SafeGet(() => e.ClassName));

                return new DesktopElementNode
                {
                    AutomationId = automationId,
                    Name = name,
                    ClassName = className,
                    ControlType = controlType,
                    AutomationIdMatchIndex = automationIdIndex,
                    AutomationIdMatchCount = automationIdCount,
                    NameMatchIndex = nameIndex,
                    NameMatchCount = nameCount,
                    ClassNameMatchIndex = classNameIndex,
                    ClassNameMatchCount = classNameCount
                };
            }
            catch
            {
                return null;
            }
        }, cancellationToken);
    }

    /// <summary>Megszámolja, hány elem osztozik ugyanazon a tulajdonság-értéken a target
    /// ELEM TÉNYLEGES top-level ablakán belül, és hányadik a talált (target) elem
    /// közöttük. FONTOS: szándékosan NEM a driver _mainWindow mezőjét használja
    /// keresési körnek, hanem a target szülőláncán felfelé sétálva megkeresi a
    /// ténylegesen őt tartalmazó Window-t — enélkül több-ablakos vagy lapozott
    /// alkalmazásoknál (pl. Windows Beállítások, ahol navigáció közben új top-level
    /// ablak jöhet létre, vagy a tartalom más ablakba kerül) a _mainWindow elavulhat,
    /// és a keresés hamisan 0 találatot adna vissza olyan elemeknél is, amikből
    /// ténylegesen több van a képernyőn. Az Index 1-alapú, emberi számozású
    /// (1 = első találat).</summary>
    private (int Index, int Count) ComputeMatchInfo(AutomationElement target, string value, Func<AutomationElement, string> selector)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (0, 0);

        try
        {
            var root = FindTopLevelWindow(target) ?? _mainWindow;
            if (root is null)
                return (0, 0);

            var all = root.FindAllDescendants();
            var matches = all.Where(e => selector(e) == value).ToArray();
            var zeroBasedIndex = Array.FindIndex(matches, e => e.Equals(target));
            return (Math.Max(0, zeroBasedIndex) + 1, matches.Length);
        }
        catch
        {
            return (0, 0);
        }
    }

    /// <summary>Felfelé sétál a target szülőláncán, amíg egy Window ControlType-ú elemet
    /// nem talál — ez a target elemet TÉNYLEGESEN tartalmazó, aktuális top-level ablak.
    /// Null-t ad vissza, ha valamiért nem sikerül (pl. az elem közben eltűnt) — ilyenkor
    /// a hívó a driver _mainWindow mezőjére esik vissza, tartalék megoldásként.</summary>
    private static Window? FindTopLevelWindow(AutomationElement element)
    {
        try
        {
            var current = element;
            while (current is not null)
            {
                if (current.ControlType == FlaUI.Core.Definitions.ControlType.Window)
                    return current.AsWindow();

                current = current.Parent;
            }
        }
        catch
        {
            // ha a szülőlánc bejárása közben bármi hibázik (pl. az elem közben
            // eltűnt/invalidálódott), csendben null-t adunk vissza — a hívó tud
            // ezzel mit kezdeni (visszaesik _mainWindow-ra).
        }

        return null;
    }

    private static string SafeGet(Func<string?> getter)
    {
        try { return getter() ?? ""; }
        catch { return ""; }
    }

}