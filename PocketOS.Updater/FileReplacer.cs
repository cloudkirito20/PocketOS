namespace PocketOS.Updater;

public class FileReplacer
{
    public void ReplaceDirectory(
        string source,
        string target)
    {
        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException(source);

        Directory.CreateDirectory(target);

        foreach (string file in Directory.GetFiles(
            source,
            "*",
            SearchOption.AllDirectories))
        {
            string relative =
                Path.GetRelativePath(
                    source,
                    file);

            string destination =
                Path.Combine(
                    target,
                    relative);

            Directory.CreateDirectory(
                Path.GetDirectoryName(destination)!);

            File.Copy(
                file,
                destination,
                true);
        }
    }
}
