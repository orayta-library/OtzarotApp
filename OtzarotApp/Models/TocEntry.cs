namespace OtzarotApp.Models;

/// <summary>פריט בתוכן עניינים של ספר</summary>
public class TocEntry
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int? ParentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int LineIndex { get; set; }
    public int Level { get; set; }
    public List<TocEntry> Children { get; set; } = [];
}
