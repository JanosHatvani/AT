using System.Xml.Serialization;
using AT.Core.Contracts;
using AT.Core.Models;

namespace AT.Infrastructure;

/// <summary>
/// Egy elmentett lépéssor XML-reprezentációja. A gyökérelem attribútumként tárolja,
/// melyik modulhoz (Web / Desktop / Android / iOS) készült — ez teszi lehetővé, hogy
/// betöltéskor ellenőrizni lehessen, csak a megfelelő modulba töltődjön be.
/// </summary>
[XmlRoot("ATTestSuite")]
public sealed class TestSuiteFile
{
    [XmlAttribute("target")]
    public AutomationTarget Target { get; set; }

    [XmlAttribute("name")]
    public string? Name { get; set; }

    /// <summary>A teszt-kategória Id-ja (TestCategory.Id) — a teszt "fej" adata, ugyanúgy
    /// mint a Name/Target, nem lépésenkénti. Régebbi, kategória nélkül mentett fájlok
    /// betöltéskor null-t adnak vissza (az XmlAttribute nem kötelező elem).</summary>
    [XmlAttribute("categoryId")]
    public string? CategoryId { get; set; }

    [XmlAttribute("savedAtUtc")]
    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;

    [XmlAttribute("formatVersion")]
    public int FormatVersion { get; set; } = 1;

    [XmlElement("Step")]
    public List<TestStepDto> Steps { get; set; } = new();
}

/// <summary>Egy lépés XML-re szabott, szabadon szerializálható mása (a TestStep "required"/"init" tagjai nélkül).</summary>
public sealed class TestStepDto
{
    [XmlAttribute("id")]
    public string Id { get; set; } = "";

    [XmlAttribute("name")]
    public string Name { get; set; } = "";

    [XmlAttribute("action")]
    public string Action { get; set; } = "";

    [XmlElement("Locator")]
    public string? Locator { get; set; }

    [XmlAttribute("locatorType")]
    public LocatorType LocatorType { get; set; }

    [XmlElement("Value")]
    public string? Value { get; set; }

    [XmlElement("TargetLocator")]
    public string? TargetLocator { get; set; }

    [XmlAttribute("targetLocatorType")]
    public LocatorType TargetLocatorType { get; set; }

    [XmlAttribute("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Hiba esetén ennyiszer próbálja újra a lépést, mielőtt véglegesen hibásnak
    /// jelölné — lásd TestStep.RetryCount. Egyszerű, nem-nullable int, ugyanúgy mint a
    /// TimeoutSeconds, mert alapértelmezetten 0 (nincs retry) — nincs szükség a
    /// ElementIndex-nél alkalmazott "hiányzó vs. nulla" megkülönböztetésre.</summary>
    [XmlAttribute("retryCount")]
    public int RetryCount { get; set; }

    /// <summary>"Self-healing" tartalék lokátor — lásd TestStep.FallbackLocator. Ugyanaz a
    /// minta, mint a TargetLocator/TargetLocatorType párnál.</summary>
    [XmlElement("FallbackLocator")]
    public string? FallbackLocator { get; set; }

    [XmlAttribute("fallbackLocatorType")]
    public LocatorType FallbackLocatorType { get; set; }

    [XmlAttribute("continueOnError")]
    public bool ContinueOnError { get; set; }

    [XmlAttribute("skip")]
    public bool Skip { get; set; }

    /// <summary>A lépés saját azonosító címkéje — lásd TestStep.Label.</summary>
    [XmlAttribute("label")]
    public string Label { get; set; } = "";

    /// <summary>Siker esetén ugrás célja (Label) — lásd TestStep.OnSuccessGoToLabel.</summary>
    [XmlElement("OnSuccessGoToLabel")]
    public string? OnSuccessGoToLabel { get; set; }

    /// <summary>Hiba esetén ugrás célja (Label) — lásd TestStep.OnFailureGoToLabel.</summary>
    [XmlElement("OnFailureGoToLabel")]
    public string? OnFailureGoToLabel { get; set; }

    /// <summary>Hányadik találattal dolgozzon a lépés, ha a lokátor több elemre is illik —
    /// 1-alapú, emberi számozás. Lásd TestStep.ElementIndex. Az XmlElement-et (nem
    /// XmlAttribute-ot) azért használjuk, mert nullable int-eknél az XmlAttribute
    /// problémásabban kezeli a hiányzó/null értéket régi, ElementIndex nélkül mentett
    /// fájloknál — az XmlElement egyszerűen kimarad a régi XML-ekből, és a
    /// deserializálás után is null marad, ahogy elvárjuk.
    ///
    /// FONTOS: az XmlSerializer BETÖLTÉSKOR közvetlenül ezt a settert hívja (mert ezen
    /// van az [XmlElement] attribútum) — SOHA nem az ElementIndex wrapper property
    /// setterét. Ezért a _hasElementIndex flaget itt is be kell állítani, különben
    /// betöltés után az ElementIndex getter mindig null-t adna vissza, még akkor is,
    /// ha az érték ténylegesen be lett olvasva az XML-ből.</summary>
    [XmlElement("ElementIndex")]
    public int ElementIndexValue
    {
        get => _elementIndexValue;
        set
        {
            _elementIndexValue = value;
            _hasElementIndex = true;
        }
    }

    [XmlIgnore]
    private int _elementIndexValue;

    /// <summary>Igaz, ha az ElementIndexValue-t ténylegesen ki kell írni — az
    /// XmlSerializer ezt a "ShouldSerialize" mintát keresi automatikusan
    /// (ShouldSerialize + a mező neve), hogy eldöntse, kiírja-e az elemet. Enélkül
    /// egy null ElementIndex-ű lépésnél is mindig kiírna egy "&lt;ElementIndex&gt;0&lt;/ElementIndex&gt;"
    /// sort, ami félrevezető lenne (0 nem ugyanaz, mint "nincs beállítva").</summary>
    [XmlIgnore]
    public int? ElementIndex
    {
        get => _hasElementIndex ? _elementIndexValue : null;
        set
        {
            _hasElementIndex = value.HasValue;
            _elementIndexValue = value ?? 0;
        }
    }

    [XmlIgnore]
    private bool _hasElementIndex;

    public bool ShouldSerializeElementIndexValue() => _hasElementIndex;
}

public static class TestSuiteMapper
{
    public static TestStepDto ToDto(TestStep step) => new()
    {
        Id = step.Id,
        Name = step.Name,
        Action = step.Action,
        Locator = step.Locator,
        LocatorType = step.LocatorType,
        Value = step.Value,
        TargetLocator = step.TargetLocator,
        TargetLocatorType = step.TargetLocatorType,
        TimeoutSeconds = step.TimeoutSeconds,
        RetryCount = step.RetryCount,
        FallbackLocator = step.FallbackLocator,
        FallbackLocatorType = step.FallbackLocatorType,
        ContinueOnError = step.ContinueOnError,
        Skip = step.Skip,
        Label = step.Label,
        OnSuccessGoToLabel = step.OnSuccessGoToLabel,
        OnFailureGoToLabel = step.OnFailureGoToLabel,
        ElementIndex = step.ElementIndex
    };

    public static TestStep ToTestStep(TestStepDto dto, AutomationTarget target) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        Target = target,
        Action = dto.Action,
        Locator = dto.Locator,
        LocatorType = dto.LocatorType,
        Value = dto.Value,
        TargetLocator = dto.TargetLocator,
        TargetLocatorType = dto.TargetLocatorType,
        TimeoutSeconds = dto.TimeoutSeconds,
        RetryCount = dto.RetryCount,
        FallbackLocator = dto.FallbackLocator,
        FallbackLocatorType = dto.FallbackLocatorType,
        ContinueOnError = dto.ContinueOnError,
        Skip = dto.Skip,
        Label = dto.Label,
        OnSuccessGoToLabel = dto.OnSuccessGoToLabel,
        OnFailureGoToLabel = dto.OnFailureGoToLabel,
        ElementIndex = dto.ElementIndex
    };
}
