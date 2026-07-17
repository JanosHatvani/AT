using AT.Core.Contracts;

namespace AT.App.ViewModels;

// Egy lehetséges lokátor a rögzített elemhez — az Elem-kereső ezekből listáz.
public sealed class LocatorCandidate
{
    public required LocatorType Type { get; init; }
    public required string Label { get; init; }
    public required string Value { get; init; }

    /// <summary>1-alapú index (1 = első találat) — hogy a Value-val megegyező lokátorra
    /// hány találat közül hányadik ez a konkrét elem. Null, ha a lokátor egyedi (csak 1
    /// találat van rá) — ilyenkor a lépésbe nem kell ElementIndex-et felvenni.</summary>
    public int? MatchIndex { get; init; }

    /// <summary>Hány elem illik összesen erre a lokátorra — csak tájékoztató, a Label-ben
    /// jelenik meg (pl. "content-desc — lokátor 2. eleme (összesen 4)").</summary>
    public int? MatchCount { get; init; }
}
