using AppModules;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.Interactions;
using OpenQA.Selenium.Appium.iOS;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using WDModules;


namespace TestAutomationUI
{
    public class AppMethods
    {
        #region app start, stop and driver init
        public static AppiumDriver driver;
        private static bool isRunningMobile = false;
        public static string testName { get; set; }
        public static bool IsRunningMobile => isRunningMobile;
        public static bool CaptureScreenshots { get; set; }
        private static Process? displayProcess;

        public static async Task StartAndroidAppAsync(string deviceName, string appPackage)
        {
            Console.WriteLine($"📱 Android app indítása: {appPackage} ({deviceName})");

            try
            {
                // 1️⃣ Indítsd el az Android appot ADB-n keresztül
                await RunAdbCommandAsync(deviceName, $"shell monkey -p {appPackage} -c android.intent.category.LAUNCHER 1");

                // 2️⃣ Nyisd meg a WPF megjelenítő ablakot
                StartDisplayUI(deviceName, "Android");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba az Android app indításakor: {ex.Message}");
            }
        }

        public static async Task StartIOSAppAsync(string deviceName, string bundleId)
        {
            Console.WriteLine($"🍏 iOS app indítása: {bundleId} ({deviceName})");

            try
            {
                // 1️⃣ Indítsd el az iOS appot (itt Appium vagy Xcode parancs jöhet)
                await Task.Run(() =>
                {
                    // Példa dummy hívás helyett Appium start parancsot tehetünk ide
                    Console.WriteLine($"ios-deploy --id {deviceName} --bundle {bundleId}");
                });

                // 2️⃣ Nyisd meg a WPF megjelenítő ablakot
                StartDisplayUI(deviceName, "iOS");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba az iOS app indításakor: {ex.Message}");
            }
        }

        public static void StopMobile()
        {
            Console.WriteLine("🛑 Mobil app és megjelenítő leállítása...");

            try
            {
                // 1️⃣ Zárjuk be a megjelenítőt
                if (displayProcess != null && !displayProcess.HasExited)
                {
                    displayProcess.Kill();
                    displayProcess.Dispose();
                    displayProcess = null;
                }

                // 2️⃣ (Opcionálisan) Zárjuk be a mobil appot is
                RunAdbCommandAsync(null, "shell am force-stop com.your.app.package");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba a leállításkor: {ex.Message}");
            }
        }

        private static void StartDisplayUI(string deviceName, string platform)
        {
            try
            {
                string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppDisplayUI.exe");
                if (!File.Exists(exePath))
                {
                    throw new FileNotFoundException($"Nem található a WPF megjelenítő: {exePath}");
                }

                // 3️⃣ Indítsd el külön folyamatként
                displayProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"--device \"{deviceName}\" --platform \"{platform}\"",
                    UseShellExecute = false,
                    CreateNoWindow = false
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nem sikerült megnyitni az AppDisplay ablakot: {ex.Message}");
            }
        }

        private static async Task RunAdbCommandAsync(string? deviceName, string command)
        {
            await Task.Run(() =>
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = $"{(deviceName != null ? $"-s {deviceName} " : "")}{command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process proc = Process.Start(psi)!)
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    string error = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();

                    if (!string.IsNullOrEmpty(error))
                        Console.WriteLine($"ADB hiba: {error}");
                    else
                        Console.WriteLine($"ADB válasz: {output}");
                }
            });
        }
    

        //public static Task StartAndroidAppAsync(string deviceName, string platformVersion, string testname, string appPackage, string appActivity, int maxwaittimeMobil)
        //{
        //    return Task.Run(() =>
        //    {
        //        StartAndroidApp(deviceName, platformVersion, testname, appPackage, appActivity, maxwaittimeMobil);
        //    });
        //}


        //public static Task StartIOSAppAsync(string deviceName, string platformVersion, string bundleId, int maxwaittimeMobil)
        //{
        //    return Task.Run(() =>
        //    {
        //        StartIOSApp(deviceName, platformVersion, bundleId, maxwaittimeMobil);
        //    });
        //}

        #endregion

        #region element search, action, methods
        // --- ELEMENT KERESÉS --- kérdés, hogy van további param, ami alapján lehet keresni?
        private static IWebElement FindElement(string locator, PropertyTypes elementType, int timeoutSeconds)
        {
            By by = elementType switch
            {
                PropertyTypes.Id => MobileBy.Id(locator),
                PropertyTypes.Xpath => MobileBy.XPath(locator),
                PropertyTypes.Accessibilityid => MobileBy.AccessibilityId(locator),
                PropertyTypes.Name => MobileBy.Name(locator),
                PropertyTypes.ClassName => MobileBy.ClassName(locator),
                _ => throw new ArgumentException($"Ismeretlen lokátor típus: {elementType}")
            };

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
            return wait.Until(drv => drv.FindElement(by));
        }

        private static void PerformElementAction(string element, PropertyTypes elementType, Action<IWebElement> action, int timeoutSeconds)
        {
            if (driver == null)
                throw new InvalidOperationException("A driver nincs inicializálva.");

            By by = elementType switch
            {
                PropertyTypes.Id => MobileBy.Id(element),
                PropertyTypes.Name => MobileBy.Name(element),
                PropertyTypes.ClassName => MobileBy.ClassName(element),
                PropertyTypes.TagName => MobileBy.TagName(element),
                PropertyTypes.Xpath => MobileBy.XPath(element),
                PropertyTypes.Accessibilityid => MobileBy.AccessibilityId(element),
                PropertyTypes.IosClassChain => MobileBy.IosClassChain(element),
                PropertyTypes.IosNSPredicate => MobileBy.IosNSPredicate(element),
                PropertyTypes.AndroidDataMatcher => MobileBy.AndroidDataMatcher(element),
                PropertyTypes.AndroidViewMatcher => MobileBy.AndroidViewMatcher(element),
                _ => throw new NotSupportedException($"Nem támogatott property típus: {elementType}")
            };

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
            var elem = wait.Until(drv => drv.FindElement(by));
            action(elem);
        }

        // --- ACTION METÓDUSOK ---
        public static Task MobileClick(string locator, PropertyTypes elementType, int timeoutSeconds) =>
            Task.Run(() => PerformElementAction(locator, elementType, elem => elem.Click(), timeoutSeconds));

        public static Task MobileSendKeys(string locator, string text, PropertyTypes elementType, int timeoutSeconds) =>
            Task.Run(() => PerformElementAction(locator, elementType, elem => elem.SendKeys(text), timeoutSeconds));

        public static Task MobileClear(string locator, PropertyTypes elementType, int timeoutSeconds) =>
            Task.Run(() => PerformElementAction(locator, elementType, elem => elem.Clear(), timeoutSeconds));

        public static string MobileGetText(string locator, PropertyTypes elementType, int timeoutSeconds) =>
            FindElement(locator, elementType, timeoutSeconds).Text;

        public static bool MobileWaitForElementVisible(string locator, PropertyTypes elementType, int timeoutSeconds)
        {
            try { return FindElement(locator, elementType, timeoutSeconds).Displayed; }
            catch { return false; }
        }

        public static Task MobileScrollToElementAndClick(string locator, PropertyTypes elementType, string name, int timeoutSeconds, int maxScrollAttempts = 5) =>
            Task.Run(() =>
            {
                var parent = FindElement(locator, elementType, timeoutSeconds);
                IWebElement elementToClick = null;
                bool clicked = false;
                int attempts = 0;

                while (!clicked && attempts < maxScrollAttempts)
                {
                    try
                    {
                        elementToClick = parent.FindElement(By.Name(name));
                        if (elementToClick.Displayed)
                        {
                            elementToClick.Click();
                            clicked = true;
                            break;
                        }
                    }
                    catch { }

                    try
                    {
                        if (driver is IJavaScriptExecutor js)
                        {
                            js.ExecuteScript("arguments[0].scrollIntoView(true);", elementToClick);
                            Task.Delay(200).Wait();
                        }
                    }
                    catch
                    {
                        var touchScreen = new OpenQA.Selenium.Appium.Interactions.PointerInputDevice(PointerKind.Touch);
                        var actions = new ActionSequence(touchScreen, 0);
                        int startX = parent.Location.X + parent.Size.Width / 2;
                        int startY = parent.Location.Y + parent.Size.Height / 2;
                        int endX = startX;
                        int endY = startY - 300;

                        actions.AddAction(touchScreen.CreatePointerMove(CoordinateOrigin.Viewport, startX, startY, TimeSpan.Zero));
                        actions.AddAction(touchScreen.CreatePointerDown(PointerButton.TouchContact));
                        actions.AddAction(touchScreen.CreatePointerMove(CoordinateOrigin.Viewport, endX, endY, TimeSpan.FromMilliseconds(500)));
                        actions.AddAction(touchScreen.CreatePointerUp(PointerButton.TouchContact));

                        driver.PerformActions(new[] { actions });
                    }

                    Task.Delay(500).Wait();
                    attempts++;
                }

                if (!clicked)
                    throw new Exception($"Az elem '{name}' nem található vagy nem látható a görgetések után.");
            });
    }
    #endregion
}