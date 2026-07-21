using System.Diagnostics;
using System.IO;
using System.Threading;
using AT.Core.Contracts;
using AT.Core.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;

namespace AT.Automation.Web;

/// <summary>
/// Selenium-alapú megvalósítás, mindhárom böngészőre (Chrome / Firefox / Edge).
/// A driver-bináris feloldását a Selenium 4.6+ beépített Selenium Manager-je végzi
/// automatikusan — a régi kézi driver-verzió-ellenőrzés (DriverValidator) és a
/// hardcode-olt "Tools" mappás .exe elérési utak megszűntek, mert erre a
/// modern Seleniumban már nincs szükség.
/// </summary>
public sealed class WebAutomationDriver : IAutomationDriver, IDisposable
{
    private IWebDriver? _driver;

    public string PlatformName => "Web";
    public bool IsRunning => _driver is not null;

    /// <summary>Igaz, ha a LEGUTÓBB végrehajtott lépésnél az elsődleges lokátor nem volt
    /// megtalálható, és a driver a tartalék (FallbackLocator) lokátorral tudta csak
    /// megtalálni az elemet — a ViewModel ezt ellenőrzi minden sikeres ExecuteStepAsync
    /// után, hogy figyelmeztető üzenetet mutasson. Minden ExecuteStepAsync hívás elején
    /// false-ra áll vissza.</summary>
    public bool LastStepUsedFallbackLocator { get; private set; }

    /// <summary>Melyik böngészőt indítsa a StartAsync — a Web-oldal UI-ja állítja be futtatás előtt.</summary>
    public BrowserType Browser { get; set; } = BrowserType.Chrome;

    /// <summary>
    /// Igaz, ha a jelenlegi session egy "remote debugging" porton keresztül csatlakozott
    /// böngészőhöz tartozik (nem egy StartAsync által indított, önálló, kizárólag
    /// automatizálásra szánt példányhoz). Ez önmagában NEM dönti el, hogy a StopAsync
    /// ténylegesen bezárja-e a böngészőt — lásd _weLaunchedAttachedBrowser.
    /// </summary>
    public bool IsAttachedToExistingBrowser { get; private set; }

    /// <summary>
    /// Igaz, ha a csatlakoztatott böngészőt MI MAGUNK indítottuk el (mert a debug-porton
    /// semmi nem futott még), tehát ez egy kizárólag az Elem-kereső miatt létrejött,
    /// eldobható debug-példány — ilyenkor a StopAsync/Dispose BIZTONSÁGGAL bezárhatja,
    /// amikor a felhasználó végzett. Ha viszont MÁR KORÁBBAN futott valami ezen a porton
    /// (pl. a felhasználó saját, parancsikonnal debug-módra állított, mindennapi
    /// böngészője), ezt false-ra állítjuk — azt a böngészőt SOHA nem zárjuk be
    /// automatikusan, csak leválasztjuk róla a vezérlést.
    /// </summary>
    private bool _weLaunchedAttachedBrowser;

    private const int ChromiumDebugPort = 9222;

    // ===================== ÉLETCIKLUS =====================

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_driver is not null)
            return Task.CompletedTask;

        return Task.Run(() =>
        {
            _driver = Browser switch
            {
                BrowserType.Chrome => CreateChrome(),
                BrowserType.Firefox => CreateFirefox(),
                BrowserType.Edge => CreateEdge(),
                _ => throw new ArgumentOutOfRangeException(nameof(Browser), Browser, null)
            };
            IsAttachedToExistingBrowser = false;
        }, cancellationToken);
    }

    /// <summary>
    /// Csatlakozik egy MÁR FUTÓ, "remote debugging" móddal indított Chromium-alapú
    /// böngészőhöz (Chrome vagy Edge) — NEM indít új, önálló, kizárólag automatizálásra
    /// szánt böngészőpéldányt. Ha a megadott porton (alapértelmezetten 9222) még nem fut
    /// ilyen böngésző, EGYETLEN egyszer elindítja (egy teljesen normál, látható ablakként,
    /// amiben a felhasználó szabadon navigálhat), és megvárja, amíg válaszol. Minden
    /// további hívás — amíg ez az ablak nyitva van — UGYANEHHEZ csatlakozik, nem nyit
    /// új ablakot. A Firefox-hoz jelenleg nem támogatott ez a mód (más, nem-CDP-alapú
    /// remote protokollt használ) — ott a normál StartAsync-ra esik vissza.
    /// </summary>
    public Task AttachToRunningBrowserAsync(CancellationToken cancellationToken = default)
    {
        if (_driver is not null)
            return Task.CompletedTask;

        if (Browser == BrowserType.Firefox)
            return StartAsync(cancellationToken);

        return Task.Run(() =>
        {
            var executableName = Browser == BrowserType.Edge ? "msedge" : "chrome";

            // FONTOS: ezt a MI KAPCSOLÁSUNK ELŐTT kell megnézni — ha itt már fut valami,
            // az egy MÁR KORÁBBAN, nem általunk indított böngésző (pl. a felhasználó saját,
            // parancsikonnal debug-módra állított Chrome-ja), amit soha nem szabad
            // automatikusan bezárnunk.
            var wasAlreadyRunning = IsDebugPortOpen(ChromiumDebugPort);

            if (!wasAlreadyRunning)
                LaunchDebugModeBrowser(executableName, ChromiumDebugPort);

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
            while (!IsDebugPortOpen(ChromiumDebugPort))
            {
                if (DateTime.UtcNow > deadline)
                    throw new InvalidOperationException(
                        $"A debug módú {(Browser == BrowserType.Edge ? "Edge" : "Chrome")} nem indult el időben. " +
                        "Ellenőrizd, hogy a böngésző telepítve van-e, és nem blokkolja-e tűzfal/vírusirtó.");
                Thread.Sleep(300);
            }

            if (Browser == BrowserType.Edge)
            {
                var options = new EdgeOptions { DebuggerAddress = $"127.0.0.1:{ChromiumDebugPort}" };
                // A HideCommandPromptWindow enélkül egy látható konzolablakot nyitna a
                // helyi msedgedriver.exe-nek — ez a maga a driver "közvetítő" folyamata,
                // nem a böngésző, a felhasználónak semmi dolga vele.
                var service = EdgeDriverService.CreateDefaultService();
                service.HideCommandPromptWindow = true;
                _driver = new EdgeDriver(service, options);
            }
            else
            {
                var options = new ChromeOptions { DebuggerAddress = $"127.0.0.1:{ChromiumDebugPort}" };
                var service = ChromeDriverService.CreateDefaultService();
                service.HideCommandPromptWindow = true;
                _driver = new ChromeDriver(service, options);
            }

            IsAttachedToExistingBrowser = true;
            _weLaunchedAttachedBrowser = !wasAlreadyRunning;
        }, cancellationToken);
    }

    /// <summary>Gyors, ~500ms-es próbálkozással ellenőrzi, hogy fut-e már valami a megadott
    /// porton — ha igen, feltételezzük, hogy az a korábban általunk indított debug-módú
    /// böngésző (mivel ez a port nem szokványos, ütközés esélye elhanyagolható).</summary>
    private static bool IsDebugPortOpen(int port)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var result = client.BeginConnect("127.0.0.1", port, null, null);
            var connected = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
            if (connected)
                client.EndConnect(result);
            return connected;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Az általunk indított debug-módú böngésző folyamat-objektuma — ezt
    /// KÖZVETLENÜL állítjuk le bezáráskor (CloseBrowserForceAsync), NEM a Selenium
    /// driver.Quit()-jára hagyatkozva. Ez azért fontos, mert amikor a ChromeDriver egy
    /// MÁR FUTÓ böngészőhöz csatlakozik "debuggerAddress"-en keresztül (nem ő indította
    /// a folyamatot közvetlenül a saját ChromeDriverService-én keresztül), a Quit() sok
    /// esetben NEM zárja be ténylegesen a böngészőt — csak a WebDriver-session-t
    /// fejezi be, a böngésző-ablak nyitva marad. A saját magunk által indított folyamat
    /// kilövése (Kill) viszont garantáltan működik.</summary>
    private Process? _launchedBrowserProcess;

    /// <summary>
    /// Elindít egy Chrome/Edge-et "remote debugging" móddal — ez egy TELJESEN NORMÁL,
    /// látható böngészőablak, amiben a felhasználó szabadon navigálhat, bejelentkezhet,
    /// könyvjelzőzhet, stb., mintha csak simán megnyitotta volna a böngészőt. A
    /// --remote-debugging-port kapcsoló miatt tudunk hozzá utólag csatlakozni (DevTools
    /// protokollon keresztül) — enélkül a kapcsoló nélkül induló, "hétköznapi" böngészőhöz
    /// SEMMILYEN automatizálási eszköz nem tud csatlakozni, ez böngésző-biztonsági
    /// korlátozás. Külön, ideiglenes profilmappát használ, hogy ne ütközzön a felhasználó
    /// "normál", nem debug-módú böngészőjével (a kettő nem futtatható ugyanazzal a
    /// profillal egyszerre).
    /// </summary>
    private void LaunchDebugModeBrowser(string executableName, int port)
    {
        var profileDir = Path.Combine(Path.GetTempPath(), $"AT-Studio-{executableName}-Debug-Profile");
        Directory.CreateDirectory(profileDir);

        // UseShellExecute=false + a folyamat-objektum elmentése — enélkül (UseShellExecute
        // =true esetén) nem biztos, hogy a visszaadott Process a TÉNYLEGES böngésző-
        // folyamatra mutat (némely gépen a ShellExecute csak egy indító "launcher"
        // folyamatot ad vissza, ami rögtön ki is lép, miután elindította a valódi
        // böngészőt) — UseShellExecute=false-szal a .NET a megadott .exe-t KÖZVETLENÜL
        // indítja el egy gyerekfolyamatként, aminek életciklusát megbízhatóan tudjuk követni.
        // CSERÉBE viszont a teljes elérési utat magunknak kell feloldanunk (lásd
        // ResolveBrowserExecutablePath) — a puszta "chrome"/"msedge" név, amit korábban
        // a ShellExecute az "App Paths" registry-n keresztül automatikusan feloldott,
        // UseShellExecute=false mellett már nem elég.
        _launchedBrowserProcess = Process.Start(new ProcessStartInfo
        {
            FileName = ResolveBrowserExecutablePath(executableName),
            // A --remote-allow-origins=* KÖTELEZŐ Chrome 111+ verziótól — enélkül a
            // böngésző elutasítja/instabillá teszi a külső DevTools-kapcsolatokat
            // (amit a Selenium a csatlakozáshoz használ), ami pont olyan, időszakos,
            // nehezen visszakövethető hibákban nyilvánul meg, mint az ERR_NAME_NOT_RESOLVED
            // manuálisan beírt URL-eknél — miközben a kezdőlap (helyi, nem hálózati
            // navigáció) még simán betöltődik, ezért tűnhet úgy, mintha "majdnem" működne.
            Arguments = $"--remote-debugging-port={port} --remote-allow-origins=* --user-data-dir=\"{profileDir}\" --no-first-run --no-default-browser-check",
            UseShellExecute = false
        });
    }

    /// <summary>A régi kód UseShellExecute=true mellett a Windows "App Paths" registry-
    /// bejegyzését használta a puszta "chrome"/"msedge" név feloldásához — mivel most
    /// UseShellExecute=false-ra váltottunk (hogy a folyamat-objektumot megbízhatóan
    /// tudjuk követni/kilőni), ezt a feloldást explicit magunknak kell elvégeznünk.</summary>
    private static string ResolveBrowserExecutablePath(string executableName)
    {
        try
        {
            var registryPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{executableName}.exe";
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(registryPath)
                ?? Microsoft.Win32.Registry.CurrentUser.OpenSubKey(registryPath);

            if (key?.GetValue(null) as string is { } registryPathValue && File.Exists(registryPathValue))
                return registryPathValue;
        }
        catch
        {
            // Ha a registry-olvasás bármiért hibázna, egyszerűen a lenti tartalék
            // útvonalakkal próbálkozunk tovább.
        }

        // Végső tartalék: a leggyakoribb telepítési helyek, ha a registry-bejegyzés
        // valamiért hiányozna.
        var fallbackCandidates = executableName == "msedge"
            ? new[]
            {
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"
            }
            : new[]
            {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
            };

        foreach (var candidate in fallbackCandidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        // Ha semmi nem vált be, visszaadjuk az eredeti nevet — a Process.Start ekkor
        // valószínűleg hibázni fog, de legalább egyértelmű "nem található" hibát kapsz,
        // nem egy hamis "sikeresen elindult, de nem követhető" állapotot.
        return executableName;
    }

    /// <summary>Közvetlenül, az OS-folyamatot leállítva bezárja a MI ÁLTALUNK indított
    /// debug-böngészőt (ha volt ilyen) — a Selenium driver.Quit()-jától FÜGGETLENÜL,
    /// mert az (lásd _launchedBrowserProcess doksi) attach-elt session-nél gyakran nem
    /// hatásos. Előbb megpróbálja "szelíden" (CloseMainWindow — ez engedi a böngészőnek
    /// elmenteni a munkamenet-állapotot, "Visszaállítja a korábbi lapokat" funkcióhoz),
    /// majd ha rövid időn belül nem záródna be magától, kényszerrel (Kill) megszünteti.</summary>
    private void KillLaunchedBrowserProcessIfAny()
    {
        var process = _launchedBrowserProcess;
        _launchedBrowserProcess = null;

        if (process is null)
            return;

        try
        {
            if (process.HasExited)
                return;

            process.CloseMainWindow();

            if (!process.WaitForExit(2000))
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Ha már időközben magától bezáródott, vagy bármi más hiba történne itt,
            // nincs mit tenni — a lényeg, hogy ez sose dobjon tovább kivételt a hívónak.
        }
        finally
        {
            process.Dispose();
        }
    }

    private static IWebDriver CreateChrome()
    {
        var options = new ChromeOptions();
        options.AddArgument("--start-maximized");
        var service = ChromeDriverService.CreateDefaultService();
        service.HideCommandPromptWindow = true;
        return new ChromeDriver(service, options);
    }

    private static IWebDriver CreateFirefox()
    {
        var options = new FirefoxOptions();
        options.AddArgument("--start-maximized");
        var service = FirefoxDriverService.CreateDefaultService();
        service.HideCommandPromptWindow = true;
        var driver = new FirefoxDriver(service, options);
        driver.Manage().Window.Maximize();
        return driver;
    }

    private static IWebDriver CreateEdge()
    {
        var options = new EdgeOptions();
        var service = EdgeDriverService.CreateDefaultService();
        service.HideCommandPromptWindow = true;
        var driver = new EdgeDriver(service, options);
        driver.Manage().Window.Maximize();
        return driver;
    }

    /// <summary>
    /// Mindig TÉNYLEGESEN bezárja a böngészőt, függetlenül attól, hogy csatlakoztunk-e
    /// hozzá (IsAttachedToExistingBrowser) vagy mi indítottuk — ezt a "Böngésző bezárása"
    /// gomb hívja, ami egy EXPLICIT, szándékos felhasználói döntés, nem egy mellékhatásos,
    /// automatikus leállás (mint pl. az Elem-kereső bezárása, amit a StopAsync véd). Ha a
    /// felhasználó rákattint a "Böngésző bezárása" gombra, helyénvaló, hogy tényleg
    /// bezáródjon, akkor is, ha épp egy már korábban futó böngészőhöz csatlakoztunk.
    /// </summary>
    public Task CloseBrowserForceAsync(CancellationToken cancellationToken = default)
    {
        var driver = _driver;
        _driver = null;
        IsAttachedToExistingBrowser = false;
        _weLaunchedAttachedBrowser = false;

        return Task.Run(() =>
        {
            if (driver is not null)
            {
                try { driver.Quit(); } catch { /* ha ez nem hatásos (attach-elt session), a lenti Kill mindenképp bezárja */ }
                driver.Dispose();
            }

            // A driver.Quit() attach-elt session-nél (debuggerAddress) gyakran NEM zárja
            // be ténylegesen a böngészőt — ezért, ha MI indítottuk a folyamatot, azt
            // közvetlenül, az OS-szinten is leállítjuk, a Selenium-tól függetlenül.
            KillLaunchedBrowserProcessIfAny();
        }, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        var driver = _driver;
        // Csak akkor hagyjuk érintetlenül a böngészőt, ha CSATLAKOZTUNK egy MÁR KORÁBBAN
        // is futó, nem általunk indított böngészőhöz (pl. a felhasználó saját, parancs-
        // ikonnal debug-módra állított Chrome-ja). Ha MI magunk indítottuk a debug-
        // böngészőt (mert a porton semmi nem futott még), azt biztonsággal, elvárt módon
        // bezárjuk — ez egy kizárólag az Elem-kereső miatt létrejött, eldobható példány.
        var shouldPreserveBrowser = IsAttachedToExistingBrowser && !_weLaunchedAttachedBrowser;
        _driver = null;
        IsAttachedToExistingBrowser = false;
        _weLaunchedAttachedBrowser = false;

        if (driver is null && shouldPreserveBrowser)
            return Task.CompletedTask;

        return Task.Run(() =>
        {
            if (shouldPreserveBrowser)
            {
                // A felhasználó saját, már korábban is futó böngészőjénél NEM hívunk
                // Quit()-et/Kill-t — az bezárná a böngészőt is. Egyszerűen leválasztjuk a
                // vezérlést, a böngésző (és a felhasználó tabjai/munkamenete) nyitva
                // és érintetlenül marad. (KillLaunchedBrowserProcessIfAny ilyenkor
                // amúgy is no-op lenne, mert _launchedBrowserProcess null, hiszen nem mi
                // indítottuk ezt a böngészőt.)
                return;
            }

            if (driver is not null)
            {
                try { driver.Quit(); } catch { /* ha ez nem hatásos (attach-elt session), a lenti Kill mindenképp bezárja */ }
                driver.Dispose();
            }

            KillLaunchedBrowserProcessIfAny();
        }, cancellationToken);
    }

    // ===================== EGYSZERŰ MŰVELETEK (IAutomationDriver szerződés) =====================

    public Task NavigateAsync(string target, CancellationToken cancellationToken = default)
        => RunOnDriver(d => d.Navigate().GoToUrl(target), cancellationToken);

    public Task ClickAsync(string locator, LocatorType locatorType, CancellationToken cancellationToken = default)
        => RunOnDriver(d => FindElement(d, locator, locatorType, DefaultTimeout).Click(), cancellationToken);

    public Task SendKeysAsync(string locator, LocatorType locatorType, string text, CancellationToken cancellationToken = default)
        => RunOnDriver(d => FindElement(d, locator, locatorType, DefaultTimeout).SendKeys(text), cancellationToken);

    public Task<byte[]> GetScreenshotAsync(CancellationToken cancellationToken = default)
        => RunOnDriver(d => ((ITakesScreenshot)d).GetScreenshot().AsByteArray, cancellationToken);

    // ===================== TELJES LÉPÉS-VÉGREHAJTÁS (a lépéslista UI ezt hívja) =====================

    /// <summary>Egy összeállított TestStep végrehajtása — ez fedi le a régi WebMethods összes műveletét.</summary>
    public Task ExecuteStepAsync(TestStep step, CancellationToken cancellationToken = default)
    {
        LastStepUsedFallbackLocator = false;
        EnsureStarted();
        return Task.Run(() => ExecuteStepCore(step), cancellationToken);
    }

    private void ExecuteStepCore(TestStep step)
    {
        var driver = _driver!;
        var timeout = TimeSpan.FromSeconds(Math.Max(1, step.TimeoutSeconds));
        var action = Enum.Parse<WebStepAction>(step.Action);

        // A TestStep.ElementIndex 1-alapú, emberi számozású (1 = első elem) — itt váltjuk
        // 0-alapú tömb-indexre, amit a FindElements(...)[index] vár.
        var elementIndex = Math.Max(0, (step.ElementIndex ?? 1) - 1);

        // "Self-healing": ha az elsődleges lokátor a Timeout-on belül nem található, és
        // van megadva tartalék lokátor, azzal próbálkozunk újra, mielőtt hibásnak
        // jelölnénk a lépést.
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
            case WebStepAction.Navigate:
                driver.Navigate().GoToUrl(step.Value ?? string.Empty);
                break;

            case WebStepAction.Click:
                Element().Click();
                break;

            case WebStepAction.DoubleClick:
                new Actions(driver).DoubleClick(Element()).Perform();
                break;

            case WebStepAction.RightClick:
                new Actions(driver).ContextClick(Element()).Perform();
                break;

            case WebStepAction.SendKeys:
                Element().SendKeys(step.Value ?? string.Empty);
                break;

            case WebStepAction.Clear:
                Element().Clear();
                break;

            case WebStepAction.Hover:
                new Actions(driver).MoveToElement(Element()).Perform();
                break;

            case WebStepAction.SelectByText:
                new SelectElement(Element()).SelectByText(step.Value ?? string.Empty);
                break;

            case WebStepAction.SelectByValue:
                new SelectElement(Element()).SelectByValue(step.Value ?? string.Empty);
                break;

            case WebStepAction.DragAndDrop:
                var source = Element();
                var target = FindElement(driver, RequireLocator(step.TargetLocator), step.TargetLocatorType, timeout);
                new Actions(driver).DragAndDrop(source, target).Perform();
                break;

            case WebStepAction.Wait:
                Thread.Sleep(timeout);
                break;

            case WebStepAction.WaitVisible:
                Wait().Until(_ => Element().Displayed);
                break;

            case WebStepAction.WaitClickable:
                Wait().Until(_ => Element().Enabled);
                break;

            case WebStepAction.WaitPresent:
                Wait().Until(d => d.FindElements(ToBy(RequireLocator(step.Locator), step.LocatorType)).Count > elementIndex);
                break;

            case WebStepAction.WaitAbsent:
                Wait().Until(d => d.FindElements(ToBy(RequireLocator(step.Locator), step.LocatorType)).Count <= elementIndex);
                break;

            case WebStepAction.WaitHasText:
                Wait().Until(_ => Element().Text.Contains(step.Value ?? string.Empty));
                break;

            case WebStepAction.WaitHasClass:
                Wait().Until(_ => (Element().GetAttribute("class") ?? string.Empty).Contains(step.Value ?? string.Empty));
                break;

            case WebStepAction.WaitHasValue:
                Wait().Until(_ => (Element().GetAttribute("value") ?? string.Empty).Contains(step.Value ?? string.Empty));
                break;

            case WebStepAction.WaitHasStyle:
                Wait().Until(_ => (Element().GetAttribute("style") ?? string.Empty).Contains(step.Value ?? string.Empty));
                break;

            case WebStepAction.WaitHasAttribute:
                {
                    var (attr, val) = ParseKeyValue(step.Value);
                    Wait().Until(_ => (Element().GetAttribute(attr) ?? string.Empty).Contains(val));
                    break;
                }

            case WebStepAction.WaitHasCssValue:
                {
                    var (prop, val) = ParseKeyValue(step.Value);
                    Wait().Until(_ => (Element().GetCssValue(prop) ?? string.Empty).Contains(val));
                    break;
                }

            default:
                throw new NotSupportedException($"Ismeretlen művelet: {step.Action}");
        }
    }

    // ===================== ELEM-KERESŐ (Inspector) =====================
    // A böngészőbe injektált JS folyamatosan figyeli, mi van az egér alatt — így a
    // tényleges "elolvasás" pillanatában (a UI-oldali 3 mp-es delay lejártakor) nem kell
    // kattintani semmire, csak a JS-oldali változót kiolvasni.

    private const string HoverTrackerScript = @"
        (function() {
            if (window.__atHoverAttached) return;
            window.__atHoverAttached = true;
            window.__atLastHover = null;
            document.addEventListener('mousemove', function(e) {
                var el = document.elementFromPoint(e.clientX, e.clientY);
                if (el) window.__atLastHover = el;
            }, true);
        })();";

    private const string ReadHoverScript = @"
        var el = window.__atLastHover;
        if (!el) return null;
        function atXPath(node) {
            if (node.id) return '//*[@id=""' + node.id + '""]';
            var parts = [];
            while (node && node.nodeType === 1) {
                var idx = 1, sib = node.previousElementSibling;
                while (sib) { if (sib.tagName === node.tagName) idx++; sib = sib.previousElementSibling; }
                parts.unshift(node.tagName.toLowerCase() + '[' + idx + ']');
                node = node.parentElement;
            }
            return '/' + parts.join('/');
        }
        function matchInfo(listFn) {
            try {
                var arr = Array.prototype.slice.call(listFn());
                var idx = arr.indexOf(el);
                return { count: arr.length, index: idx < 0 ? 0 : (idx + 1) };
            } catch (e) {
                return { count: 0, index: 0 };
            }
        }
        var idVal = el.id || '';
        var nameVal = el.getAttribute('name') || '';
        var classVal = (typeof el.className === 'string') ? el.className.trim() : '';

        var idInfo = idVal ? matchInfo(function() { return document.querySelectorAll('[id=""' + idVal + '""]'); }) : { count: 0, index: 0 };
        var nameInfo = nameVal ? matchInfo(function() { return document.getElementsByName(nameVal); }) : { count: 0, index: 0 };
        var classInfo = classVal ? matchInfo(function() {
            var sel = '.' + classVal.split(/\s+/).join('.');
            return document.querySelectorAll(sel);
        }) : { count: 0, index: 0 };

        return JSON.stringify({
            tag: el.tagName.toLowerCase(),
            id: idVal,
            name: nameVal,
            className: classVal,
            xpath: atXPath(el),
            idMatchIndex: idInfo.index,
            idMatchCount: idInfo.count,
            nameMatchIndex: nameInfo.index,
            nameMatchCount: nameInfo.count,
            classNameMatchIndex: classInfo.index,
            classNameMatchCount: classInfo.count
        });";

    /// <summary>Elindítja a böngészőben az egér-figyelést — az Elem-kereső "Inspect indítása" gombja hívja.</summary>
    public Task StartHoverTrackingAsync(CancellationToken cancellationToken = default)
        => RunOnDriver(d => ((IJavaScriptExecutor)d).ExecuteScript(HoverTrackerScript), cancellationToken);

    /// <summary>A delay lejártakor hívva visszaadja, milyen elem volt utoljára az egér alatt.</summary>
    public Task<WebInspectResult?> ReadLastHoveredElementAsync(CancellationToken cancellationToken = default)
        => RunOnDriver(d =>
        {
            var raw = ((IJavaScriptExecutor)d).ExecuteScript(ReadHoverScript) as string;
            if (string.IsNullOrWhiteSpace(raw))
                return (WebInspectResult?)null;

            return System.Text.Json.JsonSerializer.Deserialize<WebInspectResult>(
                raw, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }, cancellationToken);

    // ===================== FELVEVŐ MÓD (Recorder) =====================
    // A böngészőbe injektált JS "capture" fázisban (harmadik paraméter: true) figyeli a
    // click és change eseményeket az egész dokumentumon — ezért olyan elemekre kattintva
    // is elkapja az eseményt, amiken belül van egy másik, mélyebb elem (pl. egy span egy
    // gombon belül). A change esemény (NEM input/keyup) szándékos: szövegmezőknél csak a
    // fókusz elvesztésekor (blur) tüzel, a VÉGSŐ értékkel — nem generál egy SendKeys
    // lépést minden egyes lenyomott billentyűre.

    private const string RecorderAttachScript = @"
        (function() {
            if (window.__atRecorderAttached) return;
            window.__atRecorderAttached = true;
            window.__atRecordedQueue = [];

            function atXPath(node) {
                if (node.id) return '//*[@id=""' + node.id + '""]';
                var parts = [];
                while (node && node.nodeType === 1) {
                    var idx = 1, sib = node.previousElementSibling;
                    while (sib) { if (sib.tagName === node.tagName) idx++; sib = sib.previousElementSibling; }
                    parts.unshift(node.tagName.toLowerCase() + '[' + idx + ']');
                    node = node.parentElement;
                }
                return '/' + parts.join('/');
            }

            function bestLocator(el) {
                if (el.id) return { type: 'Id', value: el.id };
                var nameAttr = el.getAttribute('name');
                if (nameAttr) return { type: 'Name', value: nameAttr };
                return { type: 'XPath', value: atXPath(el) };
            }

            document.addEventListener('click', function(e) {
                var el = e.target;
                if (!el || el.nodeType !== 1) return;
                var tag = el.tagName.toLowerCase();
                // Szövegbeviteli mezőkre kattintva NEM rögzítünk 'Click'-et — a
                // 'change' esemény úgyis fel fogja venni SendKeys-ként a végleges
                // értékkel, egy plusz, felesleges Click lépés csak zajt adna.
                if (tag === 'input' || tag === 'textarea') return;

                var loc = bestLocator(el);
                window.__atRecordedQueue.push({
                    action: 'Click',
                    locatorType: loc.type,
                    locator: loc.value,
                    value: null
                });
            }, true);

            document.addEventListener('change', function(e) {
                var el = e.target;
                if (!el || el.nodeType !== 1) return;
                var tag = el.tagName.toLowerCase();
                if (tag !== 'input' && tag !== 'textarea' && tag !== 'select') return;

                var loc = bestLocator(el);
                var isSelect = tag === 'select';
                window.__atRecordedQueue.push({
                    action: isSelect ? 'SelectByText' : 'SendKeys',
                    locatorType: loc.type,
                    locator: loc.value,
                    value: isSelect ? (el.options[el.selectedIndex] ? el.options[el.selectedIndex].text : '') : el.value
                });
            }, true);
        })();";

    private const string RecorderPollScript = @"
        var queue = window.__atRecordedQueue || [];
        window.__atRecordedQueue = [];
        return JSON.stringify(queue);";

    private const string RecorderDetachScript = @"
        window.__atRecorderAttached = false;
        window.__atRecordedQueue = [];";

    /// <summary>Elindítja a felvételt: beinjektálja az esemény-figyelő JS-t a böngészőbe.
    /// Idempotens — ha már fut a figyelés (pl. egy korábbi StartRecordingAsync óta nem
    /// navigáltunk el), nem duplázza a feliratkozásokat (lásd __atRecorderAttached flag).
    /// FONTOS: ha a felhasználó közben egy ÚJ oldalra navigál, a böngésző törli a JS
    /// állapotot (ez normál, minden oldal saját JS-kontextussal indul) — ilyenkor a
    /// StartRecordingAsync-et újra meg kell hívni az új oldalon. A ViewModel-oldali
    /// polling-hurok ezt automatikusan megteszi minden ciklusban (lásd WebTestViewModel).</summary>
    public Task StartRecordingAsync(CancellationToken cancellationToken = default)
        => RunOnDriver(d => ((IJavaScriptExecutor)d).ExecuteScript(RecorderAttachScript), cancellationToken);

    /// <summary>Leállítja a felvételt — a JS-oldali figyelőket "kikapcsolja" (a flag
    /// visszaállításával; a document-re feliratkozott addEventListener-eket ez nem
    /// távolítja el ténylegesen, de üresen hagyja a queue-t, és egy új StartRecordingAsync
    /// hívás újra felül tudja írni az állapotot).</summary>
    public Task StopRecordingAsync(CancellationToken cancellationToken = default)
        => RunOnDriver(d => ((IJavaScriptExecutor)d).ExecuteScript(RecorderDetachScript), cancellationToken);

    /// <summary>Lekéri és KIÜRÍTI a felvételi sort — a ViewModel-oldali időzítő hívja
    /// rendszeresen (pl. 600ms-enként), amíg a felvétel aktív. Minden hívás csak az
    /// ELŐZŐ hívás óta történt eseményeket adja vissza (a JS oldalon a queue kiürül
    /// olvasáskor), így nem lesz duplikáció.</summary>
    public Task<List<RecordedWebAction>> PollRecordedActionsAsync(CancellationToken cancellationToken = default)
        => RunOnDriver(d =>
        {
            var raw = ((IJavaScriptExecutor)d).ExecuteScript(RecorderPollScript) as string;
            if (string.IsNullOrWhiteSpace(raw))
                return new List<RecordedWebAction>();

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<RecordedWebAction>>(
                    raw, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            catch
            {
                // Ha a felhasználó közben navigált, és a JS-kontextus törlődött, a
                // window.__atRecordedQueue undefined lehet — ilyenkor egyszerűen nincs
                // mit visszaadni, nem hiba.
                return new List<RecordedWebAction>();
            }
        }, cancellationToken);

    // ===================== SEGÉDMETÓDUSOK =====================

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private Task RunOnDriver(Action<IWebDriver> action, CancellationToken cancellationToken)
    {
        EnsureStarted();
        return Task.Run(() => action(_driver!), cancellationToken);
    }

    private Task<T> RunOnDriver<T>(Func<IWebDriver, T> func, CancellationToken cancellationToken)
    {
        EnsureStarted();
        return Task.Run(() => func(_driver!), cancellationToken);
    }

    private void EnsureStarted()
    {
        if (_driver is null)
            throw new InvalidOperationException("A böngésző nincs elindítva — a Futtatás gomb ezt automatikusan megteszi.");
    }

    private static string RequireLocator(string? locator)
    {
        if (string.IsNullOrWhiteSpace(locator))
            throw new ArgumentException("A lokátor mező kötelező ehhez a lépéstípushoz.");
        return locator;
    }

    private static (string Key, string Value) ParseKeyValue(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !raw.Contains('='))
            throw new ArgumentException("Az Érték mezőben 'kulcs=érték' formátum szükséges, pl. class=aktiv.");

        var idx = raw.IndexOf('=');
        return (raw[..idx].Trim(), raw[(idx + 1)..].Trim());
    }

    private static IWebElement FindElement(IWebDriver driver, string locator, LocatorType type, TimeSpan timeout, int elementIndex = 0)
    {
        var wait = new WebDriverWait(driver, timeout);
        return wait.Until(d =>
        {
            var matches = d.FindElements(ToBy(locator, type));
            return elementIndex < matches.Count ? matches[elementIndex] : null;
        });
    }

    private static By ToBy(string locator, LocatorType type) => type switch
    {
        LocatorType.Id => By.Id(locator),
        LocatorType.XPath => By.XPath(locator),
        LocatorType.Name => By.Name(locator),
        LocatorType.ClassName => By.ClassName(locator),
        LocatorType.CssSelector => By.CssSelector(locator),
        LocatorType.LinkText => By.LinkText(locator),
        LocatorType.PartialLinkText => By.PartialLinkText(locator),
        LocatorType.TagName => By.TagName(locator),
        LocatorType.AccessibilityId => throw new NotSupportedException("AccessibilityId nem értelmezett webes automatizálásnál."),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public void Dispose()
    {
        var shouldPreserveBrowser = IsAttachedToExistingBrowser && !_weLaunchedAttachedBrowser;
        if (!shouldPreserveBrowser)
        {
            try { _driver?.Quit(); } catch { /* ignore */ }
            KillLaunchedBrowserProcessIfAny();
        }
    }
}

/// <summary>Egy, a Felvevő mód által a böngészőben elkapott esemény — a
/// WebTestViewModel ebből épít teljes TestStep-et.</summary>
public sealed class RecordedWebAction
{
    /// <summary>"Click", "SendKeys" vagy "SelectByText" — közvetlenül a WebStepAction
    /// enum értékének felel meg, a ViewModel Enum.Parse-szal alakítja át.</summary>
    public string Action { get; set; } = "";

    /// <summary>"Id", "Name" vagy "XPath" — közvetlenül a LocatorType enum értékének felel meg.</summary>
    public string LocatorType { get; set; } = "";

    public string Locator { get; set; } = "";
    public string? Value { get; set; }
}