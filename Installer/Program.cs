using System;
using System.Windows;

namespace LazyTech.PocketOS.Installer;

public class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new System.Windows.Application();

        app.Run(new InstallerWindow());
    }
}