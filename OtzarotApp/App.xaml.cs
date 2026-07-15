using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using OtzarotApp.Services;
using OtzarotApp.ViewModels;
using OtzarotApp.Views;

namespace OtzarotApp;

/// <summary>
/// נקודת הכניסה של האפליקציה.
/// מאתחלת DI container ופותחת את החלון הראשי.
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static MainWindow? MainWindow    { get; private set; }

    public App()
    {
        // אתחול Windows App SDK כ-unpackaged app
        Microsoft.WindowsAppRuntime.Bootstrap.Initialize(0x00010006);

        InitializeComponent();
        Services = ConfigureServices();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }

    // ─── Dependency Injection ────────────────────────────────
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // ── Services (Singleton) ──
        services.AddSingleton<SettingsService>();
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<TantivyService>();
        services.AddSingleton<FontService>();

        // ── ViewModels ──
        services.AddSingleton<MainViewModel>();
        services.AddTransient<BookPanelViewModel>();
        services.AddSingleton<SearchViewModel>();
        services.AddSingleton<SettingsViewModel>();

        return services.BuildServiceProvider();
    }
}
