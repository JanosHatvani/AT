namespace AT.Automation.Desktop;

/// <summary>
/// A régi Winium-alapú WDMethods.cs teljes funkciókészlete, FlaUI (UIA3) alapon.
/// A duplikált "attribútum-kiolvasó" metódusok (ElementCheck, ValueReadAndParse,
/// GetTextFromSelectedElement — mind ugyanazt csinálta) egyetlen ReadAttribute
/// lépéssé lettek összevonva. A lokátor a megosztott AT.Core.Contracts.LocatorType-ot
/// használja: Id → AutomationId, Name → Name property, ClassName → ClassName property,
/// XPath → FlaUI beépített XPath-kereséssel az elem-fán.
/// </summary>
public enum DesktopStepAction
{
    /// <summary>Új alkalmazás indítása — az Érték mezőben az .exe elérési útja.</summary>
    LaunchApp,

    /// <summary>Csatlakozás egy már futó alkalmazás ablakához — az Érték mezőben az ablak címe.</summary>
    AttachToWindow,

    Click,
    DoubleClick,
    RightClick,

    /// <summary>Szövegbeviteli mező tartalmának beállítása.</summary>
    SetText,

    Clear,
    Hover,

    /// <summary>Legördülő lista kinyitása és egy elem kiválasztása név alapján (régi ScrollToElementAndClick).</summary>
    SelectComboBoxItem,

    DragAndDrop,

    /// <summary>Egy tulajdonság kiolvasása és megjelenítése toast-üzenetben (régi ElementCheck / ValueReadAndParse / GetTextFromSelectedElement).</summary>
    ReadAttribute,

    /// <summary>Fix várakozás (mp) a Timeout mező alapján.</summary>
    Wait,

    WaitVisible,
    WaitEnabled,

    /// <summary>Látható ÉS elérhető egyszerre (Selenium "clickable" megfelelője).</summary>
    WaitClickable,

    /// <summary>Az elem megjelenik a fában (láthatóságtól függetlenül).</summary>
    WaitPresent,

    /// <summary>Az elem eltűnik a fából.</summary>
    WaitAbsent,

    /// <summary>Kiválasztott állapotba kerül (SelectionItem minta).</summary>
    WaitSelected,

    WaitHasText,
    WaitHasValue,
    WaitHasClass,

    /// <summary>Az Érték mezőben "attribútum=érték" formátumban, pl. IsEnabled=True</summary>
    WaitHasAttribute,

    /// <summary>Az elindított/csatolt alkalmazás bezárása.</summary>
    Close
}