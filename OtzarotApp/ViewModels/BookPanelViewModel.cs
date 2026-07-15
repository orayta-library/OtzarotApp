using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OtzarotApp.Models;
using OtzarotApp.Services;

namespace OtzarotApp.ViewModels;

/// <summary>
/// ViewModel לחלון ספר בודד.
/// מנהל: טעינת תוכן, ניווט בפרקים, רשימת מפרשים, גלילה.
/// </summary>
public partial class BookPanelViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly SettingsService _settings;

    // ─── מזהים ──────────────────────────────────────────────
    public int BookId { get; private set; }

    [ObservableProperty] private Book? _bookInfo;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _currentHeRef = string.Empty;

    // ─── תוכן ────────────────────────────────────────────────
    [ObservableProperty] private List<BookLine> _lines = [];
    [ObservableProperty] private string _htmlContent = string.Empty;
    [ObservableProperty] private bool _isLoading;

    // ─── TOC ─────────────────────────────────────────────────
    [ObservableProperty] private List<TocEntry> _tocRoots = [];
    [ObservableProperty] private bool _isTocOpen;

    // ─── מפרשים ──────────────────────────────────────────────
    [ObservableProperty] private List<Commentary> _commentaries = [];
    [ObservableProperty] private bool _isCommentaryPanelOpen;

    // ─── גופן ────────────────────────────────────────────────
    [ObservableProperty] private string _fontFamily = "David";
    [ObservableProperty] private double _fontSize   = 18;

    // ─── גלילה ───────────────────────────────────────────────
    private int _currentLine;
    private int _totalLines;
    private const int PageSize = 300;

    public event Action<string>? HtmlContentChanged;
    public event Action<int>? ScrollToLineRequested;

    public BookPanelViewModel(DatabaseService db, SettingsService settings)
    {
        _db       = db;
        _settings = settings;
        FontFamily = _settings.FontFamily;
        FontSize   = _settings.FontSize;
    }

    // ─── טעינה ──────────────────────────────────────────────
    public async Task LoadBookAsync(int bookId, int startLine = 0)
    {
        BookId       = bookId;
        _currentLine = startLine;
        IsLoading    = true;

        try
        {
            await Task.Run(() =>
            {
                BookInfo     = _db.GetBook(bookId);
                _totalLines  = _db.GetBookLineCount(bookId);
                TocRoots     = _db.GetBookToc(bookId);
                Commentaries = _db.GetBookCommentaries(bookId);
                Lines        = _db.GetBookLines(bookId, startLine, PageSize);
            });

            Title = BookInfo?.DisplayName ?? string.Empty;
            RebuildHtml();
            UpdateHeRef(startLine);
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ─── ניווט עמודים ────────────────────────────────────────
    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (_currentLine + PageSize >= _totalLines) return;
        _currentLine += PageSize;

        var more = await Task.Run(() =>
            _db.GetBookLines(BookId, _currentLine, PageSize));

        Lines = [.. Lines, .. more];
        RebuildHtml();
    }

    public async Task ScrollToLineAsync(int lineIndex)
    {
        // אם השורה לא טעונה עדיין — טעון עמוד מתאים
        int pageStart = (lineIndex / PageSize) * PageSize;
        if (pageStart != _currentLine)
        {
            _currentLine = pageStart;
            await Task.Run(() =>
                Lines = _db.GetBookLines(BookId, _currentLine, PageSize));
            RebuildHtml();
        }
        UpdateHeRef(lineIndex);
        ScrollToLineRequested?.Invoke(lineIndex);
    }

    public async Task ScrollToTocEntryAsync(TocEntry entry)
        => await ScrollToLineAsync(entry.LineIndex);

    // ─── HTML ────────────────────────────────────────────────
    private void RebuildHtml()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($$"""
            <html dir="rtl"><head>
            <meta charset="utf-8">
            <style>
              body { font-family: '{{FontFamily}}', David, serif;
                      font-size: {{FontSize}}px; line-height: 1.8;
                      margin: 16px 20px; color: #1a1a1a; direction: rtl; }
              p { margin: 0 0 4px; }
              h1,h2,h3,h4 { color: #3d2b1f; margin: 12px 0 6px; }
              .he-ref { color: #888; font-size: 0.75em; margin-left: 8px; }
            </style></head><body>
            """);

        foreach (var line in Lines)
        {
            var anchor = $"id=\"line-{line.LineIndex}\"";
            sb.Append($"<p {anchor}>");
            if (!string.IsNullOrEmpty(line.HeRef))
                sb.Append($"<span class=\"he-ref\">{line.HeRef}</span> ");
            sb.Append(line.Content);
            sb.Append("</p>\n");
        }

        sb.Append("</body></html>");
        HtmlContent = sb.ToString();
        HtmlContentChanged?.Invoke(HtmlContent);
    }

    // ─── עדכון כותרת מיקום ──────────────────────────────────
    private void UpdateHeRef(int lineIndex)
    {
        var line = Lines.FirstOrDefault(l => l.LineIndex == lineIndex)
                ?? Lines.FirstOrDefault();
        CurrentHeRef = line?.HeRef ?? string.Empty;
    }

    // ─── גופן ────────────────────────────────────────────────
    [RelayCommand]
    private void IncreaseFontSize()
    {
        FontSize = Math.Min(FontSize + 2, 48);
        _settings.FontSize = FontSize;
        RebuildHtml();
    }

    [RelayCommand]
    private void DecreaseFontSize()
    {
        FontSize = Math.Max(FontSize - 2, 10);
        _settings.FontSize = FontSize;
        RebuildHtml();
    }

    public void ApplyFont(string family, double size)
    {
        FontFamily = family;
        FontSize   = size;
        RebuildHtml();
    }

    // ─── TOC ─────────────────────────────────────────────────
    [RelayCommand] private void ToggleToc()          => IsTocOpen = !IsTocOpen;
    [RelayCommand] private void ToggleCommentaries() => IsCommentaryPanelOpen = !IsCommentaryPanelOpen;
}
