using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using TestAutomationUI;
using AppModules;

namespace AppDisplayUI
{
    public partial class AppDisplayWindow : Window
    {
        private readonly string _deviceId;
        private readonly string _platform;

        public AppDisplayWindow(string deviceId, string platform)
        {
            InitializeComponent();
            _deviceId = deviceId;
            _platform = platform;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_platform == "Android")
                await DeviceDisplayManager.StartAndroidDisplayAsync(_deviceId, PreviewImage);
            else
                await DeviceDisplayManager.StartIosDisplayAsync(_deviceId, PreviewImage);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            DeviceDisplayManager.StopDisplay();
        }

        private void BtnScreenshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PreviewImage.Source is BitmapSource bitmapSource)
                {
                    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "AppDisplayShots");
                    Directory.CreateDirectory(folder);

                    string filePath = Path.Combine(folder, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                    using (FileStream stream = new FileStream(filePath, FileMode.Create))
                    {
                        BitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                        encoder.Save(stream);
                    }

                    MessageBox.Show($"Képernyőkép elmentve ide:\n{filePath}", "Mentés sikeres", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Nincs megjelenített kép a mentéshez.", "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nem sikerült a mentés: {ex.Message}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            SafeShutdown();
            this.Close();
        }

        /// <summary>
        /// Leállít minden futó mobil kijelző- és driver-folyamatot.
        /// </summary>
        private void SafeShutdown()
        {
            try
            {
                DeviceDisplayManager.StopDisplay();
                AppMethods.StopMobile(); // csak ha a projektedben az Appium driver is fut
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Leállítás közbeni hiba: {ex.Message}", "Figyelem", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
