namespace OtzarotApp.Models;

/// <summary>ספר במסד הנתונים</summary>
public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? HeShortDesc { get; set; }
    public int TotalLines { get; set; }
    public bool HasNekudot { get; set; }
    public bool HasTeamim { get; set; }
    public string? Volume { get; set; }
    public int CategoryId { get; set; }
    public string? CategoryTitle { get; set; }

    /// <summary>שם תצוגה - שם + כרך אם יש</summary>
    public string DisplayName => Volume is { Length: > 0 }
        ? $"{Title} - {Volume}"
        : Title;
}
