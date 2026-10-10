namespace PocketOS.Core.Network;

public class WifiStatus
{
    public bool Connected { get; set; }

    public string SSID { get; set; } = "";

    public string IPAddress { get; set; } = "";
}
