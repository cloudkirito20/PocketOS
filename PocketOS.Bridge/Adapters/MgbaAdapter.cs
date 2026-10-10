namespace PocketOS.Bridge.Adapters;

public class MgbaAdapter : IEmulatorAdapter
{
    public string Name => "mGBA";

    public bool CanAttach()
    {
        return true;
    }

    public byte[] ReadMemory(long address, int length)
    {
        return new byte[length];
    }
}
