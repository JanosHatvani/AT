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
        public static AppiumDriver driver;
        private static bool isRunningMobile = false;
        public static string testName { get; set; }
        public static bool IsRunningMobile => isRunningMobile;
        public static bool CaptureScreenshots { get; set; }

        // --- ANDROID INDÍTÁS ---
        public static void StartAndroidApp(string deviceName, string platformVersion, string testname, string appPackage, string appActivity, int maxwaittimeMobil)
        {
            if (isRunningMobile)
            {
                MessageBox.Show("Az Android driver már fut. Kérlek, állítsd le először.");
                return;
            }

            try
            {
                AppiumServerManager.StartAppiumServer();

                var options = new AppiumOptions();
                options.PlatformName = "Android";
                options.AutomationName = testname;
                options.AddAdditionalOption("deviceName", deviceName);
                options.AddAdditionalOption("platformVersion", platformVersion);
                options.AddAdditionalOption("appPackage", appPackage);
                options.AddAdditionalOption("appActivity", appActivity);
                options.AddAdditionalOption("noReset", true);
                options.AddAdditionalOption("autoGrantPermissions", true);
                options.AddAdditionalOption("appWaitActivity", appActivity);

                driver = new AndroidDriver(new Uri("http://127.0.0.1:4723/wd/hub"), options, TimeSpan.FromSeconds(maxwaittimeMobil));
                isRunningMobile = true;

                ////Korábbi appium verzióhoz kellett, Csak a konkrét AndroidDriver-en hívjuk a LaunchApp-t 
                //if (driver is AndroidDriver androidDriver)
                //{
                //    androidDriver.LaunchApp();
                //}
            }
            catch (Exception ex)
            {
                driver = null;
                isRunningMobile = false;
                MessageBox.Show("Nem sikerült elindítani az Android drivert: " + ex.Message);
            }
        }

        public static Task StartAndroidAppAsync(string deviceName, string platformVersion, string testname, string appPackage, string appActivity, int maxwaittimeMobil)
        {
            return Task.Run(() =>
            {
                StartAndroidApp(deviceName, platformVersion, testname, appPackage, appActivity, maxwaittimeMobil);
            });
        }

        // --- IOS INDÍTÁS ---
        public static void StartIOSApp(string deviceName, string platformVersion, string bundleId, int maxwaittimeMobil)
        {
            if (isRunningMobile)
            {
                MessageBox.Show("Az iOS driver már fut. Kérlek, állítsd le először.");
                return;
            }

            try
            {
                AppiumServerManager.StartAppiumServer();

                var options = new AppiumOptions();
                options.PlatformName = "iOS";
                options.AutomationName = "XCUITest";
                options.AddAdditionalOption("deviceName", deviceName);
                options.AddAdditionalOption("platformVersion", platformVersion);
                options.AddAdditionalOption("bundleId", bundleId);
                options.AddAdditionalOption("noReset", true);
                options.AddAdditionalOption("autoGrantPermissions", true);

                driver = new IOSDriver(new Uri("http://127.0.0.1:4723/wd/hub"), options, TimeSpan.FromSeconds(maxwaittimeMobil));
                isRunningMobile = true;

                ////Korábbi appium verzióhoz kellett, csak a konkrét IOSDriver-en hívjuk a LaunchApp-t
                //if (driver is IOSDriver iosDriver)
                //{
                //    iosDriver.LaunchApp();
                //}
            }
            catch (Exception ex)
            {
                driver = null;
                isRunningMobile = false;
                MessageBox.Show("Nem sikerült elindítani az iOS drivert: " + ex.Message);
            }
        }

        public static Task StartIOSAppAsync(string deviceName, string platformVersion, string bundleId, int maxwaittimeMobil)
        {
            return Task.Run(() =>
            {
                StartIOSApp(deviceName, platformVersion, bundleId, maxwaittimeMobil);
            });
        }

        // --- STOP ---
        public static void StopMobile()
        {
            if (!isRunningMobile) return;

            try
            {
                driver?.Quit();
                driver = null;
            }
            finally
            {
                isRunningMobile = false;
            }
        }

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
}