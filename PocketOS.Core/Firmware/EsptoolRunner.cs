using System.Diagnostics;

namespace PocketOS.Core.Firmware;

public class EsptoolRunner
{
    public event Action<FirmwareProgress>? ProgressChanged;


    public async Task<bool> FlashAsync(
        FirmwareFlashOptions options)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "esptool.exe",
            Arguments =
                $"--chip {options.Chip} " +
                $"--port {options.Port} " +
                $"--baud {options.BaudRate} " +
                $"write_flash 0x10000 "{options.FirmwarePath}"",

            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };


        using var process = new Process
        {
            StartInfo = startInfo
        };


        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                ProgressChanged?.Invoke(
                    new FirmwareProgress
                    {
                        Status = FlashResult.Writing,
                        Message = e.Data
                    });
            }
        };


        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                ProgressChanged?.Invoke(
                    new FirmwareProgress
                    {
                        Status = FlashResult.Failed,
                        Message = e.Data
                    });
            }
        };


        process.Start();

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return process.ExitCode == 0;
    }
}
