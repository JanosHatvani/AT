namespace AT.Automation.Desktop;

/// <summary>Egy UI Automation elem "lenyomata" az elemfa-böngészőhöz — csak a lokátorhoz hasznos mezők.</summary>
public sealed class DesktopElementNode
{
    public string AutomationId { get; init; } = "";
    public string Name { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string ControlType { get; init; } = "";
    public List<DesktopElementNode> Children { get; init; } = new();

    // 1-alapú index (1 = első találat) + összes találat, attribútumonként — ugyanaz a
    // szerep, mint a Mobil/Web megfelelőinél.
    public int AutomationIdMatchIndex { get; init; }
    public int AutomationIdMatchCount { get; init; }
    public int NameMatchIndex { get; init; }
    public int NameMatchCount { get; init; }
    public int ClassNameMatchIndex { get; init; }
    public int ClassNameMatchCount { get; init; }

    /// <summary>Fa-nézetben megjelenő, olvasható címke.</summary>
    public string DisplayLabel
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(ControlType))
                parts.Add(ControlType);

            if (!string.IsNullOrWhiteSpace(Name))
                parts.Add($"\"{Name}\"");
            else if (!string.IsNullOrWhiteSpace(AutomationId))
                parts.Add($"#{AutomationId}");

            return parts.Count > 0 ? string.Join(" ", parts) : "(névtelen elem)";
        }
    }
}
