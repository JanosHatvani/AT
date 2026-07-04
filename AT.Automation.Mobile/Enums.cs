namespace AT.Automation.Mobile;

/// <summary>
/// Appium (UiAutomator2) alapú Android-automatizálás lépései. A korábbi kódban ez
/// csak részben volt megírva (emulátor-indítás, alap kattintás/beírás) — ez a modul
/// azt fejezi be és egészíti ki egy teljes lépéskészletre, a Web/Desktop modulokkal
/// azonos mintát követve. A lokátor a megosztott AT.Core.Contracts.LocatorType-ot
/// használja: Id → resource-id, AccessibilityId → content-desc, ClassName, XPath.
/// </summary>
public enum MobileStepAction
{
    /// <summary>Android emulátor indítása — az Érték mezőben az AVD neve.</summary>
    StartEmulator,

    /// <summary>APK telepítése (ha kell) és az alkalmazás indítása — az Érték mezőben az .apk elérési útja.</summary>
    LaunchApp,

    Click,

    /// <summary>Hosszan nyomás (touch & hold).</summary>
    LongPress,

    SendKeys,
    Clear,

    /// <summary>Teljes képernyős húzás — az Érték mezőben: Up / Down / Left / Right.</summary>
    Swipe,

    /// <summary>Ismételt felfelé húzás, amíg az elem meg nem jelenik.</summary>
    ScrollToElement,

    /// <summary>Egy tulajdonság kiolvasása és megjelenítése toast-üzenetben.</summary>
    ReadAttribute,

    /// <summary>Fix várakozás (mp) a Timeout mező alapján.</summary>
    Wait,

    WaitVisible,
    WaitPresent,
    WaitAbsent,
    WaitHasText,

    /// <summary>Az Érték mezőben "attribútum=érték" formátumban, pl. checked=true</summary>
    WaitHasAttribute,

    /// <summary>Az alkalmazás/session bezárása (az emulátor tovább fut).</summary>
    Close,

    /// <summary>Az emulátor teljes leállítása.</summary>
    StopEmulator
}