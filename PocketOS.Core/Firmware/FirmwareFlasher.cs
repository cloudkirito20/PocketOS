namespace PocketOS.Core.Firmware;

public class FirmwareFlasher
{
    public FlashResult Status { get; private set; }
        = FlashResult.Idle;


    public async Task<FlashResult> FlashAsync(
        string port,
        string firmwarePath)
    {
        try
        {
            Status = FlashResult.Connecting;

            await Task.Delay(500);

            Status = FlashResult.Erasing;

            await Task.Delay(500);

            Status = FlashResult.Writing;

            await Task.Delay(1000);

            Status = FlashResult.Verifying;

            await Task.Delay(500);

            Status = FlashResult.Complete;

            return Status;
        }
        catch
        {
            Status = FlashResult.Failed;
            return Status;
        }
    }
}
