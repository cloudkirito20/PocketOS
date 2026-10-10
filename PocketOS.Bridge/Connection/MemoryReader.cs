namespace PocketOS.Bridge.Connection;

public class MemoryReader
{
    public byte[] Read(
        int processId,
        long address,
        int length)
    {
        // Memory access implementation
        // will be connected per emulator profile.
        return new byte[length];
    }
}
