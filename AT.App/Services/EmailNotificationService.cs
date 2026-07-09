using System.Net;
using System.Net.Mail;
using AT.App.Models;
using AT.Core.Models;
using AT.Infrastructure;

namespace AT.App.Services;

public interface IEmailNotificationService
{
    /// <summary>
    /// Elküld egy egyszerű, szöveges riport-emailt egy hibás futtatásról a Beállításokban
    /// megadott SMTP-szerveren és címzett-listára. Nem dob kivételt hívási hiba esetén —
    /// a hívó (SchedulerService/TestExecutionService) egy bool-t kap vissza, hogy sikerült-e,
    /// és ez alapján dönt a toast-üzenetről. Ez azért fontos, mert egy felügyelet nélküli,
    /// éjszakai ütemezett futtatást nem szabad, hogy egy rossz SMTP-jelszó megállítson vagy
    /// kivétellel elszálljon — legfeljebb a riport-email marad el.
    /// </summary>
    Task<bool> SendFailureReportAsync(TestRunRecord record);

    /// <summary>Egy rövid, "ez egy teszt email" tartalmú üzenetet küld — a Beállítások oldal
    /// "Teszt email küldése" gombjához, hogy az SMTP-adatok helyessége gyorsan ellenőrizhető legyen.</summary>
    Task<bool> SendTestEmailAsync();
}

/// <summary>
/// A .NET beépített System.Net.Mail.SmtpClient-jét használja — nincs hozzá külön NuGet-csomag.
/// Az SmtpClient a .NET-ben elavultként (obsolete) van jelölve újabb, aszinkron-natív
/// megoldások (pl. MailKit) javára, de egyszerű, alkalmi email-küldéshez (nem nagy
/// volumenű, nem teljesítménykritikus) még mindig működik és a legkevesebb új függőséggel jár.
/// </summary>
public sealed class EmailNotificationService : IEmailNotificationService
{
    private readonly AT.Infrastructure.ISettingsService _settingsService;
    private readonly AT.Infrastructure.ITestCategoryService _categoryService;
    private readonly INotificationService _notificationService;

    public EmailNotificationService(
        AT.Infrastructure.ISettingsService settingsService,
        AT.Infrastructure.ITestCategoryService categoryService,
        INotificationService notificationService)
    {
        _settingsService = settingsService;
        _categoryService = categoryService;
        _notificationService = notificationService;
    }

    public async Task<bool> SendFailureReportAsync(TestRunRecord record)
    {
        var settings = _settingsService.Current;

        if (!settings.EmailNotificationsEnabled)
            return false;

        var subject = $"[AT Framework] Ütemezett teszt hibával futott le: {record.TestName}";
        var body = BuildFailureReportBody(record);

        return await TrySendAsync(subject, body);
    }

    public async Task<bool> SendTestEmailAsync()
    {
        const string subject = "[AT Framework] Teszt email";
        const string body = "Ez egy teszt email az AT Framework riport-email beállításainak ellenőrzésére.\r\n\r\nHa ezt megkaptad, az SMTP-beállítások helyesek.";

        return await TrySendAsync(subject, body);
    }

    private async Task<bool> TrySendAsync(string subject, string body)
    {
        var settings = _settingsService.Current;

        if (string.IsNullOrWhiteSpace(settings.SmtpHost)
            || string.IsNullOrWhiteSpace(settings.EmailFrom)
            || string.IsNullOrWhiteSpace(settings.EmailRecipients))
        {
            _notificationService.Show("Riport-email kihagyva: az SMTP-beállítások hiányosak (Beállítások oldal).", NotificationType.Warning);
            return false;
        }

        var recipients = ParseRecipients(settings.EmailRecipients);
        if (recipients.Count == 0)
        {
            _notificationService.Show("Riport-email kihagyva: nincs érvényes címzett megadva.", NotificationType.Warning);
            return false;
        }

        try
        {
            using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
            {
                EnableSsl = settings.SmtpUseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrWhiteSpace(settings.SmtpUsername))
                client.Credentials = new NetworkCredential(settings.SmtpUsername, settings.SmtpPassword ?? "");

            using var message = new MailMessage
            {
                From = new MailAddress(settings.EmailFrom),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            foreach (var recipient in recipients)
                message.To.Add(recipient);

            await client.SendMailAsync(message);
            return true;
        }
        catch (Exception ex)
        {
            _notificationService.Show($"Riport-email küldése sikertelen: {ex.Message}", NotificationType.Warning);
            return false;
        }
    }

    /// <summary>A címzett-listát vesszővel és/vagy soronkénti tördeléssel is elfogadja,
    /// hogy a Beállítások mezőjébe akár egy sorba, akár soronként egyet írva is működjön.</summary>
    private static List<string> ParseRecipients(string raw)
    {
        return raw
            .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(r => r.Contains('@'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string BuildFailureReportBody(TestRunRecord record)
    {
        var categoryName = _categoryService.Categories.FirstOrDefault(c => c.Id == record.CategoryId)?.Name;

        var lines = new List<string>
        {
            $"Teszt neve: {record.TestName}",
            $"Kategória: {(string.IsNullOrWhiteSpace(categoryName) ? "—" : categoryName)}",
            $"Modul: {TargetLabel(record.Target)}",
            $"Indult: {record.StartedAt:yyyy.MM.dd. HH:mm:ss}",
            $"Befejeződött: {record.FinishedAt:yyyy.MM.dd. HH:mm:ss}",
            $"Összegzés: {record.PassedCount}/{record.TotalSteps} sikeres, {record.FailedCount} hibás, {record.SkippedCount} kihagyva",
            "",
            "Hibás lépések:"
        };

        var failedSteps = record.StepResults.Where(s => s.Status == TestStatus.Failed).ToList();
        if (failedSteps.Count == 0)
        {
            lines.Add("(nincs részletezett hibás lépés)");
        }
        else
        {
            foreach (var step in failedSteps)
            {
                var attemptSuffix = step.AttemptCount > 1 ? $" ({step.AttemptCount} próbálkozás után)" : "";
                lines.Add($"  - {step.StepName}{attemptSuffix}");
                if (!string.IsNullOrWhiteSpace(step.Message))
                    lines.Add($"    Hiba: {step.Message}");
            }
        }

        lines.Add("");
        lines.Add("Ez egy automatikus üzenet az AT Framework ütemezett futtatásától.");

        return string.Join("\r\n", lines);
    }

    private static string TargetLabel(AutomationTarget target) => target switch
    {
        AutomationTarget.Web => "Web",
        AutomationTarget.Desktop => "Windows desktop",
        AutomationTarget.Android => "Mobil (Android)",
        _ => target.ToString()
    };
}
