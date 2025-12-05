using AppModules;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.iOS;
using OpenQA.Selenium.Support.UI;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using WDModules;

namespace TestAutomationUI
{
    public class AppMethods
    {
        public static AppiumDriver driver;
        public static bool IsRunningMobile { get; private set; }

        #region ANDROID SDK PATH KEZELÉS 

        public static string GetAndroidSdkPath()
        {
            var sdkRoot = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");

            if (string.IsNullOrWhiteSpace(sdkRoot))
                sdkRoot = Environment.GetEnvironmentVariable("ANDROID_HOME");

            if (string.IsNullOrWhiteSpace(sdkRoot))
                throw new Exception("ANDROID_SDK_ROOT vagy ANDROID_HOME nincs beállítva!");

            return sdkRoot;
        }

        public static string GetEmulatorPath()
        {
            return Path.Combine(GetAndroidSdkPath(), "emulator", "emulator.exe");
        }

        public static string GetAdbPath()
        {
            return Path.Combine(GetAndroidSdkPath(), "platform-tools", "adb.exe");
        }

        #endregion


        #region ANDROID EMULÁTOR INDÍTÁS  + MEGERŐSÍTÉS

        public static async Task<bool> StartAndroidEmulator(string avdName)
        {
            var confirm = MessageBox.Show(
                $"El szeretnéd indítani ezt az Android emulátort?\n\n{avdName}",
                "Emulátor indítása",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return false;

            string emulatorPath;
            try
            {
                emulatorPath = GetEmulatorPath();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }

            if (!File.Exists(emulatorPath))
            {
                MessageBox.Show("emulator.exe nem található:\n" + emulatorPath);
                return false;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = emulatorPath,
                Arguments = $"-avd {avdName}",
                UseShellExecute = false,
                CreateNoWindow = false
            });

            // várunk míg az ADB látja
            for (int i = 0; i < 60; i++)
            {
                string devices = RunAdb("devices");
                if (devices.Contains("device") && !devices.Contains("offline"))
                    return true;

                await Task.Delay(2000);
            }

            MessageBox.Show("Az Android emulátor nem indult el időben!");
            return false;
        }

        #endregion


        #region ANDROID APP INDÍTÁS 

        public static async Task StartAndroidAppAsync(string deviceName, string appPackage, string appActivity, string avdName)
        {
            IsRunningMobile = true;

            bool emulatorReady = await StartAndroidEmulator(avdName);
            if (!emulatorReady) return;

            bool installed = InstallApkWithConfirm( @"C:\TesztApp\app-debug.apk", appPackage);   // ← EZT BEÁLLÍTHATJUK SETTINGS-BŐL IS


            if (!installed)
            {
                MessageBox.Show("Az alkalmazás nem lett telepítve!");
                return;
            }

            AppiumServerManager.StartAppiumServer();

            var caps = new AppiumOptions();
            caps.PlatformName = "Android";
            caps.AddAdditionalAppiumOption("deviceName", deviceName);
            caps.AddAdditionalAppiumOption("automationName", "UiAutomator2");
            caps.AddAdditionalAppiumOption("appPackage", appPackage);
            caps.AddAdditionalAppiumOption("appActivity", appActivity);

            driver = new AndroidDriver(
                new Uri("http://127.0.0.1:4723/"),
                caps,
                TimeSpan.FromMinutes(2));

            StartDisplayUI(deviceName, "Android");
        }

        #endregion


        #region IOS APP INDÍTÁS 

        public static async Task StartIOSAppAsync(string deviceName, string bundleId)
        {
            IsRunningMobile = true;

            AppiumServerManager.StartAppiumServer();

            var caps = new AppiumOptions();
            caps.PlatformName = "iOS";
            caps.AddAdditionalAppiumOption("deviceName", deviceName);
            caps.AddAdditionalAppiumOption("bundleId", bundleId);
            caps.AddAdditionalAppiumOption("automationName", "XCUITest");

            driver = new IOSDriver(
                new Uri("http://127.0.0.1:4723/"),
                caps,
                TimeSpan.FromMinutes(2));

            StartDisplayUI(deviceName, "iOS");
        }

        public static void StopMobile()
        {
            try
            {
                driver?.Quit();
                driver?.Dispose();
            }
            catch { }

            IsRunningMobile = false;
        }

        #endregion


        #region MOBIL ACTIONS 

        private static IWebElement FindElement(string locator, PropertyTypes type, int timeout)
        {
            By by = type switch
            {
                PropertyTypes.Id => MobileBy.Id(locator),
                PropertyTypes.Xpath => MobileBy.XPath(locator),
                PropertyTypes.Accessibilityid => MobileBy.AccessibilityId(locator),
                PropertyTypes.Name => MobileBy.Name(locator),
                PropertyTypes.ClassName => MobileBy.ClassName(locator),
                _ => throw new ArgumentException("Ismeretlen locator")
            };

            return new WebDriverWait(driver, TimeSpan.FromSeconds(timeout))
                .Until(drv => drv.FindElement(by));
        }

        public static Task MobileClick(string locator, PropertyTypes type, int timeout) =>
            Task.Run(() => FindElement(locator, type, timeout).Click());

        public static Task MobileSendKeys(string locator, string text, PropertyTypes type, int timeout) =>
            Task.Run(() => FindElement(locator, type, timeout).SendKeys(text));

        #endregion


        #region ADB ✅ TELJES PATH-ON FUT

        public static string RunAdb(string command)
        {
            string adbPath;
            try
            {
                adbPath = GetAdbPath();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return string.Empty;
            }

            var psi = new ProcessStartInfo
            {
                FileName = adbPath,
                Arguments = command,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            return p.StandardOutput.ReadToEnd();
        }

        public static bool IsAppInstalled(string packageName)
        {
            string result = RunAdb("shell pm list packages");
            return result.Contains(packageName);
        }

        public static bool InstallApkWithConfirm(string apkPath, string packageName)
        {
            if (IsAppInstalled(packageName))
                return true;

            var confirm = MessageBox.Show(
                "Az alkalmazás nincs telepítve az emulátorra.\n\nTelepítsem most?",
                "APK telepítés",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return false;

            if (!File.Exists(apkPath))
            {
                MessageBox.Show("Az APK nem található:\n" + apkPath);
                return false;
            }

            RunAdb($"install \"{apkPath}\"");

            // újra ellenőrizzük
            return IsAppInstalled(packageName);
        }


        #endregion


        #region DISPLAY UI 

        public static void StartDisplayUI(string deviceName, string platform)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "AppDisplayUI.exe",
                    Arguments = $"--device \"{deviceName}\" --platform \"{platform}\"",
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nem sikerült megnyitni a kijelzőt:\n" + ex.Message);
            }
        }

        #endregion
    }
}
