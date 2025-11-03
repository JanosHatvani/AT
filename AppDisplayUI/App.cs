using System;
using System.Windows;

namespace AppDisplayUI
{
    public partial class App : Application
    {
        [STAThread]
        public static void Main(string[] args)
        {
            string deviceId = args.Length > 0 ? args[0] : "unknown";
            string platform = args.Length > 1 ? args[1] : "Android";

            var app = new App();
            var window = new AppDisplayWindow(deviceId, platform);
            app.Run(window);
        }
    }
}
