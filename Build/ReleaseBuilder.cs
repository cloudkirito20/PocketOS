namespace PocketOS.Build;

public class ReleaseBuilder
{
    public string Version { get; }

    public ReleaseBuilder(string version)
    {
        Version = version;
    }

    public void Prepare(string path)
    {
        Directory.CreateDirectory(path);
    }
}
