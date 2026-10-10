namespace PocketOS.Bridge.Shiny;

public class ShinyCounter
{
    public int Attempts { get; private set; }


    public void Increment()
    {
        Attempts++;
    }


    public void Reset()
    {
        Attempts = 0;
    }
}
