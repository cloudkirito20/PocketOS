namespace PocketOS.Manager.Production;

public class ServiceContainer
{
    public Services.ManagerStartupService Startup { get; }
    public Services.ESP32StatusService ESP32 { get; }
    public Services.CompanionSyncService Companion { get; }

    public ServiceContainer()
    {
        Startup = new Services.ManagerStartupService();
        ESP32 = new Services.ESP32StatusService();
        Companion = new Services.CompanionSyncService();
    }
}
