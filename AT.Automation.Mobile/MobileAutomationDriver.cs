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

    /// <summary>Igaz, ha a LEGUTÓBB végrehajtott lépésnél az elsődleges lokátor nem volt
    /// megtalálható, és a driver a tartalék (FallbackLocator) lokátorral tudta csak
    /// megtalálni az elemet — a ViewModel ezt ellenőrzi minden sikeres ExecuteStepAsync
    /// után, hogy figyelmeztető üzenetet mutasson ("frissítsd az elsődleges lokátort").
    /// Minden ExecuteStepAsync hívás elején false-ra áll vissza.</summary>
    public bool LastStepUsedFallbackLocator { get; private set; }

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
                // Becsomagolt runtime használata — a végfelhasználónak nem kell
                // semmit telepítenie, teljesen önállóan működik.
                builder = builder
                    .UsingDriverExecutable(new FileInfo(BundledNodePath))
                    .WithAppiumJS(new FileInfo(BundledAppiumMainJsPath));
            }
            // Ha a becsomagolt runtime hiányzik, a builder a rendszer PATH-ján
            // keresi a node-ot és a globálisan telepített appium-ot (a korábbi,
            // "telepítsd magad" viselkedés) — ez a fallback fejlesztői gépeken hasznos.

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

            _driver = new AndroidDriver(_appiumService!.ServiceUrl, options, TimeSpan.FromSeconds(90));
        }, cancellationToken);
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
    /// Képernyőkép közvetlenül adb-vel ("adb exec-out screencap -p") — NEM igényel Appium-
    /// session-t, ezért már LaunchApp ELŐTT is meghívható, amint a telefon "device"
    /// állapotban van. A élő kijelző ezt hívja, amíg nincs aktív session; utána átvált a
    /// TryGetScreenshotAsync-re (Appium), ami session közben pontosabb/gyorsabb, mert nem
    /// indít külön folyamatot minden képkockához.
    /// </summary>
    public async Task<byte[]?> TryGetScreenshotViaAdbAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var adbPath = AndroidSdkLocator.ResolveAdbPath(SdkRootOverride);
            return await RunAdbBinaryAsync(adbPath, "exec-out screencap -p", cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Az élő kijelző képére kattintva hívjuk: a relatív (0..1) koordinátát a
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

                return BuildElementInfo(doc.Descendants().ToList(), best);
            }
            catch
            {
                return null;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Ugyanaz, mint a GetElementAtRelativePointAsync, csak közvetlenül adb-vel (nem
    /// Appium-session-en keresztül) — LaunchApp előtt is használható. Az "adb shell
    /// uiautomator dump" ugyanazt a UI-hierarchia XML-t állítja elő, amit a UiAutomator2
    /// driver PageSource-ja is ad, ezért a lokátor-kinyerési logika (TryParseBounds,
    /// legkisebb-találat-keresés, BuildElementInfo) újrahasználható változtatás nélkül.
    /// </summary>
    public async Task<MobileElementInfo?> GetElementAtRelativePointViaAdbAsync(double relativeX, double relativeY, CancellationToken cancellationToken = default)
    {
        const string deviceDumpPath = "/sdcard/at_studio_window_dump.xml";
        string? adbPath = null;

        try
        {
            adbPath = AndroidSdkLocator.ResolveAdbPath(SdkRootOverride);

            var (width, height) = await ResolveDeviceScreenSizeAsync(adbPath, cancellationToken);
            if (width <= 0 || height <= 0)
                return null;

            var targetX = (int)(relativeX * width);
            var targetY = (int)(relativeY * height);

            await RunAdbAsync(adbPath, $"shell uiautomator dump {deviceDumpPath}", cancellationToken);

            var localTempPath = Path.Combine(Path.GetTempPath(), "at-studio-window-dump.xml");
            await RunAdbAsync(adbPath, $"pull {deviceDumpPath} \"{localTempPath}\"", cancellationToken);

            if (!File.Exists(localTempPath))
                return null;

            var xml = await File.ReadAllTextAsync(localTempPath, cancellationToken);
            try { File.Delete(localTempPath); } catch { /* ignore */ }

            var doc = System.Xml.Linq.XDocument.Parse(xml);

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

            return BuildElementInfo(doc.Descendants().ToList(), best);
        }
        catch
        {
            return null;
        }
        finally
        {
            // A telefonon hagyott dump-fájlt minden esetben töröljük — hiba esetén is
            // (pl. ha a dump/pull sikerült, de az XML-parse hibázott), hogy ne maradjon
            // szemét a /sdcard-on. Csendben hagyjuk, ha ez a törlés maga hibázna (pl.
            // mert menet közben megszakadt a kapcsolat) — ez nem kritikus mellékhatás.
            if (adbPath is not null)
            {
                try { await RunAdbAsync(adbPath, $"shell rm {deviceDumpPath}", cancellationToken); }
                catch { /* ignore */ }
            }
        }
    }

    /// <summary>A "best" elemből épít egy MobileElementInfo-t, kitöltve a MatchIndex/MatchCount
    /// mezőket is — megszámolja, hány elemnek van ugyanolyan resource-id/content-desc/class
    /// attribútuma, és hányadik a "best" közöttük. A MatchIndex 1-alapú, emberi számozású
    /// (1 = első találat), hogy a felhasználó felé megjelenő szám és a mezőbe beírandó
    /// érték ugyanaz legyen, amit ténylegesen a lépésbe kell írni. FONTOS: minden
    /// attribútum-találati listát a "domináns package" heurisztikával szűkítünk —
    /// megnézzük, a nyers találatok közül melyik "package" attribútum-érték fordul elő
    /// a legtöbbször, és csak azokat vesszük figyelembe. Ez azért megbízhatóbb, mint egy
    /// előre kiolvasott csomagnévhez hasonlítani, mert MAUI/Android debug build-eknél
    /// gyakori, hogy a ténylegesen futó applicationId ELTÉR a manifestből kiolvasható
    /// névtől (pl. ".debug" utótag miatt) — egy pontos-egyezés szűrés emiatt tévesen
    /// KIZÁRHATNÁ magát a saját alkalmazást is. A domináns-package heurisztika ehelyett
    /// magukból az élő találatokból dolgozik: a rendszerszintű elemek (pl. virtuális
    /// billentyűzet saját mezői) szinte biztos kisebbségben vannak a saját alkalmazás
    /// elemeihez képest, ezért a "melyik fordul elő többször" megbízhatóan kiszűri őket.</summary>
    private MobileElementInfo BuildElementInfo(System.Collections.Generic.List<System.Xml.Linq.XElement> allElements, System.Xml.Linq.XElement best)
    {
        (int Index, int Count) MatchInfo(string attributeName, string value)
        {
            if (string.IsNullOrEmpty(value))
                return (0, 0);

            var rawMatches = allElements.Where(e => e.Attribute(attributeName)?.Value == value).ToList();
            var scopedMatches = FilterToDominantPackage(rawMatches, e => e.Attribute("package")?.Value);
            var zeroBasedIndex = scopedMatches.FindIndex(e => ReferenceEquals(e, best));
            return (Math.Max(0, zeroBasedIndex) + 1, scopedMatches.Count);
        }

        var resourceId = best.Attribute("resource-id")?.Value ?? "";
        var contentDesc = best.Attribute("content-desc")?.Value ?? "";
        var className = best.Attribute("class")?.Value ?? "";

        var (resIdx, resCount) = MatchInfo("resource-id", resourceId);
        var (descIdx, descCount) = MatchInfo("content-desc", contentDesc);
        var (clsIdx, clsCount) = MatchInfo("class", className);

        return new MobileElementInfo
        {
            ResourceId = resourceId,
            ContentDesc = contentDesc,
            ClassName = className,
            Text = best.Attribute("text")?.Value ?? "",
            ResourceIdMatchIndex = resIdx,
            ResourceIdMatchCount = resCount,
            ContentDescMatchIndex = descIdx,
            ContentDescMatchCount = descCount,
            ClassNameMatchIndex = clsIdx,
            ClassNameMatchCount = clsCount
        };
    }

    /// <summary>Egy nyers találati listát szűkít le a "domináns" (leggyakoribb) package-
    /// csoportra — lásd BuildElementInfo doksi a heurisztika indoklásáért. Ha legfeljebb
    /// 1 elem van, nincs mit szűrni, azt adjuk vissza változatlanul.</summary>
    private static System.Collections.Generic.List<System.Xml.Linq.XElement> FilterToDominantPackage(
        System.Collections.Generic.List<System.Xml.Linq.XElement> elements,
        Func<System.Xml.Linq.XElement, string?> packageSelector)
    {
        if (elements.Count <= 1)
            return elements;

        var dominantPackage = elements
            .GroupBy(packageSelector)
            .OrderByDescending(g => g.Count())
            .First().Key;

        return elements.Where(e => packageSelector(e) == dominantPackage).ToList();
    }

    /// <summary>"adb shell wm size" kimenetét dolgozza fel — jellemzően "Physical size: 1080x2400"
    /// formátumú (esetenként két sor is lehet, "Override size" felülbírálhatja — azt részesítjük
    /// előnyben, ha van, mert az tükrözi a ténylegesen aktív felbontást).</summary>
    private static async Task<(int Width, int Height)> ResolveDeviceScreenSizeAsync(string adbPath, CancellationToken cancellationToken)
    {
        var output = await RunAdbTextAsync(adbPath, "shell wm size", cancellationToken);

        var overrideLine = output.Split('\n').FirstOrDefault(l => l.Contains("Override size"));
        var physicalLine = output.Split('\n').FirstOrDefault(l => l.Contains("Physical size"));
        var line = overrideLine ?? physicalLine;

        if (line is null)
            return (0, 0);

        var sizePart = line.Split(':').LastOrDefault()?.Trim();
        var dims = sizePart?.Split('x');
        if (dims is not { Length: 2 })
            return (0, 0);

        if (!int.TryParse(dims[0], out var w) || !int.TryParse(dims[1], out var h))
            return (0, 0);

        return (w, h);
    }

    /// <summary>Szöveges kimenetet váró adb-hívás (async változat) — a meglévő, szinkron RunAdb
    /// mellett, mert ezek a metódusok async kontextusban vannak.</summary>
    private static async Task<string> RunAdbTextAsync(string adbPath, string arguments, CancellationToken cancellationToken)
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

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return output;
    }

    /// <summary>Egy adb-hívás eredményét nem olvassuk vissza (pl. "shell uiautomator dump",
    /// "pull", "shell rm") — csak megvárjuk, hogy lefusson.</summary>
    private static async Task RunAdbAsync(string adbPath, string arguments, CancellationToken cancellationToken)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = adbPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process is null)
            return;

        await process.WaitForExitAsync(cancellationToken);
    }

    /// <summary>Bináris (nem szöveges) kimenetet váró adb-hívás — a "screencap -p" nyers PNG-
    /// bájtokat ír a stdout-ra, ezt NEM szabad szövegként (ReadToEnd) olvasni, mert az a
    /// karakterkódolási konverzió miatt tönkretenné a bináris adatot.</summary>
    private static async Task<byte[]?> RunAdbBinaryAsync(string adbPath, string arguments, CancellationToken cancellationToken)
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
            return null;

        using var ms = new MemoryStream();
        await process.StandardOutput.BaseStream.CopyToAsync(ms, cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return ms.Length > 0 ? ms.ToArray() : null;
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
        LastStepUsedFallbackLocator = false;

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
        // A TestStep.ElementIndex 1-alapú, emberi számozású (1 = első elem, ahogy a
        // felhasználó a felületen megadja) — itt váltjuk 0-alapú tömb-indexre, amit a
        // FindElements(...)[index] vár. Null vagy 1 esetén az első elem (index 0).
        var elementIndex = Math.Max(0, (step.ElementIndex ?? 1) - 1);

        // "Self-healing": ha az elsődleges lokátor a Timeout-on belül nem található, és
        // van megadva tartalék lokátor (FallbackLocator), azzal próbálkozunk újra, MÉG
        // EGY teljes timeout erejéig — csak akkor dobjuk tovább a kivételt, ha a tartalék
        // sem talál semmit. A LastStepUsedFallbackLocator jelzi a hívó felé (ViewModel),
        // hogy sikerült, de nem az elsődleges lokátorral — érdemes frissíteni azt.
        IWebElement Element()
        {
            try
            {
                return FindElement(driver, RequireLocator(step.Locator), step.LocatorType, timeout, elementIndex);
            }
            catch (WebDriverTimeoutException) when (!string.IsNullOrWhiteSpace(step.FallbackLocator))
            {
                var fallbackElement = FindElement(driver, step.FallbackLocator!, step.FallbackLocatorType, timeout, elementIndex);
                LastStepUsedFallbackLocator = true;
                return fallbackElement;
            }
        }

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
                ScrollToElement(driver, RequireLocator(step.Locator), step.LocatorType, timeout, elementIndex);
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
                Wait().Until(d => d.FindElements(ToBy(RequireLocator(step.Locator), step.LocatorType)).Count > elementIndex);
                return null;

            case MobileStepAction.WaitAbsent:
                Wait().Until(d => d.FindElements(ToBy(RequireLocator(step.Locator), step.LocatorType)).Count <= elementIndex);
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

    private static void ScrollToElement(AndroidDriver driver, string locator, LocatorType type, TimeSpan timeout, int elementIndex = 0)
    {
        var by = ToBy(locator, type);
        const int maxAttempts = 8;

        for (var i = 0; i < maxAttempts; i++)
        {
            if (driver.FindElements(by).Count > elementIndex)
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
    /// jellegű állapot-sávja hívja, kézi Frissítés gombra vagy a nézet betöltésekor (és mostantól
    /// automatikusan, időzítve is — lásd MobileTestViewModel._deviceStatusTimer).
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

    /// <summary>elementIndex esetén az összes találatot lekéri (FindElements), a
    /// FilterToDominantPackage-dzsel kiszűri a nem a tesztelt alkalmazáshoz tartozó
    /// elemeket (lásd BuildElementInfo doksi a heurisztika indoklásáért — ugyanaz a
    /// "domináns package" logika, itt IWebElement-ekre alkalmazva), és a megadott
    /// 0-alapú indexűt adja vissza — ha kevesebb találat van, mint amennyi az index+1
    /// lenne, a WebDriverWait a timeout lejártáig újrapróbálkozik, majd
    /// WebDriverTimeoutException-t dob.</summary>
    private IWebElement FindElement(AndroidDriver driver, string locator, LocatorType type, TimeSpan timeout, int elementIndex = 0)
    {
        var wait = new WebDriverWait(driver, timeout);
        return wait.Until(d =>
        {
            var matches = FilterToDominantPackage(d.FindElements(ToBy(locator, type)));
            return elementIndex < matches.Count ? matches[elementIndex] : null;
        });
    }

    /// <summary>Egy nyers IWebElement-találati listát szűkít le a "domináns" (leggyakoribb)
    /// package-csoportra — lásd BuildElementInfo doksi a heurisztika indoklásáért. Ha
    /// legfeljebb 1 elem van, nincs mit szűrni, azt adjuk vissza változatlanul.</summary>
    private static System.Collections.Generic.List<IWebElement> FilterToDominantPackage(
        System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> elements)
    {
        if (elements.Count <= 1)
            return elements.ToList();

        var withPackage = elements.Select(e =>
        {
            string? package;
            try { package = e.GetAttribute("package"); }
            catch { package = null; }
            return (Element: e, Package: package);
        }).ToList();

        var dominantPackage = withPackage
            .GroupBy(x => x.Package)
            .OrderByDescending(g => g.Count())
            .First().Key;

        return withPackage.Where(x => x.Package == dominantPackage).Select(x => x.Element).ToList();
    }

    /// <summary>A ClassName lokátort szándékosan XPath-ra fordítjuk ("//{locator}"), NEM a
    /// Selenium natív By.ClassName-jét használjuk — az utóbbi az Appium/UiAutomator2
    /// "UiSelector"-alapú keresési mechanizmusára épül, ami MAUI CollectionView-szerű,
    /// több egyforma osztályú elemet tartalmazó UI-knál megbízhatatlanul csak 1 találatot
    /// adhat vissza (megfigyelt, reprodukált hiba), miközben az Inspector (PageSource-
    /// alapú) helyesen az összeset látja. Az Appium XPath-keresője más, PageSource-szerű
    /// mechanizmust használ, ami konzisztensen ugyanazokat a találatokat adja, mint amit
    /// az Inspectorban láttál — ezért ezzel megszűnik az eltérés a kettő között.</summary>
    private static By ToBy(string locator, LocatorType type) => type switch
    {
        LocatorType.Id => By.Id(locator),
        LocatorType.XPath => By.XPath(locator),
        LocatorType.ClassName => By.XPath($"//{locator}"),
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