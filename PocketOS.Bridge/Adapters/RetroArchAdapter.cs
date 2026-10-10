namespace PocketOS.Bridge.Adapters;

public class RetroArchAdapter : IEmulatorAdapter
{
    public string Name => "RetroArch";

    public bool CanAttach()
    {
        return true;
    }

    public byte[] ReadMemory(long address, int length)
    {
        return new byte[length];
    }
}
