namespace AT.Infrastructure;

/// <summary>
/// Egyetlen, önálló HTML fájlt generál egy TestRunRecord-ból — a képernyőképek
/// Base64-be ágyazva kerülnek bele, így a riport egyetlen fájlként is teljesen
/// önmagában megjeleníthető, nincs szükség mellékelt képfájlokra.
/// </summary>
public interface ITestReportService
{
    /// <summary>Legenerálja a teljes HTML riportot szövegként.</summary>
    string GenerateHtml(TestRunRecord record);
}
