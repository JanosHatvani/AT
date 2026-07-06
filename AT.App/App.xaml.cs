using System.Windows;
using AT.App.Services;
using AT.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AT.App;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // ---- Szolgáltatások ----
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IMobileMirrorWindowService, MobileMirrorWindowService>();
        services.AddSingleton<AT.Infrastructure.ISettingsService, AT.Infrastructure.SettingsService>();
        services.AddSingleton<AT.Infrastructure.ITestSuiteFileService, AT.Infrastructure.TestSuiteFileService>();
        services.AddSingleton<AT.Infrastructure.ITestRunHistoryService, AT.Infrastructure.TestRunHistoryService>();
        services.AddSingleton<AT.Infrastructure.ITestReportService, AT.Infrastructure.TestReportService>();

        // ---- Automatizálási driverek ----
        // Konkrét típusként regisztrálva (nem IAutomationDriver-ként), mert egyszerre
        // 3 különböző implementáció élne a konténerben — a modulok közvetlenül a saját
        // konkrét driverüket kapják meg, a szerződést (IAutomationDriver) belsőleg használják.
        services.AddSingleton<AT.Automation.Web.WebAutomationDriver>();
        services.AddSingleton<AT.Automation.Desktop.DesktopAutomationDriver>();
        services.AddSingleton<AT.Automation.Mobile.MobileAutomationDriver>();

        // ---- ViewModel-ek ----
        // Singleton: egy-egy oldal állapota megmarad navigáció közben (pl. futó teszt state-je).
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<WebTestViewModel>();
        services.AddSingleton<DesktopTestViewModel>();
        services.AddSingleton<MobileTestViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // ---- Ablakok ----
        services.AddSingleton<MainWindow>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        // A ViewModel-ek (Web/Desktop/Mobil/Settings) a konstruktorukban olvassák ki az
        // alapértelmezéseket, ezért a beállítások betöltésének meg kell előznie a MainWindow
        // (és így az összes ViewModel) feloldását.
        var settingsService = _host.Services.GetRequiredService<AT.Infrastructure.ISettingsService>();
        await settingsService.LoadAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();

        base.OnStartup(e);
    }

    /// <summary>
    /// Szándékosan NEM async void — az lenne a probléma (böngésző/driver nélkül maradt
    /// árva folyamat), mert a WPF nem várja meg egy async void OnExit befejeződését,
    /// és a folyamat leállhat a Dispose() (ami a WebAutomationDriver.Quit()-ját is hívja)
    /// tényleges lefutása előtt. A GetAwaiter().GetResult() szinkronba kényszeríti.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _host.StopAsync().GetAwaiter().GetResult();
        }
        finally
        {
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
