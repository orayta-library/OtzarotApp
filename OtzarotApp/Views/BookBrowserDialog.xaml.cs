using Microsoft.UI.Xaml.Controls;
using OtzarotApp.Models;
using OtzarotApp.Services;

namespace OtzarotApp.Views;

/// <summary>
/// דיאלוג לבחירת ספר מהעץ או מחיפוש.
/// </summary>
public sealed partial class BookBrowserDialog : ContentDialog
{
    private readonly DatabaseService _db;
    public int? SelectedBookId { get; private set; }

    private List<Category> _rootCategories = [];
    private CancellationTokenSource? _searchCts;

    public BookBrowserDialog()
    {
        _db = App.Services.GetService(typeof(DatabaseService)) as DatabaseService
           ?? throw new InvalidOperationException("DatabaseService not registered");
        InitializeComponent();
        LoadRootCategories();
    }

    private void LoadRootCategories()
    {
        if (!_db.IsOpen)
        {
            // אין DB פתוח
            return;
        }

        _rootCategories = _db.GetRootCategories();

        // בנה TreeView nodes
        var items = _rootCategories.Select(c => BuildCategoryNode(c)).ToList();
        foreach (var item in items)
            CategoryTree.RootNodes.Add(item);
    }

    private TreeViewNode BuildCategoryNode(Category category)
    {
        var node = new TreeViewNode
        {
            Content = category,
            HasUnrealizedChildren = true // lazy load
        };
        return node;
    }

    private void CategoryTree_Expanding(TreeView s, TreeViewExpandingEventArgs e)
    {
        if (e.Node.HasUnrealizedChildren && e.Node.Content is Category cat)
        {
            e.Node.Children.Clear();

            var subCats = _db.GetSubCategories(cat.Id);
            foreach (var sub in subCats)
                e.Node.Children.Add(BuildCategoryNode(sub));

            var books = _db.GetBooksInCategory(cat.Id);
            foreach (var book in books)
                e.Node.Children.Add(new TreeViewNode { Content = book });

            e.Node.HasUnrealizedChildren = false;
        }
    }

    private void CategoryTree_ItemInvoked(TreeView s, TreeViewItemInvokedEventArgs e)
    {
        if (e.InvokedItem is TreeViewNode node && node.Content is Book book)
        {
            SelectedBookId = book.Id;
            Hide();
        }
    }

    // ─── חיפוש ──────────────────────────────────────────────
    private void SearchBox_TextChanged(object s, TextChangedEventArgs e)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        _ = UpdateSearchAsync(SearchBox.Text, _searchCts.Token);
    }

    private async Task UpdateSearchAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            CategoryTree.Visibility      = Microsoft.UI.Xaml.Visibility.Visible;
            SearchResultsList.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            return;
        }

        await Task.Delay(250, ct);
        if (ct.IsCancellationRequested) return;

        var results = await Task.Run(() => _db.SearchBooks(query, 100), ct);
        if (ct.IsCancellationRequested) return;

        SearchResultsList.ItemsSource = results;
        CategoryTree.Visibility       = Microsoft.UI.Xaml.Visibility.Collapsed;
        SearchResultsList.Visibility  = Microsoft.UI.Xaml.Visibility.Visible;
    }

    private void SearchResult_ItemClick(object s, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Book book)
        {
            SelectedBookId = book.Id;
            Hide();
        }
    }
}
