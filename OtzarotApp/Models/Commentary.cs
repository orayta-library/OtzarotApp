namespace OtzarotApp.Models;

/// <summary>מפרש / קישור בין ספרים</summary>
public class Commentary
{
    public int SourceBookId { get; set; }
    public int TargetBookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string? HeShortDesc { get; set; }
    public string? Volume { get; set; }
    public string LinkType { get; set; } = string.Empty;
    public int LinkTypeId { get; set; }
    /// <summary>שורת היעד כשנפתח מפרשן לשורה ספציפית</summary>
    public int? TargetLineIndex { get; set; }

    public string DisplayName => Volume is { Length: > 0 }
        ? $"{BookTitle} - {Volume}"
        : BookTitle;
}
