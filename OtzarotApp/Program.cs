using Microsoft.WindowsAppSDK.Runtime;
using Microsoft.UI.Xaml;

namespace OtzarotApp;

public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // אתחול Windows App SDK כ-unpackaged app
        Bootstrap.Initialize(0x00010006); // 1.6.x

        var app = new Application();
        Application.Start(_ => new App());

        Bootstrap.Uninitialize();
    }
}
