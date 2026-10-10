namespace PocketOS.Manager.Diagnostics;

public class SystemStatus
{
    public string Version { get; set; } = "3.3.0";

    public bool ESP32Connected { get; set; }

    public bool BridgeConnected { get; set; }

    public string Mode { get; set; } = "USB";
}
