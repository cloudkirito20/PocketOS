using System.Net.Http;

namespace PocketOS.Core.Update;

public class DownloadManager
{
    private readonly HttpClient _client = new();

    public async Task DownloadAsync(
        string url,
        string destination)
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(destination)!);

        using var response =
            await _client.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        await using var stream =
            await response.Content.ReadAsStreamAsync();

        await using var file =
            File.Create(destination);

        await stream.CopyToAsync(file);
    }
}
