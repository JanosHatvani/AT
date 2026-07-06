using System.Text.Json;

namespace AT.Infrastructure;

/// <summary>
/// Fájl-alapú history-tárolás: minden futtatás egy önálló JSON fájlba kerül
/// a beállított (vagy alapértelmezett Asztal) mappában, "testrun_" prefixszel.
/// Ugyanaz a minta, mint a SettingsService-nél — nincs szükség adatbázisra
/// ehhez a mennyiségű, ritkán módosuló adathoz.
/// </summary>
public sealed class TestRunHistoryService : ITestRunHistoryService
{
    private const string FilePrefix = "testrun_";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ISettingsService _settingsService;

    public TestRunHistoryService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// A history-mappa feloldása: a Beállításokban megadott érték, vagy ha üres,
    /// az Asztal — ugyanaz a "üres → Asztal" logika, mint a ScreenshotFolderPath-nál.
    /// </summary>
    private string ResolveFolder()
    {
        var folder = _settingsService.Current.TestHistoryFolderPath;
        return string.IsNullOrWhiteSpace(folder)
            ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            : folder;
    }

    public async Task SaveRunAsync(TestRunRecord record)
    {
        var folder = ResolveFolder();
        Directory.CreateDirectory(folder);

        var fileName = $"{FilePrefix}{record.StartedAt:yyyyMMdd_HHmmss}_{record.Id}.json";
        var fullPath = Path.Combine(folder, fileName);

        var json = JsonSerializer.Serialize(record, JsonOptions);
        await File.WriteAllTextAsync(fullPath, json);
    }

    public async Task<IReadOnlyList<TestRunRecord>> GetAllRunsAsync()
    {
        var folder = ResolveFolder();
        if (!Directory.Exists(folder))
            return Array.Empty<TestRunRecord>();

        var results = new List<TestRunRecord>();

        foreach (var file in Directory.EnumerateFiles(folder, $"{FilePrefix}*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var record = JsonSerializer.Deserialize<TestRunRecord>(json, JsonOptions);
                if (record is not null)
                    results.Add(record);
            }
            catch
            {
                // Sérült/olvashatatlan egyedi rekord esetén kihagyjuk, nem szakítjuk
                // meg a teljes lista betöltését emiatt.
            }
        }

        return results.OrderByDescending(r => r.StartedAt).ToList();
    }

    public Task DeleteRunAsync(string id)
    {
        var folder = ResolveFolder();
        if (!Directory.Exists(folder))
            return Task.CompletedTask;

        var match = Directory.EnumerateFiles(folder, $"{FilePrefix}*.json")
            .FirstOrDefault(f => f.Contains(id, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
            File.Delete(match);

        return Task.CompletedTask;
    }
}
