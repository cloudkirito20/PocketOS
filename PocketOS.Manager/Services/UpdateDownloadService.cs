using PocketOS.Core.Update;

namespace PocketOS.Manager.Services;

public class UpdateDownloadService
{
    private readonly DownloadManager _downloader = new();

    public async Task DownloadAsync(
        string url,
        string fileName)
    {
        string folder =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "LazyTech",
                "PocketOS",
                "Cache",
                "Updates");

        string target =
            Path.Combine(
                folder,
                fileName);

        await _downloader.DownloadAsync(
            url,
            target);
    }
}
