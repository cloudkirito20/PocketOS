using PocketOS.Bridge.Shiny;

namespace PocketOS.Manager.Services;

public class ShinyHuntService
{
    private readonly ShinyDetector _detector = new();

    private readonly ShinyCounter _counter = new();


    public ShinyStatus CheckPokemon(
        string pokemon,
        byte[] data)
    {
        _counter.Increment();

        return new ShinyStatus
        {
            Pokemon = pokemon,
            Attempts = _counter.Attempts,
            IsShiny = _detector.Check(data),
            LastCheck = DateTime.Now
        };
    }
}
