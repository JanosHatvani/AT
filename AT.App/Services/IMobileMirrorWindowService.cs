namespace AT.App.Services;

/// <summary>
/// A MobileTestViewModel ezen keresztül nyitja/mutatja/rejti el az Élő kijelző
/// önálló ablakát, anélkül hogy közvetlenül WPF Window-típusra hivatkozna.
/// A tényleges implementáció (MobileMirrorWindowService) a View-rétegben él,
/// ahol a MobileScreenMirrorWindow ténylegesen létrejön.
/// </summary>
public interface IMobileMirrorWindowService
{
    /// <summary>Igaz, ha az ablak jelenleg látható (nem rejtett/bezárt állapotban van).</summary>
    bool IsOpen { get; }

    /// <summary>Megnyitja az ablakot, ha még nem létezik, vagy előtérbe hozza/visszamutatja, ha rejtve volt.</summary>
    void ShowOrActivate(object dataContext);

    /// <summary>
    /// Elrejti az ablakot (nem szünteti meg), és leválasztja a DataContext-et.
    /// A MobileTestViewModel akkor hívja, amikor elnavigálnak róla egy másik
    /// oldalra — így egy régi, már nem használt ViewModel-hez tartozó ablak
    /// nem marad látva a háttérben.
    /// </summary>
    void Hide();

    /// <summary>Jelzés esemény, amikor a felhasználó bezárja (elrejti) az ablakot — a ViewModel ekkor frissítheti az IsMirrorWindowOpen jelzőt.</summary>
    event EventHandler? Closed;
}
