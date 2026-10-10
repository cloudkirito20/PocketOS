using System.Windows;

namespace LazyTech.PocketOS.Installer;

public class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new Application();
        app.Run(new InstallerWindow());
    }
}
