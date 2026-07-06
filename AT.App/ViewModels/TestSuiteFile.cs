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

    [XmlAttribute("continueOnError")]
    public bool ContinueOnError { get; set; }

    [XmlAttribute("skip")]
    public bool Skip { get; set; }
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
        ContinueOnError = step.ContinueOnError,
        Skip = step.Skip
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
        ContinueOnError = dto.ContinueOnError,
        Skip = dto.Skip
    };
}