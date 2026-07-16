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

        // Mica backdrop
        SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();

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
                    UpdateTaskBar();
                    break;
                case nameof(MainViewModel.ActivePanel):
                    UpdateTaskBar();
                    break;
                case nameof(MainViewModel.Suggestions):
                    MainSearchBox.ItemsSource = _vm.Suggestions
                        .Select(s => s.Title)
                        .ToList();
                    break;
            }
        });
    }

    private void UpdateTaskBar()
    {
        TaskBarStack.Children.Clear();
        foreach (var panel in _vm.Panels)
        {
            var btn = new Button
            {
                Content = new TextBlock 
                { 
                    Text = panel.Title, 
                    MaxWidth = 150, 
                    TextTrimming = TextTrimming.CharacterEllipsis 
                },
                MinWidth = 120,
                Height = 28,
                Padding = new Thickness(12, 4, 12, 4),
                Margin = new Thickness(0, 0, 4, 0),
                Background = panel == _vm.ActivePanel 
                    ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"]
            };
            btn.Click += (s, e) => _vm.ActivePanel = panel;
            TaskBarStack.Children.Add(btn);
        }
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
        // ItemsSource הוא List<string> של כותרות — מצא את ה-BookSuggestion המתאים
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
        // אם המשתמש בחר הצעה מהרשימה — פתח אותה
        if (args.ChosenSuggestion is string chosenTitle)
        {
            var s = _vm.Suggestions.FirstOrDefault(x => x.Title == chosenTitle);
            if (s is not null)
            {
                _ = _vm.SelectSuggestionCommand.ExecuteAsync(s);
                return;
            }
        }

        // אם הוקלד טקסט חופשי — פתח דיאלוג איתור ספרים (לא חיפוש תוכן)
        if (!string.IsNullOrWhiteSpace(args.QueryText))
        {
            var s = _vm.Suggestions.FirstOrDefault(x =>
                x.Title.Contains(args.QueryText, StringComparison.OrdinalIgnoreCase));
            if (s is not null)
                _ = _vm.SelectSuggestionCommand.ExecuteAsync(s);
            else
                OpenBook_Click(sender, null!);
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
        if (SettingsPanel.Visibility == Visibility.Visible)
        {
            SettingsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        // טען את תוכן ההגדרות לתוך הפאנל
        if (SettingsScrollViewer.Content is null)
        {
            var settingsContent = CreateSettingsContent();
            SettingsScrollViewer.Content = settingsContent;
        }

        SettingsPanel.Visibility = Visibility.Visible;
    }

    private void CloseSettings_Click(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = Visibility.Collapsed;
    }

    private FrameworkElement CreateSettingsContent()
    {
        var stack = new StackPanel { Spacing = 4, Padding = new Thickness(12) };

        // הגדרות DB
        stack.Children.Add(CreateSettingsGroup("מסד נתונים", "\uE838", new[]
        {
            CreateSettingsItem("קובץ seforim.db", _settingsVm.DbPath, "בחר קובץ...",
                async () => await _settingsVm.BrowseDbPathCommand.ExecuteAsync(null))
        }));

        // הגדרות Tantivy
        stack.Children.Add(CreateSettingsGroup("מנוע חיפוש", "\uE721", new[]
        {
            CreateSettingsItem("קובץ tantivy_search.exe", _settingsVm.TantivyPath, "בחר קובץ...",
                async () => await _settingsVm.BrowseTantivyPathCommand.ExecuteAsync(null)),
            CreateSettingsItem("תיקיית אינדקס", _settingsVm.IndexPath, "בחר תיקיה...",
                async () => await _settingsVm.BrowseIndexPathCommand.ExecuteAsync(null)),
            CreateBuildIndexItem()
        }));

        // הגדרות גופן
        stack.Children.Add(CreateFontSettings());

        return stack;
    }

    private Border CreateSettingsGroup(string title, string glyph, FrameworkElement[] items)
    {
        var border = new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(12)
        };

        var stack = new StackPanel { Spacing = 12 };
        
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        header.Children.Add(new FontIcon { Glyph = glyph, FontSize = 18 });
        header.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        stack.Children.Add(header);

        foreach (var item in items)
            stack.Children.Add(item);

        border.Child = stack;
        return border;
    }

    private Grid CreateSettingsItem(string label, string value, string buttonText, Action buttonAction)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Margin = new Thickness(0, 4, 0, 4)
        };

        var labelStack = new StackPanel { Spacing = 4 };
        labelStack.Children.Add(new TextBlock { Text = label, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        labelStack.Children.Add(new TextBlock { Text = value, FontSize = 11, Opacity = 0.7, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(labelStack, 0);
        grid.Children.Add(labelStack);

        var button = new Button { Content = buttonText };
        button.Click += (s, e) => buttonAction();
        Grid.SetColumn(button, 1);
        grid.Children.Add(button);

        return grid;
    }

    private Grid CreateBuildIndexItem()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Margin = new Thickness(0, 4, 0, 4)
        };

        var labelStack = new StackPanel { Spacing = 4 };
        labelStack.Children.Add(new TextBlock { Text = "בניית אינדקס", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        labelStack.Children.Add(new TextBlock { Text = "סרוק מחדש את כל הספרים", FontSize = 11, Opacity = 0.7 });
        Grid.SetColumn(labelStack, 0);
        grid.Children.Add(labelStack);

        var button = new Button { Content = "בנה אינדקס", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        button.Click += async (s, e) => await _settingsVm.BuildIndexCommand.ExecuteAsync(null);
        Grid.SetColumn(button, 1);
        grid.Children.Add(button);

        return grid;
    }

    private Border CreateFontSettings()
    {
        var border = new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(12)
        };

        var stack = new StackPanel { Spacing = 12 };
        
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        header.Children.Add(new FontIcon { Glyph = "\uE8D2", FontSize = 18 });
        header.Children.Add(new TextBlock { Text = "גופן וגודל טקסט", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        stack.Children.Add(header);

        // גופן
        var fontGrid = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, new ColumnDefinition { Width = new GridLength(220) } } };
        fontGrid.Children.Add(new TextBlock { Text = "גופן", VerticalAlignment = VerticalAlignment.Center, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var fontCombo = new ComboBox { ItemsSource = _settingsVm.InstalledFonts, SelectedItem = _settingsVm.SelectedFont, Width = 220 };
        fontCombo.SelectionChanged += (s, e) => _settingsVm.SelectedFont = fontCombo.SelectedItem as string ?? "David";
        Grid.SetColumn(fontCombo, 1);
        fontGrid.Children.Add(fontCombo);
        stack.Children.Add(fontGrid);

        // גודל גופן
        var sizeGrid = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, new ColumnDefinition { Width = new GridLength(220) } } };
        sizeGrid.Children.Add(new TextBlock { Text = "גודל גופן", VerticalAlignment = VerticalAlignment.Center, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var sizeStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var slider = new Slider { Minimum = 10, Maximum = 48, Value = _settingsVm.FontSize, Width = 160 };
        var sizeText = new TextBlock { Text = _settingsVm.FontSize.ToString(), VerticalAlignment = VerticalAlignment.Center, Width = 30 };
        slider.ValueChanged += (s, e) => { _settingsVm.FontSize = e.NewValue; sizeText.Text = ((int)e.NewValue).ToString(); };
        sizeStack.Children.Add(slider);
        sizeStack.Children.Add(sizeText);
        Grid.SetColumn(sizeStack, 1);
        sizeGrid.Children.Add(sizeStack);
        stack.Children.Add(sizeGrid);

        // כפתור שמירה
        var saveBtn = new Button { Content = "שמור הגדרות גופן", Style = (Style)Application.Current.Resources["AccentButtonStyle"], HorizontalAlignment = HorizontalAlignment.Left };
        saveBtn.Click += (s, e) => _settingsVm.SaveFontSettingsCommand.Execute(null);
        stack.Children.Add(saveBtn);

        border.Child = stack;
        return border;
    }

    private async void Settings_Click_OLD(object sender, RoutedEventArgs e)
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
