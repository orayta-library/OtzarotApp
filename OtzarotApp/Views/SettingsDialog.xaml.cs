using Microsoft.UI.Xaml.Controls;
using OtzarotApp.ViewModels;

namespace OtzarotApp.Views;

/// <summary>
/// דיאלוג הגדרות האפליקציה.
/// </summary>
public sealed partial class SettingsDialog : ContentDialog
{
    public SettingsViewModel ViewModel { get; }

    public SettingsDialog(SettingsViewModel vm)
    {
        ViewModel = vm;
        InitializeComponent();
    }
}
