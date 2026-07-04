namespace AT.Core.Contracts;

/// <summary>
/// Közös szerződés minden automatizálási modul (Web, Desktop, Mobile) számára.
/// Az AT.App réteg csak ezt az interfészt ismeri — a konkrét technológiát
/// (Selenium, FlaUI, Appium) soha nem látja közvetlenül.
/// </summary>
public interface IAutomationDriver
{
    /// <summary>Ember-olvasható platformnév, pl. "Web", "Desktop", "Android".</summary>
    string PlatformName { get; }

    /// <summary>Igaz, ha jelenleg fut egy driver-munkamenet.</summary>
    bool IsRunning { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Web: URL megnyitása. Desktop: alkalmazás indítása útvonal alapján. Mobile: app/csomag indítása.</summary>
    Task NavigateAsync(string target, CancellationToken cancellationToken = default);

    Task ClickAsync(string locator, LocatorType locatorType, CancellationToken cancellationToken = default);

    Task SendKeysAsync(string locator, LocatorType locatorType, string text, CancellationToken cancellationToken = default);

    /// <summary>Aktuális képernyő/állapot pillanatkép PNG bájtjai — ez táplálja majd az élő kijelző-tükrözést.</summary>
    Task<byte[]> GetScreenshotAsync(CancellationToken cancellationToken = default);
}

public enum LocatorType
{
    Id,
    XPath,
    Name,
    ClassName,
    AccessibilityId,
    CssSelector,
    LinkText,
    PartialLinkText,
    TagName
}
