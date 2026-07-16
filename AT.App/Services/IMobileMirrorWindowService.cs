namespace AT.App.Services;


// A MobileTestViewModel ezen keresztül nyitja/mutatja/rejti el az Élő kijelző
// önálló ablakát, anélkül hogy közvetlenül WPF Window-típusra hivatkozna.
// A tényleges implementáció (MobileMirrorWindowService) a View-rétegben él,
// ahol a MobileScreenMirrorWindow ténylegesen létrejön.

public interface IMobileMirrorWindowService
{
    // Igaz, ha az ablak jelenleg látható (nem rejtett/bezárt állapotban van).
    bool IsOpen { get; }

    //Megnyitja az ablakot, ha még nem létezik, vagy előtérbe hozza/visszamutatja, ha rejtve volt.
    void ShowOrActivate(object dataContext);


    // Elrejti az ablakot (nem szünteti meg), és leválasztja a DataContext-et.
    // A MobileTestViewModel akkor hívja, amikor elnavigálnak róla egy másik
    // oldalra — így egy régi, már nem használt ViewModel-hez tartozó ablak
    // nem marad látva a háttérben.

    void Hide();

    // Jelzés esemény, amikor a felhasználó bezárja (elrejti) az ablakot — a ViewModel ekkor frissítheti az IsMirrorWindowOpen jelzőt.
    event EventHandler? Closed;
}
