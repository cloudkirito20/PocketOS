namespace PocketOS.Manager.Services;

public class ESP32StatusService
{
    public bool Connected { get; private set; }

    public string FirmwareVersion { get; private set; } = "Unknown";

    public void UpdateStatus(
        bool connected,
        string version)
    {
        Connected = connected;
        FirmwareVersion = version;
    }
}
