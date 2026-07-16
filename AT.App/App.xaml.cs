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
        services.AddSingleton<AT.Infrastructure.ITestCategoryService, AT.Infrastructure.TestCategoryService>();
        services.AddSingleton<IThemeService, ThemeService>();

        // Riport-email (ütemezett futtatás hibája esetén). Singleton, mint a többi
        // stateless szolgáltatás — nincs saját, hosszú életű állapota.
        services.AddSingleton<IEmailNotificationService, EmailNotificationService>();

        // Android SDK program-belüli, felhasználó-vezérelt telepítése — a Mobil nézetre
        // navigáláskor (MainViewModel.OnNavigateRequested) ellenőrizzük vele, van-e SDK,
        // és ha nincs, ez indítja a letöltést/telepítést (AndroidSdkSetupWindow-n keresztül).
        services.AddSingleton<IAndroidSdkInstallerService, AndroidSdkInstallerService>();

        // ---- Ütemezés (ütemezett/automatikus futtatás) ----
        // A ScheduledTaskService (tiszta adatmodell + JSON-perzisztencia) az AT.Infrastructure
        // projektben van, mert nincs WPF- vagy Automation-függősége. A SchedulerService és a
        // TestExecutionService viszont az AT.App projektben (AT.App.Services namespace) van,
        // mert a DispatcherTimer-t (WPF) és a Web/Desktop/MobileAutomationDriver-eket
        // használják — ha ezek az AT.Infrastructure-ben lennének, körkörös projekt-
        // referenciát okoznának (Infrastructure -> Automation/App -> ... -> Infrastructure).
        // Mindhárom Singleton: a ScheduledTaskService a betöltött feladatlistát tartja
        // memóriában a program teljes futása alatt, a SchedulerService egyetlen,
        // program-életciklus alatt futó DispatcherTimer-t kezel, a TestExecutionService
        // pedig a Singleton drivereket (Web/Desktop/MobileAutomationDriver) használja —
        // ezekből is csak egy-egy példány élhet egyszerre.
        services.AddSingleton<AT.Infrastructure.IScheduledTaskService, AT.Infrastructure.ScheduledTaskService>();
        services.AddSingleton<ITestExecutionService, TestExecutionService>();
        services.AddSingleton<ISchedulerService, SchedulerService>();

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
        services.AddSingleton<ScheduledTasksViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // ---- Ablakok ----
        // Transient: minden GetRequiredService<MainWindow>() hívás új példányt ad —
        // ez szükséges a témaváltáskori teljes ablak-újranyitáshoz (lásd MainWindow.
        // OnThemeChanged). Az állapot nem vész el, mert a DataContext (MainViewModel)
        // továbbra is Singleton, csak maga az ablak-héj cserélődik.
        services.AddTransient<MainWindow>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        // A ViewModel-ek (Web/Desktop/Mobil/Settings) a konstruktorukban olvassák ki az
        // alapértelmezéseket, ezért a beállítások betöltésének meg kell előznie a MainWindow
        // (és így az összes ViewModel) feloldását.
        var settingsService = _host.Services.GetRequiredService<AT.Infrastructure.ISettingsService>();
        await settingsService.LoadAsync();

        // A mentett téma alkalmazása még a MainWindow megjelenítése előtt, hogy ne
        // villanjon fel egy pillanatra a világos téma sötét beállítás esetén.
        var themeService = _host.Services.GetRequiredService<IThemeService>();
        themeService.ApplyTheme(settingsService.Current.IsDarkTheme);

        // A teszt-kategóriák betöltése a Beállítások betöltése UTÁN (ugyanaz a mappa-logika,
        // mint a ScheduledTaskService-nél), de MÉG a ScheduledTaskService előtt — bár a kettő
        // ma nem függ egymástól induláskor, ha még nincs egyetlen kategória sem (friss
        // telepítés), itt jön létre automatikusan az "Általános" alap-kategória minden
        // platformra érvényesként, hogy a kategória-választás kötelező volta ne akassza el
        // azonnal a használatot.
        var categoryService = _host.Services.GetRequiredService<AT.Infrastructure.ITestCategoryService>();
        await categoryService.LoadAsync();

        // Az ütemezett feladatok betöltése a Beállítások betöltése UTÁN történik (a
        // ScheduledTaskService a settingsService.Current.TestHistoryFolderPath alapján
        // dönti el, hova mentse/honnan olvassa a scheduled-tasks.json-t), de még a
        // MainWindow (és a ScheduledTasksViewModel konstruktorában futó LoadRows) előtt,
        // hogy a nézet első megnyitásakor már a friss listát lássa.
        var scheduledTaskService = _host.Services.GetRequiredService<AT.Infrastructure.IScheduledTaskService>();
        await scheduledTaskService.LoadAsync();

        // A scheduler elindítása — percenként ellenőrzi, esedékes-e valamelyik feladat.
        // Csak akkor fut le bármi, amíg az AT.App meg van nyitva (nincs Windows Task
        // Scheduler-integráció, nincs külön szolgáltatás-folyamat).
        var schedulerService = _host.Services.GetRequiredService<ISchedulerService>();
        schedulerService.Start();

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
            // A scheduler leállítása, mielőtt a host (és vele a driverek) leállnának —
            // enélkül egy épp esedékessé váló feladat egy már leállított driveren
            // próbálna meg futtatni, ami kezeletlen kivételhez vezetne kilépéskor.
            var schedulerService = _host.Services.GetRequiredService<ISchedulerService>();
            schedulerService.Stop();
        }
        catch
        {
            // Ha a host már nem szolgáltat (pl. korai kilépés induláskori hiba miatt),
            // a leállítás elhagyható — nincs mit leállítani.
        }

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
