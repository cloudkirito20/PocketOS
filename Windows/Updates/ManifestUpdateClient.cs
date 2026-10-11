using System.Net.Http.Json;

namespace PocketOSManager.Updates;

public sealed class ManifestUpdateClient
{
    private readonly HttpClient client = new();

    public async Task<UpdateManifest?> CheckAsync(string url)
    {
        return await client.GetFromJsonAsync<UpdateManifest>(url);
    }
}

public sealed class UpdateManifest
{
    public string Version { get; set; } = "";

    public string ManagerVersion { get; set; } = "";

    public string BridgeVersion { get; set; } = "";

    public string UpdaterVersion { get; set; } = "";

    public string Esp32Version { get; set; } = "";


    // Download URLs
    public string? ManagerUrl { get; set; }

    public string? BridgeUrl { get; set; }

    public string? UpdaterUrl { get; set; }


    // ESP32 Firmware
    public string? FirmwareUrl { get; set; }

    public string? FirmwareSha256 { get; set; }
}
