namespace AT.Infrastructure;

/// <summary>
/// A futtatásonkénti képernyőkép-almappa nevének előállítása — mindhárom modul
/// (Web/Desktop/Mobil) ugyanezt használja, hogy a mappaszerkezet konzisztens legyen:
///   {ScreenshotFolderPath}/{SanitizedTestName}_{yyyyMMdd_HHmmss}/
/// Ez teszi lehetővé, hogy a HTML riport pontosan tudja, melyik képek tartoznak
/// az adott futtatáshoz.
/// </summary>
public static class ScreenshotFolderResolver
{
    /// <summary>
    /// Létrehozza (ha kell) és visszaadja az adott futtatáshoz tartozó almappa teljes
    /// elérési útját. A testName üres/whitespace esetén "teszt" alapnevet használ.
    /// </summary>
    public static string CreateRunFolder(string baseFolder, string? testName, DateTime runStartedAt)
    {
        var sanitizedName = SanitizeFileName(string.IsNullOrWhiteSpace(testName) ? "teszt" : testName);
        var folderName = $"{sanitizedName}_{runStartedAt:yyyyMMdd_HHmmss}";
        var fullPath = Path.Combine(baseFolder, folderName);

        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    public static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return sanitized.Length > 60 ? sanitized[..60] : sanitized;
    }
}
