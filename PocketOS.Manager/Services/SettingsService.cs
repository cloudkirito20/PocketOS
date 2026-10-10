using System.Text.Json;
using PocketOS.Manager.Settings;

namespace PocketOS.Manager.Services;

public class SettingsService
{
    public void Save(
        SettingsModel settings,
        string path)
    {
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(settings));
    }


    public SettingsModel Load(string path)
    {
        if (!File.Exists(path))
            return new SettingsModel();

        return JsonSerializer.Deserialize<SettingsModel>(
            File.ReadAllText(path))
            ?? new SettingsModel();
    }
}
