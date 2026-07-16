namespace AT.Automation.Mobile;


// Appium (UiAutomator2) alapú Android-automatizálás lépései. A korábbi kódban ez
// csak részben volt megírva (emulátor-indítás, alap kattintás/beírás) — ez a modul
// azt fejezi be és egészíti ki egy teljes lépéskészletre, a Web/Desktop modulokkal
// azonos mintát követve. A lokátor a megosztott AT.Core.Contracts.LocatorType-ot
// használja: Id → resource-id, AccessibilityId → content-desc, ClassName, XPath.

public enum MobileStepAction
{
    //// Android emulátor indítása — az Érték mezőben az AVD neve.
    //StartEmulator,

    // APK telepítése (ha kell) és az alkalmazás indítása — az Érték mezőben az .apk elérési útja.
    LaunchApp,

    Click,

    // Hosszan nyomás (touch & hold).
    LongPress,

    SendKeys,
    Clear,

    // Teljes képernyős húzás — az Érték mezőben: Up / Down / Left / Right.
    Swipe,

    // Ismételt felfelé húzás, amíg az elem meg nem jelenik.
    ScrollToElement,

    // Egy tulajdonság kiolvasása és megjelenítése toast-üzenetben.
    ReadAttribute,

    // Fix várakozás (mp) a Timeout mező alapján.
    Wait,

    WaitVisible,
    WaitPresent,
    WaitAbsent,
    WaitHasText,

    // Az Érték mezőben "attribútum=érték" formátumban, pl. checked=true
    WaitHasAttribute,

    // Az alkalmazás/session bezárása (az emulátor tovább fut).
    Close,

    // Az emulátor teljes leállítása.
    StopEmulator
}