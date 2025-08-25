using System.Diagnostics;
using System.Net.Http;
using System.Threading;

public static class AppiumServerManager
{
    private static Process appiumProcess;

    public static void StartAppiumServer()
    {
        if (appiumProcess != null && !appiumProcess.HasExited)
        {
            // Már fut
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/C appium --address 127.0.0.1 --port 4723 --base-path /wd/hub",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        appiumProcess = new Process { StartInfo = startInfo };
        appiumProcess.Start();

        // Opcionálisan log figyelés
        _ = Task.Run(() =>
        {
            while (!appiumProcess.StandardOutput.EndOfStream)
            {
                string line = appiumProcess.StandardOutput.ReadLine();
                Debug.WriteLine("[Appium] " + line);
            }
        });

        // Várunk amíg a szerver elérhető
        WaitUntilServerIsUp("http://127.0.0.1:4723/wd/hub", 15000);
    }

    public static void StopAppiumServer()
    {
        try
        {
            if (appiumProcess != null && !appiumProcess.HasExited)
            {
                appiumProcess.Kill(true); // child processeket is kilövi
                appiumProcess = null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Appium leállítás sikertelen: " + ex.Message);
        }
    }

    private static void WaitUntilServerIsUp(string url, int timeoutMs)
    {
        var client = new HttpClient();
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                var response = client.GetAsync(url).Result;
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch { }
            Thread.Sleep(500);
        }

        throw new Exception("Appium szerver nem indult el időben.");
    }
}
