namespace PocketOS.Manager.Services;

public class ManagerStartupService
{
    public bool Initialize()
    {
        // Initialize update, bridge and ESP32 services.
        return true;
    }
}
