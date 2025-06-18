using MainWindow;
using Microsoft.Win32;
using Modules;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Xml.Linq;
using static TestAutomationUI.Settings;

namespace TestAutomationUI
{
    public class TestStep
    {
        public int Index { get; set; }
        public string StepName { get; set; }
        public string Action { get; set; }
        public string Target { get; set; }
        public string Property { get; set; }
        public string Parameter { get; set; }
        public double Duration { get; set; }
        public double FEDuration { get; set; }
        public string StepMethod { get; set; }
        public string PrtScfolderpath { get; set; }
        public string TestName { get; set; }
        public DateTime StartTime { get; set; }
        public int? TimeoutSeconds { get; set; }
        public string WiniumDriverDirectory { get; set; }
        public bool Skip { get; set; } = false;
        public string Status { get; set; } = "";
        public bool CanContinueOnError { get; set; } = false; // A mező, amely azt jelzi, hogy a lépés folytatható-e hibával
        public string Errortext { get; set; } = "";  // ÚJ MEZŐ
    }

    public class IndexPlusOneConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((int)value + 1).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class HamburgerButton : Button
    {
        public static readonly DependencyProperty IsMenuOpenProperty =
            DependencyProperty.Register(nameof(IsMenuOpen), typeof(bool), typeof(HamburgerButton), new PropertyMetadata(false));

        public bool IsMenuOpen
        {
            get => (bool)GetValue(IsMenuOpenProperty);
            set => SetValue(IsMenuOpenProperty, value);
        }
    }


    public partial class MainWindow : Window
    {
        public ObservableCollection<TestStep> Steps { get; set; } = new ObservableCollection<TestStep>();

        private Stopwatch stopwatch;
        private bool fastModeEnabled = false;  // változó a gyors üzemmódhoz

        //wpf felülettel kapcsolatos minden

        DispatcherTimer timer;
        double pandelWidth;
        bool hidden;

        private bool _stopRequested = false;

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        public MainWindow()
        {
            InitializeComponent();
            stepsDataGrid.ItemsSource = Steps;
            timer = new DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 0, 0, 1);
            timer.Tick += Timer_Tick;
            pandelWidth = sidePanel.Width;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (hidden)
            {
                sidePanel.Width += 10;
                if (sidePanel.Width >= pandelWidth)
                {
                    timer.Stop();
                    hidden = false;

                    SetSidePanelTextBlockVisibility(Visibility.Visible);
                }
                panelheader.ColumnDefinitions[0].Width = new GridLength(180);
            }
            else
            {
                sidePanel.Width -= 10;
                if (sidePanel.Width <= 60)
                {
                    timer.Stop();
                    hidden = true;

                    SetSidePanelTextBlockVisibility(Visibility.Collapsed);
                }
                panelheader.ColumnDefinitions[0].Width = new GridLength(60);


            }
        }

        private void SetSidePanelTextBlockVisibility(Visibility visibility)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(sidePanel))
            {
                if (child is FrameworkElement element)
                {
                    // TextBlock közvetlen vagy mélyebb gyermekként
                    foreach (var tb in FindVisualChildren<TextBlock>(element))
                    {
                        tb.Visibility = visibility;
                    }
                }
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                if (child != null && child is T t)
                    yield return t;

                foreach (T childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }

        //private void Button_Click(object sender, RoutedEventArgs e)
        //{
        //    timer.Start();
        //}
        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            timer.Start();

            var button = sender as HamburgerButton;
            button.IsMenuOpen = !button.IsMenuOpen;
        }

        private void panelheader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void StartSpinner()
        {
            SpinnerGrid.Visibility = Visibility.Visible;

            var rotate = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = new Duration(TimeSpan.FromSeconds(1)),
                RepeatBehavior = RepeatBehavior.Forever
            };
            SpinnerRotate.BeginAnimation(RotateTransform.AngleProperty, rotate);

        }

        private void StopSpinner()
        {
            SpinnerGrid.Visibility = Visibility.Collapsed;
            SpinnerRotate.BeginAnimation(RotateTransform.AngleProperty, null);
        }

        ////winiummal kapcsolatos minden

        // Új metódus, amelyet a checkbox állapotának változása kezel

        private void RefreshIndexes()
        {
            for (int i = 0; i < Steps.Count; i++)
            {
                Steps[i].Index = i + 1;
            }
        }

        private void AddStep_Click(object sender, RoutedEventArgs e)
        {
            var addStepWindow = new AddStepWindow
            {
                Owner = this
            };

            var settingsWindow = new Settings
            {
                Owner = this
            };

            if (addStepWindow.ShowDialog() == true)
            {
                var step = new TestStep
                {
                    StepName = addStepWindow.StepName,
                    Action = addStepWindow.Action,
                    Target = addStepWindow.Target,
                    Property = addStepWindow.Property,
                    Parameter = addStepWindow.Parameter,
                    TimeoutSeconds = int.TryParse(addStepWindow.TimeoutText, out var timeout) ? timeout : (int?)null,
                    StartTime = DateTime.Now,
                    PrtScfolderpath = settingsWindow.ScreenshotFolderTextBoxSettings,
                    TestName = addStepWindow.testNameTextBox.Text
                };

                if (string.IsNullOrEmpty(step.Action))
                {
                    CustomMessageBox.Show("Kérlek válassz metódust a listából.");
                    return;
                }

                Steps.Add(step);
                RefreshIndexes();
            }
        }

        private void StopTest_click(object sender, RoutedEventArgs e)
        {
            _stopRequested = true;

            if (WDMethods.IsRunning)
            {
                WDMethods.Stop();
                StopSpinner();
                CustomMessageBox.Show("Teszt megszakítva és a driver leállítva.", "Megszakítás");
            }
            else
            {
                CustomMessageBox.Show("A driver nem fut, nincs mit leállítani.", "Figyelem");
            }
        }

        private void stepsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            actualstepTextBox.Text = (stepsDataGrid.SelectedIndex + 1).ToString();
        }


        public void writelogtotext(string msg)
        {
            try
            {
                string txtlogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Log.txt");

                // Append mód: nem írja felül minden hívásnál a fájlt
                using (var txtwriter = new StreamWriter(txtlogPath, true, Encoding.UTF8))
                {
                    txtwriter.WriteLine($"{msg} - {DateTime.Now:MM/dd/yyyy hh:mm:ss}");
                }
            }
            catch (Exception ex)
            {
                // Hibakezelés, hogy ne dobjon ki a program
                // (akár ezt is naplózhatnád egy külön fájlba)
                Debug.WriteLine("Log írás sikertelen: " + ex.Message);
            }
        }

        private async void RunTest_Click(object sender, RoutedEventArgs e)
        {
            writelogtotext("runtest click");

            foreach (var step in Steps)
            {
                step.Status = "";
                step.Errortext = "";
                step.Duration = 0;

            }
            stepsDataGrid.Items.Refresh();

            var settingsWindow = new Settings
            {
                Owner = this
            };

            var addStepWindow = new AddStepWindow
            {
                Owner = this
            };
            
            WDMethods.CaptureScreenshots = !settingsWindow.FastMode;

            if (Steps == null || Steps.Count == 0)
            {
                CustomMessageBox.Show("Nincs végrehajtható lépés.");
                return;
            }

            if (!int.TryParse(settingsWindow.waitTimeText.Text, out int maxWaitTime))
            {
                CustomMessageBox.Show("Kérlek, adj meg egy érvényes számot a MaxWaitTime mezőben.");
                return;
            }

            string testNameMain = this.testnameTextBox.Text;
            string programPath = settingsWindow.programPathTextBox.Text;
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string winiumDriverPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools");
            string prtscfolderpathMain = settingsWindow.screenshotFolderTextBox.Text;
            WDMethods.MaxWaitTime = maxWaitTime;

            StartSpinner();

            foreach (var step in Steps)
            {
                int timeout = step.TimeoutSeconds.Value;
                int effectiveTimeout = timeout;
                writelogtotext("foreach kezdés");
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(effectiveTimeout)); 

                if (_stopRequested)
                {
                    step.Status = "Megszakítva";
                    WDMethods.TakePrtsc(testNameMain, prtscfolderpathMain);
                    writelogtotext("megszakítva");
                    break;
                }

                int actualstep = Steps.IndexOf(step) + 1;
                actualstepTextBox.Text = actualstep.ToString();

                if (step.Skip)
                {
                    step.Status = "Átlépve";
                    writelogtotext("átlépve");
                    continue;
                }
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    writelogtotext("try ág");

                    if (!step.TimeoutSeconds.HasValue || step.TimeoutSeconds.Value <= 0)
                    {
                        CustomMessageBox.Show($"A(z) \"{step.StepName}\" lépésnél nincs érvényes timeout beállítva.");
                        continue;
                    }

                    PropertyTypes propType = PropertyTypes.Id;

                    if (!string.IsNullOrEmpty(step.Property))
                    {
                        if (!Enum.TryParse(step.Property, out propType) || !Enum.IsDefined(typeof(PropertyTypes), propType))
                        {
                            step.Status = "HIBA";
                            step.Errortext = $"Ismeretlen Property típus: {step.Property}";
                            CustomMessageBox.Show($"\"{step.Property}\" A lépésnél ismeretlen property került megadásra.");
                            stepsDataGrid.Items.Refresh();
                            writelogtotext("hiba stat property után");
                            continue;
                        }
                    }

                    Task task = step.Action switch
                    {
                        "Start" => WDMethods.StartProg(programPath, winiumDriverPath),
                        "Click" => WDMethods.Click(step.Target ?? "", propType, step.TimeoutSeconds ?? WDMethods.MaxWaitTime),
                        "SendKeys" => WDMethods.Sendkeys(step.Target ?? "", step.Parameter ?? "", propType, step.TimeoutSeconds ?? WDMethods.MaxWaitTime),
                        "DoubleClick" => WDMethods.DoubleClick(step.Target ?? "", propType, step.TimeoutSeconds ?? WDMethods.MaxWaitTime),
                        "RightClick" => WDMethods.RightClick(step.Target ?? "", propType, step.TimeoutSeconds ?? WDMethods.MaxWaitTime),
                        "TextClear" => WDMethods.TextClear(step.Target ?? "", propType, step.TimeoutSeconds ?? WDMethods.MaxWaitTime),
                        "MoveToElement" => WDMethods.MoveToElement(step.Target ?? "", propType, step.TimeoutSeconds ?? WDMethods.MaxWaitTime),
                        "ScrollToElementAndClick" => WDMethods.ScrollToElementAndClick(step.Target ?? "", propType, step.Parameter ?? "", step.TimeoutSeconds ?? WDMethods.MaxWaitTime),
                        "Stop" => Task.Run(() => WDMethods.Stop()),
                        _ => throw new Exception($"Ismeretlen művelet: {step.Action}")
                    };

                    writelogtotext("await task előtt");
                    await task;
                    writelogtotext("await task után, ok kép előtt");
                    WDMethods.TakePrtsc(testNameMain, prtscfolderpathMain);
                    stopwatch.Stop();
                    writelogtotext("ok státusz előtt");
                    step.Duration = Math.Round(stopwatch.Elapsed.TotalSeconds, 2);                        
                    step.Status = "OK";
                    writelogtotext("ok státusz után");
                    
                }
                catch (Exception)
                {
                    writelogtotext("catch ág");
                    if (stopwatch.IsRunning)
                        stopwatch.Stop();

                    step.Duration = Math.Round(stopwatch.Elapsed.TotalSeconds, 2);  // <- TÉNYLEGES IDŐT MÉRÜNK                    

                    if (step.CanContinueOnError)
                    {
                        writelogtotext("hiba de folytatva");
                        cts.Cancel(); // Megszakítjuk a futó taskot, ha van
                        step.Status = "HIBA, de folytatva";
                        step.Errortext = $"Az element nem található: {step.Target}";
                        writelogtotext("hiba de folytatva continue előtt");
                        continue;
                    }
                    else
                    {
                        writelogtotext("else ág hiba grid kezelés előtt");
                        step.Status = "HIBA";
                        step.Errortext = $"Az element nem található: {step.Target}";
                        writelogtotext("else ág hiba grid kezelés után");

                        CustomMessageBox.Show(
                            $"Hiba a(z) {step.Index}. lépésnél:\n\n" +
                            $"StepName: {step.StepName}\n" +
                            $"Action: {step.Action}\n" +
                            $"Target: {step.Target}\n" +
                            $"Parameter: {step.Parameter}\n" +
                            $"Property: {step.Property}\n\n" +
                            $"Hibaüzenet: {step.Errortext}",
                            "Hiba történt");
                        writelogtotext("else ág hiba kép előtt");
                        WDMethods.TakePrtsc(testNameMain, prtscfolderpathMain);
                        writelogtotext("else ág hiba kép után");
                        StopSpinner();
                        WDMethods.Stop(); // Ha hiba történt, leállítjuk a Winium drivert                
                        break;
                    }
                }                
                stepsDataGrid.Items.Refresh();
            }

            // Eredmények kiértékelése
            int sikeres = Steps.Count(s => s.Status == "OK");
            int hibas = Steps.Count(s => s.Status == "HIBA");
            int atlepett = Steps.Count(s => s.Status == "Átlépve");
            int hibadefolytatva = Steps.Count(s => s.Status == "HIBA, de folytatva");
            int duration = (int)Steps.Sum(s => s.Duration);
            writelogtotext("statisztika után");

            //CustomMessageBox.Show(
            //    $"A lépések lefutottak.\n\n" +
            //    $"Összes lépés: {Steps.Count}\n" +
            //    $"Összes idő: {duration} másodperc\n" +
            //    $"Sikeres: {sikeres}\n" +
            //    $"Hibás: {hibas}\n" +
            //    $"Átlépett: {atlepett}\n" +
            //    $"Hibára futott, de folytatásra került: {hibadefolytatva}\n",
            //    "Teszt befejezve");

            //Naplózás CSV fájlba
            try
            {                
                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"TestLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                using (var writer = new StreamWriter(logPath, false, Encoding.UTF8))
                {
                    writer.WriteLine("StepName,Status,Duration,TimeoutSeconds,ErrorText");
                    foreach (var s in Steps)
                    {
                        writer.WriteLine($"{s.StepName},{s.Status},{s.Duration},{s.TimeoutSeconds},{s.Errortext}");
                    }
                }

                CustomMessageBox.Show($"Teszt futása befejeződött. Napló elmentve:\n{logPath}");
                writelogtotext("naplózás try ág után");
 
            }
            catch (Exception ex)
            {
                writelogtotext("naplózás catch ág");
                CustomMessageBox.Show($"A napló fájl mentése nem sikerült:\n{ex.Message}", "Hibanapló");
            }
            finally
            {
                _stopRequested = false;
                WDMethods.Stop(); // <-- garantált driver leállítás itt
                writelogtotext("finnaly ág");
            }
            StopSpinner();
        }
        
        private void DeleteStep_Click(object sender, RoutedEventArgs e)
        {
            // Ellenőrzés: ha a Steps üres, akkor ne mentsen
            if (Steps == null || !Steps.Any())
            {
                CustomMessageBox.Show("A grid üres, nincs törlendő adat.");
                return;
            }
            else if (stepsDataGrid.SelectedItem is TestStep selected)
            {
                Steps.Remove(selected);
                RefreshIndexes();
                stepsDataGrid.Items.Refresh();
            }
        }

        private void LoadTest_Click(object sender, RoutedEventArgs e)
        {
            var addStepWindow = new AddStepWindow
            {
                Owner = this
            };

            var settingsWindow = new Settings
            {
                Owner = this
            };

            var openFileDialog = new OpenFileDialog
            {
                Filter = "XML fájl (*.xml)|*.xml",
                Title = "Teszt betöltése XML fájlból"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var doc = XDocument.Load(openFileDialog.FileName);
                    var root = doc.Element("Test");

                    this.testnameTextBox.Text = root?.Attribute("Name")?.Value ?? string.Empty;
                    settingsWindow.screenshotFolderTextBox.Text = root?.Attribute("ScreenshotFolder")?.Value ?? string.Empty;
                    settingsWindow.programPathTextBox.Text = root?.Attribute("ProgramPath")?.Value ?? string.Empty;
                    settingsWindow.waitTimeText.Text = root?.Attribute("WaitTime")?.Value ?? string.Empty;

                    Steps.Clear();
                    foreach (var el in root?.Elements("Step") ?? Enumerable.Empty<XElement>())
                    {
                        Steps.Add(new TestStep
                        {
                            StepName = el.Attribute("StepName")?.Value ?? string.Empty,
                            Action = el.Attribute("Action")?.Value ?? string.Empty,
                            Target = el.Attribute("Target")?.Value ?? string.Empty,
                            Property = el.Attribute("Property")?.Value ?? string.Empty,
                            Parameter = el.Attribute("Parameter")?.Value ?? string.Empty,
                            Skip = bool.TryParse(el.Attribute("Skip")?.Value, out var skip) && skip,
                            CanContinueOnError = bool.TryParse(el.Attribute("CanContinueOnError")?.Value, out var cont) && cont,
                            TimeoutSeconds = int.TryParse(el.Attribute("TimeoutSeconds")?.Value, out var timeout) ? timeout : (int?)null
                        });
                    }

                    RefreshIndexes();
                    stepsDataGrid.Items.Refresh();
                    CustomMessageBox.Show("Teszt sikeresen betöltve!", "Info", false, this);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show("Betöltés közben hiba történt: " + ex.Message);
                }
            }
        }

        private void SaveTest_Click(object sender, RoutedEventArgs e)
        {
            var addStepWindow = new AddStepWindow
            {
                Owner = this
            };
            var settingsWindow = new Settings
            {
                Owner = this
            };
            // Ellenőrzés: ha a Steps üres, akkor ne mentsen
            if (Steps == null || !Steps.Any())
            {
                CustomMessageBox.Show("A grid üres, nincs menthető adat.");
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "XML fájl (*.xml)|*.xml",
                Title = "Teszt mentése XML fájlba",
                FileName = "test.xml"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var doc = new XDocument(
                        new XElement("Test",
                            new XAttribute("Name", this.testnameTextBox.Text ?? string.Empty),
                            new XAttribute("ScreenshotFolder", settingsWindow.ScreenshotFolderTextBoxSettings ?? string.Empty),
                            new XAttribute("ProgramPath", settingsWindow.programPathTextBox.Text ?? string.Empty),
                            new XAttribute("WaitTime", settingsWindow.MaxWaitTimeSettings ?? string.Empty),

                            from s in Steps
                            select new XElement("Step",
                                new XAttribute("StepName", s.StepName ?? string.Empty),
                                new XAttribute("Action", s.Action ?? string.Empty),
                                new XAttribute("Target", s.Target ?? string.Empty),
                                new XAttribute("Property", s.Property ?? string.Empty),
                                new XAttribute("Skip", s.Skip),
                                new XAttribute("CanContinueOnError", s.CanContinueOnError),
                                new XAttribute("Parameter", s.Parameter ?? string.Empty),
                                s.TimeoutSeconds.HasValue
                                    ? new XAttribute("TimeoutSeconds", s.TimeoutSeconds.Value)
                                    : null
                            )
                        )
                    );

                    doc.Save(saveFileDialog.FileName);
                    CustomMessageBox.Show("Teszt sikeresen elmentve!");
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show("Mentés közben hiba történt: " + ex.Message);
                }
            }
        }

        private void NewTest_Click(object sender, RoutedEventArgs e)
        {
            var addStepWindow = new AddStepWindow
            {
                Owner = this
            };
            var settingsWindow = new Settings
            {
                Owner = this
            };
            // Textboxok ürítése
            addStepWindow.testNameTextBox.Text = string.Empty;
            settingsWindow.screenshotFolderTextBox.Text = string.Empty;
            settingsWindow.programPathTextBox.Text = string.Empty;
            settingsWindow.waitTimeText.Text = string.Empty;
            addStepWindow.timeoutTextBox.Text = string.Empty;

            // Grid kiürítése
            Steps.Clear();
            stepsDataGrid.Items.Refresh();

            CustomMessageBox.Show("Új teszt kezdődött, minden mező törölve lett.", "Info");
        }

        private Point startPoint;

        private DataGridRow GetNearestContainer(object source)
        {
            DependencyObject depObj = source as DependencyObject;

            while (depObj != null && !(depObj is DataGridRow))
            {
                depObj = VisualTreeHelper.GetParent(depObj);
            }

            return depObj as DataGridRow;
        }

        private void stepsDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            startPoint = e.GetPosition(null);
        }

        private void stepsDataGrid_MouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = startPoint - mousePos;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                DataGridRow row = GetNearestContainer(e.OriginalSource);
                if (row == null) return;

                TestStep step = (TestStep)stepsDataGrid.ItemContainerGenerator.ItemFromContainer(row);
                if (step == null) return;

                DragDrop.DoDragDrop(row, step, DragDropEffects.Move);
            }
        }

        private void stepsDataGrid_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(TestStep)))
            {
                TestStep droppedData = e.Data.GetData(typeof(TestStep)) as TestStep;
                DataGridRow targetRow = GetNearestContainer(e.OriginalSource);

                if (targetRow == null || droppedData == null)
                    return;

                TestStep targetData = targetRow.Item as TestStep;
                if (targetData == null || droppedData == targetData)
                    return;

                int removedIdx = Steps.IndexOf(droppedData);
                int targetIdx = Steps.IndexOf(targetData);

                if (removedIdx < 0 || targetIdx < 0)
                    return;

                // Row mozgatása a collentionben
                Steps.Move(removedIdx, targetIdx);

                // Rfrissíti az index számát mozgatás után
                RefreshIndexes();

                // datagrid felfrissítése 
                Dispatcher.Invoke(() =>
                {
                    stepsDataGrid.Items.Refresh();
                });
            }
        }
        private void Quit_Click(object sender, RoutedEventArgs e)
        {
            // Megerősítés kérés a kilépéshez
            bool? result = CustomMessageBox.Show("Biztosan ki szeretne lépni?", "Kilépés megerősítése", true, this);
            if (result == true)
            {
                WDMethods.Stop();
                Application.Current.Shutdown();
            }
        }

        private void ShowStatistics_Click(object sender, RoutedEventArgs e)
        {
            if (Steps == null || !Steps.Any())
            {
                CustomMessageBox.Show("Nincs elérhető statisztikai adat.");
                return;
            }

            string testName = testnameTextBox.Text; // a TextBoxból olvasunk
            var statWindow = new StatisticsWindow(Steps, testName); // átadjuk
            statWindow.ShowDialog();
        }

        private void ShowSettings_Click(object sender, RoutedEventArgs e)
        {

            var settingsWindow = new Settings();
            settingsWindow.ShowDialog();
        }
    }
}