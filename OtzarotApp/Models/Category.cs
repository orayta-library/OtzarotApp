namespace OtzarotApp.Models;

/// <summary>קטגוריה בעץ הספרים</summary>
public class Category
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public int OrderIndex { get; set; }
    public List<Category> Children { get; set; } = [];
    public List<Book> Books { get; set; } = [];
}
