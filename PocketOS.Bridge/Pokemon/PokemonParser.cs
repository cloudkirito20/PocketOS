namespace PocketOS.Bridge.Pokemon;

public class PokemonParser
{
    public PokemonData Parse(byte[] data)
    {
        return new PokemonData
        {
            Species = "Unknown",
            Level = 0,
            HP = 0,
            Status = "Normal"
        };
    }
}
