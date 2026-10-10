namespace PocketOS.Core.Network;

public class NetworkClient
{
    public bool Available { get; private set; }

    public void Check()
    {
        Available = true;
    }
}
