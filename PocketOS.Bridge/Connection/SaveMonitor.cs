namespace PocketOS.Bridge.Connection;

public class SaveMonitor
{
    public bool IsSaveChanged(
        string savePath)
    {
        return File.Exists(savePath);
    }
}
