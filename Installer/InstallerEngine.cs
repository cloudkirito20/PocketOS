using System;
using System.IO;
namespace LazyTech.PocketOS.Installer;

public class InstallerEngine
{
    public string InstallPath { get; set; } =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "LazyTech",
            "PocketOS");

    public void Install()
    {
        Directory.CreateDirectory(InstallPath);
    }
}
