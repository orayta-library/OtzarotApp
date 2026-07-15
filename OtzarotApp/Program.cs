using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.WindowsAppRuntime;

namespace OtzarotApp;

public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        DeploymentManager.Initialize();

        Application.Start(_ => new App());
    }
}
