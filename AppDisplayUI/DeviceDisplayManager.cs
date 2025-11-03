using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace TestAutomationUI
{
    public static class DeviceDisplayManager
    {
        private static Process runningProcess;
        private static bool isRunning;

        /// <summary>
        /// Elindítja az Android app kijelző megjelenítését WPF Image controlon keresztül.
        /// </summary>
        public static async Task StartAndroidDisplayAsync(string deviceId, Image displayImage)
        {
            if (isRunning)
                return;
            
            isRunning = true;

            await Task.Run(async () =>
            {
                try
                {

                    while (isRunning)
                    {
                        string tempFile = Path.Combine(Path.GetTempPath(), "device_screen.png");

                        // ADB screenshot (ha kell élőben)
                        // RunAdbCommand($"exec-out screencap -p > \"{tempFile}\"");

                        if (File.Exists(tempFile))
                        {
                            displayImage.Dispatcher.Invoke(() =>
                            {
                                displayImage.Source = new BitmapImage(new Uri(tempFile));
                            });
                        }

                        await Task.Delay(1000); // 1 mp frissítés
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DeviceDisplayManager] Error: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// iOS app kijelző kezelése (ugyanígy, WPF-kompatibilisen).
        /// </summary>
        public static async Task StartIosDisplayAsync(string deviceId, Image displayImage)
        {
            if (isRunning)
                return;

            isRunning = true;

            await Task.Run(async () =>
            {
                while (isRunning)
                {
                    // TODO: Ide jön az iOS screenshot/frissítés logika
                    await Task.Delay(1000);
                }
            });
        }

        public static void StopDisplay()
        {
            isRunning = false;

            try
            {
                if (runningProcess != null && !runningProcess.HasExited)
                {
                    runningProcess.Kill();
                    runningProcess.Dispose();
                }
            }
            catch { }
        }
    }
}
