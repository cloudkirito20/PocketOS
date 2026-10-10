using System.IO.Compression;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Forms;
namespace PocketOSInstaller;
internal static class Program
{
    [STAThread]
    private static async Task Main()
    {
        ApplicationConfiguration.Initialize();
        if (MessageBox.Show("Install LazyTech PocketOS Manager and Pokémon Bridge from the latest public GitHub Release?", "PocketOS Installer", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "LazyTech", "PocketOS");
        string zip = Path.Combine(Path.GetTempPath(), "PocketOSInstallerUpdate.zip");
        try {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("LazyTech-PocketOSInstaller", "3.2"));
            var release = JsonDocument.Parse(await http.GetStringAsync("https://api.github.com/repos/cloudkirito20/PocketOS/releases/latest"));
            var assets = release.RootElement.GetProperty("assets").EnumerateArray().ToDictionary(x => x.GetProperty("name").GetString() ?? "", x => x.GetProperty("browser_download_url").GetString() ?? "");
            if (!assets.TryGetValue("pocketos-manifest.json", out var manifestUrl)) throw new InvalidDataException("Release manifest is missing.");
            var manifest = JsonDocument.Parse(await http.GetStringAsync(manifestUrl));
            var win = manifest.RootElement.GetProperty("components").GetProperty("windows");
            string name = win.GetProperty("asset").GetString() ?? "";
            string sha = win.GetProperty("sha256").GetString() ?? "";
            if (!assets.TryGetValue(name, out var url) || sha.Length != 64 || !sha.All(Uri.IsHexDigit)) throw new InvalidDataException("Invalid release asset metadata.");
            await File.WriteAllBytesAsync(zip, await http.GetByteArrayAsync(url));
            await using (var fs = File.OpenRead(zip)) {
                if (!Convert.ToHexString(await SHA256.HashDataAsync(fs)).Equals(sha, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Checksum mismatch. Installation stopped.");
            }
            Directory.CreateDirectory(dir);
            ZipFile.ExtractToDirectory(zip, dir, overwriteFiles: true);
            if (!File.Exists(Path.Combine(dir, "PocketOSManager.exe")) || !File.Exists(Path.Combine(dir, "PokemonBridge.exe"))) throw new InvalidDataException("Release ZIP missing executables");
            var start = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "PocketOS Manager.url");
            File.WriteAllText(start, "[InternetShortcut]" + Environment.NewLine + "URL=file:///" + Path.Combine(dir, "PocketOSManager.exe").Replace('\\', '/') + Environment.NewLine);
            MessageBox.Show("PocketOS was installed. The Manager will now start.", "Installation complete");
            Process.Start(new ProcessStartInfo(Path.Combine(dir, "PocketOSManager.exe")) { WorkingDirectory = dir, UseShellExecute = true });
        } catch (Exception ex) { MessageBox.Show(ex.Message, "PocketOS installation failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { try { File.Delete(zip); } catch { } }
    }
}
