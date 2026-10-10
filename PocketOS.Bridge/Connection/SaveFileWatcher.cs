namespace PocketOS.Bridge.Connection;

public class SaveFileWatcher
{
    private DateTime _lastWrite;


    public bool HasChanged(
        string savePath)
    {
        if (!File.Exists(savePath))
            return false;


        var current =
            File.GetLastWriteTime(savePath);


        if(current != _lastWrite)
        {
            _lastWrite = current;
            return true;
        }


        return false;
    }
}
