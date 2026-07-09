using System.Text;

namespace AT.Infrastructure;

/// <summary>
/// Statikus HTML riport-generátor. A design a program kártya-alapú, letisztult
/// stílusát követi (fehér kártyák, lekerekített sarkok, státusz-színek), hogy a
/// riport vizuálisan összhangban legyen az alkalmazással, miközben teljesen
/// önálló, böngészőben megnyitható fájl marad.
/// </summary>
public sealed class TestReportService : ITestReportService
{
    private readonly ITestCategoryService _categoryService;

    public TestReportService(ITestCategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    public string GenerateHtml(TestRunRecord record)
    {
        var sb = new StringBuilder();

        sb.Append("<!DOCTYPE html>\n<html lang=\"hu\">\n<head>\n");
        sb.Append("<meta charset=\"UTF-8\" />\n");
        sb.Append($"<title>Teszt riport — {Html(record.TestName)}</title>\n");
        sb.Append("<style>\n").Append(Css).Append("\n</style>\n");
        sb.Append("</head>\n<body>\n");

        sb.Append("<div class=\"page\">\n");
        AppendHeader(sb, record);
        AppendSummary(sb, record);
        AppendSteps(sb, record);
        sb.Append("</div>\n");

        sb.Append("</body>\n</html>\n");

        return sb.ToString();
    }

    private void AppendHeader(StringBuilder sb, TestRunRecord record)
    {
        var targetLabel = record.Target switch
        {
            AT.Core.Models.AutomationTarget.Web => "Web tesztelés",
            AT.Core.Models.AutomationTarget.Desktop => "Windows desktop tesztelés",
            AT.Core.Models.AutomationTarget.Android => "Mobil (Android) tesztelés",
            _ => record.Target.ToString()
        };

        var categoryName = _categoryService.Categories.FirstOrDefault(c => c.Id == record.CategoryId)?.Name;
        var categorySuffix = string.IsNullOrWhiteSpace(categoryName) ? "" : $" &middot; {Html(categoryName)}";

        sb.Append("<div class=\"header\">\n");
        sb.Append($"<h1>{Html(string.IsNullOrWhiteSpace(record.TestName) ? "Névtelen teszt" : record.TestName)}</h1>\n");
        sb.Append($"<div class=\"subtle\">{Html(targetLabel)} &middot; {record.StartedAt:yyyy.MM.dd. HH:mm:ss}{categorySuffix}</div>\n");
        sb.Append("</div>\n");
    }

    private static void AppendSummary(StringBuilder sb, TestRunRecord record)
    {
        var statusClass = record.HasFailures ? "status-failed" : "status-passed";
        var statusLabel = record.HasFailures ? "Hibával zárult" : "Sikeresen lefutott";

        sb.Append("<div class=\"card summary-card\">\n");
        sb.Append($"<div class=\"summary-status {statusClass}\">{Html(statusLabel)}</div>\n");
        sb.Append("<div class=\"summary-grid\">\n");
        AppendSummaryItem(sb, "Összes lépés", record.TotalSteps.ToString());
        AppendSummaryItem(sb, "Sikeres", record.PassedCount.ToString(), "text-success");
        AppendSummaryItem(sb, "Hibás", record.FailedCount.ToString(), "text-danger");
        AppendSummaryItem(sb, "Kihagyva", record.SkippedCount.ToString(), "text-muted");
        AppendSummaryItem(sb, "Időtartam", FormatDuration(record.TotalDuration));
        sb.Append("</div>\n</div>\n");
    }

    private static void AppendSummaryItem(StringBuilder sb, string label, string value, string? valueClass = null)
    {
        var cls = valueClass is null ? "summary-value" : $"summary-value {valueClass}";
        sb.Append("<div class=\"summary-item\">\n");
        sb.Append($"<div class=\"summary-label\">{Html(label)}</div>\n");
        sb.Append($"<div class=\"{cls}\">{Html(value)}</div>\n");
        sb.Append("</div>\n");
    }

    private static void AppendSteps(StringBuilder sb, TestRunRecord record)
    {
        sb.Append("<div class=\"card\">\n");
        sb.Append("<h2>Lépések</h2>\n");
        sb.Append("<div class=\"steps\">\n");

        var index = 1;
        foreach (var step in record.StepResults)
        {
            AppendStep(sb, index, step);
            index++;
        }

        sb.Append("</div>\n</div>\n");
    }

    private static void AppendStep(StringBuilder sb, int index, TestStepResult step)
    {
        var (statusClass, statusLabel) = step.Status switch
        {
            AT.Core.Models.TestStatus.Passed => ("status-passed", "Sikeres"),
            AT.Core.Models.TestStatus.Failed => ("status-failed", "Hibás"),
            AT.Core.Models.TestStatus.Skipped => ("status-skipped", "Kihagyva"),
            AT.Core.Models.TestStatus.Running => ("status-running", "Fut"),
            _ => ("status-notrun", "Nincs futtatva")
        };

        sb.Append("<div class=\"step\">\n");
        sb.Append("<div class=\"step-row\">\n");
        sb.Append($"<div class=\"step-index\">{index}.</div>\n");
        sb.Append($"<div class=\"step-name\">{Html(step.StepName)}</div>\n");

        // Csak akkor jelenik meg, ha ténylegesen történt retry (AttemptCount > 1) —
        // retry nélküli lépéseknél (a legtöbb esetben) nem zsúfolja a sort felesleges
        // "1. próbálkozásra" szöveggel.
        if (step.AttemptCount > 1)
            sb.Append($"<div class=\"step-attempts\">{step.AttemptCount}. próbálkozásra</div>\n");

        sb.Append($"<div class=\"step-duration\">{(step.Duration is { } d ? $"{d.TotalSeconds:0.00} mp" : "—")}</div>\n");
        sb.Append($"<div class=\"step-status {statusClass}\">{Html(statusLabel)}</div>\n");
        sb.Append("</div>\n");

        if (!string.IsNullOrWhiteSpace(step.Message))
            sb.Append($"<div class=\"step-message\">{Html(step.Message)}</div>\n");

        if (!string.IsNullOrWhiteSpace(step.ScreenshotPath) && File.Exists(step.ScreenshotPath))
        {
            var base64 = TryReadImageAsBase64(step.ScreenshotPath);
            if (base64 is not null)
            {
                sb.Append("<div class=\"step-screenshot\">\n");
                sb.Append($"<img src=\"data:image/png;base64,{base64}\" alt=\"Képernyőkép\" />\n");
                sb.Append("</div>\n");
            }
        }

        sb.Append("</div>\n");
    }

    private static string? TryReadImageAsBase64(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            return Convert.ToBase64String(bytes);
        }
        catch
        {
            // Ha a kép menet közben törlődött/elérhetetlen, a riport a kép nélkül,
            // de a többi adattal együtt továbbra is generálódjon.
            return null;
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalMinutes >= 1
            ? $"{(int)duration.TotalMinutes} perc {duration.Seconds} mp"
            : $"{duration.TotalSeconds:0.00} mp";
    }

    private static string Html(string value) => System.Net.WebUtility.HtmlEncode(value);

    private const string Css = """
        :root {
            --bg: #f3f5f7;
            --card-bg: #ffffff;
            --border: #e3e7eb;
            --text-primary: #1f2937;
            --text-secondary: #4b5563;
            --text-muted: #9ca3af;
            --success: #16a34a;
            --danger: #dc2626;
            --warning: #d97706;
            --accent: #2563eb;
        }

        * { box-sizing: border-box; }

        body {
            margin: 0;
            padding: 32px 16px;
            background: var(--bg);
            color: var(--text-primary);
            font-family: 'Segoe UI', system-ui, -apple-system, sans-serif;
        }

        .page {
            max-width: 860px;
            margin: 0 auto;
        }

        .header {
            margin-bottom: 20px;
        }

        .header h1 {
            margin: 0 0 4px 0;
            font-size: 26px;
            font-weight: 700;
        }

        .subtle {
            color: var(--text-secondary);
            font-size: 13px;
        }

        .card {
            background: var(--card-bg);
            border: 1px solid var(--border);
            border-radius: 10px;
            padding: 20px;
            margin-bottom: 20px;
        }

        .card h2 {
            margin: 0 0 14px 0;
            font-size: 16px;
            font-weight: 600;
        }

        .summary-card {
            display: flex;
            flex-direction: column;
            gap: 16px;
        }

        .summary-status {
            display: inline-block;
            align-self: flex-start;
            padding: 6px 14px;
            border-radius: 6px;
            font-size: 13px;
            font-weight: 600;
        }

        .summary-grid {
            display: grid;
            grid-template-columns: repeat(5, 1fr);
            gap: 12px;
        }

        .summary-item {
            text-align: center;
        }

        .summary-label {
            font-size: 12px;
            color: var(--text-muted);
            margin-bottom: 4px;
        }

        .summary-value {
            font-size: 20px;
            font-weight: 700;
        }

        .text-success { color: var(--success); }
        .text-danger { color: var(--danger); }
        .text-muted { color: var(--text-muted); }

        .status-passed { background: rgba(22, 163, 74, 0.12); color: var(--success); }
        .status-failed { background: rgba(220, 38, 38, 0.12); color: var(--danger); }
        .status-skipped { background: rgba(156, 163, 175, 0.2); color: var(--text-secondary); }
        .status-running { background: rgba(217, 119, 6, 0.12); color: var(--warning); }
        .status-notrun { background: rgba(156, 163, 175, 0.15); color: var(--text-muted); }

        .steps {
            display: flex;
            flex-direction: column;
            gap: 10px;
        }

        .step {
            border: 1px solid var(--border);
            border-radius: 8px;
            padding: 12px 14px;
        }

        .step-row {
            display: flex;
            align-items: center;
            gap: 10px;
        }

        .step-index {
            color: var(--text-muted);
            font-size: 13px;
            width: 24px;
            flex-shrink: 0;
        }

        .step-name {
            flex: 1;
            font-size: 14px;
        }

        .step-attempts {
            color: var(--warning);
            font-size: 12px;
            font-style: italic;
            white-space: nowrap;
            flex-shrink: 0;
        }

        .step-duration {
            color: var(--text-muted);
            font-size: 13px;
            width: 80px;
            text-align: right;
            flex-shrink: 0;
        }

        .step-status {
            font-size: 12px;
            font-weight: 600;
            padding: 3px 10px;
            border-radius: 5px;
            white-space: nowrap;
            flex-shrink: 0;
        }

        .step-message {
            margin-top: 8px;
            padding-left: 34px;
            font-size: 13px;
            color: var(--danger);
        }

        .step-screenshot {
            margin-top: 10px;
            padding-left: 34px;
        }

        .step-screenshot img {
            max-width: 100%;
            border-radius: 6px;
            border: 1px solid var(--border);
        }

        @media (max-width: 640px) {
            .summary-grid { grid-template-columns: repeat(2, 1fr); }
        }
        """;
}
