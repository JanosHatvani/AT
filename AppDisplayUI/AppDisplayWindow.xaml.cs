using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Xml;
using TestAutomationUI;

namespace AppDisplayUI
{
    public partial class AppDisplayWindow : Window
    {
        private readonly string _deviceId;
        private readonly string _platform;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        private static POINT GetCursorPosition()
        {
            GetCursorPos(out POINT lpPoint);
            return lpPoint;
        }

        public AppDisplayWindow(string deviceId, string platform)
        {
            InitializeComponent();
            _deviceId = deviceId;
            _platform = platform;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_platform == "Android")
                    await DeviceDisplayManager.StartAndroidDisplayAsync(_deviceId, PreviewImage);
                else
                    await DeviceDisplayManager.StartIosDisplayAsync(_deviceId, PreviewImage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kijelző indítási hiba: {ex.Message}");
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            try
            {
                if (DeviceDisplayManager.isRunningAndroid == true)
                    DeviceDisplayManager.StopDisplayAndroid();
                else if (DeviceDisplayManager.isRunningIos == true)
                    DeviceDisplayManager.StopDisplayIos();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Leállítás közbeni hiba: {ex.Message}", "Figyelem", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnScreenshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PreviewImage.Source is BitmapSource bmp)
                {
                    string folder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                        "AppDisplayShots"
                    );

                    Directory.CreateDirectory(folder);

                    string filePath = Path.Combine(folder, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                    using (var fs = new FileStream(filePath, FileMode.Create))
                    {
                        BitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bmp));
                        encoder.Save(fs);
                    }

                    MessageBox.Show("A képernyőkép mentve lett:\n" + filePath);
                }
                else
                {
                    MessageBox.Show("Nincs megjelenített kép!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nem sikerült menteni a képet: {ex.Message}");
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            SafeShutdown();
            this.Close();
        }

        /// <summary>
        /// Teljes biztonságos leállítás: Android/iOS kijelző + Appium driver.
        /// </summary>
        private void SafeShutdown()
        {
            try
            {
                if (DeviceDisplayManager.isRunningAndroid == true)
                    DeviceDisplayManager.StopDisplayAndroid();
                else if (DeviceDisplayManager.isRunningIos == true)
                    DeviceDisplayManager.StopDisplayIos();

                AppMethods.StopMobile();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Leállítás közbeni hiba: {ex.Message}", "Figyelem", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool DeviceDisplayManagerIsAndroid()
        {
            return _platform == "Android";
        }

        private async void BtnInspect_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "3 másodpercen belül vidd az egeret az elem fölé az emulátor ablakban...",
                "Android Inspect indul"
            );

            await Task.Delay(3000);

            // UI dump
            AppMethods.RunAdb($"-s {_deviceId} shell uiautomator dump /sdcard/ui.xml");
            AppMethods.RunAdb($"-s {_deviceId} pull /sdcard/ui.xml ui.xml");

            POINT cursorPos = GetCursorPosition();

            XmlDocument xml = new XmlDocument();
            xml.Load("ui.xml");

            XmlNode node = FindNodeByPoint(xml.DocumentElement, cursorPos.X, cursorPos.Y);

            if (node == null)
            {
                MessageBox.Show("Nem találtam elemet az egér alatt.");
                return;
            }

            string info =
                $"📱 Android elem adatai:\n" +
                $"Text: {node.Attributes?["text"]?.Value}\n" +
                $"ResourceId: {node.Attributes?["resource-id"]?.Value}\n" +
                $"Class: {node.Attributes?["class"]?.Value}\n" +
                $"Package: {node.Attributes?["package"]?.Value}\n" +
                $"ContentDesc: {node.Attributes?["content-desc"]?.Value}\n" +
                $"Bounds: {node.Attributes?["bounds"]?.Value}\n" +
                $"Clickable: {node.Attributes?["clickable"]?.Value}";

            MessageBox.Show(info, "Inspect eredmény");
        }

        private XmlNode FindNodeByPoint(XmlNode node, int x, int y)
        {
            if (node?.Attributes?["bounds"] == null)
                return null;

            string bounds = node.Attributes["bounds"].Value;

            var match = System.Text.RegularExpressions.Regex.Match(
                bounds,
                @"\[(\d+),(\d+)\]\[(\d+),(\d+)\]"
            );

            if (!match.Success)
                return null;

            int left = int.Parse(match.Groups[1].Value);
            int top = int.Parse(match.Groups[2].Value);
            int right = int.Parse(match.Groups[3].Value);
            int bottom = int.Parse(match.Groups[4].Value);

            if (x >= left && x <= right && y >= top && y <= bottom)
            {
                foreach (XmlNode child in node.ChildNodes)
                {
                    var found = FindNodeByPoint(child, x, y);
                    if (found != null)
                        return found;
                }

                return node;
            }

            return null;
        }

        //private void RunAdb(string args)
        //{
        //    string adb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "adb", "adb.exe");

        //    var psi = new ProcessStartInfo
        //    {
        //        FileName = adb,
        //        Arguments = args,
        //        RedirectStandardOutput = true,
        //        RedirectStandardError = true,
        //        UseShellExecute = false,
        //        CreateNoWindow = true
        //    };

        //    using (var p = Process.Start(psi))
        //        p.WaitForExit();
        //}
    }
}
