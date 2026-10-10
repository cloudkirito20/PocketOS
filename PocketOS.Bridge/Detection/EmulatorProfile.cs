namespace PocketOS.Bridge.Detection;

public class EmulatorProfile
{
    public string Name { get; set; } = string.Empty;

    public string[] ProcessNames { get; set; } = [];

    public string Platform { get; set; } = string.Empty;
}
