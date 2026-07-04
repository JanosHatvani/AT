using System.Globalization;
using System.Windows.Data;
using AT.Automation.Desktop;
using AT.Automation.Mobile;
using AT.Automation.Web;
using AT.Core.Contracts;

namespace AT.App.Converters;

/// <summary>Web modul: WebStepAction → magyar megjelenítési név a "Művelet" legördülőben.</summary>
public sealed class WebStepActionToLabelConverter : IValueConverter
{
    public static readonly WebStepActionToLabelConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is WebStepAction action ? Label(action) : value?.ToString() ?? string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static string Label(WebStepAction action) => action switch
    {
        WebStepAction.Navigate => "Navigálás (URL megnyitása)",
        WebStepAction.Click => "Kattintás",
        WebStepAction.DoubleClick => "Dupla kattintás",
        WebStepAction.RightClick => "Jobb-klikk",
        WebStepAction.SendKeys => "Szöveg beírása",
        WebStepAction.Clear => "Mező ürítése",
        WebStepAction.Hover => "Rámutatás",
        WebStepAction.SelectByText => "Kiválasztás szöveg alapján",
        WebStepAction.SelectByValue => "Kiválasztás érték alapján",
        WebStepAction.DragAndDrop => "Húzás és elengedés",
        WebStepAction.Wait => "Várakozás (fix idő)",
        WebStepAction.WaitVisible => "Várakozás: legyen látható",
        WebStepAction.WaitClickable => "Várakozás: legyen kattintható",
        WebStepAction.WaitPresent => "Várakozás: jelenjen meg",
        WebStepAction.WaitAbsent => "Várakozás: tűnjön el",
        WebStepAction.WaitHasText => "Várakozás: szöveget tartalmazzon",
        WebStepAction.WaitHasAttribute => "Várakozás: attribútum egyezzen",
        WebStepAction.WaitHasClass => "Várakozás: class egyezzen",
        WebStepAction.WaitHasValue => "Várakozás: érték egyezzen",
        WebStepAction.WaitHasCssValue => "Várakozás: CSS-érték egyezzen",
        WebStepAction.WaitHasStyle => "Várakozás: style egyezzen",
        _ => action.ToString()
    };
}

/// <summary>Desktop modul: DesktopStepAction → magyar megjelenítési név.</summary>
public sealed class DesktopStepActionToLabelConverter : IValueConverter
{
    public static readonly DesktopStepActionToLabelConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DesktopStepAction action ? Label(action) : value?.ToString() ?? string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static string Label(DesktopStepAction action) => action switch
    {
        DesktopStepAction.LaunchApp => "Alkalmazás indítása",
        DesktopStepAction.AttachToWindow => "Csatlakozás futó ablakhoz",
        DesktopStepAction.Click => "Kattintás",
        DesktopStepAction.DoubleClick => "Dupla kattintás",
        DesktopStepAction.RightClick => "Jobb-klikk",
        DesktopStepAction.SetText => "Szöveg beállítása",
        DesktopStepAction.Clear => "Mező ürítése",
        DesktopStepAction.Hover => "Rámutatás",
        DesktopStepAction.SelectComboBoxItem => "Lista-elem kiválasztása",
        DesktopStepAction.DragAndDrop => "Húzás és elengedés",
        DesktopStepAction.ReadAttribute => "Attribútum kiolvasása",
        DesktopStepAction.Wait => "Várakozás (fix idő)",
        DesktopStepAction.WaitVisible => "Várakozás: legyen látható",
        DesktopStepAction.WaitEnabled => "Várakozás: legyen elérhető",
        DesktopStepAction.WaitClickable => "Várakozás: legyen kattintható",
        DesktopStepAction.WaitPresent => "Várakozás: jelenjen meg",
        DesktopStepAction.WaitAbsent => "Várakozás: tűnjön el",
        DesktopStepAction.WaitSelected => "Várakozás: legyen kiválasztva",
        DesktopStepAction.WaitHasText => "Várakozás: szöveget tartalmazzon",
        DesktopStepAction.WaitHasAttribute => "Várakozás: attribútum egyezzen",
        DesktopStepAction.WaitHasClass => "Várakozás: class egyezzen",
        DesktopStepAction.WaitHasValue => "Várakozás: érték egyezzen",
        DesktopStepAction.Close => "Alkalmazás bezárása",
        _ => action.ToString()
    };
}

/// <summary>Mobil modul: MobileStepAction → magyar megjelenítési név.</summary>
public sealed class MobileStepActionToLabelConverter : IValueConverter
{
    public static readonly MobileStepActionToLabelConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is MobileStepAction action ? Label(action) : value?.ToString() ?? string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static string Label(MobileStepAction action) => action switch
    {
        MobileStepAction.StartEmulator => "Emulátor indítása",
        MobileStepAction.LaunchApp => "Alkalmazás telepítése / indítása",
        MobileStepAction.Click => "Kattintás",
        MobileStepAction.LongPress => "Hosszan nyomás",
        MobileStepAction.SendKeys => "Szöveg beírása",
        MobileStepAction.Clear => "Mező ürítése",
        MobileStepAction.Swipe => "Húzás (swipe)",
        MobileStepAction.ScrollToElement => "Görgetés az elemig",
        MobileStepAction.ReadAttribute => "Attribútum kiolvasása",
        MobileStepAction.Wait => "Várakozás (fix idő)",
        MobileStepAction.WaitVisible => "Várakozás: legyen látható",
        MobileStepAction.WaitPresent => "Várakozás: jelenjen meg",
        MobileStepAction.WaitAbsent => "Várakozás: tűnjön el",
        MobileStepAction.WaitHasText => "Várakozás: szöveget tartalmazzon",
        MobileStepAction.WaitHasAttribute => "Várakozás: attribútum egyezzen",
        MobileStepAction.Close => "Alkalmazás bezárása",
        MobileStepAction.StopEmulator => "Emulátor leállítása",
        _ => action.ToString()
    };
}

/// <summary>Megosztott LocatorType → magyar (technikai kifejezéssel kiegészített) megjelenítési név.</summary>
public sealed class LocatorTypeToLabelConverter : IValueConverter
{
    public static readonly LocatorTypeToLabelConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is LocatorType type ? Label(type) : value?.ToString() ?? string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static string Label(LocatorType type) => type switch
    {
        LocatorType.Id => "Id (azonosító)",
        LocatorType.XPath => "XPath",
        LocatorType.Name => "Név (name)",
        LocatorType.ClassName => "Osztálynév (class)",
        LocatorType.AccessibilityId => "Kisegítő azonosító (content-desc)",
        LocatorType.CssSelector => "CSS szelektor",
        LocatorType.LinkText => "Linkszöveg",
        LocatorType.PartialLinkText => "Részleges linkszöveg",
        LocatorType.TagName => "Címke neve (tag)",
        _ => type.ToString()
    };
}