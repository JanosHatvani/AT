namespace AT.Infrastructure;

public enum ScreenshotCaptureMode
{
    Never,
    OnErrorOnly,
    Always
}

/// <summary>
/// Perzisztens, helyi alapértelmezések a Web/Desktop/Mobil modulokhoz.
/// Ez váltja ki a régi kódban hardcode-olt útvonalakat (pl. C:\TesztApp\app-debug.apk).
/// </summary>
public sealed class AppSettings
{
    /// <summary>Android SDK gyökérmappa — ha üres, a modul az ANDROID_SDK_ROOT/ANDROID_HOME
    /// környezeti változóra esik vissza.</summary>
    public string? AndroidSdkRoot { get; set; }

    public string DefaultBrowser { get; set; } = "Chrome";

    public int DefaultTimeoutSeconds { get; set; } = 10;

    public string? DefaultAvdName { get; set; }

    public string? DefaultApkPath { get; set; }

    public string? DefaultDesktopAppPath { get; set; }

    /// <summary>Soha / csak hiba esetén / minden lépés után készítsen-e képernyőképet.</summary>
    public ScreenshotCaptureMode ScreenshotCaptureMode { get; set; } = ScreenshotCaptureMode.Never;

    /// <summary>Ha üres, a képernyőképek az Asztalra kerülnek.</summary>
    public string? ScreenshotFolderPath { get; set; }

    /// <summary>
    /// A futtatási előzmények (TestRunRecord JSON-ok) mentési mappája — ha üres,
    /// az Asztalra kerülnek, ugyanúgy mint a ScreenshotFolderPath esetén.
    /// Mindhárom modul (Web/Desktop/Mobil) közösen ezt használja.
    /// </summary>
    public string? TestHistoryFolderPath { get; set; }
}
