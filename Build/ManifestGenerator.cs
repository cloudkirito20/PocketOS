using System.Text.Json;

namespace PocketOS.Build;

public class ManifestGenerator
{
    public void Generate(string output)
    {
        var manifest = new
        {
            version = "3.2.1",
            releaseDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            manager = new
            {
                version = "3.2.1",
                download = "PocketOS-Manager-v3.2.1.zip"
            },
            bridge = new
            {
                version = "3.2.1",
                download = "PocketOS-Bridge-v3.2.1.zip"
            },
            esp32 = new
            {
                version = "3.2.1",
                file = "PocketOS_ESP32.bin",
                requiresUSB = true
            }
        };

        File.WriteAllText(
            output,
            JsonSerializer.Serialize(
                manifest,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
    }
}
