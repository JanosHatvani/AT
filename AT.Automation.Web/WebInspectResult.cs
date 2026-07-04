namespace AT.Automation.Web;

/// <summary>Az utoljára az egér alatt lévő DOM-elem adatai — a böngészőbe injektált JS adja vissza JSON-ként.</summary>
public sealed class WebInspectResult
{
    public string Tag { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string XPath { get; set; } = "";
}