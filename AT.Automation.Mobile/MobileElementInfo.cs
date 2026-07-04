namespace AT.Automation.Mobile;

/// <summary>Egy Android UI-elem "lenyomata" — a PageSource XML megfelelő node-jából olvasva.</summary>
public sealed class MobileElementInfo
{
    public string ResourceId { get; init; } = "";
    public string ContentDesc { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string Text { get; init; } = "";
}