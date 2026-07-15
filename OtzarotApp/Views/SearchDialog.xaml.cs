using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using OtzarotApp.Models;
using OtzarotApp.ViewModels;
using Windows.System;

namespace OtzarotApp.Views;

/// <summary>
/// דיאלוג חיפוש מתקדם במאגר הטקסט המלא.
/// </summary>
public sealed partial class SearchDialog : ContentDialog
{
    public SearchViewModel ViewModel { get; }

    public SearchDialog(SearchViewModel vm)
    {
        ViewModel = vm;
        InitializeComponent();
    }

    private void Query_KeyDown(object s, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter &&
            ViewModel.SearchCommand.CanExecute(null))
        {
            _ = ViewModel.SearchCommand.ExecuteAsync(null);
        }
    }

    private void Result_ItemClick(object s, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SearchResult r)
        {
            ViewModel.OpenResultCommand.Execute(r);
            Hide();
        }
    }
}
