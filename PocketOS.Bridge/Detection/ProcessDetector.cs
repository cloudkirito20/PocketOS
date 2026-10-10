using System.Diagnostics;

namespace PocketOS.Bridge.Detection;

public class ProcessDetector
{
    public List<DetectedEmulator> Scan(
        IEnumerable<EmulatorProfile> profiles)
    {
        var result = new List<DetectedEmulator>();

        foreach (var process in Process.GetProcesses())
        {
            foreach (var profile in profiles)
            {
                if (profile.ProcessNames.Any(
                    p => process.ProcessName
                    .Equals(
                        p,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(new DetectedEmulator
                    {
                        Name = profile.Name,
                        ProcessName = process.ProcessName,
                        ProcessId = process.Id,
                        Connected = true
                    });
                }
            }
        }

        return result;
    }
}
