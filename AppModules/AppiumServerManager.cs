using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppModules
{
    public static class AppiumServerManager
    {
        private static Process appiumProcess;

        public static void StartAppiumServer()
        {
            if (appiumProcess != null && !appiumProcess.HasExited)
                return;

            var startInfo = new ProcessStartInfo
            {
                FileName = "appium", // CLI Appium, legyen elérhető PATH-ban
                Arguments = "--address 127.0.0.1 --port 4723 --base-path /wd/hub --session-override",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            appiumProcess = new Process { StartInfo = startInfo };
            appiumProcess.Start();
        }

        public static void StopAppiumServer()
        {
            try
            {
                if (appiumProcess != null && !appiumProcess.HasExited)
                {
                    appiumProcess.Kill(true);
                    appiumProcess = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Appium leállítás sikertelen: " + ex.Message);
            }
        }
    }
}
