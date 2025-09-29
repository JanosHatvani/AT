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

            var appiumExePath = FindAppiumDesktopPathOrInstall().Result;

            if (string.IsNullOrEmpty(appiumExePath) || !File.Exists(appiumExePath))
            {
                // csak akkor dobjon hibát, ha tényleg nincs Appium és nem indítottunk telepítőt
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = appiumExePath,
                Arguments = "--address 127.0.0.1 --port 4723 --base-path /wd/hub",
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

        private static async Task<string> FindAppiumDesktopPathOrInstall()
        {
            var result = MessageBox.Show(
                "Az Appium Desktop nincs telepítve. Szeretnéd letölteni és telepíteni most?",
                "Appium telepítés",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.No)
            {
                MessageBox.Show(
                    "Kérlek töltsd le manuálisan innen:\n" +
                    "https://github.com/appium/appium-desktop/releases/download/v1.22.3-4/Appium-Server-GUI-windows-1.22.3-4.exe",
                    "Manuális telepítés szükséges",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                Application.Exit();
                return null;
            }

            string downloadUrl = "https://github.com/appium/appium-desktop/releases/download/v1.22.3-4/Appium-Server-GUI-windows-1.22.3-4.exe";
            string installerPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "Appium-Server-GUI-windows-1.22.3-4.exe");

            // popup ablak a letöltéshez
            Form progressForm = new Form
            {
                Text = "Appium letöltés",
                Width = 450,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label statusLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Text = "A letöltés folyamatban van, ez néhány percig eltart."
            };

            ProgressBar progressBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 25,
                Minimum = 0,
                Maximum = 100
            };

            Label percentLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Text = "0 %"
            };

            progressForm.Controls.Add(percentLabel);
            progressForm.Controls.Add(progressBar);
            progressForm.Controls.Add(statusLabel);

            // letöltés külön Task-ban
            _ = Task.Run(async () =>
            {
                try
                {
                    using (var client = new HttpClient())
                    using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                        var totalRead = 0L;
                        var buffer = new byte[8192];

                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            int read;
                            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, read);
                                totalRead += read;

                                if (totalBytes > 0)
                                {
                                    int progress = (int)((totalRead * 100) / totalBytes);
                                    progressForm.Invoke(new Action(() =>
                                    {
                                        progressBar.Value = progress;
                                        percentLabel.Text = progress + " %";
                                    }));
                                }
                            }
                        }
                    }

                    progressForm.Invoke(new Action(() =>
                    {
                        progressForm.Close();
                    }));

                    // indítsuk el a telepítőt
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = installerPath,
                        UseShellExecute = true
                    });

                    MessageBox.Show("A telepítő elindult. Telepítsd az Appiumot, majd indítsd újra az alkalmazást.",
                        "Telepítés folyamatban",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    Application.Exit(); // fontos: bezárjuk az alkalmazást
                }
                catch (Exception ex)
                {
                    progressForm.Invoke(new Action(() =>
                    {
                        progressForm.Close();
                    }));

                    MessageBox.Show("Hiba történt a letöltés során: " + ex.Message,
                        "Hiba",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            });

            progressForm.ShowDialog();
            return null;
        }
    }
}
