namespace OtzarotApp.Models;

/// <summary>שורת טקסט בספר (תוכן HTML)</summary>
public class BookLine
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int LineIndex { get; set; }

    /// <summary>תוכן HTML גולמי מה-DB</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>הפניה עברית (מיקום בספר)</summary>
    public string? HeRef { get; set; }
}
