namespace AT.Automation.Mobile;

// Egy Android UI-elem "lenyomata" — a PageSource XML megfelelő node-jából olvasva.
public sealed class MobileElementInfo
{
    public string ResourceId { get; init; } = "";
    public string ContentDesc { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string Text { get; init; } = "";
}