using System.Text.Json;

namespace AT.Infrastructure;

public interface ISettingsService
{
    /// <summary>A jelenleg betöltött beállítások — a ViewModel-ek ezt olvassák és módosítják.</summary>
    AppSettings Current { get; }

    Task LoadAsync();
    Task SaveAsync();
}

/// <summary>
/// Egyszerű, fájl-alapú beállítás-tárolás (%AppData%\AT\settings.json).
/// Nem igényel adatbázist — ennyi konfigurációs adathoz ez a legegyszerűbb,
/// megbízható megoldás, és könnyen kicserélhető lesz, ha később mégis kellene SQLite.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AT", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppSettings Current { get; private set; } = new();

    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(FilePath))
                return;

            var json = await File.ReadAllTextAsync(FilePath);
            Current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            // Sérült vagy olvashatatlan beállítás-fájl esetén inkább alapértelmezettel indulunk,
            // mint hogy elakadjon az alkalmazás induláskor.
            Current = new AppSettings();
        }
    }

    public async Task SaveAsync()
    {
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(Current, JsonOptions);
        await File.WriteAllTextAsync(FilePath, json);
    }
}