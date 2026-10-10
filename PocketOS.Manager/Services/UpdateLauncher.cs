using System.Diagnostics;
using System.Text.Json;

namespace PocketOS.Manager.Services;

public class UpdateLauncher
{
    public void Launch(
        string package,
        string target,
        string restart)
    {
        var command = new
        {
            package,
            target,
            restart
        };

        string json =
            JsonSerializer.Serialize(command);

        string path =
            Path.Combine(
                AppContext.BaseDirectory,
                "update-command.json");

        File.WriteAllText(
            path,
            json);

        Process.Start(
            new ProcessStartInfo
            {
                FileName = "PocketOS.Updater.exe",
                UseShellExecute = true
            });
    }
}
