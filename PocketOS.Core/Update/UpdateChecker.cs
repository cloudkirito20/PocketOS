namespace PocketOS.Core.Update;

public class UpdateChecker
{
    public bool IsUpdateAvailable(
        string current,
        string latest)
    {
        return VersionComparer.IsNewer(
            current,
            latest);
    }
}
