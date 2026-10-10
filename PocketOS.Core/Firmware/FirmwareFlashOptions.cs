namespace PocketOS.Core.Firmware;

public class FirmwareFlashOptions
{
    public string Port { get; set; } = string.Empty;

    public string FirmwarePath { get; set; } = string.Empty;

    public int BaudRate { get; set; } = 460800;

    public string Chip { get; set; } = "esp32";
}
