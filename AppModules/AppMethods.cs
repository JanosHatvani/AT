using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.iOS;
using OpenQA.Selenium.Support.UI;
using System.Windows.Forms;
using System.Xml.Linq;
using WDModules;
using AppModules;
using static OpenQA.Selenium.BiDi.Modules.BrowsingContext.Locator;
using static System.Net.Mime.MediaTypeNames;

namespace TestAutomationUI

{
    public class AppMethods
    {

        private static AppiumDriver driver;
        private static bool isRunningMobile = false;
        public static string testName { get; set; }
        public static bool IsRunningMobile => isRunningMobile;
        public static bool CaptureScreenshots { get; set; } // Fast mode default érték
        public static int MaxWaitTime { get; set; } = 20;



        // --- ANDROID INDÍTÁS ---
        public static void StartAndroidApp(string deviceName, string platformVersion, string testname, string appPackage, string appActivity)
        {
            if (isRunningMobile)
            {
                MessageBox.Show("Az Android driver már fut. Kérlek, állítsd le először.");
                return;
            }

            try
            {
                // Először Appium szerver indítás
                AppiumServerManager.StartAppiumServer();

                var options = new AppiumOptions();
                options.PlatformName = "Android";
                options.AutomationName = testname;

                options.AddAdditionalOption("deviceName", deviceName);
                options.AddAdditionalOption("platformVersion", platformVersion);
                options.AddAdditionalOption("appPackage", appPackage);
                options.AddAdditionalOption("appActivity", appActivity);
                options.AddAdditionalOption("noReset", true);

                driver = new AndroidDriver(new Uri("http://127.0.0.1:4723/wd/hub"), options, TimeSpan.FromSeconds(MaxWaitTime));

            }
            catch (Exception ex)
            {
                driver = null;
                isRunningMobile = false;
                MessageBox.Show("Nem sikerült elindítani az Android drivert: " + ex.Message);
            }
        }


        public static Task StartAndroidAppAsync(string deviceName, string platformVersion, string testname, string appPackage, string appActivity)
        {
            return Task.Run(() =>
            {
                StartAndroidApp(deviceName, platformVersion, testname, appPackage, appActivity);
            });
        }


        // --- IOS INDÍTÁS ---
        public static void StartIOSApp(string deviceName, string platformVersion, string bundleId)
        {
            if (isRunningMobile)
            {
                MessageBox.Show("Az iOS driver már fut. Kérlek, állítsd le először.");
                return;
            }

            try
            {
                // Először indítsuk az Appium szervert
                AppiumServerManager.StartAppiumServer();

                var options = new AppiumOptions();
                options.PlatformName = "iOS";
                options.AutomationName = "XCUITest";

                options.AddAdditionalOption("deviceName", deviceName);
                options.AddAdditionalOption("platformVersion", platformVersion);
                options.AddAdditionalOption("bundleId", bundleId);
                options.AddAdditionalOption("noReset", true);

                driver = new IOSDriver(new Uri("http://127.0.0.1:4723/wd/hub"), options, TimeSpan.FromSeconds(MaxWaitTime));
                isRunningMobile = true;
            }
            catch (Exception ex)
            {
                driver = null;
                isRunningMobile = false;
                MessageBox.Show("Nem sikerült elindítani az iOS drivert: " + ex.Message);
            }
        }


        public static Task StartIOSAppAsync(string deviceName, string platformVersion, string bundleId)
        {
            return Task.Run(() =>
            {
                StartIOSApp(deviceName, platformVersion, bundleId);
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

        public static void TakePrtsc(string testName, string prtScfolderpath)
        {
            if (!CaptureScreenshots) return;

            if (string.IsNullOrWhiteSpace(prtScfolderpath) || string.IsNullOrWhiteSpace(testName))
                return;

            if (!Directory.Exists(prtScfolderpath))
                Directory.CreateDirectory(prtScfolderpath);

            try
            {
                var screenshot = ((ITakesScreenshot)driver).GetScreenshot();
                var filename = Path.Combine(prtScfolderpath, $"{testName}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                // Mentés byte tömbként, így nem kell a ScreenshotImageFormat
                File.WriteAllBytes(filename, screenshot.AsByteArray);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Screenshot mentése sikertelen: {ex.Message}");
            }
        }


        // --- ELEMENT KERESÉS ---
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

            By by;
            switch (elementType)
            {
                case PropertyTypes.Id:
                    by = MobileBy.Id(element);
                    break;
                case PropertyTypes.Name:
                    by = MobileBy.Name(element);
                    break;
                case PropertyTypes.ClassName:
                    by = MobileBy.ClassName(element);
                    break;
                case PropertyTypes.TagName:
                    by = MobileBy.TagName(element);
                    break;
                case PropertyTypes.Xpath:
                    by = MobileBy.XPath(element);
                    break;
                case PropertyTypes.Accessibilityid:
                    by = MobileBy.AccessibilityId(element);
                    break;
                case PropertyTypes.IosClassChain:
                    by = MobileBy.IosClassChain(element);
                    break;
                case PropertyTypes.IosNSPredicate:
                    by = MobileBy.IosNSPredicate(element);
                    break;
                case PropertyTypes.AndroidDataMatcher:
                    by = MobileBy.AndroidDataMatcher(element);
                    break;
                case PropertyTypes.AndroidViewMatcher:
                    by = MobileBy.AndroidViewMatcher(element);
                    break;
                default:
                    throw new NotSupportedException($"Nem támogatott property típus: {elementType}");
            }

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
            var elem = wait.Until(drv => drv.FindElement(by));

            action(elem);
        }

        // --- ACTION METÓDUSOK ---
        public static Task MobileClick(string locator, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                PerformElementAction(locator, elementType, elem => elem.Click(), timeoutSeconds);
            });
        }

        public static Task MobileSendKeys(string locator, string text, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                PerformElementAction(locator, elementType, elem => elem.SendKeys(text), timeoutSeconds);
            });

        }

        public static Task MobileClear(string locator, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                PerformElementAction(locator, elementType, elem => elem.Clear(), timeoutSeconds);
            });
        }

        public static string MobileGetText(string locator, PropertyTypes elementType, int timeoutSeconds)
        {
            var elem = FindElement(locator, elementType, timeoutSeconds);
            return elem.Text;
        }

        public static bool MobileWaitForElementVisible(string locator, PropertyTypes elementType, int timeoutSeconds)
        {
            try
            {
                var elem = FindElement(locator, elementType, timeoutSeconds);
                return elem.Displayed;
            }
            catch { return false; }
        }

        public static Task MobileScrolltoelementandClick(string locator, PropertyTypes elementType, string name, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var parent = FindElement(locator, elementType, timeoutSeconds);

                // Megkeressük a kívánt elemet a parent alatt
                var elementToClick = parent.FindElement(By.Name(name));

                // Görgetés Appiumban: JavaScript vagy TouchAction
                try
                {
                    // Példa JavaScript görgetésre (ha WebView):
                    IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                    js.ExecuteScript("arguments[0].scrollIntoView(true);", elementToClick);
                }
                catch
                {
                    // Ha nem webview, akkor Appium TouchAction
                    // new TouchAction(driver).Press(...).MoveTo(...).Release().Perform();
                }

                // Kattintás
                elementToClick.Click();
            });
        }

    }

}