using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OtzarotApp.Models;
using OtzarotApp.Services;

namespace OtzarotApp.ViewModels;

/// <summary>
/// ViewModel ראשי — מנהל את כל חלונות הספרים הפתוחים
/// ואת ההשלמה האוטומטית בתיבת החיפוש.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly DatabaseService  _db;
    private readonly TantivyService   _tantivy;
    private readonly SettingsService  _settings;

    // ─── חלונות ספרים ────────────────────────────────────────
    [ObservableProperty] private List<BookPanelViewModel> _panels = [];
    [ObservableProperty] private BookPanelViewModel?      _activePanel;

    // ─── תיבת חיפוש עליונה ──────────────────────────────────
    [ObservableProperty] private string              _searchBoxText = string.Empty;
    [ObservableProperty] private List<BookSuggestion> _suggestions  = [];
    [ObservableProperty] private bool                _showSuggestions;

    // ─── שרת ─────────────────────────────────────────────────
    [ObservableProperty] private bool   _isServerReady;
    [ObservableProperty] private string _statusMessage = string.Empty;

    private CancellationTokenSource? _suggestCts;

    public MainViewModel(DatabaseService db, TantivyService tantivy, SettingsService settings)
    {
        _db      = db;
        _tantivy = tantivy;
        _settings = settings;
    }

    // ─── אתחול ──────────────────────────────────────────────
    public async Task InitializeAsync()
    {
        // פתח DB אם יש נתיב שמור
        if (!string.IsNullOrEmpty(_settings.DbPath))
            _db.TryOpen();

        // הפעל שרת tantivy
        if (!string.IsNullOrEmpty(_settings.TantivyPath))
        {
            StatusMessage = "מפעיל מנוע חיפוש...";
            IsServerReady = await _tantivy.StartServerAsync();
            StatusMessage = IsServerReady ? "מוכן" : "מנוע חיפוש לא זמין";
        }
        else
        {
            StatusMessage = "הגדר נתיב למנוע החיפוש בהגדרות";
        }
    }

    // ─── פתיחת ספר ───────────────────────────────────────────
    /// <summary>פותח ספר חדש בחלון. אם כבר פתוח — מעביר אליו פוקוס.</summary>
    public async Task OpenBookAsync(int bookId, int lineIndex = 0)
    {
        // בדוק אם כבר פתוח
        var existing = Panels.FirstOrDefault(p => p.BookId == bookId);
        if (existing is not null)
        {
            ActivePanel = existing;
            await existing.ScrollToLineAsync(lineIndex);
            return;
        }

        var vm = App.Services.GetService(typeof(BookPanelViewModel)) as BookPanelViewModel
              ?? new BookPanelViewModel(_db, _settings);

        await vm.LoadBookAsync(bookId, lineIndex);

        Panels = [.. Panels, vm];
        ActivePanel = vm;
    }

    /// <summary>פתיחת מפרשן מתחת לחלון מסוים (יחס 1/3 : 2/3)</summary>
    public async Task OpenCommentaryAsync(BookPanelViewModel parent, Commentary commentary)
    {
        await OpenBookAsync(commentary.TargetBookId,
                            commentary.TargetLineIndex ?? 0);
    }

    [RelayCommand]
    private void ClosePanel(BookPanelViewModel panel)
    {
        var list = Panels.ToList();
        int idx  = list.IndexOf(panel);
        list.Remove(panel);
        Panels = list;

        if (ActivePanel == panel)
            ActivePanel = Panels.Count > 0
                ? Panels[Math.Max(0, idx - 1)]
                : null;
    }

    // ─── השלמה אוטומטית ─────────────────────────────────────
    partial void OnSearchBoxTextChanged(string value)
    {
        _suggestCts?.Cancel();
        _suggestCts = new CancellationTokenSource();
        _ = UpdateSuggestionsAsync(value, _suggestCts.Token);
    }

    private async Task UpdateSuggestionsAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            ShowSuggestions = false;
            Suggestions = [];
            return;
        }

        await Task.Delay(200, ct); // debounce
        if (ct.IsCancellationRequested) return;

        List<BookSuggestion> results = [];

        if (IsServerReady)
        {
            results = await _tantivy.SuggestAsync(query, 10);
        }
        
        // fallback: חיפוש ישיר ב-DB רק אם Tantivy לא החזיר תוצאות
        if (results.Count == 0 && _db.IsOpen)
        {
            var books = _db.SearchBooks(query, 10);
            results = books.Select(b => new BookSuggestion
            {
                BookId = b.Id,
                Title  = b.DisplayName,
                HeRef  = string.Empty,
                LineIndex = 0
            }).ToList();
        }

        if (ct.IsCancellationRequested) return;
        Suggestions     = results;
        ShowSuggestions = results.Count > 0;
    }

    [RelayCommand]
    private async Task SelectSuggestionAsync(BookSuggestion suggestion)
    {
        ShowSuggestions = false;
        SearchBoxText   = string.Empty;
        await OpenBookAsync(suggestion.BookId, suggestion.LineIndex);
    }

    [RelayCommand]
    private void HideSuggestions() => ShowSuggestions = false;
}
