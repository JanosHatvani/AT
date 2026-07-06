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
/// soronként lehetővé teszi a HTML riport exportálását.
/// </summary>
public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly ITestRunHistoryService _historyService;
    private readonly ITestReportService _reportService;
    private readonly INotificationService _notificationService;

    public string Title => "Előzmények";
    public string Description => "A Web / Desktop / Mobil modulokban futtatott tesztek eredményei — soronként riport exportálható belőlük.";

    public ObservableCollection<TestRunSummaryRow> Runs { get; } = new();

    [ObservableProperty]
    private bool isLoading;

    public bool HasRuns => Runs.Count > 0;

    public HistoryViewModel(ITestRunHistoryService historyService, ITestReportService reportService, INotificationService notificationService)
    {
        _historyService = historyService;
        _reportService = reportService;
        _notificationService = notificationService;

        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;

        try
        {
            var records = await _historyService.GetAllRunsAsync();

            Runs.Clear();
            foreach (var record in records)
                Runs.Add(new TestRunSummaryRow(record));

            OnPropertyChanged(nameof(HasRuns));
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

    public TestRunSummaryRow(TestRunRecord record)
    {
        Record = record;
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