using MainWindow;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TestAutomationUI
{
    public partial class Settings : Window
    {
        public Settings()
        {
            InitializeComponent();
            LoadSettings(); // Indításkor betölti
        }
        private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "settings.json");
        public string MaxWaitTimeSettings => waitTimeText.Text;
        public string ScreenshotFolderTextBoxSettings => screenshotFolderTextBox.Text;
        public bool FastMode => fastModeCheckBox.IsChecked == true;

        public SettingsModel LoadedSettings { get; set; } = new();

        public string ProgramPath => programPathTextBox.Text;

        private void LoadSettings()
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                //CustomMessageBox.Show(SettingsPath);
                var settings = JsonSerializer.Deserialize<SettingsModel>(json) ?? new SettingsModel();

                waitTimeText.Text = settings.WaitTime;
                screenshotFolderTextBox.Text = settings.ScreenshotFolder;
                fastModeCheckBox.IsChecked = settings.FastMode;
                programPathTextBox.Text = settings.ProgramPath; 
            }
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void SaveSettings()
        {
            var settings = new SettingsModel
            {
                WaitTime = waitTimeText.Text,
                ScreenshotFolder = screenshotFolderTextBox.Text,
                ProgramPath = programPathTextBox.Text,
                FastMode = fastModeCheckBox.IsChecked == true,
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!); // biztosítja a mappa létezését
            File.WriteAllText(SettingsPath, json);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings(); // Mentés gombra menti
            CustomMessageBox.Show("A mentés sikerült.");
            Close();
        }

        private void SettingsClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        } 

        public class SettingsModel
        {
            public string TestNameText {  get; set; }
            public string WaitTime { get; set; }
            public string ScreenshotFolder { get; set; }
            public bool FastMode { get; set; }
            public string ProgramPath { get; set; }
        }


    }
}
