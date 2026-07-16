using Microsoft.UI.Xaml.Controls;
using OtzarotApp.ViewModels;

namespace OtzarotApp.Views;

public sealed partial class SettingsPage : UserControl
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage(SettingsViewModel vm)
    {
        ViewModel = vm;
        InitializeComponent();
    }
}
