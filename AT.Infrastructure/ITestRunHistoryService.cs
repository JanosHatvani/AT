namespace AT.Infrastructure;

/// <summary>
/// A futtatási előzmények (TestRunRecord) mentését és visszaolvasását végzi.
/// Mindhárom modul (Web/Desktop/Mobil) ugyanazt a szolgáltatást használja, közös
/// mappába mentve — ezt böngészi az Előzmények nézet, és ebből generálódik a
/// HTML riport is.
/// </summary>
public interface ITestRunHistoryService
{
    /// <summary>Elmenti a futtatási rekordot egy önálló JSON fájlba a history-mappában.</summary>
    Task SaveRunAsync(TestRunRecord record);

    /// <summary>Beolvassa az összes mentett futtatási rekordot, a legutóbbival kezdve.</summary>
    Task<IReadOnlyList<TestRunRecord>> GetAllRunsAsync();

    /// <summary>Törli egy adott futtatási rekord JSON fájlját.</summary>
    Task DeleteRunAsync(string id);
}
