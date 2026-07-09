using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AT.Infrastructure;

public interface IScheduledTaskService
{
    /// <summary>A memóriában tartott, betöltött ütemezett feladatok — a SchedulerService is ezt figyeli.</summary>
    IReadOnlyList<ScheduledTask> Tasks { get; }

    Task LoadAsync();
    Task SaveAsync();

    /// <summary>Hozzáad egy új ütemezett feladatot, majd azonnal perzisztál.</summary>
    Task AddAsync(ScheduledTask task);

    /// <summary>Frissíti a listában lévő (Id alapján azonosított) feladatot, majd perzisztál.</summary>
    Task UpdateAsync(ScheduledTask task);

    Task DeleteAsync(string id);
}

/// <summary>
/// Az ütemezett feladatokat egyetlen JSON-fájlban tárolja — hasonlóan a Beállítások és
/// az Előzmények tárolási mintájához. A fájl helye a Beállításokban megadott (vagy alapértelmezett,
/// Asztalra mutató) mappa; a fájlnév fix: scheduled-tasks.json.
/// </summary>
public sealed class ScheduledTaskService : IScheduledTaskService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ISettingsService _settingsService;
    private readonly List<ScheduledTask> _tasks = new();

    public IReadOnlyList<ScheduledTask> Tasks => _tasks;

    public ScheduledTaskService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    private string ResolveFilePath()
    {
        var folder = string.IsNullOrWhiteSpace(_settingsService.Current.TestHistoryFolderPath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            : _settingsService.Current.TestHistoryFolderPath!;

        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "scheduled-tasks.json");
    }

    public async Task LoadAsync()
    {
        _tasks.Clear();

        var path = ResolveFilePath();
        if (!File.Exists(path))
            return;

        try
        {
            await using var stream = File.OpenRead(path);
            var loaded = await JsonSerializer.DeserializeAsync<List<ScheduledTask>>(stream, JsonOptions);
            if (loaded is not null)
                _tasks.AddRange(loaded);
        }
        catch (Exception)
        {
            // Sérült/olvashatatlan fájl esetén üres listával indulunk — nem dobjuk el a hívót,
            // mert ez tipikusan induláskor fut le, és nem szabad, hogy az egész appot elvigye.
        }
    }

    public async Task SaveAsync()
    {
        var path = ResolveFilePath();
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, _tasks, JsonOptions);
    }

    public async Task AddAsync(ScheduledTask task)
    {
        _tasks.Add(task);
        await SaveAsync();
    }

    public async Task UpdateAsync(ScheduledTask task)
    {
        var index = _tasks.FindIndex(t => t.Id == task.Id);
        if (index >= 0)
            _tasks[index] = task;
        else
            _tasks.Add(task);

        await SaveAsync();
    }

    public async Task DeleteAsync(string id)
    {
        _tasks.RemoveAll(t => t.Id == id);
        await SaveAsync();
    }
}
