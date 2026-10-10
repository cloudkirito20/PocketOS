namespace PocketOS.Bridge.ESP32;

public class CompanionDisplayService
{
    private readonly ESP32SerialClient _client = new();


    public bool Connect(string port)
    {
        return _client.Connect(port);
    }


    public void UpdatePokemon(
        string pokemon,
        int level,
        int hp,
        string status)
    {
        var message = new OLEDMessage
        {
            Pokemon = pokemon,
            Level = level,
            HP = hp,
            Status = status
        };

        _client.Send(message);
    }
}
