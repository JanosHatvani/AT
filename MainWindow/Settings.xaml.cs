﻿using MainWindow;
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
        public string Webpath => webPathTextBox.Text;

        private void LoadSettings()
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                //CustomMessageBox.Show(SettingsPath);
                var settings = JsonSerializer.Deserialize<SettingsModel>(json) ?? new SettingsModel();
                webPathTextBox.Text = settings.WEbPath ?? string.Empty;
                waitTimeText.Text = settings.WaitTime;
                screenshotFolderTextBox.Text = settings.ScreenshotFolder;
                fastModeCheckBox.IsChecked = settings.FastMode;
                programPathTextBox.Text = settings.ProgramPath; 
                androidappactivity.Text = settings.AndroidAppActivity;
                androidapppackage.Text = settings.AndroidAppPackage;
                androiddevicename.Text = settings.AndroidDevicename;
                androidplatformversion.Text = settings.AndroidPlatformVersion;
                iosdevicename.Text = settings.IosDevicename;
                iosplatformversion.Text = settings.IosPlatformVersion;
                iosbundleid.Text = settings.IosBundleiID;
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
                WEbPath = webPathTextBox.Text,
                ScreenshotFolder = screenshotFolderTextBox.Text,
                ProgramPath = programPathTextBox.Text,
                AndroidDevicename = androiddevicename.Text,
                AndroidAppActivity = androidappactivity.Text,
                AndroidPlatformVersion = androidplatformversion.Text,
                AndroidAppPackage = androidapppackage.Text,
                IosDevicename = iosdevicename.Text,
                IosPlatformVersion = iosplatformversion.Text,
                IosBundleiID = iosbundleid.Text,
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
            public string WEbPath { get; set; }
            public string IosDevicename { get; set; }
            public string AndroidDevicename { get; set; }
            public string IosPlatformVersion { get; set; }
            public string AndroidPlatformVersion { get; set; }
            public string IosBundleiID { get; set; }
            public string AndroidAppActivity { get; set; }
            public string AndroidAppPackage { get; set; }

        }
    }
}
