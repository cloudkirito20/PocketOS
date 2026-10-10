namespace PocketOS.Bridge.Connection;

public class MemoryBridge
{
    public byte[] ReadPokemonMemory(
        int processId,
        long address,
        int length)
    {
        // Emulator specific memory readers
        // will be added per platform.

        return new byte[length];
    }
}
