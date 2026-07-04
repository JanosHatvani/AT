using AT.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

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
}