namespace PocketOSManager.Firmware;

public sealed class FirmwareUpdateService
{
    public string Folder { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LazyTech",
            "PocketOS",
            "Firmware");

    public FirmwareUpdateService()
    {
        Directory.CreateDirectory(Folder);
    }

    public string FirmwarePath =>
        Path.Combine(Folder, "PocketOS_ESP32.bin");
}
