namespace PocketOS.Bridge.Detection;

public class DetectedEmulator
{
    public string Name { get; set; } = string.Empty;

    public string ProcessName { get; set; } = string.Empty;

    public int ProcessId { get; set; }

    public bool Connected { get; set; }
}
