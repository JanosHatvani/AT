using System.IO;
using System.Xml.Serialization;
using AT.Core.Models;

namespace AT.Infrastructure;

public interface ITestSuiteFileService
{
    /// <param name="categoryId">A teszt-kategória Id-ja (TestCategory.Id) — a fájl gyökér-
    /// elemének attribútumaként mentődik, ugyanúgy mint a name/target.</param>
    Task SaveAsync(string filePath, AutomationTarget target, IEnumerable<TestStep> steps, string? name = null, string? categoryId = null);

    /// <summary>Betölt egy fájlt, és eldobja, ha az nem a várt modulhoz (expectedTarget) készült.</summary>
    Task<TestSuiteFile> LoadAsync(string filePath, AutomationTarget expectedTarget);
}

public sealed class TestSuiteFileService : ITestSuiteFileService
{
    private static readonly XmlSerializer Serializer = new(typeof(TestSuiteFile));

    public Task SaveAsync(string filePath, AutomationTarget target, IEnumerable<TestStep> steps, string? name = null, string? categoryId = null)
    {
        var file = new TestSuiteFile
        {
            Target = target,
            Name = name,
            CategoryId = categoryId,
            SavedAtUtc = DateTime.UtcNow,
            Steps = steps.Select(TestSuiteMapper.ToDto).ToList()
        };

        using var stream = File.Create(filePath);
        Serializer.Serialize(stream, file);
        return Task.CompletedTask;
    }

    public Task<TestSuiteFile> LoadAsync(string filePath, AutomationTarget expectedTarget)
    {
        using var stream = File.OpenRead(filePath);

        TestSuiteFile file;
        try
        {
            file = Serializer.Deserialize(stream) as TestSuiteFile
                ?? throw new InvalidDataException("A fájl nem érvényes AT teszt-lépéssor.");
        }
        catch (InvalidOperationException ex)
        {
            // Az XmlSerializer ilyen típusú kivételbe csomagolja a formátumhibákat.
            throw new InvalidDataException("A fájl nem érvényes AT teszt-lépéssor (hibás XML formátum).", ex);
        }

        if (file.Target != expectedTarget)
        {
            throw new InvalidOperationException(
                $"Ez a fájl \"{Label(file.Target)}\" modulhoz készült — ide (\"{Label(expectedTarget)}\") nem tölthető be.");
        }

        return Task.FromResult(file);
    }

    private static string Label(AutomationTarget target) => target switch
    {
        AutomationTarget.Web => "Web",
        AutomationTarget.Desktop => "Windows desktop",
        AutomationTarget.Android => "Mobil (Android)",
        AutomationTarget.Ios => "Mobil (iOS)",
        _ => target.ToString()
    };
}
