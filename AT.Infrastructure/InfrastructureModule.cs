namespace AT.Infrastructure;

/// <summary>
/// Helyfoglaló. Ez a réteg a következő fázisokban bővül:
/// konfiguráció-kezelés (appsettings.json), Serilog-alapú logolás,
/// és a jelenlegi SQL.cs-t kiváltó SQLite/EF Core adatréteg.
/// </summary>
public static class InfrastructureModule
{
    public const string Version = "0.1.0-skeleton";
}
