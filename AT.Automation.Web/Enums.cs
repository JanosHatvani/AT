namespace AT.Automation.Web;

public enum BrowserType
{
    Chrome,
    Firefox,
    Edge
}

/// <summary>
/// A régi WebMethods.cs összes egyedi műveletét lefedi (a duplikált, egymással
/// azonos viselkedésű wait-variánsokat összevonva). A driver-verzió-ellenőrzés és
/// a kézi Tools-mappás driver-kezelés megszűnt — ezt a Selenium Manager végzi.
/// </summary>
public enum WebStepAction
{
    Navigate,
    Click,
    DoubleClick,
    RightClick,
    SendKeys,
    Clear,
    Hover,
    SelectByText,
    SelectByValue,
    DragAndDrop,

    /// <summary>Fix várakozás (mp) — a régi Pause() 1000 mp-es hardcode-olt hibája helyett konfigurálható.</summary>
    Wait,

    WaitVisible,
    WaitClickable,
    WaitPresent,
    WaitAbsent,
    WaitHasText,

    /// <summary>Az Érték mezőben "attribútum=érték" formátumban, pl. data-state=active</summary>
    WaitHasAttribute,

    WaitHasClass,
    WaitHasValue,

    /// <summary>Az Érték mezőben "css-tulajdonság=érték" formátumban, pl. display=block</summary>
    WaitHasCssValue,

    WaitHasStyle
}
