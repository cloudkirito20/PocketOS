using PocketOS.Core.Update;

namespace PocketOS.Manager.Services;

public class UpdateService
{
    private readonly ManifestClient _manifestClient = new();

    public async Task<UpdateManifest?> CheckForUpdatesAsync()
    {
        return await _manifestClient.GetManifestAsync(
            UpdateSettings.ManifestUrl);
    }

    public bool HasManagerUpdate(string currentVersion, UpdateManifest manifest)
    {
        return VersionComparer.IsNewer(currentVersion, manifest.Manager.Version);
    }

    public bool HasBridgeUpdate(string currentVersion, UpdateManifest manifest)
    {
        return VersionComparer.IsNewer(currentVersion, manifest.Bridge.Version);
    }

    public bool HasESP32Update(string currentVersion, UpdateManifest manifest)
    {
        return VersionComparer.IsNewer(currentVersion, manifest.ESP32.Version);
    }
}
