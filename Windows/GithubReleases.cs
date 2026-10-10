using System.Net.Http.Headers;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PocketOSManager;

public sealed class GithubReleaseInfo
{
    public string Tag { get; init; } = "";
    public string ManagerVersion { get; init; } = "";
    public string BridgeVersion { get; init; } = "";
    public string Esp32Version { get; init; } = "";
    public Dictionary<string, GithubArtifact> Components { get; init; } = new();
}
public sealed class GithubArtifact
{
    public string Asset { get; init; } = "";
    public string Sha256 { get; init; } = "";
}

public static class GithubReleases
{
    private const string LatestUrl = "https://api.github.com/repos/cloudkirito20/PocketOS/releases/latest";
    public static bool IsNewer(string remote, string installed) =>
        Version.TryParse(remote.TrimStart('v', 'V'), out var version) &&
        Version.TryParse(installed.TrimStart('v', 'V'), out var current) && version > current;
    public static bool IsNewerManager(string remote) => IsNewer(remote, "3.2.0");
    public static bool HasAvailableUpdates(GithubReleaseInfo release) =>
        IsNewer(release.ManagerVersion, "3.2.0") ||
        IsNewer(release.BridgeVersion, "3.0.0") ||
        IsNewer(release.Esp32Version, "2.9.0");

    public static async Task<GithubReleaseInfo> CheckAsync(HttpClient client, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestUrl);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("LazyTech-PocketOS", "3.2"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        var assets = root.GetProperty("assets").EnumerateArray().ToDictionary(
            a => a.GetProperty("name").GetString() ?? "",
            a => a.GetProperty("browser_download_url").GetString() ?? "");
        if (!assets.TryGetValue("pocketos-manifest.json", out var manifestUrl))
            throw new InvalidDataException("Release has no pocketos-manifest.json asset.");
        using var mreq = new HttpRequestMessage(HttpMethod.Get, manifestUrl);
        mreq.Headers.UserAgent.ParseAdd("LazyTech-PocketOS/3.2");
        var json = await client.SendStringAsync(mreq, ct);
        using var manifest = JsonDocument.Parse(json);
        var m = manifest.RootElement;
        var components = new Dictionary<string, GithubArtifact>();
        foreach (var c in m.GetProperty("components").EnumerateObject()) {
            string name = c.Value.GetProperty("asset").GetString() ?? "";
            string sha = c.Value.GetProperty("sha256").GetString() ?? "";
            if (!assets.ContainsKey(name) || sha.Length != 64 || !sha.All(Uri.IsHexDigit))
                throw new InvalidDataException("Invalid asset or checksum for " + c.Name);
            components[c.Name] = new GithubArtifact { Asset = name, Sha256 = sha };
        }
        return new GithubReleaseInfo {
            Tag = tag,
            ManagerVersion = m.GetProperty("managerVersion").GetString() ?? "",
            BridgeVersion = m.GetProperty("bridgeVersion").GetString() ?? "",
            Esp32Version = m.GetProperty("esp32Version").GetString() ?? "",
            Components = components
        };
    }

    public static async Task<string> DownloadVerifiedAsync(HttpClient client, GithubReleaseInfo release, string component, CancellationToken ct)
    {
        if (!release.Components.TryGetValue(component, out var item)) throw new InvalidDataException("Missing component " + component);
        var filename = Path.GetFileName(item.Asset);
        if (filename != item.Asset || !filename.EndsWith(component == "windows" ? ".zip" : ".bin", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Unexpected update filename");
        var url = $"https://github.com/cloudkirito20/PocketOS/releases/download/{Uri.EscapeDataString(release.Tag)}/{Uri.EscapeDataString(filename)}";
        string dir = Path.Combine(Path.GetTempPath(), "PocketOS-Updates", release.Tag);
        Directory.CreateDirectory(dir);
        var output = Path.Combine(dir, filename);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd("LazyTech-PocketOS/3.2");
        using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        if (resp.Content.Headers.ContentLength is > 150000000) throw new InvalidDataException("Update exceeds size limit");
        await using (var src = await resp.Content.ReadAsStreamAsync(ct))
        await using (var dest = File.Create(output)) await src.CopyToAsync(dest, ct);
        await using var verify = File.OpenRead(output);
        string actual = Convert.ToHexString(await SHA256.HashDataAsync(verify, ct));
        if (!actual.Equals(item.Sha256, StringComparison.OrdinalIgnoreCase)) { File.Delete(output); throw new InvalidDataException("Checksum mismatch"); }
        return output;
    }

    private static async Task<string> SendStringAsync(this HttpClient client, HttpRequestMessage request, CancellationToken ct)
    {
        using var resp = await client.SendAsync(request, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }
}
