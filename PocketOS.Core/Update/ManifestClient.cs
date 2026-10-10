using System.Net.Http.Json;

namespace PocketOS.Core.Update;

public class ManifestClient
{
    private readonly HttpClient _client = new();

    public ManifestClient()
    {
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "LazyTech-PocketOS");
    }

    public async Task<UpdateManifest?> GetManifestAsync(string url)
    {
        try
        {
            return await _client.GetFromJsonAsync<UpdateManifest>(url);
        }
        catch
        {
            return null;
        }
    }
}
