using System.Diagnostics;

namespace PocketOSManager.Firmware;

public sealed class FirmwareFlasher
{
    public async Task<int> FlashAsync(
        string pythonExe,
        string port,
        string firmware,
        IProgress<string>? progress = null)
    {
        progress?.Report("STARTING ESP32 FLASH...");

        var psi = new ProcessStartInfo(pythonExe)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var arg in new[]
        {
            "-m",
            "esptool",
            "--chip",
            "esp32",
            "--port",
            port,
            "--baud",
            "460800",
            "write_flash",
            "0x10000",
            firmware
        })
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi)
            ?? throw new Exception("Unable to start esptool.");

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                progress?.Report(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                progress?.Report(e.Data);
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        progress?.Report(
            process.ExitCode == 0
            ? "FLASH COMPLETE."
            : "FLASH FAILED.");

        return process.ExitCode;
    }
}
