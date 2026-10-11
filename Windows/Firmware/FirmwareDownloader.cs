using System.Net.Http;
using System.Security.Cryptography;

namespace PocketOSManager.Firmware;

public sealed class FirmwareDownloader
{
    private readonly HttpClient client = new();

    public async Task<string> DownloadAsync(
        string url,
        string output,
        IProgress<string>? progress = null)
    {
        progress?.Report("DOWNLOADING ESP32 FIRMWARE...");

        await using var input =
            await client.GetStreamAsync(url);

        await using var file =
            File.Create(output);

        await input.CopyToAsync(file);

        progress?.Report("FIRMWARE DOWNLOAD COMPLETE.");

        return output;
    }

    public static string CalculateSha256(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(
            SHA256.HashData(stream));
    }
}
