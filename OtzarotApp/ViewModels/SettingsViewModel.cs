using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OtzarotApp.Services;
using Windows.Storage.Pickers;

namespace OtzarotApp.ViewModels;

/// <summary>
/// ViewModel לדף ההגדרות.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly DatabaseService _db;
    private readonly TantivyService  _tantivy;
    private readonly FontService     _fonts;
    private readonly MainViewModel   _main;

    [ObservableProperty] private string _dbPath        = string.Empty;
    [ObservableProperty] private string _tantivyPath   = string.Empty;
    [ObservableProperty] private string _indexPath     = string.Empty;
    [ObservableProperty] private string _selectedFont  = "David";
    [ObservableProperty] private double _fontSize      = 18;
    [ObservableProperty] private string _selectedTheme = "Default";
    [ObservableProperty] private IReadOnlyList<string> _installedFonts = [];
    [ObservableProperty] private string _statusMessage  = string.Empty;
    [ObservableProperty] private bool   _isBuildingIndex;
    [ObservableProperty] private string _buildProgress  = string.Empty;

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatusMessage));

    public List<string> Themes { get; } = ["Default", "Light", "Dark"];

    public SettingsViewModel(SettingsService settings, DatabaseService db,
                             TantivyService tantivy, FontService fonts,
                             MainViewModel main)
    {
        _settings = settings;
        _db       = db;
        _tantivy  = tantivy;
        _fonts    = fonts;
        _main     = main;

        DbPath        = settings.DbPath;
        TantivyPath   = settings.TantivyPath;
        IndexPath     = settings.IndexPath;
        SelectedFont  = settings.FontFamily;
        FontSize      = settings.FontSize;
        SelectedTheme = settings.Theme;
        InstalledFonts = fonts.GetInstalledFonts();
    }

    // ─── בחירת קבצים ─────────────────────────────────────────
    [RelayCommand]
    private async Task BrowseDbPathAsync()
    {
        var path = await PickFileAsync("db");
        if (path is null) return;

        if (_db.TryOpen(path))
        {
            DbPath = path;
            _settings.DbPath = path;
            StatusMessage = "מסד הנתונים נפתח בהצלחה ✓";
        }
        else
        {
            StatusMessage = "לא ניתן לפתוח — בדוק שזה קובץ seforim.db תקין";
        }
    }

    [RelayCommand]
    private async Task BrowseTantivyPathAsync()
    {
        var path = await PickFileAsync("exe");
        if (path is null) return;
        TantivyPath = path;
        _settings.TantivyPath = path;
        StatusMessage = "נתיב מנוע החיפוש עודכן";
    }

    [RelayCommand]
    private async Task BrowseIndexPathAsync()
    {
        var folder = await PickFolderAsync();
        if (folder is null) return;
        IndexPath = folder;
        _settings.IndexPath = folder;
        StatusMessage = "נתיב האינדקס עודכן";
    }

    // ─── בניית אינדקס ────────────────────────────────────────
    [RelayCommand(CanExecute = nameof(CanBuildIndex))]
    private async Task BuildIndexAsync()
    {
        if (string.IsNullOrEmpty(DbPath) || string.IsNullOrEmpty(IndexPath))
        {
            StatusMessage = "יש לבחור נתיב DB ונתיב לאינדקס תחילה";
            return;
        }
        if (string.IsNullOrEmpty(TantivyPath))
        {
            StatusMessage = "יש לבחור נתיב ל-tantivy_search.exe תחילה";
            return;
        }

        IsBuildingIndex = true;
        BuildProgress   = string.Empty;
        StatusMessage   = "בונה אינדקס — אנא המתן...";

        var progress = new Progress<string>(msg =>
            BuildProgress = msg);

        var ok = await _tantivy.BuildIndexAsync(DbPath, IndexPath, progress);

        IsBuildingIndex = false;
        StatusMessage   = ok ? "האינדקס נבנה בהצלחה ✓" : "שגיאה בבניית האינדקס";

        if (ok)
            await _main.InitializeAsync();
    }

    private bool CanBuildIndex() =>
        !IsBuildingIndex &&
        !string.IsNullOrEmpty(DbPath) &&
        !string.IsNullOrEmpty(TantivyPath) &&
        !string.IsNullOrEmpty(IndexPath);

    // ─── שמירת גופן ─────────────────────────────────────────
    [RelayCommand]
    private void SaveFontSettings()
    {
        _settings.FontFamily = SelectedFont;
        _settings.FontSize   = FontSize;
        foreach (var panel in _main.Panels)
            panel.ApplyFont(SelectedFont, FontSize);
        StatusMessage = "הגדרות גופן נשמרו ✓";
    }

    // ─── ערכת נושא ──────────────────────────────────────────
    [RelayCommand]
    private void SaveTheme()
    {
        _settings.Theme = SelectedTheme;
        var root = App.MainWindow?.Content as Microsoft.UI.Xaml.FrameworkElement;
        if (root is not null)
        {
            root.RequestedTheme = SelectedTheme switch
            {
                "Light" => Microsoft.UI.Xaml.ElementTheme.Light,
                "Dark"  => Microsoft.UI.Xaml.ElementTheme.Dark,
                _       => Microsoft.UI.Xaml.ElementTheme.Default
            };
        }
        StatusMessage = "ערכת הנושא שונתה ✓";
    }

    // ─── File / Folder pickers ──────────────────────────────
    private static async Task<string?> PickFileAsync(string extension)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add($".{extension}");
        picker.FileTypeFilter.Add("*");
        picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private static async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
        picker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
