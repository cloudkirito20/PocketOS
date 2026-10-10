using PocketOS.Bridge.Connection;

namespace PocketOS.Bridge.Pokemon;

public class PokemonReader
{
    private readonly MemoryBridge _memory =
        new();


    public PokemonData Read(
        int processId)
    {
        var data =
            _memory.ReadPokemonMemory(
                processId,
                0,
                32);


        return new PokemonData
        {
            Species = "Unknown",
            Level = 0,
            HP = 0,
            Status = "Normal"
        };
    }
}
