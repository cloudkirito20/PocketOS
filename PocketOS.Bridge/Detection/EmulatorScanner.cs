namespace PocketOS.Bridge.Detection;

public class EmulatorScanner
{
    private readonly ProcessDetector detector = new();

    public List<DetectedEmulator> Scan()
    {
        var profiles = new[]
        {
            new EmulatorProfile
            {
                Name = "RetroArch",
                Platform = "Multi",
                ProcessNames = ["retroarch"]
            },

            new EmulatorProfile
            {
                Name = "mGBA",
                Platform = "GBA",
                ProcessNames = ["mgba"]
            },

            new EmulatorProfile
            {
                Name = "gpSP",
                Platform = "GBA",
                ProcessNames = ["gpsp"]
            },

            new EmulatorProfile
            {
                Name = "melonDS",
                Platform = "Nintendo DS",
                ProcessNames = ["melonds"]
            },

            new EmulatorProfile
            {
                Name = "Citra",
                Platform = "Nintendo 3DS",
                ProcessNames = ["citra"]
            }
        };

        return detector.Scan(profiles);
    }
}
