using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using TestAutomationUI;

namespace MainWindow
{
    public partial class StatisticsWindow : Window
    {
        public SeriesCollection StatusSeries { get; set; }
        public SeriesCollection DurationSeries { get; set; }
        public List<string> Labels { get; set; }
        public int StepCount { get; set; }
        public int stepIndexes { get; set; }
        private string TestName { get; set; }
        public List<string> StepIndexes { get; set; }
        public StatisticsWindow(IEnumerable<TestStep> steps, string testName)
        {
            

            InitializeComponent();
            this.TestName = testName;

            // Felső diagram: metódusonkénti csoportosítás
            var grouped = steps
                .Select((step, index) => new { Step = step, Index = index })
                .GroupBy(x => x.Step.StepMethod)
                .Select(g => new
                {
                    Method = g.Key,
                    Success = g.Where(x => x.Step.Status == "OK").Select(x => x.Index+1).ToList(),
                    Failed = g.Where(x => x.Step.Status == "HIBA").Select(x => x.Index+1).ToList(),
                    Skipped = g.Where(x => x.Step.Status == "Átlépve").Select(x => x.Index + 1).ToList(),
                    Continued = g.Where(x => x.Step.Status == "HIBA, de folytatva").Select(x => x.Index+1).ToList()
                })
                .ToList();
            StepCount = steps.Count(); // Lépések száma
            // 2. Beállítás címkékhez
            Labels = grouped.Select(g => g.Method).ToList();

            // 3. Sorozatok létrehozása
            var success = new ChartValues<int[]>();
            var failed = new ChartValues<int[]>();
            var skipped = new ChartValues<int[]>();
            var continued = new ChartValues<int[]>();

            foreach (var g in grouped)
            {
                success.Add(g.Success.ToArray());
                failed.Add(g.Failed.ToArray());
                skipped.Add(g.Skipped.ToArray());
                continued.Add(g.Continued.ToArray());
            }

            StatusSeries = new SeriesCollection
            {
                new RowSeries
                {
                    Title = "Sikeres",
                    Values = new ChartValues<int>(grouped.Select(g => g.Success.Count)),
                    DataLabels = true,
                    LabelPoint = point =>
                    {
                        int index = (int)point.Y;
                        var indexes = grouped[index].Success;
                        return indexes.Count == 0 ? "" : $"[{string.Join(", ", indexes)}]";
                    },
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#09E85E"))
                },

                new RowSeries
                {
                    Title = "Hibás",
                    Values = new ChartValues<int>(grouped.Select(g => g.Failed.Count)),
                    DataLabels = true,
                    LabelPoint = point =>
                    {
                        int index = (int)point.Y;
                        var indexes = grouped[index].Failed;
                        return indexes.Count == 0 ? "" : $"[{string.Join(", ", indexes)}]";
                    },
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF0000"))
                },
                new RowSeries
                {
                    Title = "Átlépve",
                    Values = new ChartValues<int>(grouped.Select(g => g.Skipped.Count)),
                    DataLabels = true,
                    LabelPoint = point =>
                    {
                        int index = (int)point.Y;
                        var indexes = grouped[index].Skipped;
                        return indexes.Count == 0 ? "" : $"[{string.Join(", ", indexes)}]";
                    },
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1DEDE"))
                },
                new RowSeries
                {
                    Title = "Hiba, de folytatva",
                    Values = new ChartValues<int>(grouped.Select(g => g.Continued.Count)),
                    DataLabels = true,
                    LabelPoint = point =>
                    {
                        int index = (int)point.Y;
                        var indexes = grouped[index].Continued;
                        return indexes.Count == 0 ? "" : $"[{string.Join(", ", indexes)}]";
                    },
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BC3908"))
                    }
            };


            // Alsó diagram: lépések performance (step index vs duration)
            var durations = new ChartValues<double>();

            // Az első NaN a "nulladik" helyre
            durations.Add(double.NaN);

            // Majd az értékek
            foreach (var step in steps)
            {
                durations.Add(step.Duration);
            }

            // X tengely címkék: egy üres + 1-től kezdve
            StepIndexes = new List<string> { "" };
            StepIndexes.AddRange(steps.Select((s, index) => (index + 1).ToString()));

            // A Series
            DurationSeries = new SeriesCollection
{
    new LineSeries
    {
        Title = "Futási idő",
        Values = durations,
        PointGeometry = DefaultGeometries.Circle,
        PointForeground = System.Windows.Media.Brushes.SteelBlue,
        Stroke = System.Windows.Media.Brushes.SteelBlue,
        Fill = System.Windows.Media.Brushes.LightBlue,
        StrokeThickness = 2
    }
};

            DataContext = this;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ExportHtml_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new Settings
            {
                Owner = this
            };
            string testName = this.TestName;
            string date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // 1. Desktop elérési út meghatározása
            string today = DateTime.Today.ToString("yyyy/MM/dd.");
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string htmlPath = Path.Combine(desktopPath, today +"tesztriport.html");

            // 2. HTML felépítés
            var html = new StringBuilder();

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html><head><meta charset='UTF-8'><title>Riport</title>");
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f9f9f9; color: black; margin: 10px; }");
            html.AppendLine("h1, h2, h3 { color: black; }");

            /* Általános tábla stílus */
            html.AppendLine("table { border-collapse: collapse; width: 100%; margin-bottom: 30px; box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1); border-radius: 6px; overflow: hidden; color: black; }");
            html.AppendLine("th, td { border: 1px solid #e0e0e0; padding: 12px 16px; text-align: left; color: black; }");
            html.AppendLine("th { background-color: #e0e0e0; color: black; font-weight: 600; }");
            html.AppendLine("tr:nth-child(even) { background-color: #f2f2f2; }");
            html.AppendLine("tr:hover { background-color: #eaf2f8; }");

            /* Grafikon konténer */
            html.AppendLine(".chart-container { display: flex; justify-content: space-around; margin-bottom: 30px; padding: 10px; }");
            html.AppendLine(".bar { text-align: center; width: 40px; margin: 0 5px; color: black; }");
            html.AppendLine(".bar-fill { width: 100%; height: 0; transition: height 1s ease-in-out; border-radius: 10px 10px 0 0; }");
            html.AppendLine(".bar-label { margin-top: 10px; font-weight: bold; color: black; }");

            /* Futurisztikus tábla */
            html.AppendLine(".futuristic-table {");
            html.AppendLine("  width: 100%;");
            html.AppendLine("  border-collapse: collapse;");
            html.AppendLine("  color: black;");
            html.AppendLine("  background: linear-gradient(to right, #5e87a1, #6a82ab, #8a9fbf, #7a7ebd, #8b6ea4);");
            html.AppendLine("  border-radius: 10px;");
            html.AppendLine("  overflow: hidden;");
            html.AppendLine("  margin-bottom: 30px;");
            html.AppendLine("  font-weight: 500;");
            html.AppendLine("}");
            html.AppendLine(".futuristic-table th, .futuristic-table td {");
            html.AppendLine("  padding: 5px;");
            html.AppendLine("  text-align: center;");
            html.AppendLine("  color: black;");
            html.AppendLine("}");
            html.AppendLine(".futuristic-table th {");
            html.AppendLine("  background-color: rgba(255, 255, 255, 0.1);");
            html.AppendLine("  font-size: 16px;");
            html.AppendLine("  color: black;");
            html.AppendLine("  letter-spacing: 0.5px;");
            html.AppendLine("}");
            html.AppendLine(".futuristic-table tr td {");
            html.AppendLine("  background-color: rgba(255, 255, 255, 0.05);");
            html.AppendLine("  color: black;");
            html.AppendLine("}");
            html.AppendLine(".futuristic-table tr:nth-child(even) td {");
            html.AppendLine("  background-color: rgba(255, 255, 255, 0.08);");
            html.AppendLine("  color: black;");
            html.AppendLine("}");
            html.AppendLine(".futuristic-table th, .futuristic-table td {");
            html.AppendLine("  border: 1px solid black;"); // rácsvonal fekete
            html.AppendLine("  color: black;");
            html.AppendLine("}");
            html.AppendLine("</style>");

            html.AppendLine("</head><body>");

            html.AppendLine($"<h3>Teszt neve: {testName}</h3>");
            html.AppendLine($"<h3>Teszt dátuma: {date}</h3>");


            // 3. Státusz adatok – színpaletta (#004e64, #00A5CF, #9FFFCB, #25A18E, #6D9F71)
            html.AppendLine("<h2 style='color: #000;'>Metódusonkénti státusz</h2>");
            html.AppendLine("<div style='background-color: #093A3E; border-radius: 10px; padding: 15px; box-shadow: 0 4px 15px rgba(0,0,0,0.4); margin-bottom: 30px;'>");
            html.AppendLine("<table style='width: 100%; border-collapse: separate; border-spacing: 0 8px; color: #004e64;'>");
            html.AppendLine("<thead>");
            html.AppendLine("<tr style='background-color: #00A5CF;'>");
            html.AppendLine("<th style='padding: 12px 16px; border: 1px solid #004e64; border-radius: 8px;'>Metódus</th>");
            html.AppendLine("<th style='padding: 12px 16px; border: 1px solid #004e64; border-radius: 8px;'>Sikeres</th>");
            html.AppendLine("<th style='padding: 12px 16px; border: 1px solid #004e64; border-radius: 8px;'>Hibás</th>");
            html.AppendLine("<th style='padding: 12px 16px; border: 1px solid #004e64; border-radius: 8px;'>Átlépve</th>");
            html.AppendLine("<th style='padding: 12px 16px; border: 1px solid #004e64; border-radius: 8px;'>Folytatva</th>");
            html.AppendLine("</tr>");
            html.AppendLine("</thead>");
            html.AppendLine("<tbody>");

            // Összesített sor (#6D9F71)
            var totalSuccess = ((RowSeries)StatusSeries[0]).Values[0];
            var totalFailed = ((RowSeries)StatusSeries[1]).Values[0];
            var totalSkipped = ((RowSeries)StatusSeries[2]).Values[0];
            var totalContinued = ((RowSeries)StatusSeries[3]).Values[0];
            html.AppendLine("<tr style='background-color: #25A18E;'>");
            html.AppendLine($"<td style='padding: 12px 16px; border: 1px solid #004e64; border-radius: 8px;'>Metódus száma összesen</td>");
            html.AppendLine($"<td style='padding: 12px 16px; border: 1px solid #004e64; border-radius: 8px;'>{totalSuccess}</td>");
            html.AppendLine($"<td style='padding: 12px 16px; border: 1px solid #004e64; border-radius: 8px;'>{totalFailed}</td>");
            html.AppendLine($"<td style='padding: 12px 16px; border: 1px solid #004e64; border-radius: 8px;'>{totalSkipped}</td>");
            html.AppendLine($"<td style='padding: 12px 16px; border: 1px solid #004e64; border-radius: 8px;'>{totalContinued}</td>");
            html.AppendLine("</tr>");

            // Részletes sorok (váltakozó háttérszín: #25A18E / #00A5CF)
            for (int i = 1; i < Labels.Count; i++)
            {
                string method = Labels[i];
                var success = ((RowSeries)StatusSeries[0]).Values[i];
                var failed = ((RowSeries)StatusSeries[1]).Values[i];
                var skipped = ((RowSeries)StatusSeries[2]).Values[i];
                var continued = ((RowSeries)StatusSeries[3]).Values[i];

                string rowColor = i % 2 == 0 ? "#25A18E" : "#00A5CF";
                html.AppendLine($"<tr style='background-color: {rowColor};'>");
                html.AppendLine($"<td style='padding: 12px 16px; border: 1px solid #004e64; border-radius: 8px;'>{method}</td>");
                html.AppendLine($"<td style='padding: 12px 16px; border: 1px solid #004e64; border-radius: 8px;'>{success}</td>");
                html.AppendLine($"<td style='padding: 12px 16px; border: 1px solid #004e64; border-radius: 8px;'>{failed}</td>");
                html.AppendLine($"<td style='padding: 12px 16px; border: 1px solid #004e64; border-radius: 8px;'>{skipped}</td>");
                html.AppendLine($"<td style='padding: 12px 16px; border: 1px solid #004e64; border-radius: 8px;'>{continued}</td>");
                html.AppendLine("</tr>");
            }

            html.AppendLine("</tbody>");
            html.AppendLine("</table>");
            html.AppendLine("</div>");

            // 4. Futási idők – dizájn és fix magasság
            var durations = DurationSeries[0].Values.Cast<double>().ToList();
            var durationLabels = string.Join(",", Enumerable.Range(1, durations.Count - 1).Select(i => $"'Lépés {i}'"));
            var durationData = string.Join(",", Enumerable.Range(1, durations.Count - 1).Select(i => durations[i].ToString("0.00", CultureInfo.InvariantCulture)));

            html.AppendLine("<h2 style='color: #000;'>Futási idők (lépésszám szerint)</h2>");
            html.AppendLine("<div style='background-color: #093A3E; border-radius: 12px; padding: 15px; box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3); margin-bottom: 30px;'>");
            html.AppendLine("<canvas id='durationChart' width='1200' height='400' style='max-height: 435px; margin: 0 auto;'></canvas>");
            html.AppendLine("</div>");
            html.AppendLine("<script src='https://cdn.jsdelivr.net/npm/chart.js'></script>");
            html.AppendLine("<script>");
            html.AppendLine("const ctx = document.getElementById('durationChart').getContext('2d');");
            html.AppendLine("new Chart(ctx, {");
            html.AppendLine("    type: 'bar',");
            html.AppendLine("    data: {");
            html.AppendLine($"        labels: [{durationLabels}],");
            html.AppendLine("        datasets: [{");
            html.AppendLine("            label: 'Futási idő (mp)',");
            html.AppendLine($"            data: [{durationData}],");
            html.AppendLine("            backgroundColor: '#25A18E',");
            html.AppendLine("            borderColor: '#000',");
            html.AppendLine("            borderWidth: 1.5,");
            html.AppendLine("            borderRadius: 6");
            html.AppendLine("        }]");
            html.AppendLine("    },");
            html.AppendLine("    options: {");
            html.AppendLine("        responsive: true,");
            html.AppendLine("        maintainAspectRatio: false,");
            html.AppendLine("        plugins: {");
            html.AppendLine("            legend: {");
            html.AppendLine("                labels: { color: '#000', font: { size: 16 } }");
            html.AppendLine("            },");
            html.AppendLine("            tooltip: {");
            html.AppendLine("                backgroundColor: '#7AE582',");
            html.AppendLine("                titleColor: '#000',");
            html.AppendLine("                bodyColor: '#000'");
            html.AppendLine("            }");
            html.AppendLine("        },");
            html.AppendLine("        scales: {");
            html.AppendLine("            x: {");
            html.AppendLine("                ticks: { color: '#e0e0e0', font: { weight: 'bold', size: 12 } },");
            html.AppendLine("                grid: { color: '#e0e0e0' }");
            html.AppendLine("            },");
            html.AppendLine("            y: {");
            html.AppendLine("                beginAtZero: true,");
            html.AppendLine("                ticks: { color: '#e0e0e0', font: { weight: 'bold', size: 12 } },");
            html.AppendLine("                grid: { color: '#e0e0e0' }");
            html.AppendLine("            }");
            html.AppendLine("        }");
            html.AppendLine("    }");
            html.AppendLine("});");
            html.AppendLine("</script>");

            html.AppendLine("</body></html>");

            // 5. Mentés
            File.WriteAllText(htmlPath, html.ToString());

            CustomMessageBox.Show($"Riport sikeresen elmentve az asztalra:\n{htmlPath}", "Siker");
        }

    }
}
