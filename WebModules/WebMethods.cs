using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Winium;
using System;
using System.IO;
using WDModules;


namespace WebModules
{
    public class WebMethods
    {


        public static string prtScfolderpath { get; set; }
        public static string testName { get; set; }
        public static bool CaptureScreenshots { get; set; } // Fast mode default érték
        private static bool isRunning = false;
        private static bool isRunningWEB = false;
        public static double LastFindElementDuration { get; private set; } = 0;
        public static bool IsRunning => isRunning;
        public static bool IsRunningWEB => isRunningWEB;

        public static int MaxWaitTime { get; set; } = 20;
        public static string PrtScFolderPath { get; set; }

        private static IWebDriver driver;

        // --- Driver meghatározás ---
        private static readonly string ToolsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools");
        private static readonly string ChromeDriverPath = Path.Combine(ToolsFolder, "chromedriver.exe");
        private static readonly string EdgeDriverPath = Path.Combine(ToolsFolder, "msedgedriver.exe");
        private static readonly string FirefoxDriverPath = Path.Combine(ToolsFolder, "geckodriver.exe");

        // --- Driver check ---
        private static bool CheckChromeDriverExists()
        {
            if (!File.Exists(ChromeDriverPath))
            {
                MessageBox.Show("A Chrome driver (chromedriver.exe) nem található a Tools mappában.");
                return false;
            }
            return true;
        }

        private static bool CheckEdgeDriverExists()
        {
            if (!File.Exists(EdgeDriverPath))
            {
                MessageBox.Show("Az Edge driver (msedgedriver.exe) nem található a Tools mappában.");
                return false;
            }
            return true;
        }

        private static bool CheckFirefoxDriverExists()
        {
            if (!File.Exists(FirefoxDriverPath))
            {
                MessageBox.Show("A Firefox driver (geckodriver.exe) nem található a Tools mappában.");
                return false;
            }
            return true;
        }

        // --- WEB INDÍTÁS ---
        public static void StartChrome(string websitepath, int maxwaittimeWEB)
        {
            if (isRunningWEB)
            {
                MessageBox.Show("A Chrome driver már fut. Kérlek, állítsd le először.");
                return;
            }

            try
            {

                if (!CheckChromeDriverExists()) return;

                var options = new ChromeOptions();
                var service = ChromeDriverService.CreateDefaultService(ToolsFolder);
                service.HideCommandPromptWindow = true;
                driver = new ChromeDriver(service, options, TimeSpan.FromSeconds(maxwaittimeWEB));

                driver.Manage().Window.Maximize(); // A ChromeDriver nem támogatja a --start-maximized argumentumot, ezért itt használjuk a Manage().Window.Maximize() metódust.
                isRunningWEB = true;

                driver.Navigate().GoToUrl(websitepath);
                Thread.Sleep(1000); // Várakozás, hogy a Chrome betöltődjön

            }
            catch (Exception ex)
            {
                driver?.Quit();
                driver = null;
                isRunningWEB = false;
                MessageBox.Show("Nem sikerült elindítani a Chrome drivert: " + ex.Message);
            }
        }

        public static void StartFirefox(string websitepath, int maxwaittimeWEB)
        {
            if (isRunningWEB)
            {
                MessageBox.Show("A Firefox driver már fut. Kérlek, állítsd le először.");
                return;
            }

            try
            {
                if (!CheckFirefoxDriverExists()) return;

                var options = new FirefoxOptions();
                var service = FirefoxDriverService.CreateDefaultService(ToolsFolder);
                driver = new FirefoxDriver(service, options, TimeSpan.FromSeconds(maxwaittimeWEB));
                service.HideCommandPromptWindow = true;
                options.AddArgument("--start-maximized");
                isRunningWEB = true;

                driver.Navigate().GoToUrl(websitepath);
            }
            catch (Exception ex)
            {
                driver?.Quit();
                driver = null;
                isRunningWEB = false;
                MessageBox.Show("Nem sikerült elindítani a Firefox drivert: " + ex.Message);
            }
        }

        public static void StartMicrosoftEdge(string websitepath, int maxwaittimeWEB)
        {
            if (isRunningWEB)
            {
                MessageBox.Show("Az Edge driver már fut. Kérlek, állítsd le először.");
                return;
            }

            try
            {
                if (!CheckEdgeDriverExists()) return;

                var options = new EdgeOptions();
                var service = EdgeDriverService.CreateDefaultService(ToolsFolder);
                driver = new EdgeDriver(service, options, TimeSpan.FromSeconds(maxwaittimeWEB));
                service.HideCommandPromptWindow = true;
                driver.Manage().Window.Maximize(); // Az EdgeDriver nem támogatja a --start-maximized argumentumot, ezért itt használjuk a Manage().Window.Maximize() metódust.
                // options.AddArgument("--start-maximized");
                isRunningWEB = true;

                driver.Navigate().GoToUrl(websitepath);
            }
            catch (Exception ex)
            {
                driver?.Quit();
                driver = null;
                isRunningWEB = false;
                MessageBox.Show("Nem sikerült elindítani az Edge drivert: " + ex.Message);
            }
        }

        public static Task StopWeb()
        {
            return Task.Run(() =>
            {
                if (!isRunningWEB)
                    return;

                try
                {
                    driver?.Quit();
                    driver = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Hiba történt a Chrome/FireFox/MicrosoftEdge driver leállításakor: " + ex.Message);
                }
                finally
                {
                    isRunningWEB = false;
                }
            });
        }

        public static void Pause()
        {
            int ms = 1000000;
            if (!isRunningWEB)
            {
                MessageBox.Show("A WebMethods nem fut. Kérlek, indítsd el először.");
                return;
            }

            else
            {
                Thread.Sleep(ms);
            }
        }

        public static void SetDriver(IWebDriver webDriver)
        {
            driver = webDriver ?? throw new ArgumentNullException(nameof(webDriver));
        }

        public static void WEbStart()
        {
            if (isRunning)
            {
                MessageBox.Show("A WebMethods már fut. Kérlek, állítsd le először.");
                isRunning = true;
                CaptureScreenshots = true; // Alapértelmezett érték
            }
        }

        // --- ELEMENT KERESÉS ---
        private static By GetByLocator(string element, PropertyTypes elementType)
        {
            return elementType switch
            {
                PropertyTypes.Id => By.Id(element),
                PropertyTypes.Name => By.Name(element),
                PropertyTypes.LinkText => By.LinkText(element),
                PropertyTypes.ClassName => By.ClassName(element),
                PropertyTypes.TagName => By.TagName(element),
                PropertyTypes.Xpath => By.XPath(element),
                PropertyTypes.PartialLinkText => By.PartialLinkText(element),
                PropertyTypes.CssSelector => By.CssSelector(element),
                _ => throw new NoSuchElementException($"Az element nem található: {element}")
            };
        }

        private static IWebElement FindElement(string element, PropertyTypes elementType, int timeoutSeconds)
        {
            var by = GetByLocator(element, elementType);
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
            return wait.Until(drv => drv.FindElement(by));
        }

        private static void PerformElementAction(string element, PropertyTypes elementType, Action<IWebElement> action, int timeoutSeconds)
        {
            var elem = FindElement(element, elementType, timeoutSeconds);
            action(elem);
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


        public static Task ChromeStart(string websitepath, int maxwaittimeWEB)
        {
            return Task.Run(() =>
            {
                StartChrome(websitepath, maxwaittimeWEB);
            });
        }

        public static Task FirefoxStart(string websitepath, int maxwaittimeWEB)
        {
            return Task.Run(() =>
            {
                StartFirefox(websitepath, maxwaittimeWEB);
            });
        }

        public static Task MicrosoftEdgeStart(string websitepath, int maxwaittimeWEB)
        {
            return Task.Run(() =>
            {
                StartMicrosoftEdge(websitepath, maxwaittimeWEB);
            });
        }

        // --- ACTION METÓDUSOK ---
        public static Task SendkeysWeb(string element, string value, PropertyTypes elementType, int timeoutSeconds)
        {
            if (string.IsNullOrEmpty(element) || string.IsNullOrEmpty(value))
                throw new ArgumentException("Nem került megadásra element vagy érték.");

            return Task.Run(() =>
            {
                PerformElementAction(element, elementType, elem => elem.SendKeys(value), timeoutSeconds);
            });

        }

        public static Task ClickWeb(string element, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                PerformElementAction(element, elementType, elem => elem.Click(), timeoutSeconds);

            });
        }

        public static Task WebMoveToElement(string element, PropertyTypes elementType, int timeoutSeconds)
        {
            if (string.IsNullOrEmpty(element))
                throw new ArgumentException("Nem került megadásra element.");
            return Task.Run(() =>
            {
                PerformElementAction(element, elementType, elem => new Actions(driver).MoveToElement(elem).Perform(), timeoutSeconds);
            });
        }   
        public static Task ScrollToElementAndClickWeb(string element, string value, PropertyTypes elementType, int timeoutSeconds)
        {
            if (string.IsNullOrEmpty(element) || string.IsNullOrEmpty(value))
                throw new ArgumentException("Nem került megadásra element vagy érték.");

            return Task.Run(() =>
            {
                PerformElementAction(element, elementType, elem =>
                {
                    SelectElement selectElement = new SelectElement(elem);
                    selectElement.SelectByText(value);
                }, timeoutSeconds);

            });
        }

        public static Task TextClearWeb(string element, PropertyTypes elementType, int timeoutSeconds)
        {
            if (string.IsNullOrEmpty(element))
                throw new ArgumentException("Nem került megadásra element.");

            return Task.Run(() =>
            {
                PerformElementAction(element, elementType, elem => elem.Clear(), timeoutSeconds);
            });

        }

        public static Task DoubleClickWeb(string element, PropertyTypes elementType, int timeoutSeconds)
        {
            if (string.IsNullOrEmpty(element))
                throw new ArgumentException("Nem került megadásra element.");
            return Task.Run(() =>
            {
                PerformElementAction(element, elementType, elem => new Actions(driver).DoubleClick(elem).Perform(), timeoutSeconds);
            });
        }

        public static Task RightClickWeb(string element, PropertyTypes elementType, int timeoutSeconds)
        {
            if (string.IsNullOrEmpty(element))
                throw new ArgumentException("Nem került megadásra element.");
            return Task.Run(() =>
            {
                PerformElementAction(element, elementType, elem => new Actions(driver).ContextClick(elem).Perform(), timeoutSeconds);
            });
        }
        public static Task DragAndDropWeb(string sourceElement, string targetElement, PropertyTypes elementType, int timeoutSeconds)
        {
            if (string.IsNullOrEmpty(sourceElement) || string.IsNullOrEmpty(targetElement))
                throw new ArgumentException("Nem került megadásra forrás vagy cél elem.");
            return Task.Run(() =>
            {
                var source = FindElement(sourceElement, elementType, timeoutSeconds);
                var target = FindElement(targetElement, elementType, timeoutSeconds);
                new Actions(driver).DragAndDrop(source, target).Perform();
            });
        }
        public static Task SelectByTextWeb(string element, string value, PropertyTypes elementType, int timeoutSeconds)
        {
            if (string.IsNullOrEmpty(element) || string.IsNullOrEmpty(value))
                throw new ArgumentException("Nem került megadásra element vagy érték.");
            return Task.Run(() =>
            {
                PerformElementAction(element, elementType, elem =>
                {
                    SelectElement selectElement = new SelectElement(elem);
                    selectElement.SelectByText(value);
                }, timeoutSeconds);
            });
        }

        public static Task SelectByValueWeb(string element, string value, PropertyTypes elementType, int timeoutSeconds)
        {
            if (string.IsNullOrEmpty(element) || string.IsNullOrEmpty(value))
                throw new ArgumentException("Nem került megadásra element vagy érték.");
            return Task.Run(() =>
            {
                PerformElementAction(element, elementType, elem =>
                {
                    SelectElement selectElement = new SelectElement(elem);
                    selectElement.SelectByValue(value);
                }, timeoutSeconds);
            });
        }

        public static Task WaitForElementToHaveStyle(string element, PropertyTypes elementType, string style, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElement(By.Id(element)).GetAttribute("style").Contains(style));
            });
        }

        public static Task WaitForElementToHaveCssValue(string element, PropertyTypes elementType, string cssProperty, string value, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElement(By.Id(element)).GetCssValue(cssProperty).Contains(value));
            });
        }

        public static Task WaitForElementToBeVisible(string element, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElement(GetByLocator(element, elementType)).Displayed);
            });
        }

        public static Task WaitForElementToBeClickable(string element, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElement(GetByLocator(element, elementType)).Enabled);
            });
        }

        public static Task WaitForElementToBePresent(string element, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElements(GetByLocator(element, elementType)).Count > 0);
            });
        }

        public static Task WaitForElementToBeAbsent(string element, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElements(GetByLocator(element, elementType)).Count == 0);
            });
        }

        public static Task WaitForElementToHaveText(string element, string text, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElement(GetByLocator(element, elementType)).Text.Contains(text));
            });
        }

        public static Task WaitForElementToHaveAttribute(string element, string attribute, string value, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElement(GetByLocator(element, elementType)).GetAttribute(attribute).Contains(value));
            });
        }

        public static Task WaitForElementToHaveClass(string element, string className, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElement(GetByLocator(element, elementType)).GetAttribute("class").Contains(className));
            });
        }

        public static Task WaitForElementToHaveValue(string element, string value, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElement(GetByLocator(element, elementType)).GetAttribute("value").Contains(value));
            });
        }

        public static Task WaitForElementToHaveTextContent(string element, string text, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElement(GetByLocator(element, elementType)).Text.Contains(text));
            });
        }

        public static Task WaitForElementToHaveAttributeValue(string element, string attribute, string value, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElement(GetByLocator(element, elementType)).GetAttribute(attribute).Contains(value));
            });
        }

        public static Task WaitForElementToHaveCssProperty(string element, string cssProperty, string value, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElement(GetByLocator(element, elementType)).GetCssValue(cssProperty).Contains(value));
            });
        }

        public static Task WaitForElementToHaveStyleProperty(string element, string styleProperty, string value, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElement(GetByLocator(element, elementType)).GetAttribute("style").Contains($"{styleProperty}: {value}"));
            });
        }

        public static Task WaitForElementToHaveAttributeContains(string element, string attribute, string value, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElement(GetByLocator(element, elementType)).GetAttribute(attribute).Contains(value));
            });
        }

        public static Task WaitForElementToHaveClassContains(string element, string className, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElement(GetByLocator(element, elementType)).GetAttribute("class").Contains(className));
            });
        }

        public static Task WaitForElementToHaveValueContains(string element, string value, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElement(GetByLocator(element, elementType)).GetAttribute("value").Contains(value));
            });
        }

        public static Task WaitForElementToHaveTextContains(string element, string text, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElement(GetByLocator(element, elementType)).Text.Contains(text));
            });
        }
    }
}