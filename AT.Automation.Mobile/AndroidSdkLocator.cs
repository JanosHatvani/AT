namespace AT.Automation.Mobile;

/// <summary>
/// Az Android SDK eszközeinek (emulator.exe, adb.exe) feloldása. Elsőként a
/// Beállításokból kapott felülírást használja (ha van), különben az ANDROID_SDK_ROOT
/// vagy ANDROID_HOME környezeti változóra esik vissza — ugyanaz az elv, mint a régi,
/// részben megírt kódban, csak egy helyre összeszedve, konfigurálhatóan.
/// </summary>
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
}