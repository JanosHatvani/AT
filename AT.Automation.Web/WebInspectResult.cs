namespace AT.Automation.Web;

/// <summary>Az utoljára az egér alatt lévő DOM-elem adatai — a böngészőbe injektált JS adja vissza JSON-ként.</summary>
public sealed class WebInspectResult
{
    public string Tag { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string XPath { get; set; } = "";

    // 1-alapú index (1 = első találat) + összes találat, attribútumonként — ugyanaz a
    // szerep, mint a MobileElementInfo-nál: megmutatja, hányadik egyező elem az, amire
    // rákattintottunk.
    public int IdMatchIndex { get; set; }
    public int IdMatchCount { get; set; }
    public int NameMatchIndex { get; set; }
    public int NameMatchCount { get; set; }
    public int ClassNameMatchIndex { get; set; }
    public int ClassNameMatchCount { get; set; }
}
