namespace PocketOS.Bridge.Adapters;

public class CitraAdapter : IEmulatorAdapter
{
    public string Name => "Citra";

    public bool CanAttach()
    {
        return true;
    }

    public byte[] ReadMemory(long address, int length)
    {
        return new byte[length];
    }
}
