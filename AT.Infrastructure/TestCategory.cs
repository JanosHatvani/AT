using System.IO;
using System.Linq;
using System.Text.Json;
using AT.Core.Models;

namespace AT.Infrastructure;

/// <summary>
/// Egy teszt-kategória (pl. "Vevői megrendelés", "Betárolás") — a tesztek (Web/Desktop/
/// Mobil lépéssorok és az ebből létrehozott ütemezések) ebbe sorolhatók be, hogy az
/// Ütemezett feladatok és Előzmények nézeteken platform+kategória szerint lehessen szűrni.
/// </summary>
public sealed class TestCategory
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "";

    /// <summary>Mely platformokon (Web/Desktop/Android) választható ez a kategória a teszt
    /// létrehozásakor. Legalább egy elemet kell tartalmaznia.</summary>
    public List<AutomationTarget> AllowedTargets { get; set; } = new();

    /// <summary>
    /// A ComboBox (Web/Desktop/Mobil "Kategória" választó) néhány esetben a kiválasztott
    /// (de nem legördített) állapot megjelenítésénél nem a DisplayMemberPath-ot, hanem az
    /// elem ToString()-ját használja — enélkül a felülírás nélkül a teljes típusnév
    /// ("AT.Infrastructure.TestCategory") jelenne meg a mezőben kiválasztás után.
    /// </summary>
    public override string ToString() => Name;
}

public interface ITestCategoryService
{
    /// <summary>A memóriában tartott, betöltött kategóriák.</summary>
    IReadOnlyList<TestCategory> Categories { get; }

    Task LoadAsync();
    Task SaveAsync();

    Task AddAsync(TestCategory category);
    Task UpdateAsync(TestCategory category);
    Task DeleteAsync(string id);

    /// <summary>Az adott platformon választható kategóriák — ezt használja a Web/Desktop/Mobil
    /// nézetek kategória-választója, illetve az Ütemezett feladatok/Előzmények szűrője.</summary>
    IReadOnlyList<TestCategory> GetCategoriesForTarget(AutomationTarget target);
}

/// <summary>
/// A kategóriákat egyetlen JSON-fájlban tárolja — ugyanazt a mintát követi, mint a
/// ScheduledTaskService. Ha még nincs egyetlen kategória sem (pl. friss telepítés), az
/// első LoadAsync automatikusan létrehoz egy "Általános" alap-kategóriát minden
/// platformra érvényesként, hogy a kategória-választás kötelező volta ne akassza el
/// azonnal a használatot.
/// </summary>
public sealed class TestCategoryService : ITestCategoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly ISettingsService _settingsService;
    private readonly List<TestCategory> _categories = new();

    public IReadOnlyList<TestCategory> Categories => _categories;

    public TestCategoryService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    private string ResolveFilePath()
    {
        var folder = string.IsNullOrWhiteSpace(_settingsService.Current.TestHistoryFolderPath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            : _settingsService.Current.TestHistoryFolderPath!;

        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "test-categories.json");
    }

    public async Task LoadAsync()
    {
        _categories.Clear();

        var path = ResolveFilePath();
        if (File.Exists(path))
        {
            try
            {
                await using var stream = File.OpenRead(path);
                var loaded = await JsonSerializer.DeserializeAsync<List<TestCategory>>(stream, JsonOptions);
                if (loaded is not null)
                    _categories.AddRange(loaded);
            }
            catch (Exception)
            {
                // Sérült/olvashatatlan fájl esetén üres listával indulunk — az alábbi
                // alap-kategória létrehozás ilyenkor is lefut, hogy a program használható maradjon.
            }
        }

        if (_categories.Count == 0)
        {
            _categories.Add(new TestCategory
            {
                Name = "Általános",
                AllowedTargets = new List<AutomationTarget> { AutomationTarget.Web, AutomationTarget.Desktop, AutomationTarget.Android }
            });
            await SaveAsync();
        }
    }

    public async Task SaveAsync()
    {
        var path = ResolveFilePath();
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, _categories, JsonOptions);
    }

    public async Task AddAsync(TestCategory category)
    {
        _categories.Add(category);
        await SaveAsync();
    }

    public async Task UpdateAsync(TestCategory category)
    {
        var index = _categories.FindIndex(c => c.Id == category.Id);
        if (index >= 0)
            _categories[index] = category;
        else
            _categories.Add(category);

        await SaveAsync();
    }

    public async Task DeleteAsync(string id)
    {
        _categories.RemoveAll(c => c.Id == id);
        await SaveAsync();
    }

    public IReadOnlyList<TestCategory> GetCategoriesForTarget(AutomationTarget target)
        => _categories.Where(c => c.AllowedTargets.Contains(target)).ToList();
}
