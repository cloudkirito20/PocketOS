namespace PocketOS.Core.Update;

public static class VersionComparer
{
    public static bool IsNewer(string current, string latest)
    {
        if (!Version.TryParse(current, out var local))
            return false;

        if (!Version.TryParse(latest, out var remote))
            return false;

        return remote > local;
    }
}
