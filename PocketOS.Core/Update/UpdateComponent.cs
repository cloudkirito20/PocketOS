namespace PocketOS.Core.Update;

public class UpdateComponent
{
    public string Version { get; set; } = string.Empty;
    public string Download { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public bool RequiresUSB { get; set; }
}
