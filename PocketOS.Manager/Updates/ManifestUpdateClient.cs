using System.Net.Http.Json;

namespace PocketOSManager.Updates;

public class ManifestUpdateClient
{
    private readonly HttpClient client = new();

    public async Task<UpdateManifest?> CheckAsync(string manifestUrl)
    {
        var manifest = await client.GetFromJsonAsync<UpdateManifest>(manifestUrl);
        return manifest;
    }
}

public class UpdateManifest
{
    public string Version { get; set; } = "";

    public string ManagerPackage { get; set; } = "";

    public string BridgePackage { get; set; } = "";

    public string Esp32Firmware { get; set; } = "";
}
