namespace PocketOS.Core.Firmware;

public class FirmwareProgress
{
    public FlashResult Status { get; set; } = FlashResult.Idle;

    public int Percentage { get; set; }

    public string Message { get; set; } = string.Empty;
}
