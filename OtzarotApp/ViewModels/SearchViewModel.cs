using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using OtzarotApp.Models;
using OtzarotApp.Services;

namespace OtzarotApp.ViewModels;

/// <summary>
/// ViewModel לדיאלוג החיפוש המתקדם.
/// תומך בכל סוגי החיפוש של Tantivy.
/// </summary>
public partial class SearchViewModel : ObservableObject
{
    private readonly TantivyService _tantivy;
    private readonly DatabaseService _db;

    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private bool   _isSearching;
    [ObservableProperty] private string _statusText = string.Empty;

    // ─── אפשרויות חיפוש ─────────────────────────────────────
    [ObservableProperty] private bool _fuzzy;
    [ObservableProperty] private int  _fuzzyDistance = 1;
    [ObservableProperty] private bool _conjunction;   // AND בין מילים
    [ObservableProperty] private bool _addPrefixes;
    [ObservableProperty] private bool _addSuffixes;
    [ObservableProperty] private bool _fullSpelling;  // כתיב מלא/חסר

    // ─── תוצאות ──────────────────────────────────────────────
    [ObservableProperty] private List<SearchResult> _results = [];
    [ObservableProperty] private int  _totalHits;
    [ObservableProperty] private int  _currentOffset;
    private const int PageSize = 50;

    public Visibility HasMoreResults => Results.Count > 0 && Results.Count < TotalHits
        ? Visibility.Visible : Visibility.Collapsed;

    partial void OnResultsChanged(List<SearchResult> value) => OnPropertyChanged(nameof(HasMoreResults));
    partial void OnTotalHitsChanged(int value) => OnPropertyChanged(nameof(HasMoreResults));

    public event Action<int, int>? OpenBookRequested; // bookId, lineIndex

    public SearchViewModel(TantivyService tantivy, DatabaseService db)
    {
        _tantivy = tantivy;
        _db      = db;
    }

    // ─── חיפוש ──────────────────────────────────────────────
    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(Query)) return;

        IsSearching   = true;
        CurrentOffset = 0;
        StatusText    = "מחפש...";

        try
        {
            var parms = BuildParams();
            var resp  = await _tantivy.SearchAsync(parms);

            if (resp.Error is not null)
            {
                StatusText = $"שגיאה: {resp.Error}";
                Results = [];
                return;
            }

            Results    = resp.Hits;
            TotalHits  = resp.TotalHits;
            StatusText = TotalHits > 0
                ? $"נמצאו {TotalHits:N0} תוצאות ({resp.Took} מ\"ש)"
                : "לא נמצאו תוצאות";
        }
        catch (Exception ex)
        {
            StatusText = $"שגיאה: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    private bool CanSearch() => !IsSearching && !string.IsNullOrWhiteSpace(Query);

    [RelayCommand]
    private async Task LoadMoreResultsAsync()
    {
        if (Results.Count >= TotalHits) return;
        CurrentOffset += PageSize;

        var parms = BuildParams();
        var resp  = await _tantivy.SearchAsync(parms);

        if (resp.Error is null)
            Results = [.. Results, .. resp.Hits];
    }

    [RelayCommand]
    private void OpenResult(SearchResult result)
    {
        OpenBookRequested?.Invoke(result.BookId, result.LineIndex);
    }

    // ─── helpers ────────────────────────────────────────────
    private SearchParameters BuildParams() => new()
    {
        Query        = Query,
        Fuzzy        = Fuzzy,
        FuzzyDistance = FuzzyDistance,
        Conjunction  = Conjunction,
        AddPrefixes  = AddPrefixes,
        AddSuffixes  = AddSuffixes,
        FullSpelling = FullSpelling,
        Limit        = PageSize,
        Offset       = CurrentOffset
    };
}
