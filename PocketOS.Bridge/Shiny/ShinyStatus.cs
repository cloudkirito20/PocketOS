namespace PocketOS.Bridge.Shiny;

public class ShinyStatus
{
    public string Pokemon { get; set; } = "";

    public int Attempts { get; set; }

    public bool IsShiny { get; set; }

    public DateTime LastCheck { get; set; }
}
