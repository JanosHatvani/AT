namespace AT.Automation.Mobile;

// Egy Android UI-elem "lenyomata" — a PageSource XML megfelelő node-jából olvasva.
public sealed class MobileElementInfo
{
    public string ResourceId { get; init; } = "";
    public string ContentDesc { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string Text { get; init; } = "";

    // 1-alapú index (1 = első találat) + összes találat, attribútumonként — ez adja
    // meg, hányadik egyező elem az, amire a felhasználó kattintott, amikor egy lokátor
    // (pl. ugyanaz a resource-id) több elemre is illik egy listás/ismétlődő UI-ban.
    // Az 1-alapúság szándékos: ugyanaz a szám jelenik meg a felületen és kerül be a
    // lépés ElementIndex mezőjébe, amit a felhasználó ténylegesen beírna ("a 3. elem").
    public int ResourceIdMatchIndex { get; init; }
    public int ResourceIdMatchCount { get; init; }
    public int ContentDescMatchIndex { get; init; }
    public int ContentDescMatchCount { get; init; }
    public int ClassNameMatchIndex { get; init; }
    public int ClassNameMatchCount { get; init; }
}
