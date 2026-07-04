using AT.Core.Contracts;

namespace AT.App.ViewModels;

/// <summary>Egy lehetséges lokátor a rögzített elemhez — az Elem-kereső ezekből listáz.</summary>
public sealed class LocatorCandidate
{
    public required LocatorType Type { get; init; }
    public required string Label { get; init; }
    public required string Value { get; init; }
}