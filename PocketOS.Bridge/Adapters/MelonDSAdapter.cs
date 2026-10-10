namespace PocketOS.Bridge.Adapters;

public class MelonDSAdapter : IEmulatorAdapter
{
    public string Name => "melonDS";

    public bool CanAttach()
    {
        return true;
    }

    public byte[] ReadMemory(long address, int length)
    {
        return new byte[length];
    }
}
