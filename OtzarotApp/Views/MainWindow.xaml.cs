using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OtzarotApp.Controls;
using OtzarotApp.Helpers;
using OtzarotApp.Services;
using OtzarotApp.ViewModels;

namespace OtzarotApp.Views;

/// <summary>
/// חלון ראשי של האפליקציה.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly MainViewModel     _vm;
    private readonly SearchViewModel   _searchVm;
    private readonly SettingsViewModel _settingsVm;

    public MainWindow()
    {
        InitializeComponent();

        _vm         = App.Services.GetRequired<MainViewModel>();
        _searchVm   = App.Services.GetRequired<SearchViewModel>();
        _settingsVm = App.Services.GetRequired<SettingsViewModel>();

        // הגדר גודל מינימלי לחלון
        var appWindow = GetAppWindow();
        appWindow.Resize(new Windows.Graphics.SizeInt32(1024, 700));
        if (appWindow.Presenter is OverlappedPresenter overlapped)
        {
            overlapped.IsMinimizable = true;
            overlapped.IsMaximizable = true;
        }

        // חבר Canvas ל-ViewModel
        BookCanvas.MainViewModel = _vm;

        // AutoSuggestBox source יתעדכן דרך PropertyChanged
        _vm.PropertyChanged += Vm_PropertyChanged;

        // פתיחת ספר מחיפוש
        _searchVm.OpenBookRequested += async (bookId, line) =>
            await _vm.OpenBookAsync(bookId, line);

        // אתחול אסינכרוני
        _ = InitAsync();

        // אכוף גודל מינימלי
        SizeChanged += (_, _) =>
        {
            var win = GetAppWindow();
            var s = win.Size;
            if (s.Width < 800 || s.Height < 600)
                win.Resize(new Windows.Graphics.SizeInt32(Math.Max(s.Width, 800), Math.Max(s.Height, 600)));
        };
    }

    // ─── אתחול ──────────────────────────────────────────────
    private async Task InitAsync()
    {
        await _vm.InitializeAsync();
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateStatus();
            UpdateServerStatus();
            SyncWelcomePanel();
        });
    }

    // ─── PropertyChanged ─────────────────────────────────────
    private void Vm_PropertyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.StatusMessage):
                    UpdateStatus();
                    break;
                case nameof(MainViewModel.IsServerReady):
                    UpdateServerStatus();
                    break;
                case nameof(MainViewModel.Panels):
                    SyncWelcomePanel();
                    break;
                case nameof(MainViewModel.Suggestions):
                    MainSearchBox.ItemsSource = _vm.Suggestions
                        .Select(s => s.Title)
                        .ToList();
                    break;
            }
        });
    }

    private void UpdateStatus()        => StatusText.Text = _vm.StatusMessage;
    private void UpdateServerStatus()  =>
        ServerStatusText.Text = _vm.IsServerReady ? "חיפוש: פעיל ✓" : "";
    private void SyncWelcomePanel()    =>
        WelcomePanel.Visibility = _vm.Panels.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;

    // ─── חיפוש (AutoSuggestBox) ──────────────────────────────
    private void SearchBox_TextChanged(AutoSuggestBox sender,
        AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            _vm.SearchBoxText = sender.Text;
    }

    private void SearchBox_SuggestionChosen(AutoSuggestBox sender,
        AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        // ה-ItemsSource הוא List<string> של כותרות
        if (args.SelectedItem is string title)
        {
            var s = _vm.Suggestions.FirstOrDefault(x => x.Title == title);
            if (s is not null)
                _ = _vm.SelectSuggestionCommand.ExecuteAsync(s);
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender,
        AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.QueryText))
        {
            var s = _vm.Suggestions.FirstOrDefault(x =>
                x.Title.Contains(args.QueryText, StringComparison.OrdinalIgnoreCase));
            if (s is not null)
                _ = _vm.SelectSuggestionCommand.ExecuteAsync(s);
            else
            {
                // פתח דיאלוג חיפוש עם הטקסט
                _searchVm.Query = args.QueryText;
                OpenSearch_Click(sender, null!);
            }
        }
    }

    // ─── תפריטים ─────────────────────────────────────────────
    private async void OpenBook_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new BookBrowserDialog { XamlRoot = Content.XamlRoot };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && dialog.SelectedBookId.HasValue)
            await _vm.OpenBookAsync(dialog.SelectedBookId.Value);
    }

    private void CloseActivePanel_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.ActivePanel is not null)
            _vm.ClosePanelCommand.Execute(_vm.ActivePanel);
    }

    private async void OpenSearch_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SearchDialog(_searchVm) { XamlRoot = Content.XamlRoot };
        await dialog.ShowAsync();
    }

    private void IncreaseFontSize_Click(object sender, RoutedEventArgs e) =>
        _vm.ActivePanel?.IncreaseFontSizeCommand.Execute(null);

    private void DecreaseFontSize_Click(object sender, RoutedEventArgs e) =>
        _vm.ActivePanel?.DecreaseFontSizeCommand.Execute(null);

    private void FullScreen_Click(object sender, RoutedEventArgs e)
    {
        var appWindow = GetAppWindow();
        if (appWindow.Presenter is FullScreenPresenter)
            appWindow.SetPresenter(AppWindowPresenterKind.Default);
        else
            appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog(_settingsVm) { XamlRoot = Content.XamlRoot };
        await dialog.ShowAsync();
    }

    private void BuildIndex_Click(object sender, RoutedEventArgs e) =>
        Settings_Click(sender, e);

    private async void About_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ContentDialog
        {
            Title = "אודות אוצרות",
            Content = new StackPanel
            {
                Spacing = 8,
                FlowDirection = FlowDirection.RightToLeft,
                Children =
                {
                    new TextBlock
                    {
                        Text = "אוצרות — ספרי קודש",
                        FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                    },
                    new TextBlock
                    {
                        Text = "ממשק WinUI 3 | מנוע חיפוש Tantivy | מאגר אוצריא",
                        Opacity = 0.7
                    },
                    new TextBlock
                    {
                        Text = "גרסה 1.0",
                        Opacity = 0.5, FontSize = 12
                    }
                }
            },
            CloseButtonText = "סגור",
            XamlRoot = Content.XamlRoot,
            FlowDirection = FlowDirection.RightToLeft
        };
        await dlg.ShowAsync();
    }

    // ─── helpers ────────────────────────────────────────────
    private AppWindow GetAppWindow()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var id   = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(id);
    }
}
