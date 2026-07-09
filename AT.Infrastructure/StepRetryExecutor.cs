namespace AT.Infrastructure;

/// <summary>Egy retry-ciklussal végrehajtott lépés eredménye.</summary>
public sealed class StepExecutionOutcome
{
    public required bool Succeeded { get; init; }
    public required int AttemptCount { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>Ugyanaz, mint StepExecutionOutcome, de a végrehajtott művelet egy értéket is
/// visszaad (pl. a Mobile modul ExecuteStepAsync-ja egy opcionális "result" stringet ad
/// vissza attribútum-kiolvasás lépéseknél) — ezt sikeres végrehajtás esetén megőrzi.</summary>
public sealed class StepExecutionOutcome<T>
{
    public required bool Succeeded { get; init; }
    public required int AttemptCount { get; init; }
    public string? ErrorMessage { get; init; }
    public T? Result { get; init; }
}

/// <summary>
/// Közös retry-végrehajtó logika — ezt használja a TestExecutionService (ütemezett
/// futtatásokhoz) ÉS mindhárom modul ViewModel-je (Web/Desktop/Mobil RunStepsCoreAsync,
/// kézi futtatáshoz), hogy a retry-viselkedés garantáltan azonos legyen mindenhol,
/// ahelyett hogy négyszer külön-külön lenne megírva és idővel szétcsúszna.
///
/// A retry MINDEN kivételre lefut (nem csak "elem nem található"-típusú hibákra) —
/// egy felügyelet nélküli, CI-szerű futtatásnál a legtöbb hiba amúgy is időzítés-érzékeny,
/// és a retry ezekre ad esélyt. A próbálkozások között egy rövid, fix késleltetés van,
/// hogy egy átmeneti állapotnak (animáció, lassú betöltés, hálózati késés) esélye legyen
/// lezajlani a következő próbálkozásig.
/// </summary>
public static class StepRetryExecutor
{
    /// <summary>A próbálkozások közötti várakozás — nem konfigurálható per lépés, mert ez
    /// egy technikai részlet, nem a teszt-logika része; egy fix, rövid érték elég ahhoz,
    /// hogy a leggyakoribb átmeneti állapotoknak esélyük legyen lezajlani.</summary>
    private static readonly TimeSpan DelayBetweenAttempts = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Végrehajt egy műveletet, és hiba esetén <paramref name="retryCount"/> alkalommal
    /// újrapróbálja, mielőtt véglegesen sikertelennek jelentené. Az AttemptCount mindig
    /// legalább 1 (az első, "normál" próbálkozás is beleszámít).
    /// </summary>
    public static async Task<StepExecutionOutcome> ExecuteWithRetryAsync(Func<Task> action, int retryCount)
    {
        var maxAttempts = Math.Max(1, retryCount + 1);
        string? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await action();
                return new StepExecutionOutcome { Succeeded = true, AttemptCount = attempt };
            }
            catch (Exception ex)
            {
                lastError = ex.Message;

                var isLastAttempt = attempt == maxAttempts;
                if (isLastAttempt)
                    break;

                await Task.Delay(DelayBetweenAttempts);
            }
        }

        return new StepExecutionOutcome { Succeeded = false, AttemptCount = maxAttempts, ErrorMessage = lastError };
    }

    /// <summary>Ugyanaz, mint ExecuteWithRetryAsync, de a művelet egy értéket is visszaad
    /// (pl. a Mobile modul ExecuteStepAsync-ja egy opcionális "result" stringet ad vissza).</summary>
    public static async Task<StepExecutionOutcome<T>> ExecuteWithRetryAsync<T>(Func<Task<T>> action, int retryCount)
    {
        var maxAttempts = Math.Max(1, retryCount + 1);
        string? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var result = await action();
                return new StepExecutionOutcome<T> { Succeeded = true, AttemptCount = attempt, Result = result };
            }
            catch (Exception ex)
            {
                lastError = ex.Message;

                var isLastAttempt = attempt == maxAttempts;
                if (isLastAttempt)
                    break;

                await Task.Delay(DelayBetweenAttempts);
            }
        }

        return new StepExecutionOutcome<T> { Succeeded = false, AttemptCount = maxAttempts, ErrorMessage = lastError };
    }
}
