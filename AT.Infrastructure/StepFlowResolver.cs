using AT.Core.Models;

namespace AT.Infrastructure;

/// <summary>
/// Kiszámítja egy lépéssor bejárási sorrendjét, figyelembe véve az egyes lépések
/// OnSuccessGoToLabel / OnFailureGoToLabel ugrásait. Ezt a logikát mindhárom modul
/// (Web/Desktop/Mobil) ViewModel-je használja a RunStepsCoreAsync ciklusában, hogy
/// a "lépések közötti feltételes logika" (if/else elágazás célcímkékkel) egységesen
/// működjön mindenhol.
///
/// A tényleges lépés-végrehajtást (driver.ExecuteStepAsync hívás) NEM ez az osztály
/// végzi — csak azt dönti el, hogy egy adott lépés sikere/hibája után melyik legyen
/// a KÖVETKEZŐ index a listában. A ViewModel-ek ezt hívják meg minden lépés után.
/// </summary>
public static class StepFlowResolver
{
    /// <summary>
    /// Egyetlen végrehajtás-sorozatban megengedett maximális lépésszám — ha ennyi
    /// lépés lefutott anélkül, hogy a sor véget érne, feltételezzük, hogy a
    /// siker/hiba-ugrások végtelen ciklusba kerültek, és megállítjuk a futtatást
    /// egy hibaüzenettel, ahelyett hogy az alkalmazás lefagyna.
    /// </summary>
    public const int MaxStepExecutions = 1000;

    /// <summary>
    /// Kiszámítja a következő végrehajtandó lépés indexét egy lépés sikere/hibája után.
    /// Visszatérési érték: a következő index a steps listában, vagy null, ha a
    /// futtatásnak véget kell érnie (nincs több lépés, és nincs explicit ugrás sem).
    /// </summary>
    /// <param name="steps">A teljes lépéslista (a jelenlegi sorrendjében).</param>
    /// <param name="currentIndex">Az imént lefutott lépés indexe.</param>
    /// <param name="wasSuccess">Igaz, ha a lépés sikeresen lefutott; hamis, ha hibázott.</param>
    /// <param name="continueOnError">A lépés ContinueOnError beállítása — csak akkor számít, ha wasSuccess hamis és nincs OnFailureGoToLabel.</param>
    /// <param name="shouldStop">Igaz, ha a futtatásnak meg kell állnia (hiba történt, nincs ugrás, és ContinueOnError hamis).</param>
    public static int? ResolveNextIndex(
        IReadOnlyList<TestStep> steps,
        int currentIndex,
        bool wasSuccess,
        bool continueOnError,
        out bool shouldStop)
    {
        shouldStop = false;
        var current = steps[currentIndex];

        var goToLabel = wasSuccess ? current.OnSuccessGoToLabel : current.OnFailureGoToLabel;

        if (!string.IsNullOrWhiteSpace(goToLabel))
        {
            var targetIndex = FindIndexByLabel(steps, goToLabel);
            if (targetIndex is not null)
                return targetIndex;

            // A hivatkozott címke nem található (pl. törölt lépésre mutat) — ilyenkor
            // a normál, soron következő viselkedésre esünk vissza, nem szakítjuk meg
            // csendben vagy értelmetlenül a futtatást.
        }

        if (!wasSuccess && string.IsNullOrWhiteSpace(goToLabel) && !continueOnError)
        {
            shouldStop = true;
            return null;
        }

        var nextIndex = currentIndex + 1;
        return nextIndex < steps.Count ? nextIndex : null;
    }

    /// <summary>Megkeresi egy adott Label-lel rendelkező lépés indexét — null, ha nincs ilyen.</summary>
    public static int? FindIndexByLabel(IReadOnlyList<TestStep> steps, string label)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            if (string.Equals(steps[i].Label, label, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return null;
    }

    /// <summary>
    /// Automatikus alap-címke generálása új lépéshez ("Lépés 1", "Lépés 2", ...) —
    /// az első olyan sorszámot adja, ami még nem ütközik egyetlen meglévő Label-lel sem.
    /// </summary>
    public static string GenerateNextLabel(IReadOnlyList<TestStep> existingSteps)
    {
        var existingLabels = existingSteps.Select(s => s.Label).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidate = existingSteps.Count + 1;
        while (existingLabels.Contains($"Lépés {candidate}"))
            candidate++;

        return $"Lépés {candidate}";
    }
}
