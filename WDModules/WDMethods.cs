using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Winium;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Winium.Elements.Desktop.Extensions;
using ComboBox = Winium.Elements.Desktop.ComboBox;

namespace WDModules
{
    public class WDMethods
    {
        public static WiniumDriver driver { get; private set; }
        public static WiniumDriverService service { get; private set; }
        public static string prtScfolderpath { get; set; }
        public static string testName { get; set; }
        public static bool CaptureScreenshots { get; set; } // Fast mode default érték
        private static bool isRunning = false;
        public static double LastFindElementDuration { get; private set; } = 0;
        public static bool IsRunning => isRunning;

        // Új metódus a fast mode kezelésére

        public static void Start(string appPath, string winiumDriverDirectory, int maxwaittimeWD)
        {
            if (isRunning)
            {
                MessageBox.Show("A driver már fut. Kérlek, állítsd le először.");
                return;
            }

            var options = new DesktopOptions { ApplicationPath = appPath };
            service = WiniumDriverService.CreateDesktopService(winiumDriverDirectory);
            service.Port = 9999;
            service.HideCommandPromptWindow = true;
            service.SuppressInitialDiagnosticInformation = true;

            try
            {
                service.Start();
                driver = new WiniumDriver(service, options, TimeSpan.FromSeconds(maxwaittimeWD));
                isRunning = true;  // Csak itt állítjuk true-ra, ha sikeresen elindult
            }
            catch (Exception ex)
            {
                service?.Dispose();
                service = null;
                driver = null;
                isRunning = false;
                MessageBox.Show("Nem sikerült elindítani a Winium drivert: " + ex.Message);
            }
        }
        public static Task Stop()
        {
            return Task.Run(() =>
            {
                if (!isRunning)
                    return;

                try
                {
                    driver?.Quit();
                    driver = null;

                    if (service != null)
                    {
                        if (service.IsRunning)
                        {
                            service.Dispose();
                        }

                        // Folyamat kilövése, ha még futna
                        try
                        {
                            var processes = System.Diagnostics.Process.GetProcessesByName("Winium.Desktop.Driver");
                            foreach (var process in processes)
                            {
                                process.Kill();
                                process.WaitForExit();
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Nem sikerült teljesen leállítani a Winium drivert: " + ex.Message);
                        }

                        service = null;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Hiba történt a driver vagy service leállításakor: " + ex.Message);
                }
                finally
                {
                    isRunning = false;
                }
            });
        }

        public static void Pause()
        {
            int ms = 1000000;
            if (!isRunning)
            {
                MessageBox.Show("A WdMethods nem fut. Kérlek, indítsd el először.");
                return;
            }

            else
            {
                Thread.Sleep(ms);
            }
        }

        //public static void TakePrtsc(string testName, string prtScfolderpath)
        //{
        //    if (!CaptureScreenshots) return;

        //    if (string.IsNullOrWhiteSpace(prtScfolderpath) || string.IsNullOrWhiteSpace(testName))
        //        return;

        //    if (!Directory.Exists(prtScfolderpath))
        //        Directory.CreateDirectory(prtScfolderpath);

        //    var screenshot = ((ITakesScreenshot)driver).GetScreenshot();
        //    var filename = Path.Combine(prtScfolderpath, $"{testName}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        //    screenshot.SaveAsFile(filename, ScreenshotImageFormat.Png);
        //}

        // --- ELEMENT KERESÉS ---
        private static IWebElement FindElement(string element, PropertyTypes elementType, int timeoutSeconds)
        {
            if (driver == null)
                throw new InvalidOperationException("A driver nincs inicializálva.");

            By by;
            switch (elementType)
            {
                case PropertyTypes.Id:
                    by = By.Id(element);
                    break;
                case PropertyTypes.Name:
                    by = By.Name(element);
                    break;
                case PropertyTypes.ClassName:
                    by = By.ClassName(element);
                    break;
                case PropertyTypes.TagName:
                    by = By.TagName(element);
                    break;
                case PropertyTypes.Xpath:
                    by = By.XPath(element);
                    break;
                default:
                    throw new NotSupportedException($"Nem támogatott property típus: {elementType}");
            }

            int intervalMs = 200;
            int elapsed = 0;
            while (elapsed < timeoutSeconds * 1000)
            {
                try
                {
                    var elem = driver.FindElement(by);
                    return elem;
                }
                catch (NoSuchElementException)
                {
                    Thread.Sleep(intervalMs);
                    elapsed += intervalMs;
                }
            }
            return null;
        }

        // A PerformActionOnElement és FindElement egyesítve
        private static void PerformElementAction(string element, PropertyTypes elementType, Action<IWebElement> action, int timeoutSeconds)
        {
            if (driver == null)
                throw new InvalidOperationException("A driver nincs inicializálva.");

            By by;
            switch (elementType)
            {
                case PropertyTypes.Id:
                    by = By.Id(element);
                    break;
                case PropertyTypes.Name:
                    by = By.Name(element);
                    break;
                case PropertyTypes.ClassName:
                    by = By.ClassName(element);
                    break;
                case PropertyTypes.TagName:
                    by = By.TagName(element);
                    break;
                case PropertyTypes.Xpath:
                    by = By.XPath(element);
                    break;
                default:
                    throw new NotSupportedException($"Nem támogatott property típus: {elementType}");
            }

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
            var elem = wait.Until(drv => drv.FindElement(by));

            action(elem);
        }

        // --- ACTION METÓDUSOK ---

        public static Task StartProg(string programPath, string driverPath, int maxwaittimeWD)
        {
            return Task.Run(() =>
            {
                Start(programPath, driverPath, maxwaittimeWD);
            });
        }

        public static Task Click(string element, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                PerformElementAction(element, elementType, elem => elem.Click(), timeoutSeconds);
            });
        }

        public static Task Sendkeys(string element, string text, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                PerformElementAction(element, elementType, elem => elem.SendKeys(text), timeoutSeconds);
            });
        }

        public static Task TextClear(string element, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                PerformElementAction(element, elementType, elem => elem.Clear(), timeoutSeconds);
            });
        }

        public static Task DoubleClick(string element, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                PerformElementAction(element, elementType, elem => new Actions(driver).DoubleClick(elem).Perform(), timeoutSeconds);
            });
        }

        public static Task RightClick(string element, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                PerformElementAction(element, elementType, elem => new Actions(driver).ContextClick(elem).Perform(), timeoutSeconds);
            });
        }

        public static Task MoveToElement(string element, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                PerformElementAction(element, elementType, elem => new Actions(driver).MoveToElement(elem).Perform(), timeoutSeconds);
            });
        }

        public static Task ScrollToElementAndClick(string element, PropertyTypes elementType, string name, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var parent = FindElement(element, elementType, timeoutSeconds);
                ComboBox comboBox = new ComboBox(parent);
                comboBox.Expand();
                comboBox.ScrollTo(By.Name(name));
                parent.FindElement(By.Name(name)).Click();
            });
        }

        public static Task ElementCheck(string element, PropertyTypes elementType, string value, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var elem = FindElement(element, elementType, timeoutSeconds);
                MessageBox.Show($"Checked value: {elem.GetAttribute(value)}", "Title");
            });
        }

        public static Task<int> ValueReadAndParse(string element, PropertyTypes elementType, string value, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var elem = FindElement(element, elementType, timeoutSeconds);
                return int.Parse(elem.GetAttribute(value));
            });
        }

        public static Task<string> GetTextFromSelectedElement(string element, PropertyTypes elementType, string value, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var elem = FindElement(element, elementType, timeoutSeconds);
                return elem.GetAttribute(value);
            });
        }

        public static Task DragAndDrop(string sourceElement, string targetElement, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var fromElement = FindElement(sourceElement, elementType, timeoutSeconds);
                var toElement = FindElement(targetElement, elementType, timeoutSeconds);
                new Actions(driver).DragAndDrop(fromElement, toElement).Perform();
            });
        }
        public static Task WaitForElement(string element, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElement(By.Id(element)) != null);
            });
        }

        public static Task WaitForElementToDisappear(string element, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElements(By.Id(element)).Count == 0);
            });
        }

        public static Task WaitForElementToBeClickable(string element, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(ExpectedConditions.ElementToBeClickable(By.Id(element)));
            });
        }

        public static Task WaitForElementToBeVisible(string element, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(ExpectedConditions.ElementIsVisible(By.Id(element)));
            });
        }

        public static Task WaitForElementToBeSelected(string element, PropertyTypes elementType, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(ExpectedConditions.ElementToBeSelected(By.Id(element)));
            });
        }

        //public static Task WaitForElementToBeEnabled(string element, PropertyTypes elementType, int timeoutSeconds)
        //{
        //    return Task.Run(() =>
        //    {
        //        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        //        wait.Until(ExpectedConditions.ElementToBeEnabled(By.Id(element)));
        //    });
        //}

        //public static Task WaitForElementToBeDisabled(string element, PropertyTypes elementType, int timeoutSeconds)
        //{
        //    return Task.Run(() =>
        //    {
        //        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        //        wait.Until(ExpectedConditions.ElementToBeDisabled(By.Id(element)));
        //    });
        //}

        public static Task WaitForElementToHaveText(string element, PropertyTypes elementType, string text, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElement(By.Id(element)).Text.Contains(text));
            });
        }

        public static Task WaitForElementToHaveValue(string element, PropertyTypes elementType, string value, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElement(By.Id(element)).GetAttribute("value").Contains(value));
            });
        }

        public static Task WaitForElementToHaveAttribute(string element, PropertyTypes elementType, string attribute, string value, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElement(By.Id(element)).GetAttribute(attribute).Contains(value));
            });
        }

        public static Task WaitForElementToHaveClass(string element, PropertyTypes elementType, string className, int timeoutSeconds)
        {
            return Task.Run(() =>
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
                wait.Until(drv => drv.FindElement(By.Id(element)).GetAttribute("class").Contains(className));
            });
        }

    }
}