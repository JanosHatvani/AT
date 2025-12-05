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
        public static bool isRunningAndroid;
        public static bool isRunningIos;
        public static Process? runningProcess;

        public static async Task StartAndroidDisplayAsync(string deviceId, Image displayImage)
        {
            if (isRunningAndroid) return;
            isRunningAndroid = true;

            await Task.Run(async () =>
            {
                while (isRunningAndroid)
                {
                    try
                    {
                        string tempFile = Path.Combine(Path.GetTempPath(), "device_screen.png");

                        if (File.Exists(tempFile))
                        {
                            displayImage.Dispatcher.Invoke(() =>
                            {
                                displayImage.Source = new BitmapImage(new Uri(tempFile));
                            });
                        }

                        await Task.Delay(1000);
                    }
                    catch { }
                }
            });
        }

        public static async Task StartIosDisplayAsync(string deviceId, Image displayImage)
        {
            if (isRunningIos) return;
            isRunningIos = true;

            await Task.Run(async () =>
            {
                while (isRunningIos)
                {
                    await Task.Delay(1000);
                }
            });
        }

        public static void StopDisplayAndroid() => isRunningAndroid = false;
        public static void StopDisplayIos() => isRunningIos = false;
    }
}
