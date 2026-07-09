using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using AT.App.Models;
using AT.App.Services;
using AT.Core.Models;
using AT.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AT.App.ViewModels;

/// <summary>
/// Az összes korábbi futtatás listázása (Web/Desktop/Mobil közösen) — a közös
/// ITestRunHistoryService-ből olvassa be a mentett TestRunRecord-okat, és
/// soronként lehetővé teszi a HTML riport exportálását. Platform + kategória szerint
/// szűrhető (két ComboBox a lista felett — ugyanaz a PlatformFilter/CategoryFilterOption
/// minta, mint az Ütemezett feladatok nézeten, lásd ScheduledTasksViewModel.cs).
/// </summary>
public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly ITestRunHistoryService _historyService;
    private readonly ITestReportService _reportService;
    private readonly INotificationService _notificationService;
    private readonly ITestCategoryService _categoryService;

    public string Title => "Előzmények";
    public string Description => "A Web / Desktop / Mobil modulokban futtatott tesztek eredményei — soronként riport exportálható belőlük.";

    public ObservableCollection<TestRunSummaryRow> Runs { get; } = new();

    [ObservableProperty]
    private bool isLoading;

    public bool HasRuns => Runs.Count > 0;

    // ===================== PLATFORM + KATEGÓRIA SZŰRŐ =====================
    // Ugyanaz a PlatformFilter/CategoryFilterOption típus, mint a ScheduledTasksViewModel-ben
    // (AT.App.ViewModels namespace) — nem kellett duplikálni, mindkét ViewModel ugyanazt
    // az enumot/wrapper-osztályt használja.

    private List<TestRunRecord> _allRecords = new();

    public IReadOnlyList<PlatformFilter> PlatformFilterOptions { get; } = Enum.GetValues<PlatformFilter>();

    [ObservableProperty]
    private PlatformFilter selectedPlatformFilter = PlatformFilter.All;

    public ObservableCollection<CategoryFilterOption> AvailableCategoryFilters { get; } = new();

    [ObservableProperty]
    private CategoryFilterOption? selectedCategoryFilter;

    partial void OnSelectedPlatformFilterChanged(PlatformFilter value)
    {
        RebuildCategoryFilterOptions();
        ApplyFilter();
    }

    partial void OnSelectedCategoryFilterChanged(CategoryFilterOption? value) => ApplyFilter();

    private void RebuildCategoryFilterOptions()
    {
        var previousSelectionId = SelectedCategoryFilter?.CategoryId;

        AvailableCategoryFilters.Clear();
        AvailableCategoryFilters.Add(CategoryFilterOption.AllOption);

        var relevantCategories = SelectedPlatformFilter switch
        {
            PlatformFilter.Web => _categoryService.GetCategoriesForTarget(AutomationTarget.Web),
            PlatformFilter.Desktop => _categoryService.GetCategoriesForTarget(AutomationTarget.Desktop),
            PlatformFilter.Mobile => _categoryService.GetCategoriesForTarget(AutomationTarget.Android),
            _ => _categoryService.Categories
        };

        foreach (var category in relevantCategories.OrderBy(c => c.Name))
            AvailableCategoryFilters.Add(new CategoryFilterOption(category.Id, category.Name));

        SelectedCategoryFilter = AvailableCategoryFilters.FirstOrDefault(c => c.CategoryId == previousSelectionId)
            ?? CategoryFilterOption.AllOption;
    }

    private bool MatchesFilter(TestRunRecord record)
    {
        var platformMatches = SelectedPlatformFilter switch
        {
            PlatformFilter.Web => record.Target == AutomationTarget.Web,
            PlatformFilter.Desktop => record.Target == AutomationTarget.Desktop,
            PlatformFilter.Mobile => record.Target == AutomationTarget.Android,
            _ => true
        };

        if (!platformMatches)
            return false;

        if (SelectedCategoryFilter is null || SelectedCategoryFilter.CategoryId is null)
            return true;

        return record.CategoryId == SelectedCategoryFilter.CategoryId;
    }

    private void ApplyFilter()
    {
        Runs.Clear();

        foreach (var record in _allRecords.Where(MatchesFilter))
        {
            var categoryLabel = _categoryService.Categories.FirstOrDefault(c => c.Id == record.CategoryId)?.Name ?? "Kategória nélkül";
            Runs.Add(new TestRunSummaryRow(record, categoryLabel));
        }

        OnPropertyChanged(nameof(HasRuns));
    }

    public HistoryViewModel(
        ITestRunHistoryService historyService,
        ITestReportService reportService,
        INotificationService notificationService,
        ITestCategoryService categoryService)
    {
        _historyService = historyService;
        _reportService = reportService;
        _notificationService = notificationService;
        _categoryService = categoryService;

        RebuildCategoryFilterOptions();
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;

        try
        {
            _allRecords = (await _historyService.GetAllRunsAsync()).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _notificationService.Show($"Előzmények betöltése sikertelen: {ex.Message}", NotificationType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ExportReport(TestRunSummaryRow? row)
    {
        if (row is null)
            return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Riport exportálása",
            Filter = "HTML fájl (*.html)|*.html",
            DefaultExt = ".html",
            FileName = string.IsNullOrWhiteSpace(row.Record.TestName) ? "riport.html" : $"{row.Record.TestName}-riport.html"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var html = _reportService.GenerateHtml(row.Record);
            File.WriteAllText(dialog.FileName, html);
            _notificationService.Show("Riport elmentve.", NotificationType.Success);

            Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _notificationService.Show($"Riport exportálása sikertelen: {ex.Message}", NotificationType.Error);
        }
    }

    [RelayCommand]
    private async Task DeleteRunAsync(TestRunSummaryRow? row)
    {
        if (row is null)
            return;

        try
        {
            await _historyService.DeleteRunAsync(row.Record.Id);
            Runs.Remove(row);
            _allRecords.RemoveAll(r => r.Id == row.Record.Id);
            OnPropertyChanged(nameof(HasRuns));
            _notificationService.Show("Előzmény törölve.", NotificationType.Info);
        }
        catch (Exception ex)
        {
            _notificationService.Show($"Törlés sikertelen: {ex.Message}", NotificationType.Error);
        }
    }
}

/// <summary>UI-oldali wrapper egy TestRunRecord köré, olvasható, előfeldolgozott mezőkkel a listához.</summary>
public sealed class TestRunSummaryRow
{
    public TestRunRecord Record { get; }

    /// <summary>Előre feloldott kategória-név (a TestCategory.Id alapján).</summary>
    public string CategoryLabel { get; }

    public TestRunSummaryRow(TestRunRecord record, string categoryLabel)
    {
        Record = record;
        CategoryLabel = categoryLabel;
    }

    public string TestName => string.IsNullOrWhiteSpace(Record.TestName) ? "Névtelen teszt" : Record.TestName;

    public string TargetLabel => Record.Target switch
    {
        AutomationTarget.Web => "Web",
        AutomationTarget.Desktop => "Windows desktop",
        AutomationTarget.Android => "Mobil (Android)",
        _ => Record.Target.ToString()
    };

    public string StartedAtText => Record.StartedAt.ToString("yyyy.MM.dd. HH:mm:ss");

    public string SummaryText => $"{Record.PassedCount}/{Record.TotalSteps} sikeres";

    public bool HasFailures => Record.HasFailures;

    public string DurationText => Record.TotalDuration.TotalMinutes >= 1
        ? $"{(int)Record.TotalDuration.TotalMinutes} perc {Record.TotalDuration.Seconds} mp"
        : $"{Record.TotalDuration.TotalSeconds:0.00} mp";
}
