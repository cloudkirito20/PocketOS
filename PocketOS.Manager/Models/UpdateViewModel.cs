namespace PocketOS.Manager.Models;

public class UpdateViewModel
{
    public string Component { get; set; } = "";

    public string CurrentVersion { get; set; } = "";

    public string LatestVersion { get; set; } = "";

    public bool RequiresUSB { get; set; }

    public bool Available { get; set; }
}
