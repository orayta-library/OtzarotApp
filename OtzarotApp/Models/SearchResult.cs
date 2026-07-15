namespace OtzarotApp.Models;

/// <summary>תוצאת חיפוש מ-Tantivy</summary>
public class SearchResult
{
    public float Score { get; set; }
    public int Id { get; set; }
    public int BookId { get; set; }
    public int LineIndex { get; set; }
    public string Title { get; set; } = string.Empty;
    public string HeRef { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string HeShortDesc { get; set; } = string.Empty;
}

/// <summary>תשובת חיפוש מלאה</summary>
public class SearchResponse
{
    public List<SearchResult> Hits { get; set; } = [];
    public int TotalHits { get; set; }
    public int Took { get; set; }
    public string? Error { get; set; }
}

/// <summary>פרמטרים לחיפוש מתקדם</summary>
public class SearchParameters
{
    public string Query { get; set; } = string.Empty;
    public bool Fuzzy { get; set; }
    public int FuzzyDistance { get; set; } = 1;
    public bool Conjunction { get; set; }
    public bool AddPrefixes { get; set; }
    public bool AddSuffixes { get; set; }
    public bool FullSpelling { get; set; }
    public int Limit { get; set; } = 50;
    public int Offset { get; set; }
}
