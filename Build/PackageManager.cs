namespace PocketOS.Build;

public class PackageManager
{
    public void CreatePackageFolder(string path)
    {
        Directory.CreateDirectory(path);
    }
}
