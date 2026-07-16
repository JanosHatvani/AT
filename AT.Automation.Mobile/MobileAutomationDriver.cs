using System;
using System.Linq;
using AT.Core.Contracts;
using AT.Core.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.Interactions;
using OpenQA.Selenium.Appium.Service;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using System.Diagnostics;

namespace AT.Automation.Mobile;

/// <summary>
/// Appium (UiAutomator2) alapú Android automatizálás. A régi kódban ez csak
/// részben volt kész (emulátor-indítás + pár alap metódus) — ez fejezi be:
/// teljes lépéskészlet, és a korábban törött élő kijelző-tükrözés valódi alapja
/// (GetScreenshotAsync ténylegesen visszaad egy PNG-t, amit a UI ki tud rajzolni).
/// A StartAsync az Appium szervert indítja el helyben (AppiumLocalService) — nem
/// kell külön, kézzel elindított "appium" parancssori folyamat, mint régen.
/// </summary>
public sealed class MobileAutomationDriver : IAutomationDriver, IDisposable
{
    private AppiumLocalService? _appiumService;
    private AndroidDriver? _driver;
    private Process? _emulatorProcess;

    public string PlatformName => "Mobile (Android)";

    /// <summary>A Beállítások oldalról jövő Android SDK-gyökér felülírása - ha üres, a környezeti változóra esik vissza.</summary>
    public string? SdkRootOverride { get; set; }

    public bool IsRunning => _driver is not null;

    // ===================== ÉLETCIKLUS =====================

    /// <summary>
    /// A becsomagolt, önálló Node.js + Appium runtime várt helye a kimeneti mappához
    /// képest (lásd AT.App.csproj: az AT.AppiumRuntime tartalma ide másolódik build
    /// közben). Ha ez a mappa hiányzik (pl. fejlesztői gépen még nem futtatták le az
    /// npm install-t az AT.AppiumRuntime mappában), a StartAsync visszaesik a rendszer
    /// PATH-jára — így a régi, kézzel telepített Node.js/Appium-mal dolgozó fejlesztői
    /// gépek is tovább működnek.
    /// </summary>
    private static readonly string BundledRuntimeRoot =
        Path.Combine(AppContext.BaseDirectory, "AppiumRuntime");

    private static readonly string BundledNodePath =
        Path.Combine(BundledRuntimeRoot, "node", "node.exe");

    private static readonly string BundledAppiumMainJsPath =
        Path.Combine(BundledRuntimeRoot, "node_modules", "appium", "build", "lib", "main.js");

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_appiumService is not null)
            return Task.CompletedTask;

        return Task.Run(() =>
        {
            // Az Appium driver-nyilvántartása (extensions.yml) a becsomagolt
            // runtime mappájában él, nem a felhasználó globális %APPDATA%\.appium
            // mappájában — így a végfelhasználónak sosem kell driver-t telepítenie.
            Environment.SetEnvironmentVariable("APPIUM_HOME", BundledRuntimeRoot);

            // A UiAutomator2 driver saját (Node.js-oldali) folyamatában olvassa ki
            // ezeket — teljesen független attól, hogy a C# kód honnan tudja az SDK
            // útvonalát. Enélkül a driver "Neither ANDROID_HOME nor ANDROID_SDK_ROOT..."
            // hibával elszáll, még akkor is, ha az SdkRootOverride be van állítva.
            var sdkRoot = AndroidSdkLocator.ResolveSdkRoot(SdkRootOverride);
            Environment.SetEnvironmentVariable("ANDROID_HOME", sdkRoot);
            Environment.SetEnvironmentVariable("ANDROID_SDK_ROOT", sdkRoot);

            var builder = new AppiumServiceBuilder()
                .WithIPAddress("127.0.0.1")
                .UsingAnyFreePort();

            if (File.Exists(BundledNodePath) && File.Exists(BundledAppiumMainJsPath))
            {
                builder = builder
                    .UsingDriverExecutable(new FileInfo(BundledNodePath))
                    .WithAppiumJS(new FileInfo(BundledAppiumMainJsPath));
            }

            _appiumService = builder.Build();
            _appiumService.Start();
        }, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        var driver = _driver;
        var service = _appiumService;
        _driver = null;
        _appiumService = null;

        return Task.Run(() =>
        {
            try { driver?.Quit(); } catch { /* ignore */ }
            driver?.Dispose();
            service?.Dispose();
        }, cancellationToken);
    }

    /// <summary>Android emulátor indítása AVD név alapján, várakozás amíg teljesen elindul — a "StartEmulator" lépés hívja.</summary>
    public Task StartEmulatorAsync(string avdName, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var emulatorPath = AndroidSdkLocator.ResolveEmulatorPath(SdkRootOverride);
            var adbPath = AndroidSdkLocator.ResolveAdbPath(SdkRootOverride);

            _emulatorProcess = Process.Start(new ProcessStartInfo
            {
                FileName = emulatorPath,
                Arguments = $"-avd \"{avdName}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            }) ?? throw new InvalidOperationException("Nem sikerült elindítani az emulátor folyamatot.");

            WaitForDevice(adbPath, TimeSpan.FromSeconds(120), cancellationToken);
            WaitForBootCompleted(adbPath, TimeSpan.FromSeconds(120), cancellationToken);
        }, cancellationToken);
    }

    /// <summary>APK telepítése (ha kell) és az alkalmazás indítása — az "LaunchApp" lépés hívja.</summary>

    public Task LaunchAppAsync(string apkPath, CancellationToken cancellationToken = default)
    {
        if (_appiumService is null)
            throw new InvalidOperationException("Az Appium szerver nincs elindítva — hívd meg előbb a StartAsync-et.");

        return Task.Run(() =>
        {
            if (!File.Exists(apkPath))
                throw new FileNotFoundException("Nem található az APK a megadott elérési úton.", apkPath);

            var options = new AppiumOptions
            {
                PlatformName = "Android",
                AutomationName = "UiAutomator2",
                App = apkPath
            };
            options.AddAdditionalAppiumOption("noReset", false);

            try
            {
                _driver = new AndroidDriver(_appiumService!.ServiceUrl, options, TimeSpan.FromSeconds(90));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(TranslateLaunchError(ex.Message, apkPath), ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// A UiAutomator2/adb néhány gyakori, kriptikus hibaüzenetét emberi, magyar nyelvű,
    /// tettre-ösztönző szöveggé fordítja — a nyers eredeti üzenet az InnerException-ben
    /// (és a naplóban) megmarad, csak a felhasználó felé megjelenő szöveg lesz barátságosabb.
    /// </summary>
    private static string TranslateLaunchError(string rawMessage, string apkPath)
    {
        if (rawMessage.Contains("INSTALL_FAILED_NO_MATCHING_ABIS"))
        {
            return "Az APK nem telepíthető erre a telefonra, mert nem a telefon processzor-" +
                   "architektúrájához (ARM: arm64-v8a / armeabi-v7a) készült — valószínűleg egy " +
                   "x86/x86_64 emulátorhoz buildelt APK-t próbáltál valódi telefonra telepíteni. " +
                   "Kérj vagy buildelj egy ARM-kompatibilis APK-t (a build.gradle 'abiFilters' vagy " +
                   $"'splits.abi' beállítását kell bővíteni), majd próbáld újra.{Environment.NewLine}" +
                   $"Fájl: {apkPath}";
        }

        if (rawMessage.Contains("device unauthorized") || rawMessage.Contains("device 'unauthorized'"))
        {
            return "A telefon csatlakozva van, de nincs elfogadva rajta az USB hibakeresési " +
                   "engedélykérés. Nézd meg a telefon képernyőjét, fogadd el a felugró ablakot " +
                   "(pipáld be a \"Mindig engedélyezés erről a gépről\" opciót is), majd próbáld újra.";
        }

        if (rawMessage.Contains("INSTALL_FAILED_INSUFFICIENT_STORAGE"))
        {
            return "Nincs elég szabad tárhely a telefonon az alkalmazás telepítéséhez. " +
                   "Szabadíts fel helyet, majd próbáld újra.";
        }

        if (rawMessage.Contains("INSTALL_FAILED_VERSION_DOWNGRADE"))
        {
            return "A telefonon már egy újabb verziója van telepítve ennek az alkalmazásnak, " +
                   "mint amit most telepíteni próbálsz. Előbb távolítsd el a régebbi verziót a " +
                   "telefonról, vagy állítsd be a lépésnél a \"noReset\"/újratelepítést engedélyező " +
                   "opciót, majd próbáld újra.";
        }

        if (rawMessage.Contains("INSTALL_FAILED_UPDATE_INCOMPATIBLE") || rawMessage.Contains("signatures do not match"))
        {
            return "A telefonon már telepítve van ez az alkalmazás, de más aláírással (más " +
                   "kulccsal) lett buildelve, mint a most telepíteni kívánt APK. Távolítsd el " +
                   "kézzel a telefonról a meglévő alkalmazást, majd próbáld újra.";
        }

        if (rawMessage.Contains("Could not find a driver"))
        {
            return "Az Appium UiAutomator2 driver nincs telepítve a becsomagolt runtime-hoz. " +
                   "Ez telepítési/csomagolási hiba, nem a teszt-lépés problémája — jelezd a " +
                   "fejlesztőnek.";
        }

        if (rawMessage.Contains("Could not find 'aapt2.exe'") || rawMessage.Contains("aapt2"))
        {
            return "Hiányoznak az Android Build Tools az SDK-ból (aapt2.exe nem található). " +
                   "Ez telepítési hiba — jelezd a fejlesztőnek, hogy az Android SDK telepítője " +
                   "nem futtatta le a build-tools telepítését.";
        }

        if (rawMessage.Contains("Neither ANDROID_HOME nor ANDROID_SDK_ROOT"))
        {
            return "A program nem találja az Android SDK útvonalát a háttérben futó Appium " +
                   "folyamat számára. Ellenőrizd a Beállítások oldalon az Android SDK " +
                   "gyökérmappáját.";
        }

        // Ismeretlen hiba — az eredeti nyers üzenetet adjuk tovább, hogy semmi ne vesszen el.
        return $"Az alkalmazás indítása sikertelen: {rawMessage}";
    }

    /// <summary>Az IAutomationDriver szerződés kötelező tagja — a mobil modulban ez a LaunchApp-ra delegál.</summary>
    public Task NavigateAsync(string target, CancellationToken cancellationToken = default)
        => LaunchAppAsync(target, cancellationToken);

    public Task ClickAsync(string locator, LocatorType locatorType, CancellationToken cancellationToken = default)
        => RunOnDriver(d => FindElement(d, locator, locatorType, DefaultTimeout).Click(), cancellationToken);

    public Task SendKeysAsync(string locator, LocatorType locatorType, string text, CancellationToken cancellationToken = default)
        => RunOnDriver(d => FindElement(d, locator, locatorType, DefaultTimeout).SendKeys(text), cancellationToken);

    public Task<byte[]> GetScreenshotAsync(CancellationToken cancellationToken = default)
        => RunOnDriver(d => ((ITakesScreenshot)d).GetScreenshot().AsByteArray, cancellationToken);

    /// <summary>Ugyanaz, csak nem dobja el a session-t, ha épp nem fut — az élő kijelző-tükrözés ezt hívja csendben.</summary>
    public async Task<byte[]?> TryGetScreenshotAsync(CancellationToken cancellationToken = default)
    {
        if (_driver is null)
            return null;

        try
        {
            return await GetScreenshotAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Az élő kijelző-tükrözés képére kattintva hívjuk: a relatív (0..1) koordinátát a
    /// tényleges eszköz-felbontásra vetíti, és a PageSource XML-jéből (Appium UiAutomator2
    /// dump, minden elemhez van "bounds" attribútum) megkeresi a legkisebb, a pontot
    /// tartalmazó elemet — ez a mobil megfelelője a desktopos FromPoint-nak.
    /// </summary>
    public Task<MobileElementInfo?> GetElementAtRelativePointAsync(double relativeX, double relativeY, CancellationToken cancellationToken = default)
    {
        if (_driver is null)
            return Task.FromResult<MobileElementInfo?>(null);

        return Task.Run(() =>
        {
            try
            {
                var size = _driver.Manage().Window.Size;
                var targetX = (int)(relativeX * size.Width);
                var targetY = (int)(relativeY * size.Height);

                var pageSource = _driver.PageSource;
                var doc = System.Xml.Linq.XDocument.Parse(pageSource);

                System.Xml.Linq.XElement? best = null;
                long bestArea = long.MaxValue;

                foreach (var el in doc.Descendants())
                {
                    var boundsAttr = el.Attribute("bounds")?.Value;
                    if (string.IsNullOrEmpty(boundsAttr) || !TryParseBounds(boundsAttr, out var rect))
                        continue;

                    if (targetX < rect.X1 || targetX > rect.X2 || targetY < rect.Y1 || targetY > rect.Y2)
                        continue;

                    var area = (long)(rect.X2 - rect.X1) * (rect.Y2 - rect.Y1);
                    if (area < bestArea)
                    {
                        bestArea = area;
                        best = el;
                    }
                }

                if (best is null)
                    return null;

                return new MobileElementInfo
                {
                    ResourceId = best.Attribute("resource-id")?.Value ?? "",
                    ContentDesc = best.Attribute("content-desc")?.Value ?? "",
                    ClassName = best.Attribute("class")?.Value ?? "",
                    Text = best.Attribute("text")?.Value ?? ""
                };
            }
            catch
            {
                return null;
            }
        }, cancellationToken);
    }

    /// <summary>"[x1,y1][x2,y2]" formátumú UiAutomator2 bounds string feldolgozása.</summary>
    private static bool TryParseBounds(string bounds, out (int X1, int Y1, int X2, int Y2) rect)
    {
        rect = default;

        var parts = bounds.Trim('[', ']').Split(new[] { "][" }, StringSplitOptions.None);
        if (parts.Length != 2)
            return false;

        var p1 = parts[0].Split(',');
        var p2 = parts[1].Split(',');
        if (p1.Length != 2 || p2.Length != 2)
            return false;

        if (!int.TryParse(p1[0], out var x1) || !int.TryParse(p1[1], out var y1))
            return false;
        if (!int.TryParse(p2[0], out var x2) || !int.TryParse(p2[1], out var y2))
            return false;

        rect = (x1, y1, x2, y2);
        return true;
    }

    // ===================== TELJES LÉPÉS-VÉGREHAJTÁS =====================

    public Task<string?> ExecuteStepAsync(TestStep step, CancellationToken cancellationToken = default)
    {
        var action = Enum.Parse<MobileStepAction>(step.Action);

        //if (action == MobileStepAction.StartEmulator)
        //    return StartEmulatorAsync(step.Value ?? string.Empty, cancellationToken).ContinueWith(_ => (string?)null, cancellationToken);

        if (action == MobileStepAction.LaunchApp)
            return LaunchAppAsync(step.Value ?? string.Empty, cancellationToken)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        throw t.Exception!.GetBaseException();
                    return (string?)null;
                }, cancellationToken);

        if (action == MobileStepAction.StopEmulator)
            return Task.Run(() => { StopEmulatorProcess(); return (string?)null; }, cancellationToken);

        EnsureSessionActive();
        return Task.Run(() => ExecuteStepCore(step, action), cancellationToken);
    }

    private string? ExecuteStepCore(TestStep step, MobileStepAction action)
    {
        var driver = _driver!;
        var timeout = TimeSpan.FromSeconds(Math.Max(1, step.TimeoutSeconds));

        IWebElement Element() => FindElement(driver, RequireLocator(step.Locator), step.LocatorType, timeout);
        WebDriverWait Wait() => new(driver, timeout);

        switch (action)
        {
            case MobileStepAction.Click:
                Element().Click();
                return null;

            case MobileStepAction.LongPress:
                LongPress(driver, Element());
                return null;

            case MobileStepAction.SendKeys:
                Element().SendKeys(step.Value ?? string.Empty);
                return null;

            case MobileStepAction.Clear:
                Element().Clear();
                return null;

            case MobileStepAction.Swipe:
                Swipe(driver, step.Value ?? "Up");
                return null;

            case MobileStepAction.ScrollToElement:
                ScrollToElement(driver, RequireLocator(step.Locator), step.LocatorType, timeout);
                return null;

            case MobileStepAction.ReadAttribute:
                return Element().GetAttribute(RequireLocator(step.Value));

            case MobileStepAction.Wait:
                Thread.Sleep(timeout);
                return null;

            case MobileStepAction.WaitVisible:
                Wait().Until(_ => Element().Displayed);
                return null;

            case MobileStepAction.WaitPresent:
                Wait().Until(d => d.FindElements(ToBy(RequireLocator(step.Locator), step.LocatorType)).Count > 0);
                return null;

            case MobileStepAction.WaitAbsent:
                Wait().Until(d => d.FindElements(ToBy(RequireLocator(step.Locator), step.LocatorType)).Count == 0);
                return null;

            case MobileStepAction.WaitHasText:
                Wait().Until(_ => Element().Text.Contains(step.Value ?? string.Empty));
                return null;

            case MobileStepAction.WaitHasAttribute:
                {
                    var (attr, val) = ParseKeyValue(step.Value);
                    Wait().Until(_ => (Element().GetAttribute(attr) ?? string.Empty).Contains(val));
                    return null;
                }

            case MobileStepAction.Close:
                driver.Quit();
                _driver = null;
                return null;

            default:
                throw new NotSupportedException($"Ismeretlen művelet: {action}");
        }
    }

    // ===================== GESZTUSOK (W3C Actions - stabil, nem "TouchAction") =====================

    private static void LongPress(AndroidDriver driver, IWebElement element)
    {
        var center = new System.Drawing.Point(
            element.Location.X + element.Size.Width / 2,
            element.Location.Y + element.Size.Height / 2);

        var touch = new OpenQA.Selenium.Interactions.PointerInputDevice(PointerKind.Touch);
        var seq = new ActionSequence(touch, 0);
        seq.AddAction(touch.CreatePointerMove(CoordinateOrigin.Viewport, center.X, center.Y, TimeSpan.Zero));
        seq.AddAction(touch.CreatePointerDown(OpenQA.Selenium.Interactions.MouseButton.Left));
        seq.AddAction(touch.CreatePause(TimeSpan.FromMilliseconds(800)));
        seq.AddAction(touch.CreatePointerUp(OpenQA.Selenium.Interactions.MouseButton.Left));
        driver.PerformActions(new List<ActionSequence> { seq });
    }

    private static void Swipe(AndroidDriver driver, string direction)
    {
        var size = driver.Manage().Window.Size;
        int startX, startY, endX, endY;

        switch (direction.Trim().ToLowerInvariant())
        {
            case "down":
            case "le":
                startX = endX = size.Width / 2;
                startY = (int)(size.Height * 0.2);
                endY = (int)(size.Height * 0.8);
                break;
            case "left":
            case "balra":
                startY = endY = size.Height / 2;
                startX = (int)(size.Width * 0.8);
                endX = (int)(size.Width * 0.2);
                break;
            case "right":
            case "jobbra":
                startY = endY = size.Height / 2;
                startX = (int)(size.Width * 0.2);
                endX = (int)(size.Width * 0.8);
                break;
            case "up":
            case "fel":
            default:
                startX = endX = size.Width / 2;
                startY = (int)(size.Height * 0.8);
                endY = (int)(size.Height * 0.2);
                break;
        }

        var touch = new OpenQA.Selenium.Interactions.PointerInputDevice(PointerKind.Touch);
        var seq = new ActionSequence(touch, 0);
        seq.AddAction(touch.CreatePointerMove(CoordinateOrigin.Viewport, startX, startY, TimeSpan.Zero));
        seq.AddAction(touch.CreatePointerDown(OpenQA.Selenium.Interactions.MouseButton.Left));
        seq.AddAction(touch.CreatePointerMove(CoordinateOrigin.Viewport, (startX + endX) / 2, (startY + endY) / 2, TimeSpan.FromMilliseconds(80)));
        seq.AddAction(touch.CreatePointerMove(CoordinateOrigin.Viewport, endX, endY, TimeSpan.FromMilliseconds(80)));
        seq.AddAction(touch.CreatePointerUp(OpenQA.Selenium.Interactions.MouseButton.Left));
        driver.PerformActions(new List<ActionSequence> { seq });
    }

    private static void ScrollToElement(AndroidDriver driver, string locator, LocatorType type, TimeSpan timeout)
    {
        var by = ToBy(locator, type);
        const int maxAttempts = 8;

        for (var i = 0; i < maxAttempts; i++)
        {
            if (driver.FindElements(by).Count > 0)
                return;

            Swipe(driver, "Up");
            Thread.Sleep(300);
        }

        throw new TimeoutException($"Az elem nem található görgetés után sem: {locator}");
    }

    // ===================== EMULÁTOR / ADB SEGÉDLET =====================

    private static void WaitForDevice(string adbPath, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var output = RunAdb(adbPath, "devices");
            if (output.Contains("\tdevice"))
                return;
            Thread.Sleep(1000);
        }
        throw new TimeoutException("Az emulátor nem jelent meg az adb devices listában időben.");
    }

    private static void WaitForBootCompleted(string adbPath, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var output = RunAdb(adbPath, "shell getprop sys.boot_completed");
            if (output.Trim() == "1")
                return;
            Thread.Sleep(1500);
        }
        throw new TimeoutException("Az emulátor nem fejezte be a bootolást időben.");
    }

    private static string RunAdb(string adbPath, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = adbPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process is null)
            return string.Empty;

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    // ===================== ESZKÖZ-ÁLLAPOT (Mobil nézet eszköz-sávja) =====================

    /// <summary>
    /// Lekérdezi az ADB-n keresztül látott, elsőként csatlakoztatott eszköz állapotát és
    /// (ha elérhető) a hardver-modell nevét. A Mobil nézet "Csatlakoztatva: Samsung Galaxy S23"
    /// jellegű állapot-sávja hívja, kézi Frissítés gombra vagy a nézet betöltésekor — NEM
    /// automatikusan, időzítve, hogy ne fusson feleslegesen a háttérben.
    ///
    /// Ez a metódus független az aktív Appium session-től (_driver-től) — pusztán az ADB-t
    /// hívja közvetlenül, ezért akkor is működik, ha még nincs elindítva a StartAsync/
    /// LaunchApp (vagyis ellenőrizni lehet vele a fizikai kapcsolatot, mielőtt egyáltalán
    /// elindítanál egy tesztet).
    /// </summary>
    public Task<MobileDeviceInfo> GetConnectedDeviceInfoAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            string adbPath;
            try
            {
                adbPath = AndroidSdkLocator.ResolveAdbPath(SdkRootOverride);
            }
            catch (InvalidOperationException)
            {
                // Nincs elérhető adb.exe (SDK hiányzik/hiányos) — ez nem hiba ebben a
                // kontextusban, egyszerűen "nincs csatlakoztatott eszköz" állapotot jelent,
                // a felhasználó úgyis kap külön figyelmeztetést a hiányzó SDK-ról.
                return new MobileDeviceInfo { IsConnected = false };
            }

            var devicesOutput = RunAdb(adbPath, "devices");

            // Az "adb devices" kimenete soronként: "<serial>\t<state>", az első sor egy
            // fejléc ("List of devices attached"), amit át kell ugrani.
            var deviceLine = devicesOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Skip(1)
                .FirstOrDefault(line => line.Contains('\t'));

            if (deviceLine is null)
                return new MobileDeviceInfo { IsConnected = false };

            var parts = deviceLine.Split('\t', StringSplitOptions.TrimEntries);
            var serialNumber = parts[0];
            var state = parts.Length > 1 ? parts[1] : "";

            if (state != "device")
            {
                // "unauthorized" (az USB-hibakeresési engedélykérés még nincs elfogadva a
                // telefonon) vagy "offline" — látszik az eszköz, de nem használható.
                return new MobileDeviceInfo
                {
                    IsConnected = false,
                    IsUnauthorizedOrOffline = true,
                    SerialNumber = serialNumber
                };
            }

            var deviceModel = RunAdb(adbPath, $"-s {serialNumber} shell getprop ro.product.model").Trim();

            return new MobileDeviceInfo
            {
                IsConnected = true,
                SerialNumber = serialNumber,
                DeviceModel = string.IsNullOrWhiteSpace(deviceModel) ? null : deviceModel
            };
        }, cancellationToken);
    }

    private void StopEmulatorProcess()
    {
        try
        {
            if (_emulatorProcess is { HasExited: false })
            {
                _emulatorProcess.Kill();
                _emulatorProcess.WaitForExit();
            }
        }
        catch { /* ha már leállt, nem gond */ }
        finally
        {
            _emulatorProcess = null;
        }
    }

    // ===================== SEGÉDMETÓDUSOK =====================

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private Task RunOnDriver(Action<AndroidDriver> action, CancellationToken cancellationToken)
    {
        EnsureSessionActive();
        return Task.Run(() => action(_driver!), cancellationToken);
    }

    private Task<T> RunOnDriver<T>(Func<AndroidDriver, T> func, CancellationToken cancellationToken)
    {
        EnsureSessionActive();
        return Task.Run(() => func(_driver!), cancellationToken);
    }

    private void EnsureSessionActive()
    {
        if (_driver is null)
            throw new InvalidOperationException("Nincs aktív alkalmazás-session — vegyél fel előbb egy 'LaunchApp' lépést.");
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
            throw new ArgumentException("Az Érték mezőben 'attribútum=érték' formátum szükséges, pl. checked=true.");

        var idx = raw.IndexOf('=');
        return (raw[..idx].Trim(), raw[(idx + 1)..].Trim());
    }

    private static IWebElement FindElement(AndroidDriver driver, string locator, LocatorType type, TimeSpan timeout)
    {
        var wait = new WebDriverWait(driver, timeout);
        return wait.Until(d => d.FindElement(ToBy(locator, type)));
    }

    private static By ToBy(string locator, LocatorType type) => type switch
    {
        LocatorType.Id => By.Id(locator),
        LocatorType.XPath => By.XPath(locator),
        LocatorType.ClassName => By.ClassName(locator),
        LocatorType.Name => By.Name(locator),
        LocatorType.AccessibilityId => OpenQA.Selenium.Appium.MobileBy.AccessibilityId(locator),
        _ => throw new NotSupportedException($"'{type}' lokátor-típus nem támogatott mobil automatizálásnál (Id, XPath, ClassName, Name, AccessibilityId közül választhatsz).")
    };

    public void Dispose()
    {
        try { _driver?.Quit(); } catch { /* ignore */ }
        _driver?.Dispose();
        _appiumService?.Dispose();
        StopEmulatorProcess();
    }
}
