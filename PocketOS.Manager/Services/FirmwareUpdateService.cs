using PocketOS.Core.Firmware;

namespace PocketOS.Manager.Services;

public class FirmwareUpdateService
{
    private readonly ESP32DeviceDetector _detector = new();

    private readonly EsptoolRunner _runner = new();


    public event Action<FirmwareProgress>? ProgressChanged
    {
        add => _runner.ProgressChanged += value;
        remove => _runner.ProgressChanged -= value;
    }


    public async Task<bool> FlashAsync(
        string firmwarePath)
    {
        string? port = _detector.Detect();

        if (port == null)
            return false;


        var options = new FirmwareFlashOptions
        {
            Port = port,
            FirmwarePath = firmwarePath,
            Chip = "esp32",
            BaudRate = 460800
        };


        return await _runner.FlashAsync(options);
    }
}
