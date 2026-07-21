using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using AT.Infrastructure;

namespace AT.App.Services;

/// <summary>Egy telepítési fázis — a UI (AndroidSdkSetupWindow) ezt jeleníti meg
/// szöveges státuszként ("Rendszerkép letöltése…") és — ahol értelmezhető — %-os
/// előrehaladásként.</summary>
public enum AndroidSdkInstallPhase
{
    DownloadingJava,
    InstallingJava,
    DownloadingCommandLineTools,
    ExtractingCommandLineTools,
    AcceptingLicenses,
    InstallingPlatformTools,
    InstallingEmulator,
    InstallingPlatform,
    InstallingBuildTools,      
    InstallingSystemImage,
    CreatingVirtualDevice,
    Finished
}

/// <summary>Egy telepítési progress-jelentés — a letöltési fázisoknál (DownloadingJava,
/// DownloadingCommandLineTools) van valódi, byte-alapú %-os érték; a többi (sdkmanager-
/// hívásokkal futó) fázisnál a sdkmanager saját kimenete nem ad megbízható, gépileg
/// elemezhető %-ot, ezért azoknál csak a fázis-szöveg és egy "határozatlan" (indeterminate)
/// progress-állapot érhető el.</summary>
public sealed class AndroidSdkInstallProgress
{
    public required AndroidSdkInstallPhase Phase { get; init; }

    /// <summary>0-100 közötti érték, ha ismert (csak a letöltési fázisoknál) — egyébként null,
    /// ami azt jelzi a UI-nak, hogy határozatlan (indeterminate) progress-sávot mutasson.</summary>
    public double? PercentComplete { get; init; }

    /// <summary>Csak a letöltési fázisoknál becsült, a letöltési sebesség alapján — egyébként null.</summary>
    public TimeSpan? EstimatedTimeRemaining { get; init; }

    public string PhaseLabel => Phase switch
    {
        AndroidSdkInstallPhase.DownloadingJava => "Java (JDK) letöltése — az Android SDK ehhez szükséges",
        AndroidSdkInstallPhase.InstallingJava => "Java (JDK) telepítése",
        AndroidSdkInstallPhase.DownloadingCommandLineTools => "Android SDK alapeszközök letöltése",
        AndroidSdkInstallPhase.ExtractingCommandLineTools => "Kicsomagolás",
        AndroidSdkInstallPhase.AcceptingLicenses => "Licencek elfogadása",
        AndroidSdkInstallPhase.InstallingPlatformTools => "Platform-tools (adb) telepítése",
        AndroidSdkInstallPhase.InstallingEmulator => "Emulátor telepítése",
        AndroidSdkInstallPhase.InstallingPlatform => "Android platform telepítése",
        AndroidSdkInstallPhase.InstallingBuildTools => "Build Tools telepítése",
        AndroidSdkInstallPhase.InstallingSystemImage => "Rendszerkép letöltése",
        AndroidSdkInstallPhase.CreatingVirtualDevice => "Virtuális eszköz létrehozása",
        AndroidSdkInstallPhase.Finished => "Kész",
        _ => Phase.ToString()
    };
}

public interface IAndroidSdkInstallerService
{
    /// <summary>Igaz, ha egy gyors, felszínes ellenőrzés szerint már van (valószínűleg)
    /// használható Android SDK — ugyanaz a logika, mint az AndroidSdkLocator.IsSdkAvailable,
    /// csak itt a Beállításokban tárolt útvonalat nézi elsődlegesen.</summary>
    bool IsInstalled();

    /// <summary>Letölti és telepíti a szükséges Android SDK komponenseket a megadott
    /// célmappába. A progress callback a UI-szálon (Dispatcher) történő frissítésre
    /// alkalmas jelentéseket küld. Kivétel esetén a hívó (a dialógus) jeleníti meg a hibát —
    /// ez a szolgáltatás maga nem ír a UI-ba.</summary>
    Task InstallAsync(IProgress<AndroidSdkInstallProgress> progress, CancellationToken cancellationToken = default);
}

/// <summary>
/// Az Android SDK Command Line Tools letöltését és a szükséges komponensek (platform-tools,
/// emulator, egy Android platform, egy x86_64 system image, egy alapértelmezett AVD)
/// telepítését végzi — a felhasználó a Mobil nézetre navigáláskor, egy dialóguson keresztül
/// indíthatja el, valós idejű progress-visszajelzéssel. A cél mindig a Beállításokban
/// tárolt (vagy alapértelmezett %LOCALAPPDATA%\Android\Sdk) mappa — telepítés után ez az
/// útvonal automatikusan bekerül a SettingsService-be, hogy a program azonnal megtalálja.
/// </summary>
public sealed class AndroidSdkInstallerService : IAndroidSdkInstallerService
{
    private const string CmdlineToolsUrl = "https://dl.google.com/android/repository/commandlinetools-win-11076708_latest.zip";
    private const string SystemImagePackage = "system-images;android-34;google_apis;x86_64";
    private const string AvdName = "AT_Studio_Default";

    // Az Adoptium API "binary/latest" végpontja (FONTOS: NEM "installer/latest"!) mindig a
    // legfrissebb, hivatalos Eclipse Temurin (OpenJDK) JDK 21 LTS Windows x64 ZIP-archívumára
    // mutat, HTTP redirect-tel. Az "installer/latest" végpont ezzel szemben egy .msi
    // telepítőt adna vissza, aminek futtatásához (msiexec) admin-jogosultság és UAC-prompt
    // kellene — ezt szándékosan elkerüljük, lásd az InstallJavaAsync doksi-kommentjét.
    // Miért JDK, nem JRE: az Android SDK sdkmanager/avdmanager parancsai JDK-t várnak (nem
    // elég a futtatókörnyezet). Miért 21 (nem 17/25): a 21 jelenleg LTS, stabil választás.
    private const string JdkDownloadUrl = "https://api.adoptium.net/v3/binary/latest/21/ga/windows/x64/jdk/hotspot/normal/eclipse";

    private readonly ISettingsService _settingsService;

    /// <summary>A telepítés részletes, lépésenkénti naplója (parancsok, kimenetük, exit code-ok)
    /// — ha valami hibázik, ide lehet nézni, mi történt pontosan. Minden InstallAsync-hívás
    /// felülírja az előző futás naplóját.</summary>
    public string LogFilePath { get; } = Path.Combine(Path.GetTempPath(), "at-studio-android-sdk-install.log");

    public AndroidSdkInstallerService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    private void WriteLog(string message)
    {
        try
        {
            File.AppendAllText(LogFilePath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // A naplózás hibája sose akassza meg magát a telepítést.
        }
    }

    /// <summary>
    /// Igaz, ha a "java" parancs elérhető és lefuttatható — az sdkmanager.bat/avdmanager.bat
    /// (Java-alapú eszközök) ezt igénylik, és enélkül csendben, azonnal elhasalnak
    /// ("JAVA_HOME is not set and no 'java' command could be found in your PATH").
    /// Ez a metódus a JAVA_HOME környezeti változóra és a PATH-ra egyaránt támaszkodik —
    /// pontosan úgy, ahogy az sdkmanager.bat maga is teszi.
    /// </summary>
    private static bool IsJavaAvailable()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = "-version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
                return false;

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            // Ha a "java" parancs egyáltalán nem található (Win32Exception), ez azt jelenti,
            // hogy nincs telepítve / nincs a PATH-ban — ez a leggyakoribb eset.
            return false;
        }
    }

    private string ResolveTargetSdkRoot()
    {
        var configured = _settingsService.Current.AndroidSdkRoot;
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk");
    }

    public bool IsInstalled()
    {
        return AT.Automation.Mobile.AndroidSdkLocator.IsSdkAvailable(_settingsService.Current.AndroidSdkRoot);
    }

    public async Task InstallAsync(IProgress<AndroidSdkInstallProgress> progress, CancellationToken cancellationToken = default)
    {
        // Minden új telepítési kísérlet felülírja az előző napló-fájlt, hogy mindig a
        // legutóbbi próbálkozás részletei legyenek benne, ne halmozódjon a régiekkel.
        try { File.Delete(LogFilePath); } catch { /* ignore */ }
        WriteLog("Android SDK telepítés indul.");

        var sdkRoot = ResolveTargetSdkRoot();
        WriteLog($"Cél SDK mappa: {sdkRoot}");
        Directory.CreateDirectory(sdkRoot);

        // ---- 0. Java (JDK) ellenőrzése és telepítése, ha hiányzik ----
        // Az sdkmanager.bat/avdmanager.bat Java-alapú eszközök — ha nincs telepítve Java,
        // ezek csendben, azonnal elhasalnak, még mielőtt bármi mást csinálnának. Ezt a
        // hiányt korábban nem ellenőriztük explicit, ami azt eredményezte, hogy a
        // cmdline-tools sikeresen kicsomagolódott, de utána semmi más nem történt —
        // minden sdkmanager-hívás láthatatlanul elhasalt.
        if (!IsJavaAvailable())
        {
            WriteLog("Java nem található — telepítés indul.");
            await InstallJavaAsync(progress, cancellationToken);
            WriteLog("Java telepítő-folyamat visszatért — a JAVA_HOME/PATH beállítása közvetlenül a folyamat végén, szinkron módon megtörtént.");

            // A ZIP-alapú, portable telepítésnél (InstallJavaAsync) a JAVA_HOME/PATH
            // beállítása a metódus legvégén, közvetlenül, szinkron módon történik — nincs
            // itt szükség a régi, msiexec-es telepítésnél indokolt várakozási ciklusra
            // (ott a msiexec egy háttérben futó, aszinkron folyamatot indított el, aminek a
            // befejezését meg kellett várni). Itt elég egyetlen, azonnali ellenőrzés.
            if (!IsJavaAvailable())
            {
                WriteLog("HIBA: Java telepítése lefutott, de a 'java' parancs továbbra sem elérhető.");
                throw new InvalidOperationException(
                    "A Java (JDK) telepítése lefutott, de nem sikerült megerősíteni, hogy a 'java' parancs " +
                    "elérhető. Próbáld újra a Mobil nézetre lépést (az AT Studio újraindítása nélkül is). " +
                    "Ha ez ismétlődik, indítsd újra az AT Studio-t, hogy a frissített PATH biztosan " +
                    $"érvénybe lépjen. Részletes napló: {LogFilePath}");
            }

            WriteLog("Java sikeresen elérhetővé vált.");
        }
        else
        {
            WriteLog("Java már telepítve van, ez a lépés kimarad.");
        }

        var tempZip = Path.Combine(Path.GetTempPath(), "at-studio-android-cmdline-tools.zip");
        var extractTemp = Path.Combine(Path.GetTempPath(), "at-studio-cmdline-tools-extract");

        // ---- 1. Letöltés valós progress-szel ----
        WriteLog("Command Line Tools letöltése...");
        await DownloadWithProgressAsync(CmdlineToolsUrl, tempZip, progress, cancellationToken, AndroidSdkInstallPhase.DownloadingCommandLineTools);
        WriteLog("Letöltés kész.");

        // ---- 2. Kicsomagolás ----
        progress.Report(new AndroidSdkInstallProgress { Phase = AndroidSdkInstallPhase.ExtractingCommandLineTools });

        if (Directory.Exists(extractTemp))
            Directory.Delete(extractTemp, recursive: true);

        await Task.Run(() => ZipFile.ExtractToDirectory(tempZip, extractTemp), cancellationToken);
        WriteLog("Kicsomagolás kész.");

        // A Google elvárása szerint a cmdline-tools tartalma "cmdline-tools\latest\" alá
        // kell kerüljön (nem közvetlenül "cmdline-tools\" alá) — enélkül az sdkmanager
        // nem találja meg saját magát induláskor.
        var cmdlineToolsDir = Path.Combine(sdkRoot, "cmdline-tools");
        var latestDir = Path.Combine(cmdlineToolsDir, "latest");
        Directory.CreateDirectory(cmdlineToolsDir);

        if (Directory.Exists(latestDir))
            Directory.Delete(latestDir, recursive: true);

        Directory.Move(Path.Combine(extractTemp, "cmdline-tools"), latestDir);

        var sdkManagerPath = Path.Combine(latestDir, "bin", "sdkmanager.bat");
        var avdManagerPath = Path.Combine(latestDir, "bin", "avdmanager.bat");

        if (!File.Exists(sdkManagerPath))
        {
            WriteLog($"HIBA: sdkmanager.bat nem található: {sdkManagerPath}");
            throw new InvalidOperationException($"Az sdkmanager.bat nem található a várt helyen: {sdkManagerPath}. Részletek: {LogFilePath}");
        }

        // ---- 3. Licencek elfogadása ----
        progress.Report(new AndroidSdkInstallProgress { Phase = AndroidSdkInstallPhase.AcceptingLicenses });
        WriteLog("Licencek elfogadása...");
        var licensesResult = await RunSdkManagerAsync(sdkManagerPath, sdkRoot, "--licenses", sendYesRepeatedly: true, cancellationToken);
        WriteLog($"Licencek elfogadása — exit code: {licensesResult.ExitCode}");
        WriteLog($"--- sdkmanager --licenses STDOUT ---{Environment.NewLine}{licensesResult.StandardOutput}");
        WriteLog($"--- sdkmanager --licenses STDERR ---{Environment.NewLine}{licensesResult.StandardError}");

        // ---- 4 Build Tools telepítése ----
        // A UiAutomator2 Appium driver session-indításkor "aapt2.exe"-t hív az APK
        // vizsgálatához (csomagnév/aktivitás kiolvasása) — enélkül a "Could not find
        // 'aapt2.exe'..." hibával elszáll a LaunchApp lépés, még akkor is, ha a
        // platform-tools/emulator/platform már rendben települt.
        const string BuildToolsPackage = "build-tools;34.0.0";
        progress.Report(new AndroidSdkInstallProgress { Phase = AndroidSdkInstallPhase.InstallingBuildTools });
        WriteLog($"{BuildToolsPackage} telepítése...");
        var buildToolsResult = await RunSdkManagerAsync(sdkManagerPath, sdkRoot, $"\"{BuildToolsPackage}\"", sendYesRepeatedly: false, cancellationToken);
        WriteLog($"build-tools telepítés — exit code: {buildToolsResult.ExitCode}");
        WriteLog($"--- sdkmanager build-tools STDOUT ---{Environment.NewLine}{buildToolsResult.StandardOutput}");
        WriteLog($"--- sdkmanager build-tools STDERR ---{Environment.NewLine}{buildToolsResult.StandardError}");

        var aapt2Path = Path.Combine(sdkRoot, "build-tools", "34.0.0", "aapt2.exe");
        if (!File.Exists(aapt2Path))
        {
            WriteLog($"HIBA: a build-tools telepítés lefutott, de az aapt2.exe mégsem jött létre itt: {aapt2Path}");
            throw new InvalidOperationException(
                $"A Build Tools telepítése nem sikerült (az aapt2.exe nem jött létre). " +
                $"Részletes napló: {LogFilePath}");
        }
        WriteLog("aapt2.exe létrejött, build-tools telepítés sikeres.");

        progress.Report(new AndroidSdkInstallProgress { Phase = AndroidSdkInstallPhase.InstallingSystemImage });
        WriteLog("system image telepítése (ez tart a legtovább)...");


        // ---- 5. Komponensek telepítése ----
        progress.Report(new AndroidSdkInstallProgress { Phase = AndroidSdkInstallPhase.InstallingPlatformTools });
        WriteLog("platform-tools telepítése...");
        var platformToolsResult = await RunSdkManagerAsync(sdkManagerPath, sdkRoot, "\"platform-tools\"", sendYesRepeatedly: false, cancellationToken);
        WriteLog($"platform-tools telepítés — exit code: {platformToolsResult.ExitCode}");
        WriteLog($"--- sdkmanager platform-tools STDOUT ---{Environment.NewLine}{platformToolsResult.StandardOutput}");
        WriteLog($"--- sdkmanager platform-tools STDERR ---{Environment.NewLine}{platformToolsResult.StandardError}");

        // Explicit ellenőrzés, hogy az adb.exe TÉNYLEG létrejött-e — ha nem, azonnal,
        // konkrét hibaüzenettel állunk le, ahelyett hogy csendben tovább mennénk a
        // következő komponensre, és a felhasználó csak a legvégén, egy áttételes
        // hibaüzenetből (Mobil nézet "adb.exe nem található") derítené ki, hogy itt
        // valami elakadt.
        var adbPath = Path.Combine(sdkRoot, "platform-tools", "adb.exe");
        if (!File.Exists(adbPath))
        {
            WriteLog($"HIBA: a platform-tools telepítés lefutott, de az adb.exe mégsem jött létre itt: {adbPath}");
            throw new InvalidOperationException(
                $"A platform-tools telepítése nem sikerült (az adb.exe nem jött létre). " +
                $"Ez leggyakrabban akkor fordul elő, ha a licenc-elfogadás nem volt teljes. " +
                $"Részletes napló: {LogFilePath}");
        }
        WriteLog("adb.exe létrejött, platform-tools telepítés sikeres.");

        progress.Report(new AndroidSdkInstallProgress { Phase = AndroidSdkInstallPhase.InstallingEmulator });
        WriteLog("emulator telepítése...");
        var emulatorResult = await RunSdkManagerAsync(sdkManagerPath, sdkRoot, "\"emulator\"", sendYesRepeatedly: false, cancellationToken);
        WriteLog($"emulator telepítés — exit code: {emulatorResult.ExitCode}");
        WriteLog($"--- sdkmanager emulator STDOUT ---{Environment.NewLine}{emulatorResult.StandardOutput}");
        WriteLog($"--- sdkmanager emulator STDERR ---{Environment.NewLine}{emulatorResult.StandardError}");

        progress.Report(new AndroidSdkInstallProgress { Phase = AndroidSdkInstallPhase.InstallingPlatform });
        WriteLog("platforms;android-34 telepítése...");
        var platformResult = await RunSdkManagerAsync(sdkManagerPath, sdkRoot, "\"platforms;android-34\"", sendYesRepeatedly: false, cancellationToken);
        WriteLog($"platform telepítés — exit code: {platformResult.ExitCode}");
        WriteLog($"--- sdkmanager platform STDOUT ---{Environment.NewLine}{platformResult.StandardOutput}");
        WriteLog($"--- sdkmanager platform STDERR ---{Environment.NewLine}{platformResult.StandardError}");

        progress.Report(new AndroidSdkInstallProgress { Phase = AndroidSdkInstallPhase.InstallingSystemImage });
        WriteLog("system image telepítése (ez tart a legtovább)...");
        var systemImageResult = await RunSdkManagerAsync(sdkManagerPath, sdkRoot, $"\"{SystemImagePackage}\"", sendYesRepeatedly: false, cancellationToken);
        WriteLog($"system image telepítés — exit code: {systemImageResult.ExitCode}");
        WriteLog($"--- sdkmanager system-image STDOUT ---{Environment.NewLine}{systemImageResult.StandardOutput}");
        WriteLog($"--- sdkmanager system-image STDERR ---{Environment.NewLine}{systemImageResult.StandardError}");

        // ---- 6. AVD létrehozása ----
        progress.Report(new AndroidSdkInstallProgress { Phase = AndroidSdkInstallPhase.CreatingVirtualDevice });
        if (File.Exists(avdManagerPath))
        {
            WriteLog("AVD létrehozása...");
            var avdResult = await RunProcessAsync(
                avdManagerPath, sdkRoot,
                $"create avd --name {AvdName} --package \"{SystemImagePackage}\" --device \"pixel_6\" --sdcard \"512M\" --force",
                stdinInput: "no",
                cancellationToken);
            WriteLog($"AVD létrehozás — exit code: {avdResult.ExitCode}");
            WriteLog($"--- avdmanager STDOUT ---{Environment.NewLine}{avdResult.StandardOutput}");
            WriteLog($"--- avdmanager STDERR ---{Environment.NewLine}{avdResult.StandardError}");
        }
        else
        {
            WriteLog($"FIGYELMEZTETÉS: avdmanager.bat nem található ({avdManagerPath}) — az AVD-létrehozás kimaradt.");
        }

        // ---- 7. Beállítások frissítése ----
        _settingsService.Current.AndroidSdkRoot = sdkRoot;
        await _settingsService.SaveAsync();
        WriteLog("Beállítások frissítve, telepítés sikeresen befejeződött.");

        progress.Report(new AndroidSdkInstallProgress { Phase = AndroidSdkInstallPhase.Finished, PercentComplete = 100 });

        // Ideiglenes fájlok takarítása — ha ez hibázik, nem kritikus, a telepítés
        // ettől függetlenül sikeres volt.
        try
        {
            if (File.Exists(tempZip)) File.Delete(tempZip);
            if (Directory.Exists(extractTemp)) Directory.Delete(extractTemp, recursive: true);
        }
        catch { /* ignore cleanup errors */ }
    }

    /// <summary>
    /// Letölti az Eclipse Temurin (OpenJDK) JDK 21 LTS Windows x64 ZIP-archívumát (az
    /// Adoptium API "binary/latest" végpontján keresztül), majd kicsomagolja a felhasználó
    /// saját, admin-jog nélkül írható %LOCALAPPDATA% mappájába. Ez a "portable" telepítési
    /// mód SZÁNDÉKOSAN kerüli el a hivatalos .msi telepítőt (installer/latest végpont) —
    /// annak futtatásához (msiexec) admin-jogosultság és UAC-prompt kellene, ami egy
    /// automatizált, felügyelet nélküli telepítésnél (AT Studio első indítása) felesleges
    /// súrlódási pont és hibaforrás. A ZIP-ből kicsomagolt JDK funkcionálisan ugyanaz, mint
    /// az MSI-vel telepített — csak a JAVA_HOME/PATH beállítást nekünk kell elvégeznünk,
    /// amit egyébként az MSI telepítő "FeatureEnvironment" opciója tenne meg helyettünk.
    /// </summary>
    private async Task InstallJavaAsync(IProgress<AndroidSdkInstallProgress> progress, CancellationToken cancellationToken)
    {
        var jdkInstallRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AT-Studio", "jdk-21");

        var tempZip = Path.Combine(Path.GetTempPath(), "at-studio-jdk.zip");
        var extractTemp = Path.Combine(Path.GetTempPath(), "at-studio-jdk-extract");

        progress.Report(new AndroidSdkInstallProgress { Phase = AndroidSdkInstallPhase.DownloadingJava });
        WriteLog($"Java letöltése (ZIP, portable telepítés): {JdkDownloadUrl}");

        await DownloadWithProgressAsync(JdkDownloadUrl, tempZip, progress, cancellationToken, AndroidSdkInstallPhase.DownloadingJava);
        WriteLog("Java (ZIP) letöltés kész.");

        var downloadedFileInfo = new FileInfo(tempZip);
        WriteLog($"Letöltött fájl mérete: {downloadedFileInfo.Length / 1024.0 / 1024.0:0.0} MB");

        // Egy teljes JDK ZIP jellemzően 190-210 MB — ha ennél jóval kisebb, az valószínűleg
        // hibaoldal vagy csonka letöltés volt, nem a valódi archívum.
        const long minimumExpectedBytes = 50L * 1024 * 1024;
        if (downloadedFileInfo.Length < minimumExpectedBytes)
        {
            WriteLog($"HIBA: a letöltött fájl gyanúsan kicsi ({downloadedFileInfo.Length} bájt).");
            throw new InvalidOperationException(
                $"A Java letöltése sikertelen volt — a letöltött fájl mérete ({downloadedFileInfo.Length / 1024} KB) " +
                "jóval kisebb, mint egy érvényes JDK archívum. Ez általában átmeneti hálózati/szerver-problémát " +
                $"jelez. Próbáld újra néhány perc múlva. Részletes napló: {LogFilePath}");
        }

        // A ZIP-fájlok "PK\x03\x04" (0x50 0x4B 0x03 0x04) magic byte-tal kezdődnek — ezt
        // ellenőrizzük, mielőtt megpróbálnánk kicsomagolni, hogy egyértelmű hibaüzenetet
        // kapjunk egy esetleges hibás/hiányos letöltés esetén, ahelyett hogy a ZipFile egy
        // kevésbé érthető kivételt dobna.
        await using (var validationStream = File.OpenRead(tempZip))
        {
            var magicBytes = new byte[4];
            var readCount = await validationStream.ReadAsync(magicBytes, cancellationToken);
            var expectedZipSignature = new byte[] { 0x50, 0x4B, 0x03, 0x04 };

            if (readCount < 4 || !magicBytes.SequenceEqual(expectedZipSignature))
            {
                WriteLog($"HIBA: a letöltött fájl nem érvényes ZIP-aláírással kezdődik. Első bájtok: {BitConverter.ToString(magicBytes, 0, readCount)}");
                throw new InvalidOperationException(
                    "A Java letöltése sikertelen volt — a letöltött fájl nem egy érvényes ZIP-archívum. " +
                    "Ez általában azt jelenti, hogy a letöltési forrás átmenetileg hibás választ adott. " +
                    $"Próbáld újra néhány perc múlva. Részletes napló: {LogFilePath}");
            }
        }

        WriteLog("Letöltött fájl validálva — érvényes ZIP, kicsomagolás indul...");
        progress.Report(new AndroidSdkInstallProgress { Phase = AndroidSdkInstallPhase.InstallingJava });

        if (Directory.Exists(extractTemp))
            Directory.Delete(extractTemp, recursive: true);

        await Task.Run(() => ZipFile.ExtractToDirectory(tempZip, extractTemp), cancellationToken);

        // A ZIP tartalma egyetlen belső mappa alá csomagolódik ki (pl. "jdk-21.0.5+11") —
        // ezt keressük meg és mozgatjuk a végleges, ismert nevű helyre, hogy a kódunk mindig
        // ugyanott találja a "bin\java.exe"-t, függetlenül a build pontos verziószámától.
        var extractedJdkDir = Directory.GetDirectories(extractTemp).FirstOrDefault()
            ?? throw new InvalidOperationException($"A kicsomagolt JDK mappa nem található itt: {extractTemp}. Részletek: {LogFilePath}");

        if (Directory.Exists(jdkInstallRoot))
            Directory.Delete(jdkInstallRoot, recursive: true);

        Directory.CreateDirectory(Path.GetDirectoryName(jdkInstallRoot)!);
        Directory.Move(extractedJdkDir, jdkInstallRoot);

        var javaExePath = Path.Combine(jdkInstallRoot, "bin", "java.exe");
        if (!File.Exists(javaExePath))
        {
            WriteLog($"HIBA: java.exe nem található a kicsomagolt JDK-ban: {javaExePath}");
            throw new InvalidOperationException($"A Java kicsomagolása után a java.exe nem található: {javaExePath}. Részletek: {LogFilePath}");
        }

        WriteLog($"JDK kicsomagolva ide: {jdkInstallRoot}");

        // ---- JAVA_HOME és PATH beállítása FELHASZNÁLÓI szinten (admin-jog nélkül) ----
        // Ez ugyanazt teszi, amit az MSI telepítő FeatureEnvironment funkciója tenne, csak
        // EnvironmentVariableTarget.User szinten — ehhez NEM kell admin-jogosultság, mert a
        // felhasználói környezeti változók a felhasználó saját Registry-hive-jában (HKCU)
        // vannak, amihez mindig van írási jogunk.
        var javaHomeDir = jdkInstallRoot;
        Environment.SetEnvironmentVariable("JAVA_HOME", javaHomeDir, EnvironmentVariableTarget.User);

        var binDir = Path.Combine(javaHomeDir, "bin");
        var currentUserPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
        if (!currentUserPath.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Any(p => string.Equals(p.TrimEnd('\\'), binDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
        {
            var newUserPath = string.IsNullOrEmpty(currentUserPath) ? binDir : $"{currentUserPath};{binDir}";
            Environment.SetEnvironmentVariable("PATH", newUserPath, EnvironmentVariableTarget.User);
        }

        WriteLog("JAVA_HOME és PATH beállítva (felhasználói szint).");

        // A JELENLEGI, futó folyamat számára is beállítjuk, hogy a hívó InstallAsync-ban
        // az azonnali IsJavaAvailable()-ellenőrzés már lássa — enélkül a mostani futó
        // .NET-folyamat még a régi (Java nélküli) PATH-ot látná, és csak egy alkalmazás-
        // újraindítás után válna láthatóvá az új "java" parancs.
        Environment.SetEnvironmentVariable("JAVA_HOME", javaHomeDir, EnvironmentVariableTarget.Process);
        var currentProcessPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? "";
        Environment.SetEnvironmentVariable("PATH", $"{currentProcessPath};{binDir}", EnvironmentVariableTarget.Process);

        try { File.Delete(tempZip); } catch { /* ignore cleanup errors */ }
        try { if (Directory.Exists(extractTemp)) Directory.Delete(extractTemp, recursive: true); } catch { /* ignore */ }
    }

    /// <summary>
    /// Letölt egy fájlt a megadott URL-ről, valós idejű, byte-alapú progress-jelentéssel.
    /// A megadott 'phase' paraméterrel újrahasználható mind a Java (ZIP), mind a Command
    /// Line Tools letöltésénél — a UI ez alapján dönti el, melyik állapot-szöveget mutassa.
    /// </summary>
    private static async Task DownloadWithProgressAsync(
        string url, string destinationPath, IProgress<AndroidSdkInstallProgress> progress,
        CancellationToken cancellationToken, AndroidSdkInstallPhase phase)
    {
        using var httpClient = new HttpClient();
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        var startedAt = DateTime.UtcNow;

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalRead += bytesRead;

            if (totalBytes > 0)
            {
                var percent = (double)totalRead / totalBytes * 100;
                var elapsed = DateTime.UtcNow - startedAt;
                var estimatedTotal = elapsed.TotalSeconds > 0 ? elapsed.TotalSeconds / (totalRead / (double)totalBytes) : 0;
                var remaining = TimeSpan.FromSeconds(Math.Max(0, estimatedTotal - elapsed.TotalSeconds));

                progress.Report(new AndroidSdkInstallProgress
                {
                    Phase = phase,
                    PercentComplete = percent,
                    EstimatedTimeRemaining = remaining
                });
            }
            else
            {
                // Ha a szerver nem ad Content-Length-et (ritka, de előfordulhat), nincs
                // megbízható %-os érték — csak a fázist jelezzük, határozatlan progress-szel.
                progress.Report(new AndroidSdkInstallProgress { Phase = phase });
            }
        }
    }

    /// <summary>Egy elindított külső folyamat eredménye — a naplózáshoz és a hibadiagnosztikához kell,
    /// hogy pontosan lássuk, mit írt ki az sdkmanager/avdmanager, és milyen exit code-dal tért vissza.</summary>
    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    /// <summary>Az sdkmanager.bat hívása — a --licenses hívásnál a felhasználó nevében
    /// (a telepítés megkezdése előtti figyelmeztető dialógus tájékoztat erről) minden
    /// licenckérdésre 'y'-t küldünk, hogy ne akadjon el egy interaktív promptnál.</summary>
    private static Task<ProcessResult> RunSdkManagerAsync(string sdkManagerPath, string sdkRoot, string arguments, bool sendYesRepeatedly, CancellationToken cancellationToken)
    {
        var fullArguments = $"--sdk_root=\"{sdkRoot}\" {arguments}";
        var stdinInput = sendYesRepeatedly ? string.Join("\n", Enumerable.Repeat("y", 20)) : null;
        return RunProcessAsync(sdkManagerPath, sdkRoot, fullArguments, stdinInput, cancellationToken);
    }

    private static async Task<ProcessResult> RunProcessAsync(string fileName, string workingDirectory, string arguments, string? stdinInput, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = stdinInput is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Nem sikerült elindítani a folyamatot: {fileName}");

        if (stdinInput is not null)
        {
            await process.StandardInput.WriteAsync(stdinInput);
            process.StandardInput.Close();
        }

        // A kimenetet beolvassuk (hogy a folyamat ne blokkolódjon egy megtelt buffer miatt),
        // ÉS most már el is tároljuk — a hívó (InstallAsync) ezt írja a naplófájlba, hogy
        // pontosan lásd, mit írt ki az sdkmanager/avdmanager, ha valami nem a várt módon
        // sikerül (pl. egy komponens csendben nem települ, mert egy licenc-prompt
        // formátuma megváltozott egy újabb Command Line Tools-verzióban).
        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(stdOutTask, stdErrTask);

        // Az sdkmanager néha nem-nulla exit code-dal tér vissza akkor is, ha a lényegi
        // telepítés sikeres volt (pl. egy figyelmeztetés miatt) — ezért itt szándékosan
        // NEM dobunk kivételt pusztán az exit code alapján, csak akkor, ha a folyamat
        // egyáltalán nem indult el (lásd fentebb). Az InstallAsync viszont a
        // platform-tools lépésnél EXPLICIT ellenőrzi az adb.exe tényleges létrejöttét,
        // ami megbízhatóbb jelzés, mint az exit code önmagában.
        return new ProcessResult(process.ExitCode, stdOutTask.Result, stdErrTask.Result);
    }
}