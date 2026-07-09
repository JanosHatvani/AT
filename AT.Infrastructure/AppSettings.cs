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

    /// <summary>Igaz, ha a felhasználó a sötét témát választotta a Beállításokban.</summary>
    public bool IsDarkTheme { get; set; }

    // ===================== RIPORT-EMAIL (ütemezett futtatás hibája esetén) =====================

    /// <summary>Ha be van kapcsolva, egy ütemezett (automatikus) futtatás hibája esetén
    /// riport-emailt küld a lenti SMTP-beállításokkal. Kézi futtatás hibája esetén NEM küld.</summary>
    public bool EmailNotificationsEnabled { get; set; } = false;

    /// <summary>SMTP szerver címe, pl. "smtp.gmail.com" vagy a vállalati SMTP host neve.</summary>
    public string? SmtpHost { get; set; }

    /// <summary>SMTP port — jellemzően 587 (STARTTLS) vagy 465 (SSL).</summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>SMTP-authentikáció felhasználóneve — ha üres, a kliens hitelesítés nélkül próbál csatlakozni.</summary>
    public string? SmtpUsername { get; set; }

    /// <summary>
    /// SMTP-authentikáció jelszava. MEGJEGYZÉS: ez a beállítás-fájlban (settings.json)
    /// jelenleg egyszerű, olvasható szövegként tárolódik, ugyanúgy mint a többi mező —
    /// nincs külön titkosítás rajta. Ez a fájl a saját %AppData%-dban van, helyi,
    /// egyfelhasználós gépi használatra; ha ez a szint nem elég, Gmail-nél és a legtöbb
    /// szolgáltatónál App Password (nem a tényleges fiók-jelszó) használható helyette,
    /// ami bármikor önállóan visszavonható.
    /// </summary>
    public string? SmtpPassword { get; set; }

    /// <summary>SSL/TLS titkosítás használata a kapcsolathoz.</summary>
    public bool SmtpUseSsl { get; set; } = true;

    /// <summary>A riport-emailek feladó címe (pl. "at-framework@cegneved.hu").</summary>
    public string? EmailFrom { get; set; }

    /// <summary>Címzettek, vesszővel/pontosvesszővel/soronként elválasztva (pl. "a@x.hu, b@y.hu").</summary>
    public string? EmailRecipients { get; set; }
}
