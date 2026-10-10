namespace PocketOS.Core.Update;

public class UpdateManifest
{
    public string Version { get; set; } = string.Empty;
    public string ReleaseDate { get; set; } = string.Empty;

    public UpdateComponent Manager { get; set; } = new();
    public UpdateComponent Bridge { get; set; } = new();
    public UpdateComponent ESP32 { get; set; } = new();
}
