using PocketOS.Bridge.Detection;

namespace PocketOS.Bridge.Connection;

public class EmulatorConnection
{
    public DetectedEmulator? Emulator { get; private set; }

    public ConnectionStatus Status { get; private set; }
        = ConnectionStatus.Disconnected;


    public bool Connect(
        DetectedEmulator emulator)
    {
        Status = ConnectionStatus.Connecting;

        Emulator = emulator;

        Status = ConnectionStatus.Connected;

        return true;
    }


    public void Disconnect()
    {
        Emulator = null;
        Status = ConnectionStatus.Disconnected;
    }
}
