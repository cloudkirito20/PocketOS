using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LazyTech.PocketOS.Installer;

public sealed class InstallerEngine
{
    private static readonly HttpClient Client = new();

    public string InstallPath { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LazyTech",
            "PocketOS");

    public async Task InstallAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(InstallPath);

        progress?.Report("DOWNLOADING RELEASE MANIFEST...");
        var manifest = await DownloadManifestAsync(cancellationToken);

        progress?.Report("RELEASE DETECTED: " + manifest.Version);

        var managerUrl = manifest.ResolveManagerUrl();
        var bridgeUrl = manifest.ResolveBridgeUrl();
        var updaterUrl = manifest.ResolveUpdaterUrl();
        var firmwareUrl = manifest.ResolveFirmwareUrl();

        if (!string.IsNullOrWhiteSpace(managerUrl))
        {
            progress?.Report("DOWNLOADING MANAGER PACKAGE...");
            await DownloadAndExtractZipAsync(managerUrl, "PocketOSManager.zip", progress, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(bridgeUrl))
        {
            progress?.Report("DOWNLOADING BRIDGE PACKAGE...");
            await DownloadAndExtractZipAsync(bridgeUrl, "PocketOS.Bridge.zip", progress, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(updaterUrl))
        {
            progress?.Report("DOWNLOADING UPDATER PACKAGE...");
            await DownloadAndExtractZipAsync(updaterUrl, "PocketOS.Updater.zip", progress, cancellationToken);
        }


        progress?.Report("POCKETOS INSTALLATION FILES READY.");
    }

    public string GetManagerPath()
    {
        return Path.Combine(InstallPath, "PocketOSManager.exe");
    }

    private static async Task<ReleaseManifest> DownloadManifestAsync(CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(ReleaseManifest.ManifestUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        var manifest = JsonSerializer.Deserialize<ReleaseManifest>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return manifest ?? throw new InvalidOperationException("PocketOS manifest could not be parsed.");
    }

    private async Task DownloadAndExtractZipAsync(
        string url,
        string tempName,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var tempZip = Path.Combine(Path.GetTempPath(), tempName);

        await DownloadFileAsync(url, tempZip, cancellationToken);

        progress?.Report("EXTRACTING " + tempName + "...");
        ZipFile.ExtractToDirectory(tempZip, InstallPath, overwriteFiles: true);

        try
        {
            File.Delete(tempZip);
        }
        catch
        {
            // Non-fatal cleanup failure.
        }
    }

    private static async Task DownloadFileAsync(string url, string outputPath, CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(outputPath);
        await input.CopyToAsync(output, cancellationToken);
    }

    private static string GetFileNameFromUrl(string url, string fallback)
    {
        try
        {
            var uri = new Uri(url);
            var fileName = Path.GetFileName(uri.LocalPath);
            return string.IsNullOrWhiteSpace(fileName) ? fallback : fileName;
        }
        catch
        {
            return fallback;
        }
    }
}

public sealed class ReleaseManifest
{
    public const string ManifestUrl =
        "https://raw.githubusercontent.com/cloudkirito20/PocketOS/main/Releases/v3.4.0-RC1/pocketos-manifest.json";

    public string Version { get; set; } = "v3.4.0-RC1";
    public string ManagerVersion { get; set; } = "";
    public string BridgeVersion { get; set; } = "";
    public string Esp32Version { get; set; } = "";
    public string UpdaterVersion { get; set; } = "";

    public string? ManagerPackage { get; set; }
    public string? BridgePackage { get; set; }
    public string? UpdaterPackage { get; set; }
    public string? InstallerPackage { get; set; }
    public string? Firmware { get; set; }

    public string? ManagerUrl { get; set; }
    public string? BridgeUrl { get; set; }
    public string? UpdaterUrl { get; set; }
    public string? InstallerUrl { get; set; }
    public string? FirmwareUrl { get; set; }

    public string ResolveManagerUrl() =>
        ResolveAssetUrl(ManagerUrl, ManagerPackage, "PocketOSManager.zip");

    public string ResolveBridgeUrl() =>
        ResolveAssetUrl(BridgeUrl, BridgePackage, "PocketOS.Bridge.zip", "PokemonBridge.zip");

    public string ResolveUpdaterUrl() =>
        ResolveAssetUrl(UpdaterUrl, UpdaterPackage, "PocketOS.Updater.zip");

    public string ResolveFirmwareUrl() =>
        ResolveAssetUrl(FirmwareUrl, Firmware, "PocketOS_ESP32.bin");

    private string ResolveAssetUrl(string? directUrl, string? packageName, params string[] fallbacks)
    {
        if (!string.IsNullOrWhiteSpace(directUrl))
            return directUrl!;

        if (!string.IsNullOrWhiteSpace(packageName))
            return BuildReleaseAssetUrl(Version, packageName!);

        foreach (var item in fallbacks)
        {
            if (!string.IsNullOrWhiteSpace(item))
                return BuildReleaseAssetUrl(Version, item);
        }

        return string.Empty;
    }

    private static string BuildReleaseAssetUrl(string version, string fileName)
    {
        var tag = version.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? version
            : "v" + version;

        return $"https://github.com/cloudkirito20/PocketOS/releases/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(fileName)}";
    }
}
