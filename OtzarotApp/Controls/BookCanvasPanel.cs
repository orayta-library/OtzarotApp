using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OtzarotApp.Models;
using OtzarotApp.ViewModels;
using Windows.Foundation;

namespace OtzarotApp.Controls;

/// <summary>
/// Canvas שמכיל את כל חלונות הספרים.
/// מנהל הוספה/הסרה ועמדות ראשוניות.
/// </summary>
public class BookCanvasPanel : Canvas
{
    private MainViewModel? _vm;
    private readonly Dictionary<BookPanelViewModel, BookPanel> _panelMap = [];
    private BookPanel? _activePanel;

    public MainViewModel? MainViewModel
    {
        get => _vm;
        set
        {
            if (_vm is not null)
                _vm.PropertyChanged -= Vm_PropertyChanged;

            _vm = value;

            if (_vm is not null)
                _vm.PropertyChanged += Vm_PropertyChanged;
        }
    }

    private void Vm_PropertyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.MainViewModel.Panels))
            DispatcherQueue.TryEnqueue(SyncPanels);
        else if (e.PropertyName == nameof(ViewModels.MainViewModel.ActivePanel))
            DispatcherQueue.TryEnqueue(UpdateActivePanel);
    }

    private void SyncPanels()
    {
        if (_vm is null) return;
        var vms = _vm.Panels;

        // הסר חלונות שנמחקו
        var toRemove = _panelMap.Keys.Except(vms).ToList();
        foreach (var vm in toRemove)
        {
            if (_panelMap.TryGetValue(vm, out var panel))
            {
                Children.Remove(panel);
                _panelMap.Remove(vm);
            }
        }

        // הוסף חלונות חדשים
        foreach (var vm in vms)
        {
            if (_panelMap.ContainsKey(vm)) continue;

            var panel = new BookPanel();
            panel.Initialize(vm);

            // מיקום ראשוני — cascade בהפרש 30px
            int idx = _panelMap.Count;
            double x = 20 + idx * 30;
            double y = 20 + idx * 30;

            // אם מדובר במפרשן (הספר השני ואילך) — מתחת לראשון, שליש רוחב
            if (idx > 0 && _panelMap.Count > 0)
            {
                var firstPanel = _panelMap.Values.First();
                double parentW = firstPanel.Width;
                double parentH = firstPanel.ActualHeight > 0 ? firstPanel.ActualHeight : 500;
                x = Canvas.GetLeft(firstPanel);
                y = Canvas.GetTop(firstPanel) + parentH * 0.67;
                panel.SetInitialPosition(new Point(x, y),
                    new Size(parentW, parentH * 0.33));
            }
            else
            {
                panel.SetInitialPosition(new Point(x, y), new Size(620, 520));
            }

            // הוסף event כשלוחצים על החלון להעביר אותו קדימה
            panel.PointerPressed += (s, e) =>
            {
                BringPanelToFront(panel);
                if (_vm is not null)
                    _vm.ActivePanel = vm;
            };

            panel.CloseRequested += p =>
                _vm.ClosePanelCommand.Execute(p.ViewModel);

            panel.OpenCommentaryRequested += async (p, c) =>
            {
                if (_vm is not null && p.ViewModel is not null)
                    await _vm.OpenCommentaryAsync(p.ViewModel, c);
            };

            Children.Add(panel);
            _panelMap[vm] = panel;
            
            // העבר את החלון החדש קדימה
            BringPanelToFront(panel);
        }
    }

    private void BringPanelToFront(BookPanel panel)
    {
        if (_activePanel == panel) return;

        // הסר highlight מהחלון הקודם
        if (_activePanel is not null)
        {
            Canvas.SetZIndex(_activePanel, 0);
            _activePanel.Opacity = 0.95;
        }

        // הוסף highlight לחלון הנוכחי
        Canvas.SetZIndex(panel, 100);
        panel.Opacity = 1.0;
        _activePanel = panel;
    }

    private void UpdateActivePanel()
    {
        if (_vm?.ActivePanel is not null && _panelMap.TryGetValue(_vm.ActivePanel, out var panel))
        {
            BringPanelToFront(panel);
        }
    }
}
