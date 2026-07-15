using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using OtzarotApp.Models;
using OtzarotApp.ViewModels;
using Windows.Foundation;

namespace OtzarotApp.Controls;

/// <summary>
/// פאנל תצוגת ספר — חלון נגרר עם WebView2.
/// כולל: גרירה, שינוי גודל, TOC, מפרשים.
/// </summary>
public sealed partial class BookPanel : UserControl
{
    public BookPanelViewModel? ViewModel { get; private set; }

    // ─── מיקום וגודל ─────────────────────────────────────────
    private Point  _position   = new(40, 40);
    private Size   _panelSize  = new(600, 500);
    private bool   _minimized;
    private double _savedHeight;

    // ─── גרירה ──────────────────────────────────────────────
    private bool  _isDragging;
    private Point _dragOffset;

    // ─── resize ──────────────────────────────────────────────
    private bool  _isResizing;
    private Point _resizeStart;
    private Size  _resizeStartSize;

    // ─── אירועים ─────────────────────────────────────────────
    public event Action<BookPanel>? CloseRequested;
    public event Action<BookPanel, Commentary>? OpenCommentaryRequested;

    public BookPanel()
    {
        InitializeComponent();
        ApplyPosition();
        ApplySize();
    }

    public void Initialize(BookPanelViewModel vm)
    {
        ViewModel = vm;
        vm.HtmlContentChanged += LoadHtml;
        vm.PropertyChanged    += Vm_PropertyChanged;

        BookTitleText.Text = vm.Title;
        HeRefText.Text     = vm.CurrentHeRef;

        if (!string.IsNullOrEmpty(vm.HtmlContent))
            LoadHtml(vm.HtmlContent);
    }

    // ─── HTML ────────────────────────────────────────────────
    private async void LoadHtml(string html)
    {
        await ContentWebView.EnsureCoreWebView2Async();
        ContentWebView.CoreWebView2.NavigateToString(html);
    }

    private void WebView_NavigationCompleted(WebView2 sender,
        Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
    {
        LoadingRing.IsActive = false;
        ContentWebView.Visibility = Visibility.Visible;
    }

    // ─── PropertyChanged ─────────────────────────────────────
    private void Vm_PropertyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(BookPanelViewModel.Title):
                    BookTitleText.Text = ViewModel?.Title ?? "";
                    break;
                case nameof(BookPanelViewModel.CurrentHeRef):
                    HeRefText.Text = ViewModel?.CurrentHeRef ?? "";
                    break;
                case nameof(BookPanelViewModel.IsLoading):
                    LoadingRing.IsActive = ViewModel?.IsLoading ?? false;
                    break;
                case nameof(BookPanelViewModel.IsTocOpen):
                    TocPanel.Visibility = (ViewModel?.IsTocOpen ?? false)
                        ? Visibility.Visible : Visibility.Collapsed;
                    break;
            }
        });
    }

    // ─── כפתורי כותרת ────────────────────────────────────────
    private void Close_Click(object s, RoutedEventArgs e) =>
        CloseRequested?.Invoke(this);

    private void Minimize_Click(object s, RoutedEventArgs e)
    {
        if (_minimized)
        {
            Height = _savedHeight;
            _minimized = false;
        }
        else
        {
            _savedHeight = ActualHeight;
            Height = 42; // רק כותרת
            _minimized = true;
        }
        ApplySize();
    }

    private void Maximize_Click(object s, RoutedEventArgs e)
    {
        if (Parent is Canvas canvas)
        {
            if (Width == canvas.ActualWidth)
            {
                // שחזר לגודל קודם
                _panelSize = new Size(600, 500);
                MaximizeIcon.Glyph = "\uE922";
            }
            else
            {
                _panelSize = new Size(canvas.ActualWidth, canvas.ActualHeight);
                _position  = new Point(0, 0);
                MaximizeIcon.Glyph = "\uE923";
            }
            ApplyPosition();
            ApplySize();
        }
    }

    private void LocationBreadcrumb_Click(object s, RoutedEventArgs e)
    {
        ViewModel?.ToggleTocCommand.Execute(null);
    }

    private void CloseToc_Click(object s, RoutedEventArgs e)
    {
        ViewModel?.ToggleTocCommand.Execute(null);
    }

    private void Commentary_Click(object s, RoutedEventArgs e)
    {
        CommentaryPopup.IsOpen = !CommentaryPopup.IsOpen;
    }

    private void Commentary_ItemClick(object s, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Commentary c)
        {
            CommentaryPopup.IsOpen = false;
            OpenCommentaryRequested?.Invoke(this, c);
        }
    }

    private void IncreaseFontSize_Click(object s, RoutedEventArgs e) =>
        ViewModel?.IncreaseFontSizeCommand.Execute(null);

    private void DecreaseFontSize_Click(object s, RoutedEventArgs e) =>
        ViewModel?.DecreaseFontSizeCommand.Execute(null);

    private async void LoadMore_Click(object s, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.LoadMoreCommand.ExecuteAsync(null);
    }

    private async void TocEntry_Invoked(TreeView s, TreeViewItemInvokedEventArgs e)
    {
        if (e.InvokedItem is TocEntry entry && ViewModel is not null)
            await ViewModel.ScrollToTocEntryAsync(entry);
    }

    // ─── גרירת חלון ─────────────────────────────────────────
    private void TitleBar_PointerPressed(object s, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(Parent as UIElement);
        _isDragging = true;
        _dragOffset = new Point(pt.Position.X - _position.X,
                                pt.Position.Y - _position.Y);
        ((UIElement)s).CapturePointer(e.Pointer);
    }

    private void TitleBar_PointerMoved(object s, PointerRoutedEventArgs e)
    {
        if (!_isDragging) return;
        var pt = e.GetCurrentPoint(Parent as UIElement);
        _position = new Point(
            Math.Max(0, pt.Position.X - _dragOffset.X),
            Math.Max(0, pt.Position.Y - _dragOffset.Y));
        ApplyPosition();
    }

    private void TitleBar_PointerReleased(object s, PointerRoutedEventArgs e)
    {
        _isDragging = false;
        ((UIElement)s).ReleasePointerCapture(e.Pointer);
    }

    // ─── Resize ──────────────────────────────────────────────
    private void ResizeHandle_PointerPressed(object s, PointerRoutedEventArgs e)
    {
        _isResizing      = true;
        _resizeStart     = e.GetCurrentPoint(Parent as UIElement).Position;
        _resizeStartSize = new Size(ActualWidth, ActualHeight);
        ((UIElement)s).CapturePointer(e.Pointer);
    }

    private void ResizeHandle_PointerMoved(object s, PointerRoutedEventArgs e)
    {
        if (!_isResizing) return;
        var pt   = e.GetCurrentPoint(Parent as UIElement).Position;
        var newW = Math.Max(MinWidth,  _resizeStartSize.Width  + (pt.X - _resizeStart.X));
        var newH = Math.Max(MinHeight, _resizeStartSize.Height + (pt.Y - _resizeStart.Y));
        _panelSize = new Size(newW, newH);
        ApplySize();
    }

    private void ResizeHandle_PointerReleased(object s, PointerRoutedEventArgs e)
    {
        _isResizing = false;
        ((UIElement)s).ReleasePointerCapture(e.Pointer);
    }

    // ─── עזרי מיקום ─────────────────────────────────────────
    private void ApplyPosition()
    {
        Canvas.SetLeft(this, _position.X);
        Canvas.SetTop(this, _position.Y);
    }

    private void ApplySize()
    {
        Width  = _panelSize.Width;
        Height = _minimized ? 42 : _panelSize.Height;
    }

    // ─── מיקום ראשוני ────────────────────────────────────────
    public void SetInitialPosition(Point pos, Size size)
    {
        _position  = pos;
        _panelSize = size;
        ApplyPosition();
        ApplySize();
    }

    // ─── Context menu ────────────────────────────────────────
    protected override void OnRightTapped(RightTappedRoutedEventArgs e)
    {
        base.OnRightTapped(e);
        var menu = new MenuFlyout();

        var copyItem = new MenuFlyoutItem { Text = "העתק טקסט נבחר" };
        copyItem.Click += async (_, _) =>
        {
            if (ContentWebView.CoreWebView2 is not null)
            {
                await ContentWebView.CoreWebView2.ExecuteScriptAsync(
                    "document.execCommand('copy')");
            }
        };
        menu.Items.Add(copyItem);

        var selectAllItem = new MenuFlyoutItem { Text = "בחר הכל" };
        selectAllItem.Click += async (_, _) =>
        {
            if (ContentWebView.CoreWebView2 is not null)
                await ContentWebView.CoreWebView2.ExecuteScriptAsync(
                    "document.execCommand('selectAll')");
        };
        menu.Items.Add(selectAllItem);

        menu.ShowAt(this, e.GetPosition(this));
    }
}
