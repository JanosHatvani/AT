using AT.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;

namespace AT.App.Models;

// UI-oldali wrapper egy AT.Core.Models.TestStep köré, a futási státusz és a szerkeszthetőség miatt.
public sealed partial class TestStepRow : ObservableObject
{
    [ObservableProperty]
    private TestStep step = null!;

    [ObservableProperty]
    private TestStatus status = TestStatus.NotRun;

    [ObservableProperty]
    private string? message;

    // Az utolsó futtatás időtartama — csak futásidejű adat, nem kerül XML-be
    [ObservableProperty]
    private TimeSpan? duration;

    // Az utolsó futtatáshoz készült képernyőkép teljes elérési útja, ha a Beállításokban
    // beállított mód szerint készült ilyen. A HTML riport ebből ágyazza be a képet.
    // Csak futásidejű adat, nem kerül XML-be (a lépéssor-mentés/betöltés nem érinti).

    [ObservableProperty]
    private string? screenshotPath;


    // Hány próbálkozásra sikerült (vagy hiúsult meg véglegesen) a lépés a legutóbbi
    // futtatáskor — lásd TestStep.RetryCount. 1 = elsőre sikerült/hibázott, retry nélkül.
    // Csak futásidejű adat, nem kerül XML-be.

    [ObservableProperty]
    private int attemptCount = 1;

    // Olvasható formában, pl. "1.23 mp" — üres kötőjel, ha még nem futott.
    public string DurationText => Duration is { } d ? $"{d.TotalSeconds:0.00} mp" : "—";

    // UI-megjelenítéshez: "" ha nem volt retry (AttemptCount &lt;= 1), egyébként
    // pl. " (2. próbálkozásra)" — a lépés neve mellé fűzhető a lépéslista sorában.
    public string AttemptSummaryText => AttemptCount > 1 ? $" ({AttemptCount}. próbálkozásra)" : "";

    partial void OnDurationChanged(TimeSpan? value) => OnPropertyChanged(nameof(DurationText));
    partial void OnAttemptCountChanged(int value) => OnPropertyChanged(nameof(AttemptSummaryText));
}
