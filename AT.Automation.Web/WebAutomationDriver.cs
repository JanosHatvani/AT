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

    /// <summary>Melyik böngészőt indítsa a StartAsync — a Web-oldal UI-ja állítja be futtatás előtt.</summary>
    public BrowserType Browser { get; set; } = BrowserType.Chrome;

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
        }, cancellationToken);
    }

    private static IWebDriver CreateChrome()
    {
        var options = new ChromeOptions();
        options.AddArgument("--start-maximized");
        return new ChromeDriver(options);
    }

    private static IWebDriver CreateFirefox()
    {
        var options = new FirefoxOptions();
        options.AddArgument("--start-maximized");
        var driver = new FirefoxDriver(options);
        driver.Manage().Window.Maximize();
        return driver;
    }

    private static IWebDriver CreateEdge()
    {
        var options = new EdgeOptions();
        var driver = new EdgeDriver(options);
        driver.Manage().Window.Maximize();
        return driver;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        var driver = _driver;
        _driver = null;

        if (driver is null)
            return Task.CompletedTask;

        return Task.Run(() =>
        {
            driver.Quit();
            driver.Dispose();
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
        EnsureStarted();
        return Task.Run(() => ExecuteStepCore(step), cancellationToken);
    }

    private void ExecuteStepCore(TestStep step)
    {
        var driver = _driver!;
        var timeout = TimeSpan.FromSeconds(Math.Max(1, step.TimeoutSeconds));
        var action = Enum.Parse<WebStepAction>(step.Action);

        IWebElement Element() => FindElement(driver, RequireLocator(step.Locator), step.LocatorType, timeout);
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
                Wait().Until(d => d.FindElements(ToBy(RequireLocator(step.Locator), step.LocatorType)).Count > 0);
                break;

            case WebStepAction.WaitAbsent:
                Wait().Until(d => d.FindElements(ToBy(RequireLocator(step.Locator), step.LocatorType)).Count == 0);
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
        return JSON.stringify({
            tag: el.tagName.toLowerCase(),
            id: el.id || '',
            name: el.getAttribute('name') || '',
            className: (typeof el.className === 'string') ? el.className.trim() : '',
            xpath: atXPath(el)
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

    private static IWebElement FindElement(IWebDriver driver, string locator, LocatorType type, TimeSpan timeout)
    {
        var wait = new WebDriverWait(driver, timeout);
        return wait.Until(d => d.FindElement(ToBy(locator, type)));
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

    public void Dispose() => _driver?.Quit();
}