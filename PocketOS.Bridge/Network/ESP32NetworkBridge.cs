using PocketOS.Core.Network;

namespace PocketOS.Bridge.Network;

public class ESP32NetworkBridge
{
    public ConnectionMode Mode { get; private set; }
        = ConnectionMode.USB;


    public void EnableWiFi()
    {
        Mode = ConnectionMode.WiFi;
    }


    public void UseUSB()
    {
        Mode = ConnectionMode.USB;
    }
}
