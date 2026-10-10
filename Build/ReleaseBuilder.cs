namespace PocketOS.Build;

public class ReleaseBuilder
{
    public string Version { get; }

    public ReleaseBuilder(string version)
    {
        Version = version;
    }

    public void PrepareReleaseFolder(string path)
    {
        Directory.CreateDirectory(path);
    }
}
