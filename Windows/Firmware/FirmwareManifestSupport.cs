namespace PocketOSManager.Firmware;

public sealed class FirmwareManifestInfo
{
    public string? FirmwareUrl { get; set; }
    public string? FirmwareSha256 { get; set; }
    public string? Esp32Version { get; set; }
}
