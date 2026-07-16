using System;
using System.IO;

namespace AT.Automation.Mobile;


// Az Android SDK eszközeinek (emulator.exe, adb.exe) feloldása. Elsőként a
// Beállításokból kapott felülírást használja (ha van), különben az ANDROID_SDK_ROOT
// vagy ANDROID_HOME környezeti változóra esik vissza — ugyanaz az elv, mint a régi,
// részben megírt kódban, csak egy helyre összeszedve, konfigurálhatóan.

internal static class AndroidSdkLocator
{
    public static string ResolveSdkRoot(string? overrideRoot = null)
    {
        var sdkRoot = !string.IsNullOrWhiteSpace(overrideRoot)
            ? overrideRoot
            : Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT") ?? Environment.GetEnvironmentVariable("ANDROID_HOME");

        if (string.IsNullOrWhiteSpace(sdkRoot) || !Directory.Exists(sdkRoot))
        {
            throw new InvalidOperationException(
                "Nem található Android SDK. Állítsd be a Beállítások oldalon az SDK gyökérmappáját, " +
                "vagy az ANDROID_SDK_ROOT / ANDROID_HOME környezeti változót.");
        }

        return sdkRoot;
    }

    public static string ResolveEmulatorPath(string? overrideRoot = null)
    {
        var path = Path.Combine(ResolveSdkRoot(overrideRoot), "emulator", "emulator.exe");
        if (!File.Exists(path))
            throw new InvalidOperationException($"Nem található az emulator.exe a várt helyen: {path}");
        return path;
    }

    public static string ResolveAdbPath(string? overrideRoot = null)
    {
        var path = Path.Combine(ResolveSdkRoot(overrideRoot), "platform-tools", "adb.exe");
        if (!File.Exists(path))
            throw new InvalidOperationException($"Nem található az adb.exe a várt helyen: {path}");
        return path;
    }

    /// <summary>
    /// Nem dobó, gyors ellenőrzés — a UI (Mobil ViewModel) ezzel tudja PROAKTÍVAN,
    /// futtatás-indítás ELŐTT jelezni a felhasználónak, ha nincs beállítva/telepítve
    /// az SDK, ahelyett hogy csak egy lépés menet közbeni sikertelenségekor derülne ki.
    /// Csak a gyökérmappa létezését ellenőrzi, az adb.exe/emulator.exe pontos meglétét
    /// nem — egy hiányos, félig telepített SDK-t így is elkaphat a tényleges futtatás,
    /// de a leggyakoribb esetet (SDK egyáltalán nincs megadva/telepítve) már itt jelzi.
    /// </summary>
    public static bool IsSdkAvailable(string? overrideRoot = null)
    {
        var sdkRoot = !string.IsNullOrWhiteSpace(overrideRoot)
            ? overrideRoot
            : Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT") ?? Environment.GetEnvironmentVariable("ANDROID_HOME");

        return !string.IsNullOrWhiteSpace(sdkRoot) && Directory.Exists(sdkRoot);
    }

    /// <summary>
    /// Ugyanaz, mint IsSdkAvailable, de emberi olvasásra szánt, konkrét okot ad vissza,
    /// ha valami hiányzik — null-t ad vissza, ha minden rendben van. A UI-oldali
    /// figyelmeztető üzenetekhez (pl. "Futtatás előtt" ellenőrzés) ez a hasznosabb forma,
    /// mert nem kell a hívónak külön eldöntenie, mi a pontos hiányosság szövege.
    /// </summary>
    public static string? TryDescribeMissingSdk(string? overrideRoot = null)
    {
        var sdkRoot = !string.IsNullOrWhiteSpace(overrideRoot)
            ? overrideRoot
            : Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT") ?? Environment.GetEnvironmentVariable("ANDROID_HOME");

        if (string.IsNullOrWhiteSpace(sdkRoot))
        {
            return "Nincs beállítva Android SDK gyökérmappa. Add meg a Beállítások oldalon, " +
                   "vagy állítsd be az ANDROID_SDK_ROOT / ANDROID_HOME környezeti változót.";
        }

        if (!Directory.Exists(sdkRoot))
        {
            return $"A megadott Android SDK mappa nem található: {sdkRoot}. " +
                   "Ellenőrizd az elérési utat a Beállítások oldalon.";
        }

        var adbPath = Path.Combine(sdkRoot, "platform-tools", "adb.exe");
        if (!File.Exists(adbPath))
        {
            return $"Az adb.exe nem található a várt helyen ({adbPath}) — az Android SDK telepítése " +
                   "valószínűleg hiányos. Telepítsd a \"platform-tools\" komponenst az Android SDK " +
                   "Manager-ből (Android Studio → Settings → Languages & Frameworks → Android SDK).";
        }

        return null;
    }
}
