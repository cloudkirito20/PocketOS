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
    public string Esp32Version { get; set; } = "";
    public string ManagerPackage { get; set; } = "";
    public string BridgePackage { get; set; } = "";
    public string Firmware { get; set; } = "";
}
