using AT.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;

namespace AT.App.Models;

/// <summary>UI-oldali wrapper egy AT.Core.Models.TestStep köré, a futási státusz és a szerkeszthetőség miatt.</summary>
public sealed partial class TestStepRow : ObservableObject
{
    [ObservableProperty]
    private TestStep step = null!;

    [ObservableProperty]
    private TestStatus status = TestStatus.NotRun;

    [ObservableProperty]
    private string? message;

    /// <summary>Az utolsó futtatás időtartama — csak futásidejű adat, nem kerül XML-be.</summary>
    [ObservableProperty]
    private TimeSpan? duration;

    /// <summary>Olvasható formában, pl. "1.23 mp" — üres kötőjel, ha még nem futott.</summary>
    public string DurationText => Duration is { } d ? $"{d.TotalSeconds:0.00} mp" : "—";

    partial void OnDurationChanged(TimeSpan? value) => OnPropertyChanged(nameof(DurationText));
}