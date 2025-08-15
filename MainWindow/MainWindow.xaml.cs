using MainWindow;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using WDModules;
using WebModules;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static TestAutomationUI.Settings;

namespace TestAutomationUI
{
    public class TestStep
    {
        public int Index { get; set; }
        public string StepName { get; set; }
        public string Action { get; set; }
        public string Target { get; set; }
        public string TargetElement { get; set; }
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

        private bool inspectModeActive = false;


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
        private bool _pauseRequested = false;
        private TaskCompletionSource<bool> _pauseTaskSource;

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

        private void IntroOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            IntroOverlay.Visibility = Visibility.Collapsed;
            MainContentGrid.Visibility = Visibility.Visible;
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

        //winiummal kapcsolatos minden

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
            if (IntroOverlay.Visibility == Visibility.Visible)
            {
                IntroOverlay.Visibility = Visibility.Collapsed;
                MainContentGrid.Visibility = Visibility.Visible;
            }

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
                    TargetElement = addStepWindow.TargetElement,
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

        //PauseResumeButton_Click
        private void PauseResumeButton_Click(object sender, RoutedEventArgs e)
        {
            if (IntroOverlay.Visibility == Visibility.Visible)
            {
                IntroOverlay.Visibility = Visibility.Collapsed;
                MainContentGrid.Visibility = Visibility.Visible;
            }

            if (_pauseRequested)
            {
                // Resume
                _pauseRequested = false;
                _pauseTaskSource?.TrySetResult(true);
                _pauseTaskSource = null;

                CustomMessageBox.Show("A teszt folytatódik.", "Folytatás");
            }
            else if (WDMethods.IsRunning || WebMethods.IsRunningWEB)
            {
                // Pause
                _pauseRequested = true;
                _pauseTaskSource = null; // Reset, hogy ne legyen előző érték se
                CustomMessageBox.Show("A teszt szüneteltetve lett.", "Szüneteltetés");
            }
            else
            {
                CustomMessageBox.Show("Nincs futó teszt, amelyet szüneteltetni lehetne.", "Figyelem");
                PauseResumeButton.IsChecked = false;
            }
        }



        private void StopTest_click(object sender, RoutedEventArgs e)
        {
            if (IntroOverlay.Visibility == Visibility.Visible)
            {
                IntroOverlay.Visibility = Visibility.Collapsed;
                MainContentGrid.Visibility = Visibility.Visible;
            }

            _stopRequested = true;

            if (WDMethods.IsRunning)
            {
                WDMethods.Stop();
                StopSpinner();
                CustomMessageBox.Show("Teszt megszakítva és a driver leállítva.", "Megszakítás");
            }
            if (WebMethods.IsRunningWEB)
            {
                WebMethods.StopWeb();
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

        private string _currentLogFilePath;

        // Ezt használd a writelogtotext-ben:
        public void writelogtotext(string msg)
        {
            if (string.IsNullOrEmpty(_currentLogFilePath))
            {
                string timestamp = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss", CultureInfo.InvariantCulture);
                _currentLogFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"Log_{timestamp}.txt");
            }

            try
            {
                using (var txtwriter = new StreamWriter(_currentLogFilePath, true, Encoding.UTF8))
                {
                    txtwriter.WriteLine($"{msg} - {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Log írás sikertelen: " + ex.Message);
            }
        }

        private async Task WaitIfPaused()
        {
            while (_pauseRequested)
            {
                if (_pauseTaskSource == null)
                    _pauseTaskSource = new TaskCompletionSource<bool>();

                await _pauseTaskSource.Task;
            }
        }

        private async void RunTest_Click(object sender, RoutedEventArgs e)
        {
            if (Steps.Count > 0)
            {
                string firstAction = Steps[0].Action ?? "";

                if (firstAction.Equals("Start", StringComparison.OrdinalIgnoreCase))
                {
                    bool hasWebAfter = Steps.Skip(1).Any(s => s.Action != null && s.Action.StartsWith("Web", StringComparison.OrdinalIgnoreCase));
                    if (hasWebAfter)
                    {
                        CustomMessageBox.Show("A kezdő lépés Desktop programra vonatkozik, viszont a lépések között webes műveletet is megadtál. Kérlek ellenőrizd a megadott műveleteket.", "Hiba");
                        return;
                    }
                }
                else if (firstAction.StartsWith("WebStartChrome", StringComparison.OrdinalIgnoreCase) ||
                         firstAction.StartsWith("WebStartMicrosoftEdge", StringComparison.OrdinalIgnoreCase))
                {
                    bool hasNonWebAfter = Steps.Skip(1).Any(s => s.Action != null && !s.Action.StartsWith("Web", StringComparison.OrdinalIgnoreCase));
                    if (hasNonWebAfter)
                    {
                        CustomMessageBox.Show("A kezdő lépés webes tesztelésre vonatkozik, viszont a lépések között desktopra vonatkozó műveletet is megadtál. Kérlek ellenőrizd a megadott műveleteket.", "Hiba");
                        return;
                    }
                }
            }

            if (IntroOverlay.Visibility == Visibility.Visible)
            {
                IntroOverlay.Visibility = Visibility.Collapsed;
                MainContentGrid.Visibility = Visibility.Visible;
            }

            foreach (var step in Steps)
            {
                step.Status = "";
                step.Errortext = "";
                step.Duration = 0;
            }
            stepsDataGrid.Items.Refresh();

            var settingsWindow = new Settings { Owner = this };
            var addStepWindow = new AddStepWindow { Owner = this };

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

            if (stepsDataGrid.CommitEdit(DataGridEditingUnit.Row, true))
            {
                bool? result = CustomMessageBox.Show(
                    "A lépések valamelyike szerkesztés alatt áll.\nSzeretnéd megszakítani a szerkesztést?",
                    "Szerkesztés aktív",
                    true,
                    this
                );

                if (result == true)
                {
                    stepsDataGrid.CancelEdit();
                }
                else
                {
                    if (WDMethods.IsRunning)
                    {
                        CustomMessageBox.Show("A teszt már fut, kérlek állítsd le a futó tesztet.", "Futó teszt");
                        WDMethods.Stop();
                    }

                    if (WebMethods.IsRunningWEB)
                    {
                        CustomMessageBox.Show("A teszt már fut, kérlek állítsd le a futó tesztet.", "Futó teszt");
                        WebMethods.StopWeb();
                    }
                    _stopRequested = true;
                    stopwatch?.Stop();
                }
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
                await WaitIfPaused();

                if (_stopRequested)
                {
                    step.Status = "Megszakítva";
                    WDMethods.TakePrtsc(testNameMain, prtscfolderpathMain);
                    break;
                }

                if (step.Skip)
                {
                    step.Status = "Átlépve";
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
                        continue;
                    }
                }

                bool stepCompleted = false;

                while (!stepCompleted)
                {
                    var stopwatch = Stopwatch.StartNew();
                    try
                    {
                        if (!step.TimeoutSeconds.HasValue || step.TimeoutSeconds.Value <= 0)
                        {
                            CustomMessageBox.Show($"A(z) \"{step.StepName}\" lépésnél nincs érvényes timeout beállítva.");
                            break;
                        }

                        Task task = step.Action switch
                        {
                            "Start" => WDMethods.StartProg(programPath, winiumDriverPath),
                            "WebStartChrome" => WebMethods.ChromeStart(step.Target ?? ""),
                            "WebStartFireFox" => WebMethods.FirefoxStart(step.Target ?? ""),
                            "WebStartMicrosoftEdge" => WebMethods.MicrosoftEdgeStart(step.Target ?? ""),
                            "Click" => WDMethods.Click(step.Target ?? "", propType, step.TimeoutSeconds ?? WDMethods.MaxWaitTime),
                            "WebClick" => WebMethods.ClickWeb(step.Target ?? "", propType, step.TimeoutSeconds ?? WDMethods.MaxWaitTime),
                            "SendKeys" => WDMethods.Sendkeys(step.Target ?? "", step.Parameter ?? "", propType, step.TimeoutSeconds ?? WDMethods.MaxWaitTime),
                            "WebSendKeys" => WebMethods.SendkeysWeb(step.Target ?? "", step.Parameter ?? "", propType, step.TimeoutSeconds ?? WDMethods.MaxWaitTime),
                            "DoubleClick" => WDMethods.DoubleClick(step.Target ?? "", propType, step.TimeoutSeconds ?? WDMethods.MaxWaitTime),
                            "RightClick" => WDMethods.RightClick(step.Target ?? "", propType, step.TimeoutSeconds ?? WDMethods.MaxWaitTime),
                            "WebRightClick" => WebMethods.RightClickWeb(step.Target ?? "", propType, step.TimeoutSeconds ?? WDMethods.MaxWaitTime),
                            "TextClear" => WDMethods.TextClear(step.Target ?? "", propType, step.TimeoutSeconds ?? WDMethods.MaxWaitTime),
                            "WebSelectByText" => WebMethods.SelectByTextWeb(step.Target ?? "", step.Parameter ?? "", propType, step.TimeoutSeconds ?? WDMethods.MaxWaitTime),
                            "WebSelectByValue" => WebMethods.SelectByValueWeb(step.Target ?? "", step.Parameter ?? "", propType, step.TimeoutSeconds ?? WDMethods.MaxWaitTime),
                            "WebDragAndDrop" => WebMethods.DragAndDropWeb(step.Target ?? "", step.TargetElement ?? "", propType, step.TimeoutSeconds ?? WDMethods.MaxWaitTime),
                            "DragAndDrop" => WDMethods.DragAndDrop(step.Target ?? "", step.TargetElement ?? "", propType, step.TimeoutSeconds ?? WDMethods.MaxWaitTime),
                            "MoveToElement" => WDMethods.MoveToElement(step.Target ?? "", propType, step.TimeoutSeconds ?? WDMethods.MaxWaitTime),
                            "ScrollToElementAndClick" => WDMethods.ScrollToElementAndClick(step.Target ?? "", propType, step.Parameter ?? "", step.TimeoutSeconds ?? WDMethods.MaxWaitTime),
                            "Stop" => WDMethods.Stop(),
                            _ => throw new Exception($"Ismeretlen művelet: {step.Action}")
                        };

                        await task;

                        stopwatch.Stop();
                        step.Duration = Math.Round(stopwatch.Elapsed.TotalSeconds, 2);
                        step.Status = "OK";
                        WDMethods.TakePrtsc(testNameMain, prtscfolderpathMain);

                        stepCompleted = true;
                    }
                    catch (Exception)
                    {
                        stopwatch.Stop();
                        step.Duration = Math.Round(stopwatch.Elapsed.TotalSeconds, 2);
                        step.Status = "HIBA";
                        step.Errortext = $"Az element nem található: {step.Target}";
                        stepsDataGrid.Items.Refresh();

                        if (step.CanContinueOnError)
                        {
                            step.Status = "HIBA, de folytatva";
                            step.Errortext = $"Az element nem található: {step.Target}";
                            stepsDataGrid.Items.Refresh();
                            break;
                        }

                        bool? retry = CustomMessageBox.Show(
                            $"Hiba a(z) {step.Index}. lépésnél:\n\n" +
                            $"StepName: {step.StepName}\n" +
                            $"Action: {step.Action}\n" +
                            $"Target: {step.Target}\n" +
                            $"Parameter: {step.Parameter}\n" +
                            $"Property: {step.Property}\n\n" +
                            $"Hibaüzenet: {step.Errortext}\n\n" +
                            "Szeretnéd újra lefuttatni ezt a lépést?",
                            "Hiba történt",
                            true,
                            this
                        );

                        if (retry != true)
                        {
                            WDMethods.TakePrtsc(testNameMain, prtscfolderpathMain);
                            StopSpinner();
                            WDMethods.Stop();
                            break;
                        }
                        // Ha Igen, a while ciklus újra futtatja a lépést
                    }
                }
                stepsDataGrid.Items.Refresh();
            }

            // Eredmények kiértékelése és naplózás marad változatlan
            int sikeres = Steps.Count(s => s.Status == "OK");
            int hibas = Steps.Count(s => s.Status == "HIBA");
            int atlepett = Steps.Count(s => s.Status == "Átlépve");
            int hibadefolytatva = Steps.Count(s => s.Status == "HIBA, de folytatva");
            int duration = (int)Steps.Sum(s => s.Duration);

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
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"A napló fájl mentése nem sikerült:\n{ex.Message}", "Hibanapló");
            }
            finally
            {
                _stopRequested = false;
                if (WDMethods.IsRunning) WDMethods.Stop();
                if (WebMethods.IsRunningWEB) WebMethods.StopWeb();
            }
            StopSpinner();
        }


        private void CopyStep_Click(object sender, RoutedEventArgs e)
        {
            if (IntroOverlay.Visibility == Visibility.Visible)
            {
                IntroOverlay.Visibility = Visibility.Collapsed;
                MainContentGrid.Visibility = Visibility.Visible;
            }
            // Ellenőrzés, hogy van-e kiválasztott lépés
            if (stepsDataGrid.SelectedItem is TestStep selectedStep)
            {
                var newStep = new TestStep
                {
                    StepName = selectedStep.StepName + " - Másolat",
                    Action = selectedStep.Action,
                    Target = selectedStep.Target,
                    TargetElement = selectedStep.TargetElement,
                    Property = selectedStep.Property,
                    Parameter = selectedStep.Parameter,
                    TimeoutSeconds = selectedStep.TimeoutSeconds,
                    PrtScfolderpath = selectedStep.PrtScfolderpath,
                    TestName = selectedStep.TestName
                };
                Steps.Add(newStep);
                RefreshIndexes();
                stepsDataGrid.Items.Refresh();
            }
            else
            {
                CustomMessageBox.Show("Kérlek válassz ki egy lépést a másoláshoz.");
            }

        }

        private void InsertStep_Click(object sender, RoutedEventArgs e)
        {
            if (IntroOverlay.Visibility == Visibility.Visible)
            {
                IntroOverlay.Visibility = Visibility.Collapsed;
                MainContentGrid.Visibility = Visibility.Visible;
            }

            // Ha nincs még egy lépés sem, sima hozzáadás
            if (Steps.Count == 0)
            {
                AddStep_Click(sender, e); // már létező módszer, sima hozzáadást végez
                return;
            }

            // Ha van lépés, de nincs kijelölve semmi
            if (stepsDataGrid.SelectedIndex < 0)
            {
                CustomMessageBox.Show("Kérlek válassz ki egy lépést, amely után be szeretnéd szúrni az újat.");
                return;
            }

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
                var newStep = new TestStep
                {
                    StepName = addStepWindow.StepName,
                    Action = addStepWindow.Action,
                    Target = addStepWindow.Target,
                    TargetElement = addStepWindow.TargetElement,
                    Property = addStepWindow.Property,
                    Parameter = addStepWindow.Parameter,
                    TimeoutSeconds = int.TryParse(addStepWindow.TimeoutText, out var timeout) ? timeout : (int?)null,
                    StartTime = DateTime.Now,
                    PrtScfolderpath = settingsWindow.ScreenshotFolderTextBoxSettings,
                    TestName = addStepWindow.testNameTextBox.Text
                };

                if (string.IsNullOrEmpty(newStep.Action))
                {
                    CustomMessageBox.Show("Kérlek válassz metódust a listából.");
                    return;
                }

                // Beszúrás a kiválasztott lépés után
                int selectedIndex = stepsDataGrid.SelectedIndex;
                Steps.Insert(selectedIndex + 1, newStep);

                RefreshIndexes();
                stepsDataGrid.Items.Refresh();
            }
        }



        private void DeleteStep_Click(object sender, RoutedEventArgs e)
        {
            IntroOverlay.Visibility = Visibility.Collapsed;
            MainContentGrid.Visibility = Visibility.Visible;

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

            if (IntroOverlay.Visibility == Visibility.Visible)
            {
                IntroOverlay.Visibility = Visibility.Collapsed;
                MainContentGrid.Visibility = Visibility.Visible;
            }

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
                            TargetElement = el.Attribute("TargetElement")?.Value ?? string.Empty,
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

            if (IntroOverlay.Visibility == Visibility.Visible)
            {
                IntroOverlay.Visibility = Visibility.Collapsed;
                MainContentGrid.Visibility = Visibility.Visible;
            }
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

            var bgtask = new BackgroundWorker();

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
                                new XAttribute("TargetElement", s.TargetElement ?? string.Empty),
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
            if (IntroOverlay.Visibility == Visibility.Visible)
            {
                IntroOverlay.Visibility = Visibility.Collapsed;
                MainContentGrid.Visibility = Visibility.Visible;
            }
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

            CustomMessageBox.Show("Új teszt kezdődött.", "Info");
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

        // WinAPI: egérpozíció lekérése
        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        private POINT GetCursorPosition()
        {
            GetCursorPos(out POINT point);
            return point;
        }


        private async void ElementInspect_Click(object sender, RoutedEventArgs e)
        {
            CustomMessageBox.Show("3 másodpercen belül vidd az egeret az ellenőrizni kívánt elemre...", "Inspect indul");

            await Task.Delay(3000);

            POINT point = GetCursorPosition();
            var element = AutomationElement.FromPoint(new System.Windows.Point(point.X, point.Y));

            if (element != null)
            {
                string info =
                    $"🖥️ Desktop elem adatai:\n" +
                    $"Name: {element.Current.Name}\n" +
                    $"ClassName: {element.Current.ClassName}\n" +
                    $"AutomationId: {element.Current.AutomationId}\n" +
                    $"ControlType: {element.Current.ControlType.ProgrammaticName}\n" +
                    $"NID: {element.Current.ControlType.Id}\n" +
                    $"FID: {element.Current.FrameworkId}\n" +
                    $"ProcessId: {element.Current.ProcessId}\n" +
                    $"BoundingRect: {element.Current.BoundingRectangle}\n" +
                    $"IsEnabled: {element.Current.IsEnabled}\n" +
                    $"IsOffscreen: {element.Current.IsOffscreen}\n" +
                    $"IsKeyboardFocusable: {element.Current.IsKeyboardFocusable}\n" +
                    $"HasKeyboardFocus: {element.Current.HasKeyboardFocus}\n" +
                    $"AccessKey: {element.Current.AccessKey}\n" +
                    $"HelpText: {element.Current.HelpText}\n" +
                    $"ItemType: {element.Current.ItemType}\n" +
                    $"NativeWindowHandle: {element.Current.NativeWindowHandle}";

                CustomMessageBox.Show(info, "Inspect eredmény");              
            }
            else
            {
                CustomMessageBox.Show("Nem található elem az egér alatt.", "Hiba");              
            }
        }

        private void WebElementInspect_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new Settings
            {
                Owner = this
            };

            string url = settingsWindow.webPathTextBox.Text;
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Kérlek, adj meg egy weboldal címet!", "Figyelem", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var inspectWindow = new ElementInfoWindow(url);
            inspectWindow.Show();
        }


    }
}