namespace PocketOS.Updater;

public class RestartManager
{
    public void Restart(string application)
    {
        if (!File.Exists(application))
            return;

        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo
            {
                FileName = application,
                UseShellExecute = true
            });
    }
}
