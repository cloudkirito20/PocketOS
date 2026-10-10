namespace PocketOS.Bridge.Adapters;

public interface IEmulatorAdapter
{
    string Name { get; }

    bool CanAttach();

    byte[] ReadMemory(long address, int length);
}
